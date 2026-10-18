// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Numerics;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// Hosts a reusable context menu with serialized opens, placement-aware animation, and local close handling.
/// </summary>
public sealed partial class ContextMenuFlyout : Flyout
{
    private readonly ContextMenuFlyoutSession _session;
    private UISettings? _uiSettings;
    private Point? _targetPosition;

    /// <summary>
    /// Raised after back navigation closes the root menu so the owner can restore focus to its back target.
    /// </summary>
    public event EventHandler? BackRequested;

    /// <summary>
    /// Gets the menu state and command-invocation events for the owning surface.
    /// </summary>
    internal ContextMenuViewModel ViewModel => MenuControl.ViewModel;

    /// <summary>
    /// Creates the menu content and its flyout session on the UI thread.
    /// </summary>
    public ContextMenuFlyout()
    {
        InitializeComponent();
        _session = new(() => IsOpen, Hide, ViewModel.Close, callback => DispatcherQueue.TryEnqueue(() => callback()));
    }

    /// <summary>
    /// Opens a valid menu context, replacing any pending open and waiting for the previous menu to close.
    /// </summary>
    /// <param name="placementTarget">The loaded element that supplies the XamlRoot and placement coordinates.</param>
    /// <param name="context">The cached command context to display.</param>
    /// <param name="filterLocation">The filter box's position within the menu.</param>
    /// <param name="options">The requested placement, optional target-relative point, and show mode.</param>
    /// <param name="initialSubmenu">An optional command still present in the root context whose submenu opens first.</param>
    /// <param name="showFilterBox">Whether to show the filter box.</param>
    internal void ShowAt(
        FrameworkElement placementTarget,
        IContextMenuContext context,
        ContextMenuFilterLocation filterLocation,
        FlyoutShowOptions options,
        CommandContextItemViewModel? initialSubmenu = null,
        bool showFilterBox = true)
    {
        if (!CanOpen())
        {
            return;
        }

        _session.Show(() =>
        {
            if (!CanOpen())
            {
                return;
            }

            UIHelper.PreparePopupForShow(this, placementTarget);

            MenuControl.ShowFilterBox = showFilterBox;
            MenuControl.PrepareForOpen(context, filterLocation, initialSubmenu);

            // WinUI can clear the per-show placement override during a staged reopen.
            Placement = options.Placement;
            _targetPosition = options.Position;
            PrepareAnimations();
            ShowAt(placementTarget, options);
        });

        return;

        bool CanOpen()
        {
            return placementTarget.IsLoaded && context.CanOpenContextMenu &&
                   (initialSubmenu is null ||
                    (initialSubmenu.HasSubmenu && context.AllCommands.Contains(initialSubmenu)));
        }
    }

    /// <summary>
    /// Cancels pending opens and hides the menu; the session releases its context after closing.
    /// </summary>
    internal void Close()
    {
        _session.Hide();
    }

    private void PrepareAnimations()
    {
        var rootVisual = ElementCompositionPreview.GetElementVisual(MenuAnimationRoot);
        var surfaceVisual = ElementCompositionPreview.GetElementVisual(MenuSurface);
        var contentVisual = ElementCompositionPreview.GetElementVisual(MenuContent);
        var compositor = rootVisual.Compositor;

        ElementCompositionPreview.SetIsTranslationEnabled(MenuContent, true);
        ElementCompositionPreview.GetElementVisual(MenuContentClip).Clip ??= compositor.CreateInsetClip();

        var animationsEnabled = (_uiSettings ??= new UISettings()).AnimationsEnabled;
        rootVisual.StopAnimation(nameof(Visual.Opacity));

        // Wait for final placement before revealing the menu, including when WinUI flips it at a screen edge.
        rootVisual.Opacity = animationsEnabled ? 0 : 1;
        surfaceVisual.StopAnimation(nameof(Visual.Scale));
        surfaceVisual.Scale = Vector3.One;
        surfaceVisual.StopAnimation(nameof(Visual.CenterPoint));
        surfaceVisual.CenterPoint = Vector3.Zero;
        contentVisual.StopAnimation("Translation");
        contentVisual.Properties.InsertVector3("Translation", Vector3.Zero);

        if (!animationsEnabled)
        {
            ElementCompositionPreview.SetImplicitHideAnimation(MenuAnimationRoot, null);
            return;
        }

        var hide = compositor.CreateScalarKeyFrameAnimation();
        hide.Target = nameof(Visual.Opacity);
        hide.Duration = TimeSpan.FromMilliseconds(83);
        hide.InsertKeyFrame(1, 0, compositor.CreateLinearEasingFunction());
        ElementCompositionPreview.SetImplicitHideAnimation(MenuAnimationRoot, hide);
    }

