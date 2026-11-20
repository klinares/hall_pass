using System;
using System.Collections.Generic;
using NUnit.Framework;
using WorldTour.Core.Shows;

namespace WorldTour.Core.Tests
{
    /// <summary>A listener that throws can't keep the others (the save) from hearing an event.</summary>
    public class ShowEngineRaiseTests
    {
        [Test]
        public void AThrowingListener_TheOthersStillHear_ThenTheErrorSurfaces()
        {
            var e = ShowTestDriver.Spelling(ShowTestDriver.Words(3));
            var before = new List<ShowEventKind>();
            var after = new List<ShowEventKind>();
            e.Raised += ev => before.Add(ev.Kind);
            e.Raised += ev => { if (ev.Kind == ShowEventKind.WrongTry) throw new InvalidOperationException("the dog failed"); };
            e.Raised += ev => after.Add(ev.Kind);
            e.Start();
            e.SetupHints(2);
            var ex = Assert.Throws<InvalidOperationException>(() => e.Miss());
            Assert.AreEqual("the dog failed", ex.Message);
            Assert.AreEqual(1, e.Tries, "the engine's state moved on");
            Assert.AreEqual(0, e.Streak);
            Assert.Contains(ShowEventKind.WrongTry, before);
            Assert.Contains(ShowEventKind.WrongTry, after, "the listener after the failing one still heard it");
            e.Win();
            Assert.AreEqual(300, e.Fans);
            Assert.Contains(ShowEventKind.Answered, after);
        }
    }
}
