using System;

namespace WorldTour.Game.Modes
{
    /// <summary>What a remix round needs from the show (the web ctx): the engine calls, timers and the dog.</summary>
    public interface IRemixHost
    {
        /// <summary>The song is on screen and not answered yet.</summary>
        bool Open { get; }
        bool Resolved { get; }
        /// <summary>Wrong tries so far on this song.</summary>
        int Tries { get; }
        /// <summary>Seconds, unscaled (Time.unscaledTime).</summary>
        float Now { get; }
        int Miss();
        bool Win();
        bool Reveal();
        void Say(string line, string pose);
        /// <summary>Runs <paramref name="action"/> after a pause, while the song is still on screen.</summary>
        void Later(Action action, float seconds);
        void Next(float seconds);
        void BlockHints(bool blocked);
        /// <summary>show.js ladder() for this many wrong tries.</summary>
        string Ladder(int tries);
        void Log(string line);
        void Error(Exception e);
    }

    /// <summary>What a remix round asks of its drawing.</summary>
    public interface IRemixView
    {
        /// <summary>The tile leaves the rack (shrink). <paramref name="landed"/> may come late, or never.</summary>
        void Leave(int tile, Action landed);
        /// <summary>remix.js paint(): the slots and the rack from the board; <paramref name="landed"/> slaps that slot.</summary>
        void Paint(int landed);
        /// <summary>The slots wobble (a wrong try).</summary>
        void Wobble();
        /// <summary>No tile can be pressed (the song is over).</summary>
        void LockTiles();
        /// <summary>A right answer: the slots turn ok and pop, one by one.</summary>
        void Celebrate();
        /// <summary>The reveal: the word appears in the slots.</summary>
        void ShowWord();
        /// <summary>Every rack tile back at rest, stopping any motion still running on it (so it can't finish later).</summary>
        void ResetTiles();
    }

    /// <summary>
    /// One remix song's play (remix.js:66-101) as a plain state machine, so its timing can be tested without Unity.
    /// The board is the only state; the rules that keep it from getting stuck (2.7 tester's freeze):
    /// the wait after a wrong try is scheduled before anything else is called (the engine's listeners or the dog can't
    /// skip it by throwing); its reset carries a token, so a stale one does nothing; a tile still landing never touches
    /// Busy and is ignored once the slots have been reset; and if the reset never arrives (a dropped timer), <see cref="Tick"/>
    /// ends the wait half a second late and logs WT_PICK_RECOVER.
    /// Every pick, taken or ignored, logs "WT_PICK idx= landed= busy= open= resolved=" for the browser tests.
    /// </summary>
    public sealed class RemixRound
    {
        /// <summary>remix.js:99 the wait after a wrong try.</summary>
        public const float MissWait = .75f;
        /// <summary>How long past <see cref="MissWait"/> the safety net waits before ending a wait itself.</summary>
        public const float Grace = .5f;
        /// <summary>remix.js:83, :92, :85, :94.</summary>
        public const float OkDelay = .17f, RevealDelay = .36f, NextAfterWin = 1.4f, NextAfterReveal = 2.8f;

        public readonly RemixBoard Board;
        private readonly IRemixHost _host;
        private readonly IRemixView _view;
        private int _missToken = -1;
        private float _missDeadline;

        public RemixRound(RemixBoard board, IRemixHost host, IRemixView view)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _view = view ?? throw new ArgumentNullException(nameof(view));
        }

        /// <summary>True while waiting out a wrong try.</summary>
        public bool InMissWait { get { return _missToken >= 0; } }

        /// <summary>remix.js:66-76 a tile press (click, drop or key).</summary>
        public PickResult Pick(int tile)
        {
            bool open = _host.Open, busy = Board.Busy;
            PickResult r = open ? Board.Pick(tile) : PickResult.Ignored;
            _host.Log("WT_PICK idx=" + tile + " landed=" + B(r != PickResult.Ignored) + " busy=" + B(busy) + " open=" + B(open) + " resolved=" + B(_host.Resolved));
            if (r == PickResult.Ignored) return r;
            int generation = Board.Generation, landed = Board.Picked.Count - 1;
            Safe(() => _view.Leave(tile, () =>
            {
                // a landing never changes Busy, and one from before a reset is dropped
                if (Board.Generation == generation) Safe(() => _view.Paint(landed));
            }));
            if (r == PickResult.Complete) Check();
            return r;
        }

        /// <summary>remix.js:78-101.</summary>
        private void Check()
        {
            if (Board.IsRight)
            {
                Board.Busy = true;
                Safe(_view.LockTiles);
                _host.Later(() => Safe(_view.Celebrate), OkDelay);
                Safe(() => _host.Win());
                _host.Next(NextAfterWin);
                return;
            }
            int token = Board.StartMiss();
            int tries;
            try { tries = _host.Miss(); }
            catch (Exception e) { _host.Error(e); tries = _host.Tries; }
            Safe(_view.Wobble);
            if (tries >= 3)
            {
                Safe(_view.LockTiles);
                _host.Later(() => { Board.ResetAfterMiss(); Safe(_view.ShowWord); }, RevealDelay);
                Safe(() => _host.Reveal());
                _host.Next(NextAfterReveal);
                return;
            }
            // the way out first, then everything that could throw
            _missToken = token;
            _missDeadline = _host.Now + MissWait + Grace;
            _host.Later(() => EndMiss(token), MissWait);
            Safe(() => _host.BlockHints(true));
            Safe(() => _host.Say(_host.Ladder(tries), "oops"));
        }

        /// <summary>A typed letter with no free tile (all used, or the board is full while it waits): logged, nothing else.</summary>
        public void IgnoredKey(char c)
        {
            _host.Log("WT_PICK idx=-1 key=" + c + " landed=false busy=" + B(Board.Busy) + " open=" + B(_host.Open) + " resolved=" + B(_host.Resolved));
        }

        /// <summary>remix.js:99 the end of the wait: the slots empty, the tiles come back, the hint button too.</summary>
        public void EndMiss(int token)
        {
            if (!Board.FinishMissReset(token)) return;
            if (_missToken == token) _missToken = -1;
            Safe(_view.ResetTiles);
            Safe(() => _view.Paint(-1));
            Safe(() => _host.BlockHints(false));
        }

        /// <summary>Call every frame: ends a wait whose reset never came.</summary>
        public void Tick()
        {
            if (_missToken < 0 || _host.Now < _missDeadline) return;
            int token = _missToken;
            _missToken = -1;
            _host.Log("WT_PICK_RECOVER token=" + token + " busy=" + B(Board.Busy));
            EndMiss(token);
        }

        public bool Undo()
        {
            if (!_host.Open || !Board.Undo()) return false;
            Safe(() => _view.Paint(-1));
            return true;
        }

        public bool Clear()
        {
            if (!_host.Open || !Board.Clear()) return false;
            Safe(() => _view.Paint(-1));
            return true;
        }

        private void Safe(Action a)
        {
            try { a(); }
            catch (Exception e) { _host.Error(e); }
        }

        private static string B(bool b) { return b ? "true" : "false"; }
    }
}
