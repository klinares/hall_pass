using System;
using UnityEngine;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;

namespace WorldTour.World.Wardrobe
{
    /// <summary>
    /// Phase 10B.1: the try-on hook. The wardrobe editor and the boutique mirror call <see cref="TryOn"/> with a look to show it on the Star, riff and the stage lights in the open
    /// place without saving it; null shows the saved look again. The open place's WorldShop registers its rig and the save here (<see cref="Rig"/>, <see cref="Save"/>); with none
    /// registered the call does nothing.
    /// </summary>
    public static class AvatarPreview
    {
        /// <summary>Set by WorldShop: the rig of the open place.</summary>
        public static WardrobeRig Rig;
        /// <summary>Set by WorldShop: the open save.</summary>
        public static Func<SaveBlob> Save;
        /// <summary>The look being tried on, or null when the saved look shows.</summary>
        public static AvatarLook Showing { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Showing = null; Rig = null; Save = null; }

        public static void TryOn(AvatarLook lookOrNull)
        {
            if (Rig == null) return;
            SaveBlob blob = Save != null ? Save() : null;
            AvatarLook look = lookOrNull;
            if (look == null) look = blob != null ? AvatarRules.Look(blob) : AvatarLook.Default();
            Showing = lookOrNull == null ? null : look;
            Rig.Apply(look);
        }
    }
}
