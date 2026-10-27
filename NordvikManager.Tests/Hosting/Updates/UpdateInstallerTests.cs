using System.IO.Compression;
using NordvikManager.Hosting.Updates;

namespace NordvikManager.Tests.Hosting.Updates
{
    // The program files of a release folder are swapped for the new release's; the GM's settings
    // and data stay. A failed start puts the old program back.
    public class UpdateInstallerTests : IDisposable
    {
        private const string Exe = "NordvikManager.exe";
        private readonly string _root = Path.Combine(Path.GetTempPath(), "nm-install-" + Guid.NewGuid().ToString("N"));
        private string App => Path.Combine(_root, "app");
        private string Staging => Path.Combine(_root, "staging");

        public UpdateInstallerTests()
        {
            Write(App, Exe, "old exe");
            Write(App, "appsettings.json", "my settings");
            Write(App, "README.txt", "old readme");
            Write(App, "wwwroot/index.html", "old page");
            Write(App, "wwwroot/old-only.js", "gone in the new release");
            Write(App, "data/data.db", "my games");
            Write(App, ".localadmin", "admin");
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private static void Write(string folder, string relative, string text)
        {
            var path = Path.Combine(folder, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }

        private static string Read(string folder, string relative) => File.ReadAllText(Path.Combine(folder, relative));

        private string ReleaseZip(params (string Name, string Text)[] entries)
        {
            var path = Path.Combine(_root, $"release-{Guid.NewGuid():N}.zip");
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var (name, text) in entries)
                using (var writer = new StreamWriter(zip.CreateEntry(name).Open()))
                    writer.Write(text);
            return path;
        }

        private string NewRelease() => ReleaseZip(
            (Exe, "new exe"),
            ("appsettings.json", "release defaults"),
            ("README.txt", "new readme"),
            ("wwwroot/index.html", "new page"),
            ("BuiltInAddons/basics/info.json", "{}"));

        [Fact]
        public void Unpack_Throws_WhenTheReleaseHasNoServer()
        {
            var zip = ReleaseZip(("README.txt", "x"));

            Assert.Throws<UpdateException>(() => UpdateInstaller.Unpack(zip, Staging, Exe));
        }

        [Fact]
        public void Unpack_RefusesEntriesOutsideTheFolder()
        {
            var zip = ReleaseZip((Exe, "x"), ("../escaped.txt", "x"));

            Assert.Throws<UpdateException>(() => UpdateInstaller.Unpack(zip, Staging, Exe));
            Assert.False(File.Exists(Path.Combine(_root, "escaped.txt")));
        }

        [Fact]
        public void Apply_SwapsTheProgram_KeepsSettingsAndData()
        {
            UpdateInstaller.Unpack(NewRelease(), Staging, Exe);

            UpdateInstaller.Apply(App, Staging);

            Assert.Equal("new exe", Read(App, Exe));
            Assert.Equal("new readme", Read(App, "README.txt"));
            Assert.Equal("new page", Read(App, "wwwroot/index.html"));
            Assert.False(File.Exists(Path.Combine(App, "wwwroot/old-only.js")));
            Assert.True(File.Exists(Path.Combine(App, "BuiltInAddons/basics/info.json")));
            Assert.Equal("my settings", Read(App, "appsettings.json"));
            Assert.Equal("my games", Read(App, "data/data.db"));
            Assert.Equal("admin", Read(App, ".localadmin"));
        }

        [Fact]
        public void RollBack_PutsTheOldProgramBack()
        {
            UpdateInstaller.Unpack(NewRelease(), Staging, Exe);
            var replaced = UpdateInstaller.Apply(App, Staging);

            UpdateInstaller.RollBack(App, replaced);

            Assert.Equal("old exe", Read(App, Exe));
            Assert.Equal("old readme", Read(App, "README.txt"));
            Assert.Equal("old page", Read(App, "wwwroot/index.html"));
            Assert.True(File.Exists(Path.Combine(App, "wwwroot/old-only.js")));
            // New in the release, so nothing to put back: removed.
            Assert.False(Directory.Exists(Path.Combine(App, "BuiltInAddons")));
            Assert.Equal("my games", Read(App, "data/data.db"));
        }

        [Fact]
        public void RemoveOld_CleansUpTheReplacedProgram()
        {
            UpdateInstaller.Unpack(NewRelease(), Staging, Exe);
            UpdateInstaller.Apply(App, Staging);

            UpdateInstaller.RemoveOld(App);

            Assert.Empty(Directory.EnumerateFileSystemEntries(App, "*.old"));
            Assert.Equal("new exe", Read(App, Exe));
        }

        [Fact]
        public void Apply_CanRunAgain_AfterAnEarlierUpdateLeftOldFiles()
        {
            UpdateInstaller.Unpack(NewRelease(), Staging, Exe);
            UpdateInstaller.Apply(App, Staging);

            UpdateInstaller.Unpack(ReleaseZip((Exe, "newer exe")), Staging, Exe);
            UpdateInstaller.Apply(App, Staging);

            Assert.Equal("newer exe", Read(App, Exe));
        }
    }
}
