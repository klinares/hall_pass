using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WorldTour.Core.Progress;
using WorldTour.Core.Shows;
using WorldTour.Game.Data;
using WorldTour.Game.Map;
using WorldTour.Game.Modes;
using WorldTour.Game.Show;
using WorldTour.Game.Ticket;
using WorldTour.Game.Ui;
using WorldTour.Game.Util;

namespace WorldTour.Game.Tests
{
    /// <summary>
    /// Task 2.7: a full remix show in the real scenes, driven through the UI objects' own event handlers (no pixel
    /// positions): Boot, the start card, the map, the coffee shop's card (a chosen week), remix; a deliberate miss
    /// (it comes back three songs later as the bonus song, and again in the encore), a hint (fewer fans, streak kept),
    /// one tile dragged onto the slots, spam clicks on the last tile and on the hint, to the ticket; the encore to its
    /// ticket; back to the map, which plays the new venue's unlock moment once. The save is seeded beforehand (the
    /// Editor's own save file is put back afterwards) and checked after each ticket: fans, venues, review, history,
    /// stickers, and nothing earned going down. Any error or exception log fails the test.
    /// </summary>
    public class RemixShowTests
    {
        const string Week = "2026-10-10";          // the 10-word week, not the default one: the week chips are used
        const long SeedFans = 19000;                // one clean song crosses 20,000: the record store unlocks mid-show

        string _savePath;
        string _savedOriginal;
        bool _hadOriginal;
        readonly List<string> _logs = new List<string>();
        readonly List<string> _errors = new List<string>();
        readonly List<string> _warnings = new List<string>();

        void OnLog(string message, string stack, LogType type)
        {
            _logs.Add(message);
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors.Add(type + ": " + message + "\n" + stack);
            else if (type == LogType.Warning) _warnings.Add(message);
        }

        [SetUp]
        public void SetUp()
        {
            _savePath = Path.Combine(Application.persistentDataPath, SaveBlob.Key + ".json");
            _hadOriginal = File.Exists(_savePath);
            _savedOriginal = _hadOriginal ? File.ReadAllText(_savePath) : null;
            SaveBlob seed = SaveBlob.Defaults(LocalClock.TodayIso());
            seed.Fans.Lifetime = SeedFans;
            seed.Fans.Balance = SeedFans;
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(_savePath, SaveJson.ToText(seed));
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            if (_hadOriginal) File.WriteAllText(_savePath, _savedOriginal);
            else if (File.Exists(_savePath)) File.Delete(_savePath);
        }

        // ------------------------------------------------------------------ the test

