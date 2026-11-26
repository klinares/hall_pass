using System;
using System.IO;
using NUnit.Framework;
using WorldTour.Core.Data;

namespace WorldTour.Core.Tests
{
    /// <summary>Phase 9.1: the build reads world/StreamingAssets and never writes it.</summary>
    public class WorldDataSyncTests
    {
        string root, deployed, project;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "wds_" + Guid.NewGuid().ToString("N"));
            deployed = Path.Combine(root, "world", "StreamingAssets");
            project = Path.Combine(root, "proj", "Assets", "StreamingAssets");
            Directory.CreateDirectory(deployed);
        }

        [TearDown]
        public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

        void Seed()
        {
            File.WriteAllText(Path.Combine(deployed, "weeks.json"), "{\"weeks\":[{\"id\":\"deployed\"}]}");
            File.WriteAllText(Path.Combine(deployed, "math_config.json"), "{\"a\":1}");
        }

        [Test]
        public void ProjectGetsTheDeployedCopies_DeployedFolderIsUntouched()
        {
            Seed();
            Directory.CreateDirectory(project);
            File.WriteAllText(Path.Combine(project, "weeks.json"), "stale project copy");
            var before = WorldDataSync.Fingerprint(deployed);
            var stamp = File.GetLastWriteTimeUtc(Path.Combine(deployed, "weeks.json"));
            WorldDataSync.SyncToProject(deployed, project);
            Assert.AreEqual(File.ReadAllText(Path.Combine(deployed, "weeks.json")), File.ReadAllText(Path.Combine(project, "weeks.json")));
            Assert.AreEqual(File.ReadAllText(Path.Combine(deployed, "math_config.json")), File.ReadAllText(Path.Combine(project, "math_config.json")));
            CollectionAssert.AreEqual(before, WorldDataSync.Fingerprint(deployed));
            Assert.AreEqual(stamp, File.GetLastWriteTimeUtc(Path.Combine(deployed, "weeks.json")));
        }

        [Test]
        public void MissingFolder_FailsClearly()
        {
            var ex = Assert.Throws<DirectoryNotFoundException>(() => WorldDataSync.SyncToProject(Path.Combine(root, "nope"), project));
            StringAssert.Contains("world/StreamingAssets", ex.Message);
            Assert.IsFalse(Directory.Exists(project), "nothing is written when the master is missing");
        }

        [Test]
        public void MissingFile_Fails()
        {
            File.WriteAllText(Path.Combine(deployed, "weeks.json"), "{}");
            Assert.Throws<FileNotFoundException>(() => WorldDataSync.SyncToProject(deployed, project));
        }
    }
}
