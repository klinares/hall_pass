using UnityEngine;

namespace WorldTour.World
{
    /// <summary>Spike telemetry for the browser test: a ready line, then one position and frame-rate line a second.</summary>
    public class SpikeStatus : MonoBehaviour
    {
        public Transform player, dog;
        int frames, total;
        float acc;
        bool ready;

        void Update()
        {
            frames++; total++; acc += Time.unscaledDeltaTime;
            if (!ready && total >= 3)
            {
                ready = true;
                Debug.Log("WT_WORLD_READY");
            }
            if (acc >= 1f)
            {
                Vector3 p = player.position, d = dog.position;
                Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "WT_WORLD_POS x={0:0.00} z={1:0.00} dogx={2:0.00} dogz={3:0.00} fps={4:0.0}", p.x, p.z, d.x, d.z, frames / acc));
                acc = 0; frames = 0;
            }
        }
    }
}