        [UnityTest, Timeout(240000)]
        public IEnumerator FullRemixShowWithMissHintAndEncoreToTicketAndBackToMap()
        {
            // ---- Boot and the start card
            SceneManager.LoadScene("Boot");
            yield return Until(() => Boot.Ready, 30f, "Boot never became ready (data and save)");
            Assert.IsNotNull(Boot.Store, "Boot opened no save");
            Assert.AreEqual(SeedFans, Boot.Store.Blob.Fans.Lifetime, "the seeded save was not loaded");
            StartCard card = UnityEngine.Object.FindFirstObjectByType<StartCard>();
            Assert.IsNotNull(card, "no start card");
            StickerButton start = card.GetComponentsInChildren<StickerButton>(true).FirstOrDefault();
            Assert.IsNotNull(start, "the start card has no button");
            yield return null;
            Press(start.gameObject);

            // ---- the map
            ScreenStack stack = null;
            yield return Until(() => (stack = UnityEngine.Object.FindFirstObjectByType<ScreenStack>()) != null && stack.Top is MapScreen, 30f, "the map never showed");
            var map = (MapScreen)stack.Top;
            Assert.IsTrue(_logs.Any(l => l.StartsWith("WT_MAP_READY")), "no WT_MAP_READY");
            Assert.AreEqual(0, map.Model.Fresh.Count, "nothing should be fresh before the show");
            Transform coffee = FindDeep(map.transform, "Venue_coffee");
            Assert.IsNotNull(coffee, "no coffee shop on the map");
            Press(coffee.gameObject);                                       // during the map's own entrance: input is never blocked

            // ---- the venue card: pick the week, then remix
            yield return Until(() => stack.Top is VenueCard, 5f, "the venue card never opened");
            var venue = (VenueCard)stack.Top;
            Assert.AreEqual("coffee", venue.VenueId);
            yield return null;
            Transform chip = FindDeep(venue.transform, "Week_" + Week);
            Assert.IsNotNull(chip, "no week chip for " + Week);
            Press(chip.gameObject);
            yield return null;
            yield return null;                                              // the card rebuilds (old rows are destroyed at frame end)
            Transform remix = FindDeep(venue.transform, "Mode_remix", activeOnly: true);
            Assert.IsNotNull(remix, "no remix row on the card");
            Press(remix.gameObject);

            // ---- the show
            yield return Until(() => stack.Top is ShowScreen, 5f, "the show never opened");
            var show = (ShowScreen)stack.Top;
            Assert.IsTrue(_logs.Any(l => l == "WT_SHOW_REQUEST venue=coffee week=" + Week + " mode=remix"), "the show request did not carry the chosen week");
            Assert.AreEqual(Week, show.Request.Week != null ? show.Request.Week.id : null);
            ShowEngine engine = show.Engine;
            Assert.AreEqual(10, engine.SongCount, "the week has 10 words");
            var said = new List<string>();
            show.Said += (line, pose) => said.Add(line);

            TicketScreen ticket = null;
            Action<TicketScreen> onTicket = t => ticket = t;
            TicketScreen.Opened += onTicket;
            try
            {
                string missedWord = null, hintedWord = null;
                int play = 0;
                while (!engine.Finished)
                {
                    int i = engine.Index;
                    RemixMode mode = null;
                    yield return Until(() => engine.Finished || (engine.Index == i && (mode = ModeAt(show, i)) != null && mode.Board != null && !engine.Resolved), 10f, "song " + (i + 1) + " never became playable");
                    if (engine.Finished) break;
                    string word = mode.Board.Word;
                    int fansBefore = engine.Fans, streakBefore = engine.Streak;

                    if (play == 0)
                    {
                        // a deliberate miss: a full wrong spelling, then the right one (the first tile dragged)
                        missedWord = word;
                        yield return Spell(mode, WrongOrder(mode.Board));
                        Assert.AreEqual(1, engine.Tries, "the wrong spelling should count one try");
                        Assert.AreEqual(0, engine.Streak);
                        Assert.IsTrue(said.Any(l => l.StartsWith("not quite. it")), "the first ladder line was not said: " + string.Join(" | ", said));
                        yield return Until(() => !mode.Board.Busy && mode.Board.Picked.Count == 0, 3f, "the slots never cleared after the miss");
                        List<int> right = RightOrder(mode.Board);
                        yield return Drag(mode, right[0]);
                        Assert.AreEqual(1, mode.Board.Picked.Count, "the dragged tile did not land in the slots");
                        yield return Spell(mode, right.Skip(1).ToList());
                        yield return Until(() => engine.Resolved, 3f, "the song did not resolve");
                        Assert.AreEqual(300, engine.Fans - fansBefore, "a win after a miss is 300 fans");
                        ShowSlot back = engine.Queue.Count > i + 4 ? engine.Queue[i + 4] : null;
                        Assert.IsNotNull(back, "no slot three songs later");
                        Assert.IsTrue(back.Bonus && back.Item.Kid == engine.Queue[i].Item.Kid, "the missed word is not three songs later as the bonus song");
                    }
                    else if (play == 2)
                    {
                        // a hint, spam-clicked: one counts; fewer fans; the streak stays
                        hintedWord = word;
                        Assert.Greater(streakBefore, 0, "the hint needs a streak to protect");
                        StickerButton hint = HintButton(mode);
                        Assert.IsNotNull(hint, "no hint button");
                        int left = engine.HintsLeft;
                        Press(hint.gameObject); Press(hint.gameObject); Press(hint.gameObject);
                        yield return null;
                        Assert.AreEqual(1, engine.Hints, "three quick hint clicks must count once");
                        Assert.AreEqual(left - 1, engine.HintsLeft);
                        Assert.GreaterOrEqual(mode.Board.Glow, 0, "the hint lit no tile");
                        Assert.AreEqual(word[0], mode.Board.Mix[mode.Board.Glow], "the glowing tile is not the next letter");
                        Assert.AreEqual(streakBefore, engine.Streak, "the hint broke the streak");
                        yield return new WaitForSecondsRealtime(0.4f);
                        Press(hint.gameObject);                             // a tile already glows: free, not counted
                        yield return null;
                        Assert.AreEqual(1, engine.Hints, "a second hint while a tile glows must be free");
                        yield return Spell(mode, RightOrder(mode.Board));
                        yield return Until(() => engine.Resolved, 3f, "the hinted song did not resolve");
                        Assert.AreEqual(500, engine.Fans - fansBefore, "a hinted win is 500 fans");
                        Assert.AreEqual(streakBefore, engine.Streak, "a hinted win must keep the streak");
                    }
                    else if (play == 3)
                    {
                        // spam the last tile: one answer, no exception
                        List<int> right = RightOrder(mode.Board);
                        yield return Spell(mode, right.Take(right.Count - 1).ToList());
                        GameObject last = TileGo(mode, right[right.Count - 1]);
                        Press(last); Press(last); Press(last);
                        yield return null;
                        Press(last);
                        yield return null;
                        int results = _logs.Count(l => l.StartsWith("WT_SONG_RESULT i=" + (i + 1) + " "));
                        Assert.AreEqual(1, results, "spam clicks on the last tile gave " + results + " results");
                        Assert.IsTrue(engine.Resolved);
                    }
                    else
                    {
                        yield return Spell(mode, RightOrder(mode.Board));
                        yield return Until(() => engine.Resolved, 3f, "song " + (i + 1) + " did not resolve");
                    }
                    play++;
                    yield return Until(() => engine.Finished || engine.Index != i, 6f, "the show did not move on from song " + (i + 1));
                }

                // ---- the ticket
                yield return Until(() => ticket != null && stack.Top == ticket, 5f, "the ticket never opened");
                Assert.AreEqual(11, play, "10 songs plus the bonus replay of the missed one; got " + play + " (bonus slots: " + engine.Queue.Count(q => q.Bonus) + ")");
                ShowSummary s = ticket.Data.Summary;
                Assert.AreEqual(10, s.N);
                Assert.IsTrue(s.CanEncore, "a missed word should offer the encore");
                Assert.AreEqual(TicketPrimary.Encore, ticket.Model.Primary);
                Assert.AreEqual(1, ticket.Model.MissedCount);
                Assert.AreEqual("encore: 1 to rehearse", ticket.Model.PrimaryLabel);
                Assert.AreEqual(1, s.HintTotal);
                Assert.AreEqual(SetlistTag.Miss, s.Setlist.First(e => ((WordItem)e.Item).W == missedWord).Tag);
                Assert.AreEqual(SetlistTag.Hint, s.Setlist.First(e => ((WordItem)e.Item).W == hintedWord).Tag);
                Assert.AreEqual(8, s.Setlist.Count(e => e.Tag == SetlistTag.Ok));
                Assert.IsNotNull(ticket.Model.Unlock, "crossing 20,000 fans should show the new venue");
                Assert.AreEqual("records", ticket.Model.Unlock.Id);

                SaveBlob blob = Boot.Store.Blob;
                Assert.AreEqual(SeedFans + s.Fans, blob.Fans.Lifetime, "lifetime fans");
                Assert.AreEqual(SeedFans + s.Fans, blob.Fans.Balance, "fans to spend");
                CollectionAssert.Contains(blob.Venues.Unlocked, "records");
                CollectionAssert.Contains(ProgressRules.ReviewWords(blob), missedWord, "the missed word is not in the review queue");
                Assert.AreEqual(1, blob.Shows.Count, "the show is not in the history");
                Assert.AreEqual(1, blob.Words[missedWord].Miss);
                Assert.AreEqual(1, blob.Words[hintedWord].H);
                SaveBlob onDisk = SaveJson.Load(File.ReadAllText(_savePath), LocalClock.TodayIso()).Blob;
                Assert.AreEqual(blob.Fans.Lifetime, onDisk.Fans.Lifetime, "the save file was not written at the end of the show");
                CollectionAssert.Contains(onDisk.Venues.Unlocked, "records");
                CollectionAssert.IsSubsetOf(blob.Journal.Stickers.Keys.ToList(), onDisk.Journal.Stickers.Keys.ToList());
                Snapshot afterShow = Snapshot.Of(blob);

                // ---- the encore, pressed in the ticket's first frames (input is never blocked)
                ShowScreen encore = null;
                TicketScreen first = ticket;
                ticket = null;
                Press(first.PrimaryButton.gameObject);
                Press(first.PrimaryButton.gameObject);                      // a double click must not open two encores
                yield return Until(() => (encore = stack.Top as ShowScreen) != null, 5f, "the encore never opened");
                Assert.IsTrue(encore.Request.Encore);
                Assert.AreEqual(1, encore.Engine.SongCount, "the encore replays only the missed word");
                Assert.AreEqual(3, stack.Count, "the encore should replace the ticket (map, venue card, encore)");
                ShowEngine e2 = encore.Engine;
                RemixMode em = null;
                yield return Until(() => (em = ModeAt(encore, 0)) != null && em.Board != null, 5f, "the encore song never showed");
                Assert.AreEqual(missedWord, em.Board.Word, "the encore song is not the missed word");
                yield return Spell(em, RightOrder(em.Board));
                yield return Until(() => e2.Resolved, 3f, "the encore song did not resolve");
                Assert.AreEqual(500, e2.Fans, "a clean encore song is half of 1,000");
                yield return Until(() => ticket != null && stack.Top == ticket, 6f, "the encore ticket never opened");
                Assert.IsTrue(ticket.Data.Summary.Encore);
                Assert.AreEqual(TicketPrimary.PlayAgain, ticket.Model.Primary, "the encore ticket offers play again");
                Snapshot afterEncore = Snapshot.Of(Boot.Store.Blob);
                afterShow.AssertNothingLost(afterEncore);
                Assert.AreEqual(afterShow.Lifetime + 500, afterEncore.Lifetime);
                Assert.AreEqual(2, Boot.Store.Blob.Shows.Count);

                // ---- back to the map: the record store's unlock moment, once
                int unlockLines = _logs.Count(l => l.StartsWith("WT_UNLOCK_SHOWN"));
                Assert.AreEqual(0, unlockLines, "no unlock moment before the map");
                Press(ticket.BackButton.gameObject);
                yield return Until(() => stack.Top is MapScreen, 5f, "back to the map did nothing");
                Assert.AreEqual(1, stack.Count);
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.AreEqual(1, _logs.Count(l => l == "WT_UNLOCK_SHOWN id=records"), "the record store's unlock moment should play once");
                CollectionAssert.Contains(Boot.Store.Blob.Venues.Seen, "records");
                onDisk = SaveJson.Load(File.ReadAllText(_savePath), LocalClock.TodayIso()).Blob;
                CollectionAssert.Contains(onDisk.Venues.Seen, "records", "seen is not saved, so the moment would play again after a reload");
                afterEncore.AssertNothingLost(Snapshot.Of(onDisk));

                // the map shown again (venue card, back) does not replay it
                Transform rec = FindDeep(stack.Top.transform, "Venue_records");
                Assert.IsNotNull(rec);
                Press(rec.gameObject);
                yield return Until(() => stack.Top is VenueCard, 5f, "the record store card never opened");
                yield return null;
                stack.Pop();
                yield return new WaitForSecondsRealtime(1.0f);
                Assert.AreEqual(1, _logs.Count(l => l.StartsWith("WT_UNLOCK_SHOWN")), "the unlock moment played twice");
            }
            finally
            {
                TicketScreen.Opened -= onTicket;
            }

            Assert.IsEmpty(_errors, "error logs:\n" + string.Join("\n", _errors));
            Assert.IsEmpty(_warnings, "warning logs:\n" + string.Join("\n", _warnings));
        }

