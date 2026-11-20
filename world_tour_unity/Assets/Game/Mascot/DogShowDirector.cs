using System;
using UnityEngine;
using WorldTour.Core.Shows;
using WorldTour.Game.Modes;
using WorldTour.Game.Show;

namespace WorldTour.Game.Mascot
{
    /// <summary>
    /// riff in a show. Follow(screen) uses exactly the show screen's four hooks:
    /// <list type="bullet">
    /// <item>CoachArea: the dog (this object, with its rig and bubble) moves into it and fills it.</item>
    /// <item>EngineReady(engine, random): each engine event goes through DogCues (the one table); DogCoach decides
    /// whether the pose changes and a reaction plays.</item>
    /// <item>Said(line, pose): a mode's own line (the web's ctx.say), pose id or null to keep the pose.</item>
    /// <item>NudgeDue: the 15 s hint nudge (show.js armNudge).</item>
    /// </list>
    /// Mode lines (ladder, reveal) live in RemixLines; this class only passes them on. Nothing here waits or blocks input.
    /// </summary>
    public sealed class DogShowDirector : MonoBehaviour
    {
        [SerializeField] DogRig rig;
        [SerializeField] DogBubble bubble;

        ShowScreen _screen;
        ShowEngine _engine;
        Func<double> _random;
        DogCoach _coach = new DogCoach();

        public DogCoach Coach => _coach;

        public void Init(DogRig dogRig, DogBubble dogBubble) { rig = dogRig; bubble = dogBubble; }

        /// <summary>Follow a show screen (call before its show starts, e.g. from ShowScreen.Started's subscriber or right after Open).</summary>
        public void Follow(ShowScreen screen)
        {
            Unfollow();
            _screen = screen;
            if (screen == null) return;
            screen.EngineReady += OnEngineReady;
            screen.Said += OnSaid;
            screen.NudgeDue += Nudge;
            if (screen.CoachArea != null)
            {
                var rt = (RectTransform)transform;
                rt.SetParent(screen.CoachArea, false);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }
        }

        public void Unfollow()
        {
            if (_screen != null)
            {
                _screen.EngineReady -= OnEngineReady;
                _screen.Said -= OnSaid;
                _screen.NudgeDue -= Nudge;
            }
            _screen = null;
            Detach();
        }

        void OnEngineReady(ShowEngine engine, Func<double> random)
        {
            Detach();
            _engine = engine;
            _random = random ?? UnityRandom;
            _coach = new DogCoach(rig != null ? rig.Pose : DogPose.Idle, bubble != null ? bubble.Text : null);
            if (_engine != null) _engine.Raised += OnRaised;
        }

        static double UnityRandom() => UnityEngine.Random.value * 0.9999999;

        void Detach()
        {
            if (_engine != null) _engine.Raised -= OnRaised;
            _engine = null;
        }

        void OnDestroy() => Unfollow();

        void OnRaised(ShowEvent e)
        {
            var word = _engine != null && _engine.Current != null ? (_engine.Current.Item as WordItem)?.W : null;
            Apply(DogCues.For(e, new DogCueInput { Random = _random, Word = word, RevealLine = RemixLines.Reveal }));
        }

        void OnSaid(string line, string pose)
        {
            DogPose? p = null;
            if (pose != null && DogPoses.TryParse(pose, out var parsed)) p = parsed;
            Say(line, p);
        }

        public void Apply(DogCue cue)
        {
            var motion = _coach.Apply(cue, out bool changed);
            Show(cue.Line, changed, motion);
        }

        /// <summary>A mode's own line and optional pose (web ctx.say).</summary>
        public void Say(string line, DogPose? pose = null)
        {
            var motion = _coach.Say(line, pose, out bool changed);
            Show(line, changed, motion);
        }

        public void Nudge() => Apply(DogCues.Nudge());

        void Show(string line, bool poseChanged, DogMotion motion)
        {
            if (rig != null)
            {
                if (poseChanged) rig.SetPose(_coach.Pose);
                if (motion != DogMotion.None) rig.Play(motion);
            }
            if (line != null && bubble != null) bubble.Say(line);
        }
    }
}
