using UnityEngine;
using WorldTour.Game.Ui;
using WorldTour.World;

namespace WorldTour.World.Gameplay
{
    /// <summary>
    /// The few assets the gameplay layer needs by reference, kept in Resources so a place scene needs no wiring.
    /// Made by the WorldGameplaySetup Editor script (menu World Tour). Every field is optional: with a field missing the
    /// gameplay still runs (plain buttons, no burst, grey crowd).
    /// </summary>
    public sealed class WorldGameplayAssets : ScriptableObject
    {
        public const string ResourcePath = "WorldGameplayAssets";

        [Tooltip("The 2D game's sticker button, used for the talk and offer buttons.")]
        public StickerButton buttonPrefab;
        [Tooltip("Additive unlit particle material for the cheer burst.")]
        public Material burstMaterial;
        [Tooltip("Lit materials the placeholder crowd figures are coloured with.")]
        public Material[] figureMaterials;
        [Tooltip("Built-in capsule and sphere meshes for the placeholder crowd (the physics module is stripped from the web build, so CreatePrimitive cannot be used).")]
        public Mesh capsuleMesh, sphereMesh;

        [Tooltip("Phase 8.1 look pass: baked crowd figures (CrowdFigure prefabs in a few recolours). Empty = the capsule stand-ins.")]
        public CrowdFigure[] crowdFigures;

        static WorldGameplayAssets s_Loaded;
        public static WorldGameplayAssets Load()
        {
            if (s_Loaded == null) s_Loaded = Resources.Load<WorldGameplayAssets>(ResourcePath);
            return s_Loaded;
        }
    }
}
