// Phase 8.2 builder A: builds all four of this batch's places (records, rec, rooftop, club) in one Editor run.
//
//   Unity.exe -batchmode -nographics -projectPath world_tour_unity -executeMethod PlaceABatch.CreateAll
//
// Each place also has its own entry (PlaceRecordsBuilder.CreateAll, PlaceRecBuilder.CreateAll, PlaceRooftopBuilder.CreateAll, PlaceClubBuilder.CreateAll).
public static class PlaceABatch
{
    public static void CreateAll()
    {
        PlaceKit.Entry("PlaceABatch", () =>
        {
            PlaceRecordsBuilder.Make();
            PlaceRecBuilder.Make();
            PlaceRooftopBuilder.Make();
            PlaceClubBuilder.Make();
        });
    }
}
