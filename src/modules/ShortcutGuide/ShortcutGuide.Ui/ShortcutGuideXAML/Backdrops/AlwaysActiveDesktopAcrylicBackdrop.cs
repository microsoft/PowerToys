// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ShortcutGuide.Backdrops;

/// <summary>
/// A <see cref="SystemBackdrop"/> that renders desktop acrylic and stays in
/// the active visual state even when the hosting window is not activated.
/// </summary>
/// <remarks>
/// The built-in <see cref="DesktopAcrylicBackdrop"/> tracks the host window's
/// <c>IsInputActive</c> state and falls back to a solid color whenever the
/// window is not the foreground window. That makes it unusable for the
/// per-card backdrops inside the Shortcut Guide overlay: the overlay window
/// is technically the foreground, but the <c>SystemBackdropElement</c>s that
/// host the per-pane acrylic are themselves not activated, and the standard
/// backdrop greys out anyway when the overlay loses focus during the brief
/// hand-off to elevated windows / context menus.
///
/// This backdrop drives a <see cref="DesktopAcrylicController"/> with a
/// <see cref="SystemBackdropConfiguration"/> whose <c>IsInputActive</c> is
/// permanently <see langword="true"/>, so the native acrylic effect is always
/// rendered.
///
/// Mirrors the implementation in PR #48176 (CmdPal toast notification). Once
/// that PR lands and the type is promoted into <c>Common.UI.Controls</c>,
/// this local copy should be retired in favor of the shared one.
/// </remarks>
public sealed partial class AlwaysActiveDesktopAcrylicBackdrop : SystemBackdrop
{
    private readonly Dictionary<ICompositionSupportsSystemBackdrop, BackdropTarget> _targets = new();

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        var configuration = new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = ResolveTheme(xamlRoot),
        };

        var controller = new DesktopAcrylicController();
        controller.SetSystemBackdropConfiguration(configuration);
        controller.AddSystemBackdropTarget(connectedTarget);

        var target = new BackdropTarget(controller, configuration, xamlRoot);
        _targets[connectedTarget] = target;

        if (xamlRoot.Content is FrameworkElement rootElement)
        {
            rootElement.ActualThemeChanged += target.OnActualThemeChanged;
        }
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);

        if (_targets.Remove(disconnectedTarget, out var target))
        {
            if (target.XamlRoot.Content is FrameworkElement rootElement)
            {
                rootElement.ActualThemeChanged -= target.OnActualThemeChanged;
            }

            target.Controller.RemoveSystemBackdropTarget(disconnectedTarget);
            target.Controller.Dispose();
        }
    }

    private static SystemBackdropTheme ResolveTheme(XamlRoot xamlRoot) =>
        xamlRoot.Content is FrameworkElement rootElement
            ? rootElement.ActualTheme switch
            {
                ElementTheme.Dark => SystemBackdropTheme.Dark,
                ElementTheme.Light => SystemBackdropTheme.Light,
                _ => SystemBackdropTheme.Default,
            }
            : SystemBackdropTheme.Default;

    private sealed class BackdropTarget
    {
        public BackdropTarget(DesktopAcrylicController controller, SystemBackdropConfiguration configuration, XamlRoot xamlRoot)
        {
            Controller = controller;
            Configuration = configuration;
            XamlRoot = xamlRoot;
        }

        public DesktopAcrylicController Controller { get; }

        public SystemBackdropConfiguration Configuration { get; }

        public XamlRoot XamlRoot { get; }

        public void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            Configuration.Theme = ResolveTheme(XamlRoot);
        }
    }
}