        // ------------------------------------------------------------------ helpers

        sealed class Snapshot
        {
            public long Lifetime, Balance;
            public List<string> Unlocked, Seen, Stickers, Owned;
            public int Shows;

            public static Snapshot Of(SaveBlob b)
            {
                return new Snapshot
                {
                    Lifetime = b.Fans.Lifetime, Balance = b.Fans.Balance, Unlocked = b.Venues.Unlocked.ToList(), Seen = b.Venues.Seen.ToList(),
                    Stickers = b.Journal.Stickers.Keys.ToList(), Owned = b.Inventory.Owned.ToList(), Shows = b.Shows.Count
                };
            }

            public void AssertNothingLost(Snapshot later)
            {
                Assert.GreaterOrEqual(later.Lifetime, Lifetime, "lifetime fans went down");
                Assert.GreaterOrEqual(later.Balance, Balance, "fans to spend went down (nothing was bought)");
                CollectionAssert.IsSubsetOf(Unlocked, later.Unlocked, "a venue was lost");
                CollectionAssert.IsSubsetOf(Seen, later.Seen, "a seen venue was lost");
                CollectionAssert.IsSubsetOf(Stickers, later.Stickers, "a sticker was lost");
                CollectionAssert.IsSubsetOf(Owned, later.Owned, "an owned item was lost");
                Assert.GreaterOrEqual(later.Shows, Shows, "show history went down");
            }
        }