    private void AnimateOpen()
    {
        var rootVisual = ElementCompositionPreview.GetElementVisual(MenuAnimationRoot);
        rootVisual.StopAnimation(nameof(Visual.Opacity));
        rootVisual.Opacity = 1;
        AttachBackdropAnimation();

        if (!(_uiSettings ??= new UISettings()).AnimationsEnabled || Target is not { } target)
        {
            return;
        }

        var surfaceVisual = ElementCompositionPreview.GetElementVisual(MenuSurface);
        var contentVisual = ElementCompositionPreview.GetElementVisual(MenuContent);
        var compositor = rootVisual.Compositor;
        var menuCenter = MenuAnimationRoot.TransformToVisual(target).TransformPoint(new Point(0, MenuAnimationRoot.ActualHeight / 2));
        var upward = menuCenter.Y < (_targetPosition?.Y ?? target.ActualHeight / 2);
        var easing = compositor.CreateCubicBezierEasingFunction(Vector2.Zero, new Vector2(0, 1));
        var duration = TimeSpan.FromMilliseconds(250);

        // Match MenuPopupThemeTransition: reveal half the height without scaling text or icons.
        var translation = compositor.CreateVector3KeyFrameAnimation();
        translation.Target = "Translation";
        translation.Duration = duration;
        translation.InsertExpressionKeyFrame(0, upward ? "Vector3(0, this.Target.Size.Y * 0.5, 0)" : "Vector3(0, -this.Target.Size.Y * 0.5, 0)");
        translation.InsertKeyFrame(1, Vector3.Zero, easing);
        contentVisual.StartAnimation("Translation", translation);

        var center = compositor.CreateExpressionAnimation(upward ? "Vector3(0, this.Target.Size.Y, 0)" : "Vector3(0, 0, 0)");
        surfaceVisual.StartAnimation(nameof(Visual.CenterPoint), center);
        var scale = compositor.CreateVector3KeyFrameAnimation();
        scale.Target = nameof(Visual.Scale);
        scale.Duration = duration;
        scale.InsertKeyFrame(0, new Vector3(1, 0.5f, 1));
        scale.InsertKeyFrame(1, Vector3.One, easing);
        surfaceVisual.StartAnimation(nameof(Visual.Scale), scale);
    }

    private void MenuControl_CloseRequested(object? sender, EventArgs e) => Close();

    private void AttachBackdropAnimation()
    {
        if (!MenuBackdrop.IsLoaded)
        {
            return;
        }

        // ponytail: uses SystemBackdropElement's current child visual; revisit if WinUI changes that control.
        var backdrop = ElementCompositionPreview.GetElementChildVisual(MenuBackdrop);
        if (backdrop?.Clip is not RectangleClip clip)
        {
            Trace.WriteLine("Backdrop clip is unavailable; skipping backdrop animation.", nameof(ContextMenuFlyout));
            return;
        }

        // Drive the acrylic's local clip from the same scale as the border.
        var scale = clip.Compositor.CreateExpressionAnimation("Vector2(1, surface.Scale.Y)");
        scale.SetReferenceParameter("surface", ElementCompositionPreview.GetElementVisual(MenuSurface));
        clip.StartAnimation(nameof(CompositionClip.Scale), scale);

        var center = clip.Compositor.CreateExpressionAnimation("Vector2(0, surface.CenterPoint.Y > 0 ? backdrop.Size.Y : 0)");
        center.SetReferenceParameter("surface", ElementCompositionPreview.GetElementVisual(MenuSurface));
        center.SetReferenceParameter("backdrop", backdrop);
        clip.StartAnimation(nameof(CompositionClip.CenterPoint), center);
    }

    private void MenuControl_BackRequested(object? sender, EventArgs e)
    {
        Close();

        // Hide restores the previous focus synchronously, before the owner chooses its back target.
        BackRequested?.Invoke(this, e);
    }

    private void Flyout_Opening(object sender, object e)
    {
        _session.Opening();
    }

    private void Flyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
    {
        _session.Closing();
    }

    private void Flyout_Closed(object sender, object e)
    {
        // The session releases closed menus without clearing a newer open.
        _session.Closed();
    }

    private void Flyout_Opened(object sender, object e)
    {
        AnimateOpen();

        // Focus the filter box so the flyout captures keyboard input,
        // then fire a single consolidated Narrator announcement.
        MenuControl.FocusSearchBox();
        MenuControl.AnnounceOpened(() => IsOpen);
    }
}
