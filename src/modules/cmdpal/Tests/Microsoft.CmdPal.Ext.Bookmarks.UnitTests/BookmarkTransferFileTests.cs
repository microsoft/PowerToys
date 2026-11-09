// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class BookmarkTransferFileTests
{
    private const int MaximumFileBytes = 10 * 1024 * 1024;
    private const int MaximumBookmarks = 10000;

    [TestMethod]
    [DataRow(".json")]
    [DataRow(".tsv")]
    [DataRow(".txt")]
    [DataRow(".JSON")]
    public void RoundTrip_PreservesNamesAndPaths(string extension)
    {
        BookmarkData[] bookmarks =
        [
            new("Search", "https://example.com/?q={Query}"),
            new("Local file", @"C:\Users\Example\My Documents\file.txt"),
            new(string.Empty, "shell:Downloads"),
            new("  Žluťoučký\t\"bookmark\"\r\nnext line  ", "\"C:\\Program Files\\app.exe\" \"argument\""),
        ];

        var content = BookmarkTransferFile.Serialize(bookmarks, extension);
        var imported = BookmarkTransferFile.Parse(content, extension);

        CollectionAssert.AreEqual(bookmarks.Select(bookmark => bookmark.Name).ToArray(), imported.Select(bookmark => bookmark.Name).ToArray());
        CollectionAssert.AreEqual(bookmarks.Select(bookmark => bookmark.Bookmark).ToArray(), imported.Select(bookmark => bookmark.Path).ToArray());
    }

    [TestMethod]
    public void Json_ContainsOnlyNameAndPath()
    {
        var content = BookmarkTransferFile.Serialize([new BookmarkData("Example", "https://example.com")], ".json");
        using var document = JsonDocument.Parse(content);
        var entry = document.RootElement[0];

        Assert.AreEqual(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.AreEqual(2, entry.EnumerateObject().Count());
        Assert.AreEqual("Example", entry.GetProperty("Name").GetString());
        Assert.AreEqual("https://example.com", entry.GetProperty("Path").GetString());
    }

    [TestMethod]
    public void Json_ExportKeepsNamesAndUrlsReadableWithoutChangingStoreSerialization()
    {
        var bookmark = new BookmarkData("Žluťoučký C++ <tag> 'name'", "https://example.com/?a=1&b=2");
        var content = BookmarkTransferFile.Serialize([bookmark], ".json");

        StringAssert.Contains(content, bookmark.Name);
        StringAssert.Contains(content, bookmark.Bookmark);
        var entry = BookmarkTransferFile.Parse(content, ".json").Single();
        Assert.AreEqual(bookmark.Name, entry.Name);
        Assert.AreEqual(bookmark.Bookmark, entry.Path);

        var stored = new BookmarkJsonParser().SerializeBookmarks(new BookmarksData { Data = [bookmark] });
        StringAssert.Contains(stored, @"\u017D");
        StringAssert.Contains(stored, @"\u0026");
    }

    [TestMethod]
    public void Json_SyntaxErrorReportsTheParserLocationOnlyOnce()
    {
        var error = Assert.ThrowsException<FormatException>(() => BookmarkTransferFile.Parse("[{\"Path\":", ".json"));

        Assert.IsInstanceOfType<JsonException>(error.InnerException);
        Assert.AreEqual(error.InnerException.Message, error.Message);
        StringAssert.Contains(error.Message, "LineNumber:");
    }

    [TestMethod]
    public void Json_AllowsOptionalNameAndCaseInsensitiveProperties()
    {
        var entries = BookmarkTransferFile.Parse("""[{"path":"https://example.com"}]""", ".json");

        Assert.AreEqual(string.Empty, entries.Single().Name);
        Assert.AreEqual("https://example.com", entries.Single().Path);
    }

    [TestMethod]
    public void Tsv_ReadsUnquotedFieldsAndBlankLines()
    {
        var entries = BookmarkTransferFile.Parse("Path\tName\n\nhttps://example.com\tExample\nC:\\Work\t\n", ".tsv");

        Assert.AreEqual(2, entries.Count);
        Assert.AreEqual("Example", entries[0].Name);
        Assert.AreEqual("https://example.com", entries[0].Path);
        Assert.AreEqual(string.Empty, entries[1].Name);
        Assert.AreEqual(@"C:\Work", entries[1].Path);
    }

    [TestMethod]
    public void Tsv_ExportsPathFirstAndOmitsEmptyNames()
    {
        var content = BookmarkTransferFile.Serialize(
            [new BookmarkData("Example", "https://example.com"), new BookmarkData(string.Empty, @"C:\Work")],
            ".tsv");

        Assert.AreEqual("Path\tName\r\n\"https://example.com\"\t\"Example\"\r\n\"C:\\Work\"\r\n", content);
    }

    [TestMethod]
    [DataRow("https://example.com")]
    [DataRow("https://example.com\t")]
    [DataRow("https://example.com\t\"\"")]
    [DataRow("Path\tName\nhttps://example.com")]
    [DataRow("# Comments may precede the header\nPath\tName\nhttps://example.com")]
    [DataRow("path\tname\nhttps://example.com")]
    [DataRow(" Path \t nAmE \nhttps://example.com")]
    [DataRow("Path\tName \nhttps://example.com")]
    [DataRow("Path\nhttps://example.com")]
    [DataRow(" pAtH \nhttps://example.com")]
    [DataRow("Path\t \nhttps://example.com")]
    public void Tsv_NameAndHeaderAreOptional(string content)
    {
        var entry = BookmarkTransferFile.Parse(content, ".txt").Single();

        Assert.AreEqual("https://example.com", entry.Path);
        Assert.AreEqual(string.Empty, entry.Name);
    }

    [TestMethod]
    public void Tsv_CommentsAreWholeLinesAndHashesElsewhereAreLiteral()
    {
        var content = "# A comment with \"unbalanced quotes\n"
            + "  # An indented comment\n"
            + "Path\tName\n"
            + "https://example.com/#section\t# Name\n"
            + "\t# Another comment\n"
            + "C:\\Work # literal\tWork # literal\n"
            + "# Final comment";

        var entries = BookmarkTransferFile.Parse(content, ".tsv");

        Assert.AreEqual(2, entries.Count);
        Assert.AreEqual("https://example.com/#section", entries[0].Path);
        Assert.AreEqual("# Name", entries[0].Name);
        Assert.AreEqual(@"C:\Work # literal", entries[1].Path);
        Assert.AreEqual("Work # literal", entries[1].Name);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" \t\r\n\n")]
    [DataRow("# Only comments\r\n   # Another comment")]
    [DataRow("# Comment\r\nPath\tName\r\n# End")]
    [DataRow("path")]
    [DataRow("PATH\t NAME ")]
    public void Tsv_EmptyFilesAndCommentsContainNoBookmarks(string content)
    {
        Assert.AreEqual(0, BookmarkTransferFile.Parse(content, ".txt").Count);
    }

    [TestMethod]
    [DataRow("Path", "Name")]
    [DataRow("path", "name")]
    [DataRow(" Path ", " Name ")]
    [DataRow("Path", "")]
    public void Tsv_HeaderIsRecognizedOnlyAsTheFirstUnquotedRecord(string path, string name)
    {
        var quoted = BookmarkTransferFile.Parse($"\"{path}\"\t\"{name}\"", ".tsv").Single();
        var later = BookmarkTransferFile.Parse($"Path\tName\n{path}\t{name}", ".tsv").Single();

        Assert.AreEqual(path, quoted.Path);
        Assert.AreEqual(name, quoted.Name);
        Assert.AreEqual(path, later.Path);
        Assert.AreEqual(name, later.Name);
    }

    [TestMethod]
    [DataRow("\"Path\"\tName", "Path", "Name")]
    [DataRow("Path\t\"Name\"", "Path", "Name")]
    [DataRow("\"Path\"", "Path", "")]
    [DataRow("Name\t\"Path\"", "Name", "Path")]
    public void Tsv_QuotingEitherColumnPreservesHeaderLikeData(string content, string path, string name)
    {
        var entry = BookmarkTransferFile.Parse(content, ".tsv").Single();

        Assert.AreEqual(path, entry.Path);
        Assert.AreEqual(name, entry.Name);
    }

    [TestMethod]
    [DataRow("Name\tPath")]
    [DataRow("name\tpath")]
    [DataRow(" nAmE \t PaTh ")]
    public void Tsv_RejectsReversedHeaderVariants(string header)
    {
        var error = Assert.ThrowsException<FormatException>(() => BookmarkTransferFile.Parse($"# comment\n\n{header}\nExample\thttps://example.com", ".tsv"));

        StringAssert.Contains(error.Message, Properties.Resources.bookmarks_file_reversed_header);
        StringAssert.Contains(error.Message, "Line 3:");
    }

    [TestMethod]
    [DataRow("#literal")]
    [DataRow("  #literal")]
    [DataRow("a\tb")]
    [DataRow("a\"b")]
    [DataRow("\"")]
    [DataRow("a\n\n# inside a field\nb")]
    [DataRow("a\r\r  # inside a field\rb")]
    [DataRow("a\r\n \t\r\n# inside a field\r\nb")]
    [DataRow(@"C:\new\task\r\n.txt")]
    [DataRow(@"\\server\share\")]
    [DataRow("  leading and trailing spaces  ")]
    public void Tsv_RoundTripsEscapedFieldsExactly(string value)
    {
        var content = BookmarkTransferFile.Serialize([new BookmarkData(value, value)], ".tsv");
        var entry = BookmarkTransferFile.Parse(content, ".tsv").Single();

        Assert.AreEqual(value, entry.Path);
        Assert.AreEqual(value, entry.Name);
    }

    [TestMethod]
    [Timeout(5000)]
    public void Tsv_RoundTripsCombinationsOfSpecialCharacters()
    {
        const string alphabet = "abcŽ #\"\\\t\r\n";
        var random = new Random(40332);
        var bookmarks = Enumerable.Range(0, 250).Select(_ => new BookmarkData(
            new string(Enumerable.Range(0, random.Next(40)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray()),
            "C:\\" + new string(Enumerable.Range(0, random.Next(40)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray()))).ToArray();

        var entries = BookmarkTransferFile.Parse(BookmarkTransferFile.Serialize(bookmarks, ".tsv"), ".tsv");

        CollectionAssert.AreEqual(bookmarks.Select(bookmark => bookmark.Name).ToArray(), entries.Select(entry => entry.Name).ToArray());
        CollectionAssert.AreEqual(bookmarks.Select(bookmark => bookmark.Bookmark).ToArray(), entries.Select(entry => entry.Path).ToArray());
    }

    [TestMethod]
    [DataRow("\n")]
    [DataRow("\r")]
    [DataRow("\r\n")]
    public void Tsv_PreservesEmbeddedLineEndings(string newline)
    {
        var name = "First" + newline + "Second";
        var content = BookmarkTransferFile.Serialize([new BookmarkData(name, "https://example.com")], ".tsv");

        Assert.AreEqual(name, BookmarkTransferFile.Parse(content, ".tsv").Single().Name);
    }

    [TestMethod]
    [DataRow(".json")]
    [DataRow(".tsv")]
    public void RoundTrip_EmptyBookmarks(string extension)
    {
        Assert.AreEqual(0, BookmarkTransferFile.Parse(BookmarkTransferFile.Serialize([], extension), extension).Count);
    }

    [TestMethod]
    [DataRow("", ".json")]
    [DataRow("null", ".json")]
    [DataRow("{}", ".json")]
    [DataRow("[null]", ".json")]
    [DataRow("[{\"Name\":\"Missing path\"}]", ".json")]
    [DataRow("[{\"Path\":null}]", ".json")]
    [DataRow("[{\"Path\":\" \"}]", ".json")]
    [DataRow("[{\"Name\":null,\"Path\":\"test\"}]", ".json")]
    [DataRow("[{\"Path\":42}]", ".json")]
    [DataRow("Path\tName\n\tExample", ".tsv")]
    [DataRow("Name\tPath\nExample\thttps://example.com", ".tsv")]
    [DataRow("Path\tName\n\"\"", ".tsv")]
    [DataRow("Path\tName\n\" \"\tExample", ".tsv")]
    [DataRow("Path\tName\npath\tExample\textra", ".tsv")]
    [DataRow("Path\tName\n\"Unclosed\tExample", ".tsv")]
    [DataRow("\"path\"junk\tExample", ".tsv")]
    [DataRow("path\t\"name\"junk", ".tsv")]
    [DataRow("path\tunescaped\"quote", ".tsv")]
    [DataRow("\"path\"\t\"name\"\t", ".tsv")]
    [DataRow("\"Unclosed", ".txt")]
    [DataRow("[]", ".csv")]
    public void Parse_RejectsInvalidFiles(string content, string extension)
    {
        Assert.ThrowsException<FormatException>(() => BookmarkTransferFile.Parse(content, extension));
    }

    [TestMethod]
    [DataRow(".json")]
    [DataRow(".tsv")]
    public void Import_MergesSkipsDuplicatesAndPersistsNewIds(string extension)
    {
        var original = new BookmarkData("Existing", "https://example.com");
        var dataSource = new MockBookmarkDataSource(new BookmarkJsonParser().SerializeBookmarks(new BookmarksData { Data = [original] }));
        using var manager = new BookmarksManager(dataSource);
        BookmarkData[] entries =
        [
            original,
            new("New", "https://example.com/new"),
            new("New", "https://example.com/new"),
            new("Other name", original.Bookmark),
            new(original.Name, "https://example.com/other"),
        ];
        var batches = 0;
        manager.BookmarksAdded += _ => batches++;
        var content = BookmarkTransferFile.Serialize(entries, extension);

        Assert.AreEqual((3, 2), ImportFile(manager, content, extension));
        Assert.AreEqual((0, 5), ImportFile(manager, content, extension));
        Assert.AreEqual(1, batches);
        Assert.AreEqual(1, dataSource.SaveCount);
        var bookmarks = manager.Bookmarks.ToArray();
        Assert.AreEqual(4, bookmarks.Length);
        Assert.AreEqual(original, bookmarks[0]);
        Assert.AreEqual(4, bookmarks.Select(bookmark => bookmark.Id).Distinct().Count());
        Assert.AreNotEqual(entries[1].Id, bookmarks[1].Id);
        Assert.AreEqual(4, new BookmarkJsonParser().ParseBookmarks(dataSource.GetBookmarkData()).Data.Count);

        using var reloaded = new BookmarksManager(dataSource);
        CollectionAssert.AreEqual(bookmarks, reloaded.Bookmarks.ToArray());
    }

    [TestMethod]
    [DataRow("[{\"Name\":\"Valid\",\"Path\":\"https://example.com\"},{\"Path\":\"\"}]", ".json")]
    [DataRow("Path\tName\nhttps://example.com\tValid\n\tInvalid", ".tsv")]
    [DataRow("https://example.com\tValid\n\"Unclosed", ".tsv")]
    public void Import_InvalidLaterEntryDoesNotChangeBookmarks(string content, string extension)
    {
        var original = new BookmarkData("Existing", "https://example.com/existing");
        var dataSource = new MockBookmarkDataSource(new BookmarkJsonParser().SerializeBookmarks(new BookmarksData { Data = [original] }));
        using var manager = new BookmarksManager(dataSource);

        Assert.ThrowsException<FormatException>(() => ImportFile(manager, content, extension));

        Assert.AreEqual(original, manager.Bookmarks.Single());
        Assert.AreEqual(0, dataSource.SaveCount);
    }

    [TestMethod]
    [DataRow(".json")]
    [DataRow(".tsv")]
    [DataRow(".txt")]
    public async Task Export_WritesUtf8AndReplacesExistingFile(string extension)
    {
        var directory = Directory.CreateTempSubdirectory();
        var path = Path.Combine(directory.FullName, "bookmarks" + extension);
        try
        {
            await File.WriteAllTextAsync(path, "Old export");
            BookmarkTransferFile.Export([new BookmarkData("Žluťoučký", "https://example.com")], path);

            var imported = BookmarkTransferFile.Parse(await File.ReadAllTextAsync(path), extension);
            Assert.AreEqual("Žluťoučký", imported.Single().Name);
            Assert.AreEqual(1, directory.GetFiles().Length);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task Export_FailedReplacementPreservesExistingFileAndRemovesTemporaryFile()
    {
        var directory = Directory.CreateTempSubdirectory();
        var path = Path.Combine(directory.FullName, "bookmarks.json");
        try
        {
            await File.WriteAllTextAsync(path, "Existing file");
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                try
                {
                    BookmarkTransferFile.Export([new BookmarkData("New", "new")], path);
                    Assert.Fail("Replacing a file locked against deletion should fail.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }

            Assert.AreEqual("Existing file", await File.ReadAllTextAsync(path));
            Assert.AreEqual(1, directory.GetFiles().Length);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow("""[{"Path":"first","Path":"second"}]""")]
    [DataRow("""[{"Path":"first","path":"second"}]""")]
    [DataRow("""[{"Path":"first","Name":"a","name":"b"}]""")]
    [DataRow("""[{"Path":"first","Extra":1,"extra":2}]""")]
    [DataRow("""[{"Path":"\uD800"}]""")]
    [DataRow("""[{"Path":"valid","Name":"\uDC00"}]""")]
    [DataRow("""[{"Path":"first"}""")]
    public void Json_RejectsAmbiguousOrTruncatedFiles(string content)
    {
        Assert.ThrowsException<FormatException>(() => BookmarkTransferFile.Parse(content, ".json"));
    }

    [TestMethod]
    [DataRow("[{\"Path\":\"valid\"}, {\"Name\":\"missing path\"}]", ".json", "Bookmark 2:", "path")]
    [DataRow("# comment\r\nPath\tName\r\nvalid\r\n\tMissing", ".tsv", "Line 4:", "path")]
    [DataRow("valid\r\"unclosed", ".txt", "Line 2:", "quote")]
    [DataRow("\"multi\nline\"\npath\tname\textra", ".tsv", "Line 3:", "columns")]
    public void Parse_IdentifiesTheInvalidRecord(string content, string extension, string location, string reason)
    {
        var error = Assert.ThrowsException<FormatException>(() => BookmarkTransferFile.Parse(content, extension));
        StringAssert.Contains(error.Message, location);
        StringAssert.Contains(error.Message, reason);
    }

    [TestMethod]
    [DataRow(".json", true)]
    [DataRow(".json", false)]
    [DataRow(".tsv", true)]
    [DataRow(".txt", false)]
    public async Task Read_AcceptsUtf8WithOrWithoutBom(string extension, bool bom)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + extension);
        try
        {
            var content = BookmarkTransferFile.Serialize([new BookmarkData("Žluťoučký", @"%USERPROFILE%\{Folder}\missing")], extension);
            await File.WriteAllTextAsync(path, content, new UTF8Encoding(bom));
            var entry = BookmarkTransferFile.Parse(BookmarkTransferFile.Read(path), extension).Single();
            Assert.AreEqual("Žluťoučký", entry.Name);
            Assert.AreEqual(@"%USERPROFILE%\{Folder}\missing", entry.Path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [DataRow(new byte[] { 0xC3, 0x28 })]
    [DataRow(new byte[] { 0xE2, 0x82 })]
    [DataRow(new byte[] { 0xFF, 0xFE, 0x41, 0x00 })]
    public async Task Read_RejectsMalformedUtf8(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            await File.WriteAllBytesAsync(path, bytes);
            Assert.ThrowsException<FormatException>(() => BookmarkTransferFile.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_RejectsOversizedFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            using (var file = File.OpenWrite(path))
            {
                file.SetLength(MaximumFileBytes + 1);
            }

            Assert.ThrowsException<FormatException>(() => BookmarkTransferFile.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [DataRow(".json")]
    [DataRow(".tsv")]
    [Timeout(10000)]
    public void Import_HandlesLargeBatchesWithOneSaveAndEnforcesLimits(string extension)
    {
        var entries = Enumerable.Range(0, MaximumBookmarks).Select(index => new BookmarkData($"Bookmark {index}", $"https://example.com/{index}")).ToArray();
        var dataSource = new MockBookmarkDataSource();
        using var manager = new BookmarksManager(dataSource);
        var content = BookmarkTransferFile.Serialize(entries, extension);
        Assert.AreEqual((MaximumBookmarks, 0), ImportFile(manager, content, extension));
        Assert.AreEqual(1, dataSource.SaveCount);
        Assert.ThrowsException<FormatException>(() => BookmarkTransferFile.Parse(BookmarkTransferFile.Serialize([.. entries, entries[0]], extension), extension));
        Assert.ThrowsException<FormatException>(() => BookmarkTransferFile.Parse(new string('x', MaximumFileBytes + 1), extension));
    }

    [TestMethod]
    [Timeout(5000)]
    public void Tsv_RandomInputsEitherParseLosslesslyOrReportFormatErrors()
    {
        const string alphabet = "abcŽ #\"\\\t\r\n";
        var random = new Random(40332);
        for (var iteration = 0; iteration < 2000; iteration++)
        {
            var content = new string(Enumerable.Range(0, random.Next(150)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
            System.Collections.Generic.List<BookmarkTransferFileEntry> entries;
            try
            {
                entries = BookmarkTransferFile.Parse(content, ".tsv");
            }
            catch (FormatException)
            {
                continue;
            }

            var roundTrip = BookmarkTransferFile.Parse(BookmarkTransferFile.Serialize(entries.Select(entry => new BookmarkData(entry.Name, entry.Path)), ".tsv"), ".tsv");
            CollectionAssert.AreEqual(entries.Select(entry => (entry.Name, entry.Path)).ToArray(), roundTrip.Select(entry => (entry.Name, entry.Path)).ToArray());
        }
    }

    [TestMethod]
    public void Import_EmptyFileDoesNotSaveOrNotify()
    {
        var dataSource = new MockBookmarkDataSource();
        using var manager = new BookmarksManager(dataSource);
        manager.BookmarksAdded += _ => Assert.Fail("An empty import must not notify.");

        Assert.AreEqual((0, 0), ImportFile(manager, "# No bookmarks", ".txt"));
        Assert.AreEqual(0, dataSource.SaveCount);
    }

    [TestMethod]
    public void Import_SaveFailureLeavesMemoryAndDiskUnchanged()
    {
        var directory = Directory.CreateTempSubdirectory();
        var path = Path.Combine(directory.FullName, "bookmarks.json");
        var original = new BookmarkData("Existing", "https://example.com");
        var originalJson = new BookmarkJsonParser().SerializeBookmarks(new BookmarksData { Data = [original] });
        try
        {
            File.WriteAllText(path, originalJson);
            using var manager = new BookmarksManager(new FileBookmarkDataSource(path));
            manager.BookmarksAdded += _ => Assert.Fail("A failed import must not notify.");
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                try
                {
                    ImportFile(manager, """[{"Path":"new"}]""", ".json");
                    Assert.Fail("Replacing a locked store should fail.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }

            Assert.AreEqual(original, manager.Bookmarks.Single());
            Assert.AreEqual(originalJson, File.ReadAllText(path));
            Assert.AreEqual(1, directory.GetFiles().Length);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow("bookmarks.json")]
    [DataRow("BOOKMARKS.JSON")]
    [DataRow("sub/../bookmarks.json")]
    [DataRow("bookmarks.json. ")]
    public void Export_CannotOverwriteTheActiveStore(string fileName)
    {
        var directory = Directory.CreateTempSubdirectory();
        var path = Path.Combine(directory.FullName, "bookmarks.json");
        try
        {
            File.WriteAllText(path, "Existing store");
            Assert.ThrowsException<IOException>(() => BookmarkTransferFile.Export([new BookmarkData("New", "new")], Path.Combine(directory.FullName, fileName), path));
            Assert.AreEqual("Existing store", File.ReadAllText(path));
            Assert.AreEqual(1, directory.GetFiles().Length);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow(".json")]
    [DataRow(".tsv")]
    public void Import_PreservesPortablePathsAndDistinctTargets(string extension)
    {
        string[] paths = [@"%USERPROFILE%\{Folder}\missing", @"\\offline-server\share\missing", "https://example.com/A", "https://example.com/a", @"C:\missing\..\missing\file", "relative/path"];
        var entries = paths.Select(path => new BookmarkData(string.Empty, path)).ToArray();
        using var manager = new BookmarksManager(new MockBookmarkDataSource());

        Assert.AreEqual((paths.Length, 0), ImportFile(manager, BookmarkTransferFile.Serialize(entries, extension), extension));
        CollectionAssert.AreEqual(paths, manager.Bookmarks.Select(bookmark => bookmark.Bookmark).ToArray());
    }

    [TestMethod]
    public void Import_RejectsAnUnsupportedExtensionBeforeOpeningTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        var dataSource = new MockBookmarkDataSource();
        using var manager = new BookmarksManager(dataSource);

        var error = Assert.ThrowsException<FormatException>(() => new BookmarkTransferService(manager).Import(path));

        Assert.AreEqual(Properties.Resources.bookmarks_file_extension, error.Message);
        Assert.AreEqual(0, dataSource.SaveCount);
    }

    [TestMethod]
    public void Export_UnsupportedExtensionLeavesDestinationUnchanged()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "Existing file");
            var error = Assert.ThrowsException<FormatException>(() => BookmarkTransferFile.Export([new BookmarkData("New", "new")], path));
            Assert.AreEqual(Properties.Resources.bookmarks_file_extension, error.Message);
            Assert.AreEqual("Existing file", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Export_CannotReplaceTheStoreThroughAnExtendedPathAlias()
    {
        var directory = Directory.CreateTempSubdirectory();
        var path = Path.Combine(directory.FullName, "bookmarks.json");
        try
        {
            const string original = """{"Data":[]}""";
            File.WriteAllText(path, original);
            using var manager = new BookmarksManager(new FileBookmarkDataSource(path));
            try
            {
                new BookmarkTransferService(manager, path).Export(@"\\?\" + path);
                Assert.Fail("Replacing the store through an alias should fail.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            Assert.AreEqual(original, File.ReadAllText(path));
            Assert.AreEqual(1, directory.GetFiles().Length);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow(".json")]
    [DataRow(".tsv")]
    public void Import_PersistsAcrossManagerInstancesAndExportsWithoutIds(string extension)
    {
        var directory = Directory.CreateTempSubdirectory();
        var path = Path.Combine(directory.FullName, "bookmarks.json");
        var exportPath = Path.Combine(directory.FullName, "export" + extension);
        try
        {
            BookmarkData original;
            using (var manager = new BookmarksManager(new FileBookmarkDataSource(path)))
            {
                ImportFile(manager, """[{"Name":"Portable","Path":"%USERPROFILE%\\{Folder}\\missing"}]""", ".json");
                original = manager.Bookmarks.Single();
                Assert.AreEqual(1, new BookmarkTransferService(manager, path).Export(exportPath));
            }

            using var reloaded = new BookmarksManager(new FileBookmarkDataSource(path));
            Assert.AreEqual(original, reloaded.Bookmarks.Single());
            var exported = BookmarkTransferFile.Parse(File.ReadAllText(exportPath), extension).Single();
            Assert.AreEqual(original.Name, exported.Name);
            Assert.AreEqual(original.Bookmark, exported.Path);
            Assert.AreEqual((0, 1), ImportFile(reloaded, File.ReadAllText(exportPath), extension));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void Import_InvalidUtf8DoesNotChangeBookmarks()
    {
        var original = new BookmarkData("Existing", "https://example.com");
        var dataSource = new MockBookmarkDataSource(new BookmarkJsonParser().SerializeBookmarks(new BookmarksData { Data = [original] }));
        using var manager = new BookmarksManager(dataSource);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllBytes(path, [0xC3, 0x28]);
            Assert.ThrowsException<FormatException>(() => new BookmarkTransferService(manager).Import(path));
            Assert.AreEqual(original, manager.Bookmarks.Single());
            Assert.AreEqual(0, dataSource.SaveCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task Transfer_ConcurrentExportsAndUpdatesPreserveStore()
    {
        var directory = Directory.CreateTempSubdirectory();
        var path = Path.Combine(directory.FullName, "bookmarks.json");
        var exportPath = Path.Combine(directory.FullName, "export.json");
        try
        {
            using var manager = new BookmarksManager(new FileBookmarkDataSource(path));
            manager.AddRange([("Existing", "https://example.com")]);
            var transfer = new BookmarkTransferService(manager, path);
            using var start = new ManualResetEventSlim();
            var exports = Task.Run(() =>
            {
                start.Wait();
                for (var index = 0; index < 30; index++)
                {
                    var count = transfer.Export(exportPath);
                    Assert.AreEqual(count, BookmarkTransferFile.Parse(File.ReadAllText(exportPath), ".json").Count);
                }
            });
            var updates = Task.Run(() =>
            {
                start.Wait();
                for (var index = 0; index < 30; index++)
                {
                    Assert.AreEqual(1, manager.AddRange([($"Bookmark {index}", $"https://example.com/{index}")]));
                }
            });
            start.Set();
            await Task.WhenAll(exports, updates);

            using var reloaded = new BookmarksManager(new FileBookmarkDataSource(path));
            Assert.AreEqual(31, reloaded.Bookmarks.Count);
            CollectionAssert.AreEqual(manager.Bookmarks.ToArray(), reloaded.Bookmarks.ToArray());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static (int Imported, int Skipped) ImportFile(IBookmarksManager manager, string content, string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
        try
        {
            File.WriteAllText(path, content);
            return new BookmarkTransferService(manager).Import(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
