// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

using MouseJump.WinUI3.Helpers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace MouseJump.WinUI3.UI;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
/// <remarks>
/// Lives under the "/MouseJumpXAML" folder to avoid collisions with the compiled *.xbf files
/// from xaml files in other modules when they all get copied to the shared build output folder.
/// </remarks>
internal sealed partial class PreviewWindow : Window
{
    internal PreviewWindow(SettingsHelper settingsHelper)
    {
        this.SettingsHelper = settingsHelper ?? throw new ArgumentNullException(nameof(settingsHelper));
        this.InitializeComponent();
        this.InitializeWindow();
    }

    private SettingsHelper SettingsHelper
    {
        get;
    }

    /// <summary>
    /// Governs every screenshot capture started by the current activation's
    /// <see cref="ScreenshotCapturePipeline"/> - linked to the <see cref="CancellationToken"/>
    /// <see cref="ShowPreviewAsync"/> was called with (see its remarks for what that guards
    /// against), and *also* cancelled whenever the preview is cleared for any other reason (see
    /// <see cref="ClearPreview"/> - e.g. the user dismissed the window while backfill captures
    /// were still running), so outstanding captures for a no-longer-relevant activation don't
    /// keep doing GDI work in the background either way.
    /// </summary>
    private CancellationTokenSource? activationCancellation;

    /// <summary>
    /// Maximum time to wait for every screen's capture to finish before showing the window
    /// anyway - long enough that a typical (fast, local) activation shows fully populated with
    /// no visible placeholder-then-backfill repainting, short enough that one slow screen (e.g.
    /// a future remote capture provider) can't make the window feel unresponsive. Matches the
    /// grace period the legacy WinForms version used.
    /// </summary>
    private static readonly TimeSpan ScreenshotGracePeriod = TimeSpan.FromMilliseconds(250);
}
