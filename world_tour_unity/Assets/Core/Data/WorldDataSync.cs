using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace WorldTour.Core.Data
{
    /// <summary>
    /// Phase 9.1: world/StreamingAssets/ holds the master copies of weeks.json and math_config.json. A build reads them,
    /// mirrors them into the project's Assets/StreamingAssets/, and never writes to the deployed folder. Plain file code
    /// so it is tested in edit mode; BuildWorld calls it.
    /// </summary>
    public static class WorldDataSync
    {
        public static readonly string[] Files = { "weeks.json", "math_config.json" };

        /// <summary>Copies each deployed file into projectDir when it differs. Throws if the deployed folder or a file is missing. Never writes deployedDir.</summary>
        public static List<string> SyncToProject(string deployedDir, string projectDir)
        {
            if (!Directory.Exists(deployedDir))
                throw new DirectoryNotFoundException("world/StreamingAssets is missing (" + deployedDir + "). It holds the master weeks.json and math_config.json; the build will not fall back to anything else.");
            foreach (var name in Files)
                if (!File.Exists(Path.Combine(deployedDir, name)))
                    throw new FileNotFoundException("master data file missing from world/StreamingAssets: " + name, Path.Combine(deployedDir, name));
            var log = new List<string>();
            Directory.CreateDirectory(projectDir);
            foreach (var name in Files)
            {
                byte[] src = File.ReadAllBytes(Path.Combine(deployedDir, name));
                string dest = Path.Combine(projectDir, name);
                bool same = File.Exists(dest) && Same(src, File.ReadAllBytes(dest));
                if (!same) File.WriteAllBytes(dest, src);
                log.Add("world/StreamingAssets/" + name + " -> Assets/StreamingAssets/" + name + (same ? " (already identical)" : " (copied)"));
            }
            return log;
        }

        /// <summary>SHA-256 of each master file in dir, "name=hex" entries in a fixed order.</summary>
        public static List<string> Fingerprint(string dir)
        {
            var list = new List<string>();
            using (var sha = SHA256.Create())
                foreach (var name in Files)
                {
                    string p = Path.Combine(dir, name);
                    list.Add(name + "=" + (File.Exists(p) ? BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-", "").ToLowerInvariant() : "missing"));
                }
            return list;
        }

        static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
