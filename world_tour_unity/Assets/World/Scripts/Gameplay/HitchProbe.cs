using System;
using System.Globalization;
using UnityEngine;

namespace WorldTour.World.Gameplay
{
    /// <summary>
    /// Phase 10B.1 (carry-over: the 117 to 200 ms frames): a test-only probe (the page URL has wt_test). It says what a long frame was made of, so a fix aims at
    /// the right thing: how long the scripts took in the frame before it, whether the garbage collector ran, how many MB the heap holds, the last camera rescan
    /// and the last fade. It logs "WT_HITCH dt=183 scr=4 gc=1 mem=212 rs=3 mode=Free" for every frame over 70 ms (dt is how long the frame before this one took in all,
    /// scr how much of that the scripts used, so dt minus scr is drawing, the browser, the garbage collector and shader builds).
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class HitchProbe : MonoBehaviour
    {
        public const float LogOverSeconds = 0.07f;
        public static HitchProbe Instance { get; private set; }
        /// <summary>Set by whatever knows what the game is doing ("Free", "Panel"...), for the log line.</summary>
        public static string Mode = "";

        float _start, _lastScript;
        int _gc;
        HitchProbeEnd _end;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Mode = ""; }

        void Awake()
        {
            Instance = this;
            _gc = GC.CollectionCount(0);
            _end = gameObject.AddComponent<HitchProbeEnd>();
            _end.Owner = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update() { _start = Time.realtimeSinceStartup; }

        internal void Finish()
        {
            float scr = Time.realtimeSinceStartup - _start;
            float dt = Time.unscaledDeltaTime;
            int gc = GC.CollectionCount(0);
            if (dt >= LogOverSeconds)
            {
                Debug.Log("WT_HITCH dt=" + Mathf.RoundToInt(dt * 1000f).ToString(CultureInfo.InvariantCulture)
                    + " scr=" + Mathf.RoundToInt(_lastScript * 1000f).ToString(CultureInfo.InvariantCulture)
                    + " gc=" + (gc - _gc).ToString(CultureInfo.InvariantCulture)
                    + " mem=" + (GC.GetTotalMemory(false) >> 20).ToString(CultureInfo.InvariantCulture)
                    + " rs=" + Mathf.RoundToInt(FollowCamera.LastRescanMs).ToString(CultureInfo.InvariantCulture)
                    + " m=" + Mode);
            }
            _gc = gc;
            _lastScript = scr;
        }
    }
}
