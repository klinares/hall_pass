using System.Collections.Generic;
using WorldTour.Core.Maths.Gen;

namespace WorldTour.Core.Progress
{
    /// <summary>One piece standing in a room: its cell (the corner with the smallest x and z, after turning), its turn in 90 degree steps (0 to 3) and the room.</summary>
    public sealed class HomeItem
    {
        public string Id;
        public int X, Z, Rot, Room;
    }

    /// <summary>
    /// Phase 10B.2: the home part of the save. Written as the "home" key only when it holds something (an old save has none: an empty first room).
    /// <see cref="Owned"/> are all the pieces she owns, repeats allowed; <see cref="Placed"/> are the ones standing in a room (the rest are in the put-away box).
    /// </summary>
    public sealed class HomeData
    {
        /// <summary>Keys this version doesn't know, kept in stored order (null = none).</summary>
        public JsonValue Extra;
        public List<string> Owned = new List<string>();
        public List<HomeItem> Placed = new List<HomeItem>();
        /// <summary>Palette index of the wall and the floor, or -1 for the room's own.</summary>
        public int Wall = -1, Floor = -1;
        /// <summary>The extra rooms she has bought (1 den, 2 studio, 3 garden).</summary>
        public List<int> Rooms = new List<int>();
        /// <summary>The free starters were given (and put in the room).</summary>
        public bool Started;

        public bool IsEmpty { get { return Owned.Count == 0 && Placed.Count == 0 && Wall < 0 && Floor < 0 && Rooms.Count == 0 && !Started && Extra == null; } }
    }
}
