using System;
using WorldTour.Core.Meta;
using WorldTour.Core.Progress;

namespace WorldTour.Game.Mascot
{
    /// <summary>riff's five poses (art/dog.js: idle, cheer, think, sleepy, oops).</summary>
    public enum DogPose { Idle, Cheer, Think, Oops, Sleepy }

    /// <summary>A short reaction on top of the pose. Each one is under 400 ms and never blocks input.</summary>
    public enum DogMotion
    {
        None,
        /// <summary>js/motion.js hop: up 16px with a squash, 380 ms. The web plays it when the pose changes to cheer.</summary>
        Hop,
        /// <summary>js/motion.js wobble: side to side with a small turn, 340 ms. The web wobbles the play area on a miss; here riff wobbles too.</summary>
        Wobble
    }

    public static class DogPoses
    {
        /// <summary>The manifest and dog.js id of a pose ("idle", "cheer", ...).</summary>
        public static string Id(DogPose p)
        {
            switch (p)
            {
                case DogPose.Cheer: return "cheer";
                case DogPose.Think: return "think";
                case DogPose.Oops: return "oops";
                case DogPose.Sleepy: return "sleepy";
                default: return "idle";
            }
        }

        public static bool TryParse(string id, out DogPose pose)
        {
            switch (id)
            {
                case "idle": pose = DogPose.Idle; return true;
                case "cheer": pose = DogPose.Cheer; return true;
                case "think": pose = DogPose.Think; return true;
                case "oops": pose = DogPose.Oops; return true;
                case "sleepy": pose = DogPose.Sleepy; return true;
                default: pose = DogPose.Idle; return false;
            }
        }

        /// <summary>The outfits dog.js draws (data/shop.js "dog:" items without "dog:none").</summary>
        public static readonly string[] Outfits = { "bandana", "beanie", "shades", "phones", "collar" };

        /// <summary>
        /// The manifest's stack key for an outfit: "none" for no outfit or an unknown one (dog.js draws nothing for
        /// those), and "none" for every outfit when sleepy (dog.js lying() draws no outfit).
        /// </summary>
        public static string StackOutfit(DogPose pose, string outfit)
        {
            if (pose == DogPose.Sleepy || string.IsNullOrEmpty(outfit) || Array.IndexOf(Outfits, outfit) < 0) return "none";
            return outfit;
        }

        /// <summary>What riff wears, from the save (meta.js look(): anything unknown or not owned is no outfit).</summary>
        public static string OutfitOf(SaveBlob save) => save == null ? "" : MetaRules.GetLook(save).Outfit ?? "";

        /// <summary>screens/map.js: riff naps on the map when she has been away 3 days or more.</summary>
        public static DogPose HomePose(int? daysAway) => daysAway != null && daysAway >= 3 ? DogPose.Sleepy : DogPose.Idle;
    }
}
