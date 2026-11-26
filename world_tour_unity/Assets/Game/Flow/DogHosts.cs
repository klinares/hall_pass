using System;
using UnityEngine;
using WorldTour.Game.Mascot;
using WorldTour.Game.Motion;
using WorldTour.Game.Show;
using WorldTour.Game.Ticket;
using WorldTour.Game.Ui;

namespace WorldTour.Game.Flow
{
    /// <summary>
    /// Puts riff where the screens ask for him. A show: <see cref="OpenWithDog"/> adds a dog with its bubble and a DogShowDirector to the
    /// show screen as the stack places it, so the director follows the engine from its first event (ShowScreen raises
    /// EngineReady inside Open, before Open returns), then moves the dog into the show's CoachArea when it starts.
    /// Each screen owns its own dog, so a dog is never lost when a screen is destroyed. The art comes from the theme
    /// asset (ThemeAsset.dogArt); the motion is the 2.2 tweener and reduce-motion setting, through the mascot ports.
    /// </summary>
    public static class DogHosts
    {
        /// <summary>The coach dog's size in a show (web: 190 at 1920x1080; the coach column is 280 wide).</summary>
        const float CoachDog = 190f;

        /// <summary>Phase 8.1: where the world wants riff's bubble drawn while it hosts a show (<see cref="ShowLayout.Compact"/>). Null = the usual dog card.</summary>
        public static RectTransform BubbleHost { get; set; }
        public const float CompactBubbleWidth = 340f;

        /// <summary>Destroys the world-side bubble with the show's coach.</summary>
        sealed class BubbleOwner : MonoBehaviour
        {
            public GameObject Bubble;
            void OnDestroy() { if (Bubble != null) Destroy(Bubble); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Wire()
        {
            ShowScreen.Started -= OnShowStarted;
            ShowScreen.Started += OnShowStarted;
            TicketScreen.Opened -= OnTicketOpened;
            TicketScreen.Opened += OnTicketOpened;
        }

        // ---------- the ticket ----------

        /// <summary>ticket.js:79-82 the coach on the ticket: riff in the cheer pose beside the ticket's bubble.</summary>
        static void OnTicketOpened(TicketScreen ticket)
        {
            if (ticket == null || ticket.CoachArea == null) return;
            if (Art == null) { Debug.LogWarning("[dog] no dog art in the theme asset; the ticket shows no riff"); return; }
            if (ticket.CoachArea.GetComponentInChildren<DogRig>(true) != null) return;
            DogRig rig = NewRig(ticket.CoachArea);
            rig.SetPose(DogPose.Cheer);
            rig.Play(DogMotion.Hop);
        }

        static DogArt Art { get { return Theme.Asset != null ? Theme.Asset.dogArt : null; } }

        static DogRig NewRig(Transform parent)
        {
            var go = new GameObject("Dog", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            DogRig rig = go.AddComponent<DogRig>();
            rig.Init(Art, TweenerPort.Instance, MotionSettingsPort.Instance);
            rig.SetOutfit(DogPoses.OutfitOf(Boot.Blob));   // riff wears his backstage outfit everywhere (shop.js, look.js setOutfit)
            return rig;
        }

        // ---------- a show ----------

        /// <summary>
        /// Runs <paramref name="open"/> (ShowScreen.Open, OpenEncore or OpenAgain on <paramref name="stack"/>) with the
        /// dog attached to the new show screen before its show starts.
        /// </summary>
        public static ShowScreen OpenWithDog(ScreenStack stack, Func<ShowScreen> open)
        {
            if (stack == null || open == null) return open != null ? open() : null;
            bool attached = false;
            Action<GameScreen> hook = top =>
            {
                if (attached || !(top is ShowScreen s)) return;
                attached = true;
                try { AttachToShow(s); }
                catch (Exception e) { Debug.LogException(e); }
            };
            stack.TopChanged += hook;
            try { return open(); }
            finally { stack.TopChanged -= hook; }
        }

        static void AttachToShow(ShowScreen show)
        {
            if (Art == null) { Debug.LogWarning("[dog] no dog art in the theme asset; the show runs without riff"); return; }
            var go = new GameObject("Coach", typeof(RectTransform));
            var root = (RectTransform)go.transform;
            root.SetParent(show.Rect, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;

            if (ShowLayout.Compact && BubbleHost != null)
            {
                // phase 8.1: the world hosts the show. No dog card in the panel: riff's lines are a speech bubble in the world's canvas, which the
                // world places by the 3D riff. The director (and so the bubble) lives and dies with the show screen.
                var bubbleHostGo = new GameObject("Bubble", typeof(RectTransform));
                var hostRt = (RectTransform)bubbleHostGo.transform;
                hostRt.SetParent(BubbleHost, false);
                hostRt.anchorMin = hostRt.anchorMax = new Vector2(.5f, .5f);
                hostRt.pivot = new Vector2(.5f, 0f);
                hostRt.sizeDelta = new Vector2(CompactBubbleWidth, 64f);
                DogBubble worldBubble = bubbleHostGo.AddComponent<DogBubble>();
                worldBubble.Init(Art, Theme.Font(FontRole.BodyBold));
                worldBubble.Say(null);
                go.AddComponent<BubbleOwner>().Bubble = bubbleHostGo;
                DogShowDirector worldDirector = go.AddComponent<DogShowDirector>();
                worldDirector.Init(null, worldBubble);
                worldDirector.Follow(show);
                return;
            }

            // the dog sits at the bottom of the coach column, the bubble above it
            var dogGo = new GameObject("DogBox", typeof(RectTransform));
            var dogBox = (RectTransform)dogGo.transform;
            dogBox.SetParent(root, false);
            dogBox.anchorMin = dogBox.anchorMax = new Vector2(0.5f, 0f);
            dogBox.pivot = new Vector2(0.5f, 0f);
            dogBox.sizeDelta = new Vector2(CoachDog, CoachDog);
            dogBox.anchoredPosition = Vector2.zero;
            DogRig rig = NewRig(dogBox);

            var bubbleGo = new GameObject("Bubble", typeof(RectTransform));
            var bubbleRt = (RectTransform)bubbleGo.transform;
            bubbleRt.SetParent(root, false);
            bubbleRt.anchorMin = new Vector2(0f, 0f); bubbleRt.anchorMax = new Vector2(1f, 0f);
            bubbleRt.pivot = new Vector2(0.5f, 0f);
            bubbleRt.anchoredPosition = new Vector2(0f, CoachDog + 18f);
            bubbleRt.sizeDelta = new Vector2(0f, 64f);
            DogBubble bubble = bubbleGo.AddComponent<DogBubble>();
            bubble.Init(Art, Theme.Font(FontRole.BodyBold));   // polish 6.2E item 5: the regular weight read light grey at 1366
            bubble.Say(null);

            DogShowDirector director = go.AddComponent<DogShowDirector>();
            director.Init(rig, bubble);
            director.Follow(show);
        }

        /// <summary>The show built its layout: move the coach into its column (Follow ran before CoachArea existed).</summary>
        static void OnShowStarted(ShowScreen show)
        {
            if (show == null || show.CoachArea == null) return;
            DogShowDirector d = show.GetComponentInChildren<DogShowDirector>(true);
            if (d == null || d.transform.parent == show.CoachArea) return;
            var rt = (RectTransform)d.transform;
            rt.SetParent(show.CoachArea, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
