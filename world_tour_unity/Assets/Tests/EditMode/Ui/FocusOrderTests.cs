using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using WorldTour.Game.Ui;

namespace WorldTour.Game.EditTests
{
    /// <summary>Polish 6.2B item 9: on the venue card Tab goes through the rows in order, then the sound toggle.</summary>
    public class FocusOrderTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown() { foreach (GameObject g in _made) Object.DestroyImmediate(g); _made.Clear(); }

        private Selectable Button(Transform parent, string name, Vector2 center)
        {
            var go = new GameObject(name, typeof(RectTransform));
            _made.Add(go);
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.sizeDelta = new Vector2(100, 40);
            rt.anchoredPosition = center;
            return go.AddComponent<Button>();
        }

        private static string Order(List<Selectable> l, Transform hud, bool hudLast)
        {
            l.Sort((a, b) => FocusNavigator.Compare(a, b, hud, hudLast));
            return string.Join(",", l.ConvertAll(s => s.name));
        }

        [Test]
        public void HudLast_PutsTheSoundToggleAfterEveryRow()
        {
            var screen = new GameObject("Screen", typeof(RectTransform)); _made.Add(screen);
            var hud = new GameObject("Hud", typeof(RectTransform)); _made.Add(hud);
            var list = new List<Selectable>
            {
                Button(screen.transform, "letters", new Vector2(400, 600)),
                Button(screen.transform, "vowels", new Vector2(400, 500)),
                Button(hud.transform, "sound", new Vector2(40, 400)),      // between the rows by position
                Button(screen.transform, "remix", new Vector2(400, 300)),
            };
            Assert.AreEqual("letters,vowels,sound,remix", Order(new List<Selectable>(list), hud.transform, false), "reading order alone");
            Assert.AreEqual("letters,vowels,remix,sound", Order(new List<Selectable>(list), hud.transform, true));
        }
    }
}
