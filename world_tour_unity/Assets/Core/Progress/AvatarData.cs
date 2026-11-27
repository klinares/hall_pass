using System.Collections.Generic;
using WorldTour.Core.Maths.Gen;
using WorldTour.Core.Meta;

namespace WorldTour.Core.Progress
{
    /// <summary>
    /// Phase 10B.1 (save version 3): the avatar part of the save. <see cref="Owned"/> are the avatar items she bought or was given (the free starters are
    /// never listed); <see cref="Look"/> is what she wears, or null for a save that has never worn anything new (then <see cref="AvatarRules.Look"/> works it
    /// out from the old backstage items); <see cref="Outfits"/> are her five named looks (null = empty). Written as the "avatar" key only when it holds something.
    /// </summary>
    public sealed class AvatarData
    {
        /// <summary>Keys this version doesn't know, kept in stored order (null = none).</summary>
        public JsonValue Extra;
        public List<string> Owned = new List<string>();
        public AvatarLook Look;
        public AvatarOutfit[] Outfits = new AvatarOutfit[AvatarCatalogue.OutfitSlots];

        public bool IsEmpty
        {
            get
            {
                if (Owned.Count > 0 || Look != null || Extra != null) return false;
                foreach (AvatarOutfit o in Outfits) if (o != null) return false;
                return true;
            }
        }
    }
}
