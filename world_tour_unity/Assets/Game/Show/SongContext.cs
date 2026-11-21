using System;
using UnityEngine;
using WorldTour.Core.Data;
using WorldTour.Core.Shows;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Show
{
    /// <summary>
    /// What a mode gets for one song: the web game's <c>ctx</c> (show.js:207-213). Every call is safe to make twice
    /// or late: once the song is answered, the show has moved on or finished, or the screen is gone, Win, Miss, Reveal,
    /// Next and Later do nothing, so a double click can never reach the engine (which would throw).
    /// </summary>
    public sealed class SongContext
    {
        private readonly ShowScreen _host;

        /// <summary>The song's position in the queue.</summary>
        public readonly int Index;
        public readonly ShowEngine Engine;
        /// <summary>The slot being played (a bonus replay has <see cref="ShowSlot.Bonus"/>).</summary>
        public readonly ShowSlot Slot;
        /// <summary>Where the mode draws (show.js <c>ctx.el</c>).</summary>
        public readonly RectTransform Area;
        /// <summary>The source every random choice uses (Math.random in the JS).</summary>
        public readonly Func<double> Random;
        public readonly StickerButton ButtonPrefab;

        internal SongContext(ShowScreen host, int index, ShowEngine engine, ShowSlot slot, RectTransform area, Func<double> random, StickerButton buttonPrefab)
        {
            _host = host; Index = index; Engine = engine; Slot = slot; Area = area; Random = random; ButtonPrefab = buttonPrefab;
        }

        /// <summary>The word (show.js <c>ctx.item</c>).</summary>
        public WordItem Item { get { return Slot.Item as WordItem; } }
        /// <summary>The week's full word record (clue, syllables), when the show was built from a week.</summary>
        public Word Word { get { return Item != null ? Item.Data as Word : null; } }
        /// <summary>The week the show plays (show.js ctx.week; fan mail draws its choices from it). Null when the show has none.</summary>
        public Week Week { get { return _host != null && _host.Request != null ? _host.Request.Week : null; } }
        /// <summary>studio.js:20 the vinyl sticker for the record button, or null.</summary>
        public Sprite RecordSticker { get { return _host != null ? _host.RecordStickerSprite : null; } }
        /// <summary>The clue (word.h).</summary>
        public string Clue { get { return Word != null ? WorldTour.Core.Data.TextRules.Curly(Word.h ?? "") : ""; } }
        /// <summary>show.js:208 <c>level</c>: the skill's level for a spelling song.</summary>
        public int Level { get { return Engine.CurrentLevel; } }
        public bool Bonus { get { return Slot.Bonus; } }
        public int Tries { get { return Engine.Tries; } }
        public int Hints { get { return Engine.Hints; } }

        /// <summary>True while this song is the one being played and isn't answered yet.</summary>
        public bool Open { get { return Live && !Engine.Resolved; } }

        /// <summary>True while this song is the one on screen.</summary>
        public bool Live { get { return _host != null && _host.IsCurrent(this) && !Engine.Finished; } }

        /// <summary>ctx.say: the dog's bubble. pose: "idle", "think", "oops", "cheer", or null to keep the pose.</summary>
        public void Say(string line, string pose = null)
        {
            if (Live) _host.RaiseSaid(line, pose);
        }

        /// <summary>ctx.miss: a wrong try. Returns the wrong tries so far (unchanged if the song is already answered).</summary>
        public int Miss()
        {
            if (!Open) return Engine.Tries;
            return Engine.Miss();
        }

        /// <summary>ctx.win. False when ignored (already answered, or not this song any more).</summary>
        public bool Win(int extra = 0)
        {
            if (!Open) return false;
            Engine.Win(extra);
            return true;
        }

        /// <summary>ctx.reveal. False when ignored.</summary>
        public bool Reveal()
        {
            if (!Open) return false;
            Engine.Reveal();
            return true;
        }

        /// <summary>ctx.next(ms): the next song after a pause, unless the show has already moved on.</summary>
        public void Next(float seconds)
        {
            if (Live) _host.ScheduleNext(this, seconds);
        }

        /// <summary>ctx.later: runs after a pause, only while this song is still on screen.</summary>
        public void Later(Action action, float seconds)
        {
            if (Live) _host.Later(this, action, seconds);
        }

        /// <summary>ctx.setupHints(max, apply): once per song. apply(n) shows hint n and returns false when there was nothing new (free).</summary>
        public void SetupHints(int max, Func<int, bool> apply)
        {
            if (Live) _host.SetupHints(this, max, apply);
        }

        /// <summary>ctx.mistake(tag): which known mistake a wrong answer matched, counted on the skill (show.js:211).</summary>
        public void Mistake(string tag)
        {
            if (Open) Engine.NoteMistake(tag);
        }

        /// <summary>show.js:209 ctx.prob: the math problem for this song (null for a word).</summary>
        public WorldTour.Core.Maths.Gen.Problem Problem { get { var m = Slot.Item as MathItem; return m != null ? m.Prob : null; } }

        /// <summary>The map's sprites (stickers by name), for modes that draw stickers (merch table), or null.</summary>
        public WorldTour.Game.Map.MapArt Art { get { return _host != null ? _host.ArtAsset : null; } }

        /// <summary>ctx.lockHints: the hint button off for the rest of the song.</summary>
        public void LockHints()
        {
            if (Live) { Engine.LockHints(); _host.PaintHint(); }
        }

        /// <summary>The mode's own pause on the hint button (remix disables it for 750 ms after a wrong try).</summary>
        public void BlockHints(bool blocked)
        {
            if (Live) _host.SetHintBlocked(blocked);
        }

        /// <summary>The hint button for this song, placed in the mode's controls (show.js ctx.hintBtn()).</summary>
        public ShowButton HintButton(RectTransform parent)
        {
            return _host.MakeHintButton(this, parent);
        }
    }
}
