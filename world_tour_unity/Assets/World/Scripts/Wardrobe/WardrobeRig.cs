using UnityEngine;
using WorldTour.Core.Meta;

namespace WorldTour.World.Wardrobe
{
    /// <summary>
    /// Phase 8.3: puts a <see cref="WardrobeLook"/> on whatever the open place has: the Star (the PlayerController's figure), riff (the DogFollower) and the stage lights.
    /// <see cref="Bind"/> after every place load; <see cref="Apply"/> with the save's look, or with an item being tried on (the shop), and again with the save's look to revert.
    /// </summary>
    public sealed class WardrobeRig
    {
        public StarWardrobe Star { get; private set; }
        public DogWardrobe Dog { get; private set; }
        public StageTint Lights { get; private set; }
        public WardrobeLook Current { get; private set; }
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
            if (HasLook) Apply(Current);
        }

        public void Apply(WardrobeLook look)
        {
            Current = look; HasLook = true;
            if (Star != null) Star.Apply(look);
            if (Dog != null) Dog.Apply(look.Dog);
            if (Lights != null) Lights.Apply(look.Lights);
        }

        /// <summary>The guitar on her (during a show on the stage, and while a guitar skin is tried on).</summary>
        public void ShowGuitar(bool on)
        {
            if (Star != null) Star.ShowGuitar(on);
        }
    }
}
