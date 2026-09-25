// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;
using System.Text;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Media.MediaProperties;
using Windows.Storage;

namespace AdvancedPaste.UITests;

[TestClass]
[DoNotParallelize]
[TestCategory("AdvancedPaste")]
public sealed class AdvancedPasteFileTests : AdvancedPasteTestBase
{
    private readonly List<long> explorerWindows = [];

    [TestCleanup]
    public async Task CloseExplorerWindows()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        foreach (var handle in explorerWindows)
        {
            WindowControl.TryCloseByApp("explorer", window => window.Hwnd == handle);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TextIsPastedAsUtf8File(bool directShortcut)
    {
        const string text = "Offline file\r\ncaf\u00e9 \u4e2d\u6587 \U0001f680\r\n\tindented";
        SetClipboardText(text);
        var output = await PasteFile(ProductStrings.PasteAsTxtFile, Key.T, ".txt", directShortcut);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(text), File.ReadAllBytes(output), "The TXT file did not preserve UTF-8 text and whitespace.");
    }

    [TestMethod]
    public async Task HtmlOnlyClipboardIsPastedAsTextFile()
    {
        SetHtmlClipboard("<p>Offline text file</p>");
        var output = await PasteFile(ProductStrings.PasteAsTxtFile, Key.T, ".txt");
        Assert.AreEqual("Offline text file", File.ReadAllText(output).Trim(), "The HTML-only clipboard did not produce a plain-text file.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task HtmlIsPastedAsHtmlFileWithoutClipboardMetadata(bool directShortcut)
    {
        const string html = "<h2>Offline file</h2><p>alpha &amp; beta</p>";
        SetHtmlClipboard(html);
        var output = await PasteFile(ProductStrings.PasteAsHtmlFile, Key.H, ".html", directShortcut);
        Assert.AreEqual(html, File.ReadAllText(output), "The HTML file changed the fragment or included CF_HTML metadata.");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public async Task ImageIsPastedAsPngFile(bool fileInput, bool directShortcut)
    {
        var source = Path.Combine(TestDirectory, "source.png");
        ClipboardFixtures.CreateImage(source);
        if (fileInput)
        {
            SetFileClipboard(source);
        }
        else
        {
            await SetBitmapClipboard(source);
        }

        var output = await PasteFile(ProductStrings.PasteAsPngFile, Key.P, ".png", directShortcut, source);
        using var bitmap = new Bitmap(output);
        Assert.AreEqual(ClipboardFixtures.ImageWidth, bitmap.Width);
        Assert.AreEqual(ClipboardFixtures.ImageHeight, bitmap.Height);
        Assert.AreEqual(Color.CornflowerBlue.ToArgb(), bitmap.GetPixel(10, 10).ToArgb(), "PNG conversion changed the left fixture color.");
        Assert.AreEqual(Color.Gold.ToArgb(), bitmap.GetPixel(bitmap.Width - 10, 10).ToArgb(), "PNG conversion changed the right fixture color.");
    }

    [TestMethod]
    public async Task AudioIsTranscodedAndPastedAsMp3()
    {
        var source = Path.Combine(TestDirectory, "offline-audio.wav");
        ClipboardFixtures.CreateWave(source);
        var original = File.ReadAllBytes(source);
        SetFileClipboard(source);
        var output = await PasteFile(ProductStrings.TranscodeToMp3, Key.F7, ".mp3", source: source);
        await AssertAudio(output);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(source), "Transcoding modified the source audio.");
    }

    [TestMethod]
    public async Task VideoAudioIsExtractedAndPastedAsMp3()
    {
        var source = Path.Combine(TestDirectory, "offline-video.mp4");
        await ClipboardFixtures.CreateVideoAsync(source);
        SetFileClipboard(source);
        var output = await PasteFile(ProductStrings.TranscodeToMp3, Key.F7, ".mp3", directShortcut: true, source: source);
        await AssertAudio(output);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task VideoIsPastedAsH264Mp4WithoutOverwritingSource(bool directShortcut)
    {
        var source = Path.Combine(TestDirectory, "offline-video.mp4");
        await ClipboardFixtures.CreateVideoAsync(source);
        var original = File.ReadAllBytes(source);
        SetFileClipboard(source);
        var output = await PasteFile(ProductStrings.TranscodeToMp4Action, Key.F8, ".mp4", directShortcut, source);
        Assert.AreEqual("offline-video_1.mp4", Path.GetFileName(output), "Same-extension conversion did not use a distinct output name.");
        var profile = await MediaEncodingProfile.CreateFromFileAsync(await StorageFile.GetFileFromPathAsync(output));
        Assert.IsNotNull(profile.Video, "The MP4 output has no video stream.");
        Assert.IsNotNull(profile.Audio, "The MP4 output lost its audio stream.");
        Assert.AreEqual(MediaEncodingSubtypes.H264, profile.Video.Subtype);
        Assert.IsTrue(string.Equals(MediaEncodingSubtypes.Aac, profile.Audio.Subtype, StringComparison.OrdinalIgnoreCase), $"Unexpected audio subtype: {profile.Audio.Subtype}");
        Assert.AreEqual(320U, profile.Video.Width);
        Assert.AreEqual(180U, profile.Video.Height);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(source), "Transcoding overwrote the source video.");
    }

    private async Task<string> PasteFile(string action, Key directKey, string extension, bool directShortcut = false, string? source = null)
    {
        var destination = Directory.CreateDirectory(Path.Combine(TestDirectory, $"PastedFiles_{Guid.NewGuid():N}"));
        Step("Opening an empty Explorer destination for real file paste");
        var existingWindows = WindowControl.EnumerateAllWindows()
            .Where(window => window.ClassName is "CabinetWClass" or "ExploreWClass")
            .Select(window => window.Hwnd.ToInt64()).ToHashSet();
        using var process = Process.Start(new ProcessStartInfo("explorer.exe", $"/n,\"{destination.FullName}\"") { UseShellExecute = true });
        var explorer = WindowsFinder.WaitForWindowByApp(
            "explorer",
            window => (window.ClassName is "CabinetWClass" or "ExploreWClass") && !existingWindows.Contains(window.Hwnd),
            timeoutMS: 30_000);
        Assert.IsNotNull(explorer, "Explorer did not open the file-paste destination.");
        explorerWindows.Add(explorer.WindowHandle);
        WaitUntil(
            () => WindowControl.EnumerateProcessWindows([explorer.ProcessId]).Any(window =>
                window.Hwnd.ToInt64() == explorer.WindowHandle && window.Title.Contains(destination.Name, StringComparison.OrdinalIgnoreCase)),
            "The new Explorer window did not navigate to the unique paste destination.",
            timeoutMS: 30_000);
        FocusExplorerItemsView(explorer);

        Step($"Executing '{action}' and waiting for Explorer to receive the generated file");
        if (directShortcut)
        {
            SendShortcut(Key.Ctrl, Key.Alt, Key.LWin, directKey);
        }
        else
        {
            SelectAction(OpenAdvancedPasteForWindow(new IntPtr(explorer.WindowHandle)), action);
        }

        // Reading CF_HDROP opens the clipboard and can race Explorer's actual Ctrl+V.
        // Observe the destination first, then inspect the transferred clipboard data.
        WaitUntil(
            () =>
            {
                Assert.IsNotEmpty(GetModuleProcessIds(), $"Advanced Paste exited while '{action}' was running.");
                return destination.EnumerateFiles().Any();
            },
            $"Explorer did not receive any file from '{action}'.",
            timeoutMS: 60_000,
            shouldRetryException: exception => exception is IOException);

        var result = WaitHelper.WaitForStable(
            () =>
            {
                Assert.IsNotEmpty(GetModuleProcessIds(), $"Advanced Paste exited while '{action}' was running. Check its logs and Windows Application Error events.");
                return ReadClipboardFilePaths();
            },
            files => files is { Length: 1 } && !string.Equals(files[0], source, StringComparison.OrdinalIgnoreCase) &&
                Path.GetExtension(files[0]).Equals(extension, StringComparison.OrdinalIgnoreCase) && File.Exists(files[0]),
            timeoutMS: 60_000,
            requiredConsecutiveMatches: 2);
        Assert.IsTrue(result.Succeeded, $"'{action}' did not produce one {extension} file. Clipboard files: {string.Join(", ", result.LastObservation ?? [])}");
        var generated = result.LastObservation![0];
        TrackGeneratedFile(generated);
        var pasted = Path.Combine(destination.FullName, Path.GetFileName(generated));
        var expectedBytes = await File.ReadAllBytesAsync(generated);
        Assert.IsNotEmpty(expectedBytes, "The generated file is empty.");
        WaitUntil(
            () => File.Exists(pasted) && new FileInfo(pasted).Length == expectedBytes.LongLength,
            $"Explorer did not receive the generated file '{pasted}'.",
            timeoutMS: 30_000,
            shouldRetryException: exception => exception is IOException);
        Assert.HasCount(1, destination.GetFiles(), "The action pasted more than one file.");
        CollectionAssert.AreEqual(expectedBytes, await File.ReadAllBytesAsync(pasted), "Explorer received different file content.");
        WaitUntil(() => !IsAdvancedPasteVisible(), "Advanced Paste remained visible after file paste.");
        return pasted;
    }

    private void FocusExplorerItemsView(Session explorer)
    {
        var handle = new IntPtr(explorer.WindowHandle);
        (bool Visible, bool Foreground, bool ShellReady, bool EmptySelection, bool KeyboardFocus)? lastState = null;
        var ready = WaitHelper.WaitForStable(
            () => explorer.FindAll<Element>(By.Name("Items View"), 0)
                .SingleOrDefault(element => element.ControlType == "List" && element.ClassName == "UIItemsView"),
            view =>
            {
                if (view is null)
                {
                    lastState = null;
                    return false;
                }

                var selection = ExplorerShell.TryGetSelection(handle);
                var state = (
                    Visible: view.Width > 0 && view.Height > 0 && !view.IsOffscreen,
                    Foreground: WindowControl.GetForegroundWindowHandle() == handle,
                    ShellReady: selection is not null,
                    EmptySelection: selection?.SelectedPaths.Count == 0,
                    KeyboardFocus: WindowControl.IsKeyboardFocusWithinClass(handle, "SHELLDLL_DefView"));
                lastState = state;
                return state.Visible && state.Foreground && state.ShellReady && state.EmptySelection && state.KeyboardFocus;
            },
            timeoutMS: 30_000,
            requiredConsecutiveMatches: 3,
            recover: view =>
            {
                if (WindowControl.GetForegroundWindowHandle() != handle)
                {
                    WindowControl.TryBringToForeground(handle);
                }
                else if (view is { Width: > 0, Height: > 0 } &&
                    WindowControl.IsPointOwnedByWindow(handle, view.X + (view.Width / 2), view.Y + (view.Height / 2)))
                {
                    // Empty Items View has no focusable UIA item. Focus Shell's native view
                    // through its blank content area, not the address bar or navigation tree.
                    view.Click(msPostAction: 0);
                }
            },
            shouldRetryException: AdvancedPasteUi.IsStaleElement);
        if (!ready.Succeeded && ready.LastObservation is { } lastView)
        {
            var focused = WinappCli.Invoke("ui", "get-focused", explorer.TargetFlag, explorer.TargetValue, "--json");
            var properties = WinappCli.Invoke("ui", "get-property", lastView.Selector, explorer.TargetFlag, explorer.TargetValue, "--json");
            Step($"Explorer focus diagnostic: {focused.StdOut}; items view properties: {properties.StdOut}");
        }

        Assert.IsTrue(
            ready.Succeeded,
            $"Explorer's empty file view did not acquire stable keyboard focus. Last state: {lastState} (visible, foreground, Shell ready, empty selection, keyboard focus); foreground: {WindowControl.GetForegroundWindowInfo()}; last exception: {ready.LastException}.");
        Step("Explorer's empty file view has stable keyboard focus");
    }

    private static async Task AssertAudio(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        var profile = await MediaEncodingProfile.CreateFromFileAsync(file);
        Assert.IsNotNull(profile.Audio, "The MP3 output has no audio stream.");
        Assert.IsTrue(string.Equals(MediaEncodingSubtypes.Mp3, profile.Audio.Subtype, StringComparison.OrdinalIgnoreCase), $"Unexpected audio subtype: {profile.Audio.Subtype}");
        var properties = await file.Properties.GetMusicPropertiesAsync();
        Assert.IsTrue(properties.Duration.TotalSeconds is >= 0.8 and <= 1.3, $"The MP3 duration was {properties.Duration}, not the one-second fixture.");
    }
}
