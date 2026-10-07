using DndOnePlaceManager.Application.Services;
using System.IO.Compression;

namespace DndOnePlaceManager.Application.UnitTests.Services
{
    public class BuiltInAddonsTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "builtin-addons-" + Guid.NewGuid());

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private void WriteFile(string relative, string content)
        {
            var path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        private static List<string> EntryNames(byte[] zip)
        {
            using var archive = new ZipArchive(new MemoryStream(zip));
            return archive.Entries.Select(e => e.FullName).ToList();
        }

        [Fact]
        public void GetAll_ZipsEachFolderWithItsFilesAtTheArchiveRoot()
        {
            WriteFile("basics/info.json", "{\"key\":\"basics\"}");
            WriteFile("basics/Resources/token_generic.json", "{}");
            WriteFile("basics/Templates/note_template.json", "{}");

            var addons = new BuiltInAddons(_root).GetAll();

            var basics = Assert.Single(addons);
            Assert.Equal("basics.zip", basics.FileName);
            // The installer matches folders case-insensitively, but separators must be "/"
            // (as in a zip made by pnpm run pack), also when packed on Windows.
            Assert.Equal(
                new[] { "info.json", "Resources/token_generic.json", "Templates/note_template.json" }.Order(StringComparer.Ordinal),
                EntryNames(basics.Data).Order(StringComparer.Ordinal));
        }

        [Fact]
        public void GetAll_SkipsFoldersWithoutInfoJson()
        {
            WriteFile("half-done/Resources/a.json", "{}");

            Assert.Empty(new BuiltInAddons(_root).GetAll());
        }

        [Fact]
        public void GetAll_MissingRootFolder_ReturnsNothing()
        {
            Assert.Empty(new BuiltInAddons(Path.Combine(_root, "nope")).GetAll());
        }
    }
}
