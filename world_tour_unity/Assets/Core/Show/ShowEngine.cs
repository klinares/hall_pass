using System;
using System.Collections.Generic;
using System.Linq;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Shows
{
    /// <summary>Everything a show needs to start (the arguments of JS <c>mountShow</c> that carry rules).</summary>
    public sealed class ShowSetup
    {
        /// <summary>JS <c>mode.kind === 'math'</c>.</summary>
        public bool IsMath;
        /// <summary>JS <c>mode.id</c>.</summary>
        public string ModeId;
        /// <summary>
        /// The skill whose level this show reads and moves. Math: the mode's skill ("dec", "frac", ...).
        /// Spelling: leave null and it becomes "spell:" + ModeId, as show.js:58 does.
        /// </summary>
        public string Skill;
        /// <summary>Passed through to the summary (JS <c>V.id</c>).</summary>
        public string VenueId;
        /// <summary>The songs, in order (from <see cref="ShowPicker"/>, or the encore list).</summary>
        public IReadOnlyList<ShowItem> Items;
        public bool Encore;
        /// <summary>The saved record for <see cref="Skill"/>. The engine updates it in place (JS P.recordSkill). Null = a fresh level-1 record.</summary>
        public SkillRecord SkillRecord;
        /// <summary>Phase 7.1: the level rules (cap, floor, run lengths). Null = the web game's rules (levels 1 to 3, up after 4).</summary>
        public LevelPolicy Policy;
        /// <summary>JS <c>mode.generate(level, seed)</c>. Null = <c>Generators.Generate(Skill, level, seed)</c>.</summary>
        public Func<int, long, Problem> Generate;
        /// <summary>Milliseconds now, for the show's length in seconds (JS Date.now). Null = the system clock.</summary>
        public Func<long> ClockMs;

        public ShowSetup Clone()
        {
            return (ShowSetup)MemberwiseClone();
        }
    }

    /// <summary>
    /// The rules of one show, extracted from <c>mountShow</c> in world_tour/js/show.js with no UI in it: the
    /// queue, scoring, streak, hints, re-inserting a miss three songs later, the summary, and building the
    /// encore. The Game layer draws the song, calls <see cref="Win"/>, <see cref="Miss"/>, <see cref="Reveal"/>,
    /// <see cref="Hint"/> and <see cref="Next"/> exactly where a mode calls ctx.win / ctx.miss / ctx.reveal /
    /// the hint button / ctx.next, and reacts to <see cref="Raised"/>. Nothing here ever lowers fans already won.
    /// </summary>
    public sealed class ShowEngine
    {
        private readonly ShowSetup _setup;
        private readonly List<ShowSlot> _q;
        private readonly HashSet<string> _hinted = new HashSet<string>();
        private readonly HashSet<string> _missed = new HashSet<string>();
        private readonly Func<int, long, Problem> _generate;
        private readonly Func<long> _clock;
        private long _startedMs;
        private bool _started;

        /// <summary>Every event, in order, as it is raised.</summary>
        public event Action<ShowEvent> Raised;

        public ShowEngine(ShowSetup setup)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (setup.Items == null) throw new ArgumentException("a show needs its items", nameof(setup));
            _setup = setup.Clone();
            if (_setup.Skill == null) _setup.Skill = _setup.IsMath ? null : "spell:" + _setup.ModeId;
            if (_setup.IsMath && string.IsNullOrEmpty(_setup.Skill)) throw new ArgumentException("a math show needs its skill", nameof(setup));
            if (_setup.SkillRecord == null) _setup.SkillRecord = new SkillRecord();
            string skill = _setup.Skill;
            _generate = _setup.Generate ?? ((lv, seed) => Generators.Generate(skill, lv, seed));
            _clock = _setup.ClockMs ?? (() => DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond);
            // show.js:54 q: items.map(v => ({ v, bonus: false, state: '' }))
            _q = _setup.Items.Select(v => new ShowSlot(v, false)).ToList();
            // show.js:56 bonusLeft: encore ? 0 : MAX_BONUS
            BonusLeft = _setup.Encore ? 0 : Scoring.MaxBonus;
        }

        // ---------- state ----------

        public bool IsMath { get { return _setup.IsMath; } }
        public bool IsEncore { get { return _setup.Encore; } }
        public string ModeId { get { return _setup.ModeId; } }
        public string VenueId { get { return _setup.VenueId; } }
        public string Skill { get { return _setup.Skill; } }
        /// <summary>The skill record this show reads and updates.</summary>
        public SkillRecord SkillRecord { get { return _setup.SkillRecord; } }
        /// <summary>The level rules this show plays by.</summary>
        public LevelPolicy Policy { get { return _setup.Policy ?? LevelPolicy.Legacy; } }
        /// <summary>The level the skill plays at now: the saved level held between the policy's floor and cap.</summary>
        public int SkillLevel { get { return Policy.Effective(SkillRecord.Level); } }

        /// <summary>The queue, bonus replays included (for the pips).</summary>
        public IReadOnlyList<ShowSlot> Queue { get { return _q; } }
        /// <summary>Position in <see cref="Queue"/> (JS G.i).</summary>
        public int Index { get; private set; }
        /// <summary>The song in play, or null before Start and after the end (JS G.cur).</summary>
        public ShowSlot Current { get; private set; }

        /// <summary>Fans won this show (JS G.fans). Only ever goes up.</summary>
        public int Fans { get; private set; }
        public int Streak { get; private set; }
        /// <summary>Best streak this show (JS G.best).</summary>
        public int Best { get; private set; }
        /// <summary>Clean wins (JS G.firstTry), bonus replays included.</summary>
        public int FirstTry { get; private set; }
        /// <summary>Wins with hints and no wrong try (JS G.hintOnly).</summary>
        public int HintOnly { get; private set; }
        public int HintTotal { get; private set; }
        /// <summary>Re-inserts still allowed this show (JS G.bonusLeft).</summary>
        public int BonusLeft { get; private set; }

        /// <summary>Wrong tries on the current song (JS G.tries).</summary>
        public int Tries { get; private set; }
        /// <summary>Hints used on the current song (JS G.hints).</summary>
        public int Hints { get; private set; }
        public int MaxHints { get; private set; }
        public int HintsLeft { get { return Math.Max(0, MaxHints - Hints); } }
        /// <summary>The hint button is off (after an answer, or when a mode locks it).</summary>
        public bool HintsLocked { get; private set; }
        /// <summary>The hint button should be enabled (JS paintHint: <c>left &gt; 0</c>, and not locked).</summary>
        public bool CanHint { get { return Current != null && !HintsLocked && Hints < MaxHints; } }
        /// <summary>The current song has been won or revealed.</summary>
        public bool Resolved { get; private set; }

        public bool Started { get { return _started; } }
        public bool Finished { get; private set; }
        public ShowSummary Summary { get; private set; }

        /// <summary>Kids of songs missed at least once this show (JS G.missed).</summary>
        public IReadOnlyCollection<string> MissedKids { get { return _missed; } }
        /// <summary>Kids of songs a hint was used on (JS G.hinted).</summary>
        public IReadOnlyCollection<string> HintedKids { get { return _hinted; } }

        /// <summary>The current song is a bonus replay ("bonus song, one more try").</summary>
        public bool IsBonus { get { return Current != null && Current.Bonus; } }
        /// <summary>Main songs in the show (JS stats(): <c>real</c>).</summary>
        public int SongCount { get { return _q.Count(x => !x.Bonus); } }
        /// <summary>"song N of M" (JS stats(): <c>Math.min(n, real)</c>).</summary>
        public int SongNumber
        {
            get
            {
                int n = _q.Take(Math.Min(Index + 1, _q.Count)).Count(x => !x.Bonus);
                return Math.Min(n, SongCount);
            }
        }

        /// <summary>
        /// The level the current song is played at: a math item's own level, or the skill's level for a
        /// spelling song (show.js:208 <c>G.math ? G.cur.v.level : P.level(G.skill)</c>).
        /// </summary>
        public int CurrentLevel
        {
            get
            {
                var m = Current != null ? Current.Item as MathItem : null;
                return m != null && m.Level.HasValue ? m.Level.Value : SkillLevel;
            }
        }

        /// <summary>What a right answer would earn on the current song right now (the same rule as <see cref="Win"/>).</summary>
        public int PotentialGain(int extra = 0)
        {
            WinKind kind = Scoring.Kind(Tries, Hints);
            return Scoring.Gain(kind, kind == WinKind.Clean ? Streak + 1 : Streak, IsEncore, extra);
        }

        // ---------- flow ----------

        /// <summary>Starts the first song (JS: the <c>show()</c> call at the end of mountShow). An empty show finishes at once.</summary>
        public void Start()
        {
            if (_started) throw new InvalidOperationException("the show has already started");
            _started = true;
            _startedMs = _clock();
            Play();
        }

        /// <summary>The next song, or the end of the show (JS next(): <c>G.i++; show()</c>).</summary>
        public void Next()
        {
            RequireLive();
            Index++;
            Play();
        }

        // show.js:192-214 show()
        private void Play()
        {
            if (Index >= _q.Count) { Finish(); return; }
            Tries = 0;
            Hints = 0;
            MaxHints = 0;
            HintsLocked = false;
            Resolved = false;
            Current = _q[Index];
            var m = Current.Item as MathItem;
            if (IsMath && m != null)
            {
                // show.js:201 a fresh problem takes her level now
                if (!m.Level.HasValue) m.Level = SkillLevel;
                // show.js:202 a bonus replay reuses the same problem
                if (m.Prob == null) m.Prob = _generate(m.Level.Value, m.Seed);
            }
            Raise(new ShowEvent { Kind = ShowEventKind.ItemStarted, Index = Index, Slot = Current, Level = CurrentLevel });
        }

        // ---------- hints ----------

        /// <summary>JS setupHints(max, apply): the mode says how many hints this song has. Resets the count.</summary>
        public void SetupHints(int max)
        {
            RequireSong();
            MaxHints = Math.Max(0, max);
            Hints = 0;
            HintsLocked = false;
            Raise(new ShowEvent { Kind = ShowEventKind.HintsReady, Index = Index, Slot = Current, Amount = MaxHints });
        }

        /// <summary>
        /// The hint button (show.js:123-129). <paramref name="apply"/> gets the hint number (1-based) and shows it;
        /// returning false means there was nothing new to show, so the hint is free and not counted. Returns
        /// true when a hint was counted. A hint never touches the streak or fans already won.
        /// </summary>
        public bool Hint(Func<int, bool> apply = null)
        {
            RequireSong();
            if (HintsLocked || Hints >= MaxHints) return false;
            if (apply != null && !apply(Hints + 1)) return false;
            int before = PotentialGain();
            Hints++;
            HintTotal++;
            _hinted.Add(Current.Item.Kid);
            int cost = Math.Max(0, before - PotentialGain());
            Raise(new ShowEvent { Kind = ShowEventKind.HintUsed, Index = Index, Slot = Current, Level = Hints, Amount = cost, Streak = Streak });
            return true;
        }

        /// <summary>JS lockHints(): turns the hint button off for this song.</summary>
        public void LockHints()
        {
            HintsLocked = true;
        }

        // ---------- outcomes ----------

        /// <summary>A wrong try (show.js:160-165): the streak resets and the song counts as missed. Returns the wrong tries so far.</summary>
        public int Miss()
        {
            RequireOpenSong();
            Tries++;
            Streak = 0;
            _missed.Add(Current.Item.Kid);
            Raise(new ShowEvent { Kind = ShowEventKind.WrongTry, Index = Index, Slot = Current, Tries = Tries, Streak = Streak });
            return Tries;
        }

        /// <summary>
        /// Right answer (show.js:139-159). <paramref name="extra"/> is bonus fans on top (soundcheck's beat the
        /// meter), never halved and never negative. A win after a wrong try puts the song back in the queue.
        /// </summary>
        public void Win(int extra = 0)
        {
            RequireOpenSong();
            if (extra < 0) throw new ArgumentOutOfRangeException(nameof(extra), "bonus fans can't be negative");
            LockHints();
            Resolved = true;
            WinKind kind = Scoring.Kind(Tries, Hints);
            bool clean = kind == WinKind.Clean;
            if (clean) { Streak++; FirstTry++; Best = Math.Max(Best, Streak); }
            else if (kind == WinKind.HintOnly) HintOnly++;
            int gain = Scoring.Gain(kind, Streak, IsEncore, extra);
            Fans += gain;
            Raise(new ShowEvent { Kind = ShowEventKind.FansAwarded, Index = Index, Slot = Current, Amount = gain });
            Record(clean, Tries > 0);
            RecordSkill(true);
            Current.State = Tries > 0 ? SlotState.Miss : SlotState.Done;
            Raise(new ShowEvent { Kind = ShowEventKind.Answered, Index = Index, Slot = Current, Win = kind, Clean = clean, Amount = gain, Streak = Streak, Tries = Tries, Hinted = Hints > 0 });
            if (Tries > 0) Requeue();
        }

        /// <summary>The answer is shown after the last wrong try (show.js:172-181). Puts the song back in the queue.</summary>
        public void Reveal()
        {
            RequireOpenSong();
            LockHints();
            Resolved = true;
            Record(false, true);
            RecordSkill(false);
            Current.State = SlotState.Miss;
            Raise(new ShowEvent { Kind = ShowEventKind.Revealed, Index = Index, Slot = Current, Tries = Tries, Streak = Streak });
            Requeue();
        }

        /// <summary>JS ctx.mistake(tag): which wrong answer she gave, counted on the skill record.</summary>
        public void NoteMistake(string tag)
        {
            RequireSong();
            if (string.IsNullOrEmpty(tag)) return;
            LevelRules.NoteMistake(SkillRecord, tag);
            Raise(new ShowEvent { Kind = ShowEventKind.MistakeNoted, Index = Index, Slot = Current, Tag = tag });
        }

        // show.js:133-137 record()
        private void Record(bool clean, bool missed)
        {
            bool keep = _missed.Contains(Current.Item.Kid);
            Raise(new ShowEvent
            {
                Kind = ShowEventKind.ItemRecorded, Index = Index, Slot = Current,
                Clean = clean, Missed = missed, Keep = keep, Hinted = Hints > 0
            });
        }

        // show.js:151 and 176 P.recordSkill
        private void RecordSkill(bool correct)
        {
            bool hinted = Hints > 0;
            int moved = LevelRules.Record(SkillRecord, correct, hinted, null, Policy);
            Raise(new ShowEvent
            {
                Kind = ShowEventKind.SkillRecorded, Index = Index, Slot = Current,
                Correct = correct, Hinted = hinted, Moved = moved, Level = SkillLevel
            });
            if (moved != 0)
                Raise(new ShowEvent { Kind = ShowEventKind.LevelChanged, Index = Index, Slot = Current, Moved = moved, Level = SkillLevel });
        }

        // show.js:182-187 requeue(), with one intended difference (QUESTIONS 10): a miss on the final song of the
        // queue does not replay at once (she has just seen the worked solution); it goes to the encore and the review
        // queue only, and keeps its bonus slot unused. Any other miss returns three songs later, or as the last song
        // when fewer than three remain. At most Scoring.MaxBonus misses return inside a show.
        private void Requeue()
        {
            if (BonusLeft <= 0 || Current.Bonus)
            {
                Raise(new ShowEvent { Kind = ShowEventKind.RequeueSkipped, Index = Index, Slot = Current, Tag = Current.Bonus ? "bonus" : "no-bonus-left" });
                return;
            }
            if (Index >= _q.Count - 1)
            {
                Raise(new ShowEvent { Kind = ShowEventKind.RequeueSkipped, Index = Index, Slot = Current, Tag = "final-song" });
                return;
            }
            BonusLeft--;
            int at = Math.Min(Index + Scoring.RequeueOffset, _q.Count);
            var slot = new ShowSlot(Current.Item, true);
            _q.Insert(at, slot);
            Raise(new ShowEvent { Kind = ShowEventKind.Requeued, Index = at, Slot = slot });
        }

        // ---------- the end ----------

        // show.js:216-231 finish(), and what ticket.js reads
        private void Finish()
        {
            Current = null;
            Finished = true;
            var prim = _q.Where(x => !x.Bonus).ToList();
            int n = prim.Count;
            double pct = Scoring.Percent(FirstTry, HintOnly, n);
            long ms = _clock() - _startedMs;
            var s = new ShowSummary
            {
                Mode = ModeId,
                Venue = VenueId,
                N = n,
                First = FirstTry,
                Hinted = _hinted.Count,
                Fans = Fans,
                Secs = (int)Math.Floor(ms / 1000.0 + 0.5),
                Best = Best,
                Sold = Scoring.SoldOut(IsEncore, n, pct),
                Encore = IsEncore,
                Pct = pct,
                Vip = pct >= Scoring.VipAt,
                HintOnly = HintOnly,
                HintTotal = HintTotal,
                IsMath = IsMath,
                Missed = _missed.Count
            };
            foreach (ShowSlot x in prim)
            {
                string k = x.Item.Kid;
                bool missed = _missed.Contains(k);
                s.Setlist.Add(new SetlistEntry { Item = x.Item, Tag = missed ? SetlistTag.Miss : _hinted.Contains(k) ? SetlistTag.Hint : SetlistTag.Ok });
                if (missed) s.MissedItems.Add(x.Item);
            }
            s.CanEncore = s.MissedItems.Count > 0 && !IsEncore;
            Summary = s;
            Raise(new ShowEvent { Kind = ShowEventKind.ShowFinished, Index = Index, Summary = s });
        }

        /// <summary>
        /// show.js:226: the encore's songs, the main songs missed this show in a shuffled order (the same item
        /// objects, so a math problem comes back exactly as it was). Empty when nothing was missed.
        /// </summary>
        public List<ShowItem> EncoreItems(Func<double> random)
        {
            if (!Finished) throw new InvalidOperationException("the encore is built when the show is over");
            return ShowPicker.Shuffle(Summary.MissedItems, random);
        }

        /// <summary>The encore show (half fans, no re-inserts), sharing this show's setup and skill record.</summary>
        public ShowEngine CreateEncore(Func<double> random)
        {
            if (!Finished) throw new InvalidOperationException("the encore is built when the show is over");
            if (!Summary.CanEncore) throw new InvalidOperationException("no encore: nothing was missed, or this was the encore");
            var s = _setup.Clone();
            s.Items = EncoreItems(random);
            s.Encore = true;
            return new ShowEngine(s);
        }

        // ---------- guards ----------

        /// <summary>
        /// Every listener hears every event, even if an earlier one throws (the save must not miss an answer because the
        /// dog's handler failed). The engine's own state is already updated. The first error is rethrown once all have
        /// run, so the caller still sees it.
        /// </summary>
        private void Raise(ShowEvent e)
        {
            var h = Raised;
            if (h == null) return;
            Exception first = null;
            foreach (Delegate d in h.GetInvocationList())
            {
                try { ((Action<ShowEvent>)d)(e); }
                catch (Exception ex) { if (first == null) first = ex; }
            }
            if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
        }

        private void RequireLive()
        {
            if (!_started) throw new InvalidOperationException("the show hasn't started");
            if (Finished) throw new InvalidOperationException("the show is over");
        }

        private void RequireSong()
        {
            RequireLive();
            if (Current == null) throw new InvalidOperationException("no song in play");
        }

        private void RequireOpenSong()
        {
            RequireSong();
            if (Resolved) throw new InvalidOperationException("this song is already answered");
        }
    }
}
