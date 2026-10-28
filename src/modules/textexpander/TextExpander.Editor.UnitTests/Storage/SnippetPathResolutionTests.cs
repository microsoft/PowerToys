// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.PowerToys.TextExpander.Editor.ViewModels;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.PowerToys.TextExpander.Editor.UnitTests;

/// <summary>
/// The configured snippet-path rule the engine and editor share, <see cref="SnippetPathResolution"/>,
/// and the editor's fallback around it.
/// </summary>
[TestClass]
public sealed class SnippetPathResolutionTests
{
    private const string RootVariable = "TEXT_EXPANDER_PATH_TEST_ROOT";

    // Longer than Windows allows, so Path.GetFullPath refuses it before anything touches the disk.
    private static readonly string TooLongPath = @"C:\" + new string('x', 40_000);

    private string _root;

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "PowerToys-TextExpander-Paths-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable(RootVariable, _root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable(RootVariable, null);
        Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public void BlankValuesSelectNothing()
    {
        foreach (string value in new[] { null, string.Empty, "   ", "\t", "\"\"", " \"  \" " })
        {
            Assert.IsNull(Resolve(value), $"[{value}]");
        }
    }

    [TestMethod]
    public void QuotesSpacesAndEnvironmentVariablesAreResolvedFirst()
    {
        string folder = Path.Combine(_root, "my snippets");
        var expected = new SnippetPathResolution(Path.Combine(folder, TextExpanderSnippetFileNames.Current), ResolveFolderNames: true);

        Assert.AreEqual(expected, Resolve($"  \"{folder}\"  "));
        Assert.AreEqual(expected, Resolve($"%{RootVariable}%\\my snippets"));
        Assert.AreEqual(expected, Resolve($"\"%{RootVariable}%\\my snippets\""), "Quotes are trimmed before expansion.");
        Assert.AreEqual(
            expected,
            SnippetPathResolution.FromConfiguredPath("%SNIPPETS%", value => value.Replace("%SNIPPETS%", folder, StringComparison.Ordinal), File.Exists),
            "The expansion is an adapter, as the editor's storage environment supplies it.");
    }

    [TestMethod]
    public void RelativeValuesSelectNothingBecauseTheProcessesShareNoWorkingDirectory()
    {
        foreach (string value in new[] { "snips", @".\snips", @"..\snips", "C:snips", @"\snips", "/snips", "%NOT_A_TEXT_EXPANDER_TEST_VARIABLE%" })
        {
            Assert.IsNull(Resolve(value), value);
        }

        Assert.IsNull(SnippetPathResolution.FromConfiguredPath(@"C:\%SNIPPETS%", _ => "snips", File.Exists), "Checked after expansion.");
    }

    [TestMethod]
    public void AFolderUsesTheCurrentNameThenTheLegacyName()
    {
        string folder = Path.Combine(_root, "library");
        string current = Path.Combine(folder, TextExpanderSnippetFileNames.Current);
        string legacy = Path.Combine(folder, TextExpanderSnippetFileNames.Legacy);

        Assert.AreEqual(new SnippetPathResolution(current, ResolveFolderNames: true), Resolve(folder), "A missing folder gets the current name.");
        Assert.IsFalse(Directory.Exists(folder), "Selecting has no side effects; the engine creates the folder itself.");

        Directory.CreateDirectory(folder);
        File.WriteAllText(legacy, "!old=Legacy\n");
        Assert.AreEqual(new SnippetPathResolution(legacy, ResolveFolderNames: true), Resolve(folder), "Only the legacy name exists.");

        Assert.IsTrue(TextExpanderStarter.TryCreateFile(current));
        Assert.AreEqual(new SnippetPathResolution(legacy, ResolveFolderNames: true), Resolve(folder), "An untouched starter yields to the legacy library.");

        File.WriteAllText(current, "!new=Current\n");
        Assert.AreEqual(new SnippetPathResolution(current, ResolveFolderNames: true), Resolve(folder + Path.DirectorySeparatorChar), "A changed current file wins.");
    }

    [TestMethod]
    public void AnExistingFileIsUsedAsItIsWhateverItsName()
    {
        string file = Path.Combine(_root, "work snippets.txt");
        File.WriteAllText(file, "!a=b\n");
        var expected = new SnippetPathResolution(file, ResolveFolderNames: false);

        Assert.AreEqual(expected, Resolve(file));
        Assert.AreEqual(expected, Resolve($" \"{file}\" "));
        Assert.AreEqual(expected, Resolve($"%{RootVariable}%\\work snippets.txt"));
    }

    [TestMethod]
    public void UnusableValuesThrowSoEachCallerFallsBackByItsOwnPolicy()
    {
        Assert.ThrowsException<PathTooLongException>(() => Resolve(TooLongPath));
        Assert.ThrowsException<ArgumentException>(
            () => SnippetPathResolution.FromConfiguredPath("C:\\bad\0name", value => value, File.Exists),
            "Environment.ExpandEnvironmentVariables stops at a null character; an injected expansion keeps it.");
        Assert.ThrowsException<UnauthorizedAccessException>(
            () => SnippetPathResolution.FromConfiguredPath(_root, Environment.ExpandEnvironmentVariables, _ => throw new UnauthorizedAccessException()));
    }

