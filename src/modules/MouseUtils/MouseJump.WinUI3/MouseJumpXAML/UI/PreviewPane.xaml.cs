// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using MouseJump.Common.Blurring;
using MouseJump.Models.Display;
using MouseJump.Models.Layout;

namespace MouseJump.WinUI3.UI;

/// <remarks>
/// Lives under the "/MouseJumpXAML" folder to avoid collisions with the compiled *.xbf files
/// from xaml files in other modules when they all get copied to the shared build output folder.
/// </remarks>
internal sealed partial class PreviewPane : UserControl
{
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(
        nameof(PreviewPane.Layout),
        typeof(PreviewLayout),
        typeof(PreviewPane),
        new PropertyMetadata(null, PreviewPane.OnLayoutChanged));

    public static readonly DependencyProperty ActiveScreenProperty = DependencyProperty.Register(
        nameof(PreviewPane.ActiveScreen),
        typeof(ScreenInfo),
        typeof(PreviewPane),
        new PropertyMetadata(null));

    /// <summary>
    /// A pipeline for generating the blurred screenshots that are shown
    /// while the latest screenshot is being captured if a screen capture
    /// takes more than the budget allowed to show the form.
    /// </summary>
    private readonly ScreenshotBlurPipeline blurPipeline = new();

    private List<ScreenSlot> screenSlots = [];

    /// <summary>
    /// The rasterization scale everything was last positioned for - see <see cref="ApplyScale"/>.
    /// </summary>
    private double appliedScale;

    /// <summary>
    /// The <see cref="XamlRoot"/> whose <see cref="XamlRoot.Changed"/> event this pane is
    /// listening to for scale changes, if any - see <see cref="PreviewPane_Loaded"/>.
    /// </summary>
    private XamlRoot? subscribedXamlRoot;

    /// <summary>
    /// How long <see cref="CrossfadeContent"/> takes to fade a screen's content in. Short enough
    /// not to read as a deliberate animation, but spreading the change across enough frames
    /// (roughly 6, at 60Hz) that no single frame carries more than a fraction of the total
    /// change - the goal is specifically to avoid a hard single-frame swap's sudden luminance
    /// change catching peripheral vision, not to make the fade itself noticeable.
    /// </summary>
    private static readonly TimeSpan ContentTransitionDuration = TimeSpan.FromMilliseconds(100);

    public PreviewPane()
    {
        this.InitializeComponent();
        this.PointerPressed += this.PreviewPane_PointerPressed;
        this.PreviewKeyDown += this.PreviewPane_PreviewKeyDown;
        this.Loaded += this.PreviewPane_Loaded;
        this.Unloaded += this.PreviewPane_Unloaded;
    }

    /// <summary>
    /// Gets or sets the mathematical model describing this pane's own size and the position/style of
    /// every device/screen bezel within it. Set by the hosting window - this control doesn't
    /// calculate its own size from anything else.
    /// </summary>
    public PreviewLayout? Layout
    {
        get => (PreviewLayout?)this.GetValue(PreviewPane.LayoutProperty);
        set => this.SetValue(PreviewPane.LayoutProperty, value);
    }

    /// <summary>
    /// Gets or sets the screen that keyboard navigation (Left/Right in particular) is relative
    /// to - set by the hosting window alongside <see cref="Layout"/>, normally to whichever
    /// screen was activated. This control never changes it itself: every navigation key acts
    /// immediately and the host is expected to close the preview afterwards (see
    /// <see cref="NavigateTo"/>), so there's no in-preview "browse mode" that would need this to
    /// track a moving selection.
    /// </summary>
    public ScreenInfo? ActiveScreen
    {
        get => (ScreenInfo?)this.GetValue(PreviewPane.ActiveScreenProperty);
        set => this.SetValue(PreviewPane.ActiveScreenProperty, value);
    }

    /// <summary>
    /// Raised when the pointer clicks a screen, or a keyboard shortcut that means the same
    /// thing (1-9, arrow keys relative to <see cref="ActiveScreen"/>, P for primary, Home/End)
    /// resolves to one. <see cref="NavigateToEventArgs.Location"/> is already resolved to the
    /// corresponding physical location on that screen's own display area.
    /// </summary>
    public event EventHandler<NavigateToEventArgs>? NavigateTo;

    /// <summary>
    /// Raised on a right-click or Escape - the host is expected to just close the preview
    /// without moving the pointer anywhere.
    /// </summary>
    public event EventHandler? Cancel;
}
