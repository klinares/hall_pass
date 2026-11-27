using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WorldTour.World;

/// <summary>
/// 10B stall fix, one-off: the club's queue of fans (Block circles of radius 0.42 along z = 2.3) left a slit between it and the tables behind it that she could be wedged in.
/// Sets those circles to 0.5 in Place_Club.unity, exactly what <c>PlaceClubBuilder</c> now builds. Safe to run again.
/// Run: Unity -batchmode -executeMethod ClubChairFix.Apply
/// </summary>
public static class ClubChairFix
{
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene("Assets/World/Scenes/Place_Club.unity", OpenSceneMode.Single);
        int n = 0;
        foreach (var o in Object.FindObjectsByType<Obstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Vector3 p = o.transform.position;
            if (Mathf.Abs(p.z - 2.3f) < 0.02f && Mathf.Abs(o.radius - 0.42f) < 0.001f) { o.radius = 0.5f; EditorUtility.SetDirty(o); n++; }
        }
        Debug.Log("ClubChairFix: " + n + " queue circles set to 0.5");
        if (n > 0) EditorSceneManager.SaveScene(scene);
        EditorApplication.Exit(n > 0 ? 0 : 0);
    }
}
