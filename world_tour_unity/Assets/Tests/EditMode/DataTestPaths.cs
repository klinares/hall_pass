using System;
using System.IO;

namespace WorldTour.Core.Tests
{
    /// <summary>Where the Data tests find their files: the Assets folder in Unity, or WT_ASSETS outside it.</summary>
    internal static class DataTestPaths
    {
#if UNITY_5_3_OR_NEWER
        public static string Assets => UnityEngine.Application.dataPath;
#else
        public static string Assets => Environment.GetEnvironmentVariable("WT_ASSETS");
#endif
        public static string EditMode => Path.Combine(Assets, "Tests", "EditMode");
        public static string Samples => Path.Combine(EditMode, "DataSamples");
        public static string StreamingAssets => Path.Combine(Assets, "StreamingAssets");

        public static string Read(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("missing test file", path);
            return File.ReadAllText(path);
        }

        public static string Sample(string name) => Read(Path.Combine(Samples, name));
    }
}
