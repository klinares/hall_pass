using TMPro;
using UnityEngine;
using WorldTour.Core;

namespace WorldTour.Game
{
    /// <summary>
    /// Boot scene entry point. Sets the title label from Core (proving Core runs in the player),
    /// then logs WT_BOOT_READY exactly once. The browser test times "label visible" from that line.
    /// </summary>
    public sealed class Boot : MonoBehaviour
    {
        public const string ReadyLog = "WT_BOOT_READY";

        [SerializeField] TMP_Text label;

        static bool s_Announced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Announced = false;

        void Start()
        {
            if (label != null) label.text = GameInfo.Title;
            else Debug.LogError("boot: the title label is not assigned.");

            if (s_Announced) return;
            s_Announced = true;
            Debug.Log(ReadyLog);
        }
    }
}
