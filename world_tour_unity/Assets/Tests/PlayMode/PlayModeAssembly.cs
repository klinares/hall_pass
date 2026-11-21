using NUnit.Framework;
using UnityEngine;
using WorldTour.Game.Data;

namespace WorldTour.Game.Tests
{
    /// <summary>
    /// Runs once before every play-mode test: the game reads math_config.json from a fixture (every skill on, no level
    /// caps), not from StreamingAssets, so the tests do not change when the shipped config does.
    /// </summary>
    [SetUpFixture]
    public class PlayModeAssembly
    {
        [OneTimeSetUp]
        public void UseTheFixtureConfig()
        {
            DataLoader.MathConfigFolder = Application.dataPath + "/Tests/PlayMode/Fixtures";
        }
    }
}
