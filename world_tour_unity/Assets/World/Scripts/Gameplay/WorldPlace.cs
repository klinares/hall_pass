using UnityEngine;
using WorldTour.Core.Data;

namespace WorldTour.World.Gameplay
{
    /// <summary>
    /// Optional marker for a place scene: which venue it is (the id from venues.js: coffee, records, rec, rooftop, club,
    /// theater, festival, arena). Without it the gameplay reads the scene name ("Place_Coffee" is coffee, anything else is coffee).
    /// Nothing else is needed in a place scene: the gameplay binds the empties PlayerSpawn, Crew_1 to Crew_3, StageSpot,
    /// CrowdArea and ExitSpot by name.
    /// </summary>
    public sealed class WorldPlace : MonoBehaviour
    {
        public string placeId = Venues.First;
        /// <summary>Phase 10B.1: empty for a street or a venue. A small interior (the boutique) names itself here; placeId is then the venue it opens off (coffee).</summary>
        public string interior = "";

        /// <summary>The interior's name for the open scene ("boutique"), or "" when it is a street or a venue.</summary>
        public static string InteriorOf()
        {
            var marker = Object.FindFirstObjectByType<WorldPlace>();
            return marker != null && !string.IsNullOrEmpty(marker.interior) ? marker.interior : "";
        }

        /// <summary>The place id for the open scene: the WorldPlace in it, else the scene name, else the first venue.</summary>
        public static string Resolve(string sceneName)
        {
            var marker = Object.FindFirstObjectByType<WorldPlace>();
            if (marker != null && Venues.Exists(marker.placeId)) return marker.placeId;
            string n = (sceneName ?? "").ToLowerInvariant();
            if (n.StartsWith("place_")) n = n.Substring("place_".Length);
            return Venues.Exists(n) ? n : Venues.First;
        }

        /// <summary>The scene name a place's scene has in the build ("Place_Coffee").</summary>
        public static string SceneName(string placeId)
        {
            if (string.IsNullOrEmpty(placeId)) return "Place_Coffee";
            return "Place_" + char.ToUpperInvariant(placeId[0]) + placeId.Substring(1);
        }
    }
}