        static IEnumerator Until(Func<bool> done, float seconds, string failure)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail(failure);
                yield return null;
            }
        }

        /// <summary>A left click on <paramref name="go"/>: down, up, click, as the EventSystem sends them.</summary>
        static void Press(GameObject go)
        {
            var e = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(go, e, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(go, e, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(go, e, ExecuteEvents.pointerClickHandler);
        }

        static Transform FindDeep(Transform root, string name, bool activeOnly = false)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(!activeOnly))
                if (t.name == name) return t;
            return null;
        }

        static RemixMode ModeAt(ShowScreen show, int index)
        {
            Transform area = FindDeep(show.transform, "Song " + (index + 1), activeOnly: true);
            return area != null ? area.GetComponent<RemixMode>() : null;
        }

        static GameObject TileGo(RemixMode mode, int index)
        {
            LetterTile t = mode.GetComponentsInChildren<LetterTile>(true).First(x => x.Index == index);
            return t.gameObject;
        }

        static StickerButton HintButton(RemixMode mode)
        {
            Transform t = FindDeep(mode.transform, "Hint");
            return t != null ? t.GetComponent<StickerButton>() : null;
        }

        /// <summary>Rack indexes that spell the word: for each letter, the first unused tile with it.</summary>
        static List<int> RightOrder(RemixBoard b)
        {
            var used = new HashSet<int>(b.Picked);
            var order = new List<int>();
            for (int k = b.Picked.Count; k < b.Word.Length; k++)
            {
                int i = Enumerable.Range(0, b.Mix.Count).First(x => !used.Contains(x) && b.Mix[x] == b.Word[k]);
                used.Add(i);
                order.Add(i);
            }
            return order;
        }

        /// <summary>A full spelling that is wrong: the right order with the first two different letters swapped.</summary>
        static List<int> WrongOrder(RemixBoard b)
        {
            List<int> order = RightOrder(b);
            for (int k = 1; k < order.Count; k++)
                if (b.Mix[order[k]] != b.Mix[order[0]]) { int x = order[0]; order[0] = order[k]; order[k] = x; return order; }
            Assert.Fail("no wrong spelling exists for " + b.Word);
            return order;
        }

        static IEnumerator Spell(RemixMode mode, List<int> order)
        {
            foreach (int i in order)
            {
                Press(TileGo(mode, i));
                yield return null;
            }
        }

        /// <summary>Drags a tile onto the slot row and lets go there (begin, drag, end, as the EventSystem sends them).</summary>
        static IEnumerator Drag(RemixMode mode, int index)
        {
            GameObject tile = TileGo(mode, index);
            var slots = (RectTransform)FindDeep(mode.transform, "Slots");
            Assert.IsNotNull(slots, "no slot row");
            Vector2 from = RectTransformUtility.WorldToScreenPoint(null, tile.transform.position);
            Vector2 to = RectTransformUtility.WorldToScreenPoint(null, slots.TransformPoint(slots.rect.center));
            var e = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = from, pressPosition = from };
            ExecuteEvents.Execute(tile, e, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(tile, e, ExecuteEvents.beginDragHandler);
            yield return null;
            e.position = Vector2.Lerp(from, to, 0.5f);
            ExecuteEvents.Execute(tile, e, ExecuteEvents.dragHandler);
            yield return null;
            e.position = to;
            ExecuteEvents.Execute(tile, e, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(tile, e, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(tile, e, ExecuteEvents.endDragHandler);
            yield return null;
        }
    }
}
