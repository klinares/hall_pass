using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using WorldTour.Game.Save;

namespace WorldTour.Game.Tests
{
    /// <summary>
    /// SaveStore keeps an unreadable stored save before anything overwrites it (1.5/1.6 tester, D5): a failed copy leaves
    /// the main key alone and plays in memory; a second unreadable save goes to a second slot; the desktop file swap is atomic.
    /// </summary>
    public class SaveStoreTests
    {
        const string Today = "2026-11-20";
        const string Key = "worldtour.v1";

        sealed class FakeBackend : ISaveBackend
        {
            public readonly Dictionary<string, string> Data = new Dictionary<string, string>();
            public readonly HashSet<string> RefuseWrites = new HashSet<string>();
            public bool Persistent => true;
            public string Read(string key) => Data.TryGetValue(key, out string v) ? v : null;
            public bool Write(string key, string text)
            {
                if (RefuseWrites.Contains(key)) return false;
                Data[key] = text;
                return true;
            }
        }

        [Test]
        public void UnreadableSaveIsKeptThenReplaced()
        {
            var b = new FakeBackend();
            b.Data[Key] = "{not json";
            SaveStore st = SaveStore.Open(Today, b);
            Assert.IsNotNull(st.LoadError);
            Assert.IsTrue(st.Rescued);
            Assert.AreEqual("{not json", b.Data[SaveStore.RescueKey]);
            Assert.IsTrue(st.Save());
            Assert.AreNotEqual("{not json", b.Data[Key], "a fresh save replaces it once it is kept");
            Assert.IsTrue(st.Persistent);
        }

        [Test]
        public void FailedBackupLeavesTheMainKeyAndPlaysInMemory()
        {
            var b = new FakeBackend();
            b.Data[Key] = "{not json";
            b.RefuseWrites.Add(SaveStore.RescueKey);
            SaveStore st = SaveStore.Open(Today, b);
            Assert.IsNotNull(st.LoadError);
            Assert.IsFalse(st.Rescued);
            Assert.IsFalse(st.Persistent, "this session runs in memory");
            Assert.IsFalse(st.Save());
            Assert.AreEqual("{not json", b.Data[Key], "the unreadable save is never overwritten");
        }

        [Test]
        public void ASecondUnreadableSaveGoesToTheSecondSlot()
        {
            var b = new FakeBackend();
            b.Data[SaveStore.RescueKey] = "{older";
            b.Data[Key] = "{newer";
            SaveStore st = SaveStore.Open(Today, b);
            Assert.IsTrue(st.Rescued);
            Assert.AreEqual("{older", b.Data[SaveStore.RescueKey], "the older one is kept");
            Assert.AreEqual("{newer", b.Data[SaveStore.RescueKey2]);
            Assert.IsTrue(st.Save());
        }

        [Test]
        public void TheSameUnreadableTextIsNotCopiedTwice()
        {
            var b = new FakeBackend();
            b.Data[SaveStore.RescueKey] = "{not json";
            b.Data[Key] = "{not json";
            SaveStore st = SaveStore.Open(Today, b);
            Assert.IsTrue(st.Rescued);
            Assert.IsFalse(b.Data.ContainsKey(SaveStore.RescueKey2));
        }

        [Test]
        public void BothSlotsTakenLeavesEverythingAlone()
        {
            var b = new FakeBackend();
            b.Data[SaveStore.RescueKey] = "{first";
            b.Data[SaveStore.RescueKey2] = "{second";
            b.Data[Key] = "{third";
            SaveStore st = SaveStore.Open(Today, b);
            Assert.IsFalse(st.Rescued);
            Assert.IsFalse(st.Persistent);
            st.Save();
            Assert.AreEqual("{first", b.Data[SaveStore.RescueKey]);
            Assert.AreEqual("{second", b.Data[SaveStore.RescueKey2]);
            Assert.AreEqual("{third", b.Data[Key]);
        }

        [Test]
        public void FileBackendReplacesInOneStep()
        {
            string dir = Path.Combine(Path.GetTempPath(), "wt-savestore-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var f = new FileBackend(dir);
                Assert.IsTrue(f.Write(Key, "one"));
                Assert.IsTrue(f.Write(Key, "two"));
                Assert.AreEqual("two", f.Read(Key));
                Assert.AreEqual(1, Directory.GetFiles(dir).Length, "no .tmp left behind");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
