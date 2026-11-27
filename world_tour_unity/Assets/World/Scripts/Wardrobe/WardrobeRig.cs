using UnityEngine;
using WorldTour.Core.Meta;

namespace WorldTour.World.Wardrobe
{
    /// <summary>
    /// Phase 8.3: puts a look on whatever the open place has: the Star (the PlayerController's figure), riff (the DogFollower) and the stage lights.
    /// <see cref="Bind"/> after every place load; <see cref="Apply(AvatarLook)"/> (phase 10B.1) with the saved look, or with one being tried on, and again with the saved look to revert.
    /// The older <see cref="Apply(WardrobeLook)"/> stays for the 8.3 tests and previews.
    /// </summary>
    public sealed class WardrobeRig
    {
        public StarWardrobe Star { get; private set; }
        public DogWardrobe Dog { get; private set; }
        public StageTint Lights { get; private set; }
        public WardrobeLook Current { get; private set; }
        public AvatarLook CurrentAvatar { get; private set; }
        public bool HasLook { get; private set; }
        readonly GameObject _holder;

        public WardrobeRig(GameObject holder) { _holder = holder; }

        /// <summary>Finds the Star, riff and the stage lights in the loaded scene and puts the last look back on them (a new place starts in the look she had).</summary>
        public void Bind()
        {
            var pc = Object.FindFirstObjectByType<PlayerController>();
            Star = pc != null ? StarWardrobe.On(pc.gameObject) : null;
            var dog = Object.FindFirstObjectByType<DogFollower>();
            Dog = dog != null ? DogWardrobe.On(dog.gameObject) : null;
            if (Lights == null && _holder != null) Lights = _holder.AddComponent<StageTint>();
            if (Lights != null) Lights.Find();
            if (CurrentAvatar != null) { _pending = null; ApplyNow(CurrentAvatar); }
            else if (HasLook) Apply(Current);
        }

        public void Apply(WardrobeLook look)
        {
            Current = look; HasLook = true; CurrentAvatar = null; _pending = null;
            if (Star != null) Star.Apply(look);
            if (Dog != null) Dog.Apply(look.Dog);
            if (Lights != null) Lights.Apply(look.Lights);
        }

        AvatarLook _pending;
        RigPump _pump;

        /// <summary>
        /// Phase 10B.1: the whole avatar look on the Star, riff and the lights. While the game runs the request is queued and applied once, in the frame's late update, so a burst of try-ons
        /// (clicking through colors, the editor, the mirror, an outfit slot) is one apply per frame and the latest look wins. Outside play mode, or with <see cref="Flush"/>, it is applied at once.
        /// </summary>
        public void Apply(AvatarLook look)
        {
            CurrentAvatar = look.Clone(); HasLook = true;
            if (_pump == null && _holder != null && (Coalesce ?? Application.isPlaying)) { _pump = _holder.AddComponent<RigPump>(); _pump.Rig = this; }
            if (_pump == null) { ApplyNow(CurrentAvatar); return; }
            _pending = CurrentAvatar;
        }

        /// <summary>Applies the queued look now (the latest one asked for), if there is one. The pump calls this once a frame.</summary>
        public void Flush()
        {
            AvatarLook p = _pending;
            _pending = null;
            if (p != null) ApplyNow(p);
        }

        /// <summary>Number of looks applied since the rig was made (for tests: one per frame at most).</summary>
        public int AppliedCount { get; private set; }

        /// <summary>Queue and coalesce applies (default: while the game runs). Tests set it on and call <see cref="Flush"/> as the frame.</summary>
        public bool? Coalesce;

        void ApplyNow(AvatarLook look)
        {
            AppliedCount++;
            if (Star != null) Star.Apply(look);
            if (Dog != null) Dog.Apply(look.Dog);
            if (Lights != null) Lights.Apply(look.Lights.Id);
        }

        /// <summary>Applies the queued look once a frame.</summary>
        sealed class RigPump : MonoBehaviour
        {
            public WardrobeRig Rig;
            void LateUpdate() { if (Rig != null) Rig.Flush(); }
        }
        /// <summary>The guitar on her (during a show on the stage, and while a guitar is tried on).</summary>
        public void ShowGuitar(bool on)
        {
            Flush();
            if (Star != null) Star.ShowGuitar(on);
        }
    }
}