    [TestMethod]
    public void CharactersWindowsRejectsAreLeftToTheCallerToCreate()
    {
        string folder = Path.Combine(_root, "a<b");

        Assert.AreEqual(new SnippetPathResolution(Path.Combine(folder, TextExpanderSnippetFileNames.Current), ResolveFolderNames: true), Resolve(folder));

        // The engine can't create the folder, so it falls back to its default library
        // (SnippetStoreTests); the editor reports the folder's error rather than switching libraries.
        using TextExpanderViewModel editor = CreateEditor(folder, Path.Combine(_root, "default"));
        Assert.IsTrue(editor.HasSnippetError);
        Assert.AreEqual(Path.Combine(folder, TextExpanderSnippetFileNames.Current), editor.SnippetsFilePath);
        Assert.IsFalse(Directory.Exists(Path.Combine(_root, "default")));
    }

    [TestMethod]
    public void UncPathsFollowTheSameRuleThroughTheAdapters()
    {
        const string Folder = @"\\server\share\snippets";
        string current = Folder + @"\" + TextExpanderSnippetFileNames.Current;
        string legacy = Folder + @"\" + TextExpanderSnippetFileNames.Legacy;
        var probed = new List<string>();
        bool LegacyOnly(string path)
        {
            probed.Add(path);
            return path == legacy;
        }

        Assert.AreEqual(
            new SnippetPathResolution(legacy, ResolveFolderNames: true),
            SnippetPathResolution.FromConfiguredPath($"\"{Folder}\"", Environment.ExpandEnvironmentVariables, LegacyOnly));
        CollectionAssert.AreEqual(new[] { Folder, current, legacy }, probed, "Only the adapter is asked, so no share is contacted.");

        Assert.AreEqual(current, SnippetPathResolution.FromConfiguredPath("//server/share/snippets", Environment.ExpandEnvironmentVariables, _ => false)?.FilePath);

        const string PinnedFile = @"\\server\share\mine.txt";
        Assert.AreEqual(
            new SnippetPathResolution(PinnedFile, ResolveFolderNames: false),
            SnippetPathResolution.FromConfiguredPath(PinnedFile, Environment.ExpandEnvironmentVariables, path => path == PinnedFile));
    }

    [TestMethod]
    public void TheEditorFallsBackToItsDefaultFolder()
    {
        string defaultFolder = Path.Combine(_root, "default");
        string expected = Path.Combine(defaultFolder, TextExpanderSnippetFileNames.Current);
        foreach (string value in new[] { null, "  ", "relative", "C:snips", TooLongPath })
        {
            using TextExpanderViewModel editor = CreateEditor(value, defaultFolder);

            string label = value?.Length > 20 ? value[..20] + "..." : value;
            Assert.AreEqual(expected, editor.SnippetsFilePath, label);
            Assert.IsFalse(editor.HasSnippetError, label);
        }

        using TextExpanderViewModel injected = CreateEditor("C:\\bad\0name", defaultFolder, expandEnvironmentVariables: value => value);
        Assert.AreEqual(expected, injected.SnippetsFilePath, "An invalid character falls back too.");
    }

    [TestMethod]
    public void TheEngineAndEditorSelectTheSameFileForTheSameValue()
    {
        string defaultFolder = Path.Combine(_root, "default");
        string library = Path.Combine(_root, "library");
        Directory.CreateDirectory(library);
        File.WriteAllText(Path.Combine(library, TextExpanderSnippetFileNames.Legacy), "!old=Legacy\n");
        string pinned = Path.Combine(_root, "pinned.txt");
        File.WriteAllText(pinned, "!pinned=File\n");

        foreach (string value in new[] { null, "relative", library, $" \"{library}\\\" ", $"%{RootVariable}%\\library", pinned, $"%{RootVariable}%\\pinned.txt", Path.Combine(_root, "new") })
        {
            // SnippetStore.ResolveConfiguredPathInfo's adapters, apart from it creating the folder.
            SnippetPathResolution engine = SnippetPathResolution.FromConfiguredPath(value, Environment.ExpandEnvironmentVariables, File.Exists)
                ?? SnippetPathResolution.InFolder(defaultFolder, File.Exists);

            using TextExpanderViewModel editor = CreateEditor(value, defaultFolder);

            Assert.AreEqual(engine.FilePath, editor.SnippetsFilePath, $"[{value}]");
        }
    }

    private static SnippetPathResolution? Resolve(string value)
        => SnippetPathResolution.FromConfiguredPath(value, Environment.ExpandEnvironmentVariables, File.Exists);

    /// <summary>Creates an editor with its own path adapters, unless one is given, and a scratch default folder.</summary>
    private static TextExpanderViewModel CreateEditor(string snippetsPath, string defaultFolder, Func<string, string> expandEnvironmentVariables = null)
    {
        var settings = new TextExpanderSettings();
        settings.Properties.SnippetsPath.Value = snippetsPath;
        var utils = new Mock<TextExpanderSettingsUtils>(new FileSystem(), null);
        utils.Setup(u => u.GetSettingsOrDefault<TextExpanderSettings>(TextExpanderSettings.ModuleName, SettingsUtils.DefaultFileName)).Returns(settings);

        var adapters = new TextExpanderStorageEnvironment();
        var environment = new Mock<ITextExpanderStorageEnvironment>();
        environment.SetupGet(e => e.DefaultSnippetsFolder).Returns(defaultFolder);
        environment.Setup(e => e.ExpandEnvironmentVariables(It.IsAny<string>())).Returns<string>(expandEnvironmentVariables ?? adapters.ExpandEnvironmentVariables);
        environment.Setup(e => e.FileExists(It.IsAny<string>())).Returns<string>(adapters.FileExists);
        return new TextExpanderViewModel(utils.Object, null, isElevated: false, key => key, environment.Object);
    }
}
