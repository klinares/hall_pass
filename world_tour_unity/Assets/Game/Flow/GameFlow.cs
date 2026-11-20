using System.Linq;
using UnityEngine;
using WorldTour.Core.Data;
using WorldTour.Game.Data;
using WorldTour.Game.Map;
using WorldTour.Game.Modes;
using WorldTour.Game.Show;
using WorldTour.Game.Ticket;
using WorldTour.Game.Ui;
using WorldTour.Game.Util;

namespace WorldTour.Game.Flow
{
    /// <summary>
    /// Where the map, the show and the ticket meet (screens/show.js and the ticket's onEncore and onAgain). Subscribes
    /// once per play session, after the scene loads:
    /// venue card "start" (VenueCard.StartShowRequested) pushes a show over the card, so quit goes back to it;
    /// the show's end replaces it with the ticket; the ticket's encore and play again replace the ticket with a new
    /// show; back to the map pops to the root. The stack is the one in the Main scene.
    /// </summary>
    public static class GameFlow
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Wire()
        {
            VenueCard.StartShowRequested -= OnStartShow;
            VenueCard.StartShowRequested += OnStartShow;
        }

        static ScreenStack Stack()
        {
            return Object.FindFirstObjectByType<ScreenStack>();
        }

        /// <summary>venue card: (venueId, weekId, modeId).</summary>
        static void OnStartShow(string venueId, string weekId, string modeId)
        {
            ScreenStack stack = Stack();
            if (stack == null) { Debug.LogWarning("[flow] no screen stack in the scene"); return; }
            Week week = FindWeek(weekId);
            // DogHosts.OpenWithDog: riff (DogShowDirector.Follow) is attached as the stack places the show, before it starts.
            Follow(DogHosts.OpenWithDog(stack, () => ShowScreen.Open(stack, new ShowRequest { ModeId = modeId, VenueId = venueId, Week = week })));
        }

        static Week FindWeek(string weekId)
        {
            var weeks = DataLoader.Weeks != null && DataLoader.Weeks.File != null && DataLoader.Weeks.File.weeks != null
                ? DataLoader.Weeks.File.weeks : new Week[0];
            return weeks.FirstOrDefault(w => w != null && w.id == weekId) ?? WeekRules.DefaultWeek(weeks, LocalClock.TodayIso());
        }

        static void Follow(ShowScreen show)
        {
            if (show != null) show.Finished += OnShowFinished;
        }

        static void OnShowFinished(ShowResult r)
        {
            ScreenStack stack = Stack();
            if (stack == null) return;
            var mode = ModeList.ById(r.Request.ModeId, null);
            TicketScreen t = TicketScreen.Open(stack, new TicketData
            {
                Summary = r.Summary,
                Venue = Venues.ById(r.Request.VenueId),
                WeekLabel = r.Week != null ? r.Week.label : null,
                ModeName = mode != null ? mode.Name : null,
                NewStickers = r.Stickers,
                Unlocks = r.Unlocks
            });
            t.StartEncore += () => Follow(DogHosts.OpenWithDog(stack, () => ShowScreen.OpenEncore(stack, r, replaceTop: true)));
            t.PlayAgain += () => Follow(DogHosts.OpenWithDog(stack, () => ShowScreen.OpenAgain(stack, r, replaceTop: true)));
            t.BackToMap += () => stack.PopToRoot();
        }
    }
}
