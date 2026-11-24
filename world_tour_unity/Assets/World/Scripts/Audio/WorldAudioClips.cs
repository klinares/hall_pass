using UnityEngine;

namespace WorldTour.World.Audio
{
    /// <summary>
    /// Phase 8.3: the one object in the WorldAudioBank scene. It holds the five CC0 cue clips by reference. The scene is in the 3D (world/) build only, so the clips
    /// are not in Resources and the 2D tour/ build never includes them. <see cref="WorldAudio"/> loads the scene once, takes the clips, and unloads it.
    /// </summary>
    public sealed class WorldAudioClips : MonoBehaviour
    {
        public static WorldAudioClips Instance { get; private set; }

        public AudioClip open, close, whoosh, done, unlock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
