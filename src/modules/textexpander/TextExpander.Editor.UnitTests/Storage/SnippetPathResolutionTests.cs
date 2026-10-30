// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
        string file = Path.Combine(_root, "my snippets.json");
        var expected = new SnippetPathResolution(file, IsDefault: false);

        Assert.AreEqual(expected, Resolve($"  \"{file}\"  "));
        Assert.AreEqual(expected, Resolve($"%{RootVariable}%\\my snippets.json"));
        Assert.AreEqual(expected, Resolve($"\"%{RootVariable}%\\my snippets.json\""), "Quotes are trimmed before expansion.");
        Assert.AreEqual(
            expected,
            SnippetPathResolution.FromConfiguredPath("%SNIPPETS%", value => value.Replace("%SNIPPETS%", file, StringComparison.Ordinal)),
            "The expansion is an adapter, as the editor's storage environment supplies it.");
    }

    [TestMethod]
    public void RelativeValuesSelectNothingBecauseTheProcessesShareNoWorkingDirectory()
    {
        foreach (string value in new[] { "snips", @".\snips", @"..\snips", "C:snips", @"\snips", "/snips", "%NOT_A_TEXT_EXPANDER_TEST_VARIABLE%" })
        {
            Assert.IsNull(Resolve(value), value);
        }

        Assert.IsNull(SnippetPathResolution.FromConfiguredPath(@"C:\%SNIPPETS%", _ => "snips"), "Checked after expansion.");
    }

    [TestMethod]
    public void AFullPathIsTheSnippetsFileWhateverItsNameAndNothingIsCreated()
    {
        string existing = Path.Combine(_root, "work snippets.json");
        File.WriteAllText(existing, SnippetJson.Of("!a=b"));
        string moved = Path.Combine(_root, "moved", "my snippets.json");
        string folder = Directory.CreateDirectory(Path.Combine(_root, "a folder")).FullName;
        string text = Path.Combine(_root, "old.txt");

        foreach (string file in new[] { existing, moved, folder, text, @"\\server\share\mine.json" })
        {
            Assert.AreEqual(new SnippetPathResolution(file, IsDefault: false), Resolve(file), file);
        }

        Assert.AreEqual(@"\\server\share\snippets.json", Resolve("//server/share/snippets.json")?.FilePath);
        Assert.IsFalse(Directory.Exists(Path.Combine(_root, "moved")), "Selecting has no side effects.");
    }

    [TestMethod]
    public void TheDefaultLocationIsTheSnippetsFileInTheFolder()
    {
        string folder = Path.Combine(_root, "default");
        Assert.AreEqual(new SnippetPathResolution(Path.Combine(folder, "PowerToysTextExpanderSnippets.json"), IsDefault: true), SnippetPathResolution.Default(folder));
        Assert.IsFalse(Directory.Exists(folder), "The caller creates it.");
    }

    [TestMethod]
    public void UnusableValuesThrowSoEachCallerFallsBackByItsOwnPolicy()
    {
        Assert.ThrowsException<PathTooLongException>(() => Resolve(TooLongPath));
        Assert.ThrowsException<ArgumentException>(
            () => SnippetPathResolution.FromConfiguredPath("C:\\bad\0name.json", value => value),
            "Environment.ExpandEnvironmentVariables stops at a null character; an injected expansion keeps it.");
    }

    [TestMethod]
    public void CharactersWindowsRejectsAreReportedAndNothingIsCreated()
    {
        string file = Path.Combine(_root, "a<b", "snippets.json");

        Assert.AreEqual(new SnippetPathResolution(file, IsDefault: false), Resolve(file));

        // The editor reports the file's error rather than switching to the default location.
        using TextExpanderViewModel editor = CreateEditor(file, Path.Combine(_root, "default"));
        Assert.IsTrue(editor.HasSnippetError);
        Assert.AreEqual(file, editor.SnippetsFilePath);
        Assert.IsFalse(Directory.Exists(Path.Combine(_root, "default")));
    }

    [TestMethod]
    public void TheEditorFallsBackToItsDefaultFolder()
    {
        string defaultFolder = Path.Combine(_root, "default");
        string expected = Path.Combine(defaultFolder, TextExpanderSnippetFileNames.Current);
        foreach (string value in new[] { null, "  ", "relative.json", "C:snips.json", TooLongPath })
        {
            using TextExpanderViewModel editor = CreateEditor(value, defaultFolder);

            string label = value?.Length > 20 ? value[..20] + "..." : value;
            Assert.AreEqual(expected, editor.SnippetsFilePath, label);
            Assert.IsFalse(editor.HasSnippetError, label);
        }

        using TextExpanderViewModel injected = CreateEditor("C:\\bad\0name.json", defaultFolder, expandEnvironmentVariables: value => value);
        Assert.AreEqual(expected, injected.SnippetsFilePath, "An invalid character falls back too.");
    }

    [TestMethod]
    public void TheEngineAndEditorSelectTheSameFileForTheSameValue()
    {
        string defaultFolder = Path.Combine(_root, "default");
        string pinned = Path.Combine(_root, "pinned.json");
        File.WriteAllText(pinned, SnippetJson.Of("!pinned=File"));

        foreach (string value in new[] { null, "relative", pinned, $" \"{pinned}\" ", $"%{RootVariable}%\\pinned.json", Path.Combine(_root, "new.json"), _root })
        {
            // SnippetStore.ResolveConfiguredPathInfo's adapters, apart from it creating the default folder.
            SnippetPathResolution engine = SnippetPathResolution.FromConfiguredPath(value, Environment.ExpandEnvironmentVariables)
                ?? SnippetPathResolution.Default(defaultFolder);

            using TextExpanderViewModel editor = CreateEditor(value, defaultFolder);

            Assert.AreEqual(engine.FilePath, editor.SnippetsFilePath, $"[{value}]");
        }
    }

    private static SnippetPathResolution? Resolve(string value)
        => SnippetPathResolution.FromConfiguredPath(value, Environment.ExpandEnvironmentVariables);

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
        environment.Setup(e => e.CreateDirectory(It.IsAny<string>())).Callback<string>(adapters.CreateDirectory);
        return new TextExpanderViewModel(utils.Object, isElevated: false, key => key, environment.Object);
    }
}
