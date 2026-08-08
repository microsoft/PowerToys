// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WindowsCompositionColorBrush = Windows.UI.Composition.CompositionColorBrush;
using WindowsCompositionCompositor = Windows.UI.Composition.Compositor;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// A composition-backed <see cref="SystemBackdrop"/> whose material and tint can be
/// updated without replacing the backdrop attached to a SystemBackdropElement.
/// </summary>
/// <remarks>
/// The projected <see cref="ICompositionSupportsSystemBackdrop"/> target owns a
/// thread-affine native ContentExternalBackdropLink. Keeping the target in
/// <see cref="_targets"/> for its entire connected lifetime prevents C#/WinRT from
/// releasing that link on the finalizer thread while CmdPal switches materials.
/// </remarks>
internal sealed partial class TintedControllerBackdrop : SystemBackdrop, IDisposable
{
    private readonly Dictionary<ICompositionSupportsSystemBackdrop, BackdropTarget?> _targets = [];

    private BackdropSettings? _settings;
    private bool _isBackdropAttached;
    private bool _isInputActive = true;
    private bool _isDisposed;

    public event Action<bool>? BackdropAttachmentChanged;

    public bool IsBackdropAttached => _isBackdropAttached;

    /// <summary>
    /// Gets or sets a value indicating whether the host window is currently activated.
    /// </summary>
    public bool IsInputActive
    {
        get => _isInputActive;
        set
        {
            _isInputActive = value;

            foreach (var target in _targets.Values)
            {
                target?.SetIsInputActive(value);
            }
        }
    }

    /// <summary>
    /// Updates the material rendered by every connected target. Controller cleanup happens
    /// synchronously on the XAML thread; direct brush attachment is deferred through that
    /// thread's dispatcher when the native backdrop link is being handed off.
    /// </summary>
    public void Update(BackdropParameters backdrop, BackdropControllerKind kind, bool isImageMode, bool hasColorization)
    {
        if (_isDisposed)
        {
            return;
        }

        var settings = new BackdropSettings(
            kind,
            Color.FromArgb(
                (byte)(backdrop.EffectiveOpacity * 255),
                backdrop.TintColor.R,
                backdrop.TintColor.G,
                backdrop.TintColor.B),
            backdrop.TintColor,
            isImageMode ? 0.0f : backdrop.EffectiveOpacity,
            backdrop.FallbackColor,
            backdrop.EffectiveLuminosityOpacity,
            hasColorization || isImageMode);
        _settings = settings;

        foreach (var (target, state) in _targets)
        {
            state?.Apply(target, settings);
        }

        UpdateBackdropAttachmentState();
    }

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        // Root the projected target before creating any other WinRT objects. The target
        // projection must not be finalized while its native backdrop link is connected.
        _targets[connectedTarget] = null;

        try
        {
            var target = new BackdropTarget(xamlRoot, _isInputActive, UpdateBackdropAttachmentState);
            _targets[connectedTarget] = target;

            if (!_isDisposed && _settings is { } settings)
            {
                target.Apply(connectedTarget, settings);
            }
        }
        catch (Exception ex)
        {
            // Do not let an attach failure escape after the base class has registered the
            // target. XAML can still disconnect it later without corrupting base state.
            Logger.LogError("Failed to connect controller-backed system backdrop", ex);
        }

        UpdateBackdropAttachmentState();
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        try
        {
            base.OnTargetDisconnected(disconnectedTarget);
        }
        finally
        {
            if (_targets.Remove(disconnectedTarget, out var target))
            {
                target?.Close(disconnectedTarget);
            }

            UpdateBackdropAttachmentState();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _settings = null;

        // Keep the connected targets rooted until XAML disconnects them. Only the
        // controller or color brush is closed here, synchronously on the owning UI thread.
        foreach (var (target, state) in _targets)
        {
            state?.Close(target);
        }

        UpdateBackdropAttachmentState();
    }

    private void UpdateBackdropAttachmentState()
    {
        var isBackdropAttached = false;

        foreach (var target in _targets.Values)
        {
            if (target?.IsBackdropAttached == true)
            {
                isBackdropAttached = true;
                break;
            }
        }

        if (_isBackdropAttached == isBackdropAttached)
        {
            return;
        }

        _isBackdropAttached = isBackdropAttached;

        try
        {
            BackdropAttachmentChanged?.Invoke(isBackdropAttached);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update system backdrop fallback", ex);
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

    private readonly record struct BackdropSettings(
        BackdropControllerKind Kind,
        Color SolidColor,
        Color TintColor,
        float TintOpacity,
        Color FallbackColor,
        float LuminosityOpacity,
        bool ApplyTint);

    private sealed class BackdropTarget
    {
        private const int SolidAttachRetryCount = 2;

        private readonly XamlRoot _xamlRoot;
        private readonly SystemBackdropConfiguration _configuration;
        private readonly Action _backdropAttachmentChanged;

        private BackdropSettings? _appliedSettings;
        private BackdropSettings? _queuedSettings;
        private WindowsCompositionCompositor? _solidColorCompositor;
        private WindowsCompositionColorBrush? _solidColorBrush;
        private MicaController? _micaController;
        private DesktopAcrylicController? _acrylicController;
        private int _queuedSolidAttachRetries;
        private bool _backdropHasTarget;
        private bool _isApplyQueued;
        private bool _isClosed;

        public bool IsBackdropAttached => _backdropHasTarget;

        public BackdropTarget(XamlRoot xamlRoot, bool isInputActive, Action backdropAttachmentChanged)
        {
            _xamlRoot = xamlRoot;
            _backdropAttachmentChanged = backdropAttachmentChanged;
            _configuration = new SystemBackdropConfiguration
            {
                IsInputActive = isInputActive,
                Theme = ResolveTheme(xamlRoot),
            };
        }

        public void SetIsInputActive(bool isInputActive)
        {
            if (!_isClosed)
            {
                _configuration.IsInputActive = isInputActive;
            }
        }

        public void Apply(ICompositionSupportsSystemBackdrop target, BackdropSettings settings)
        {
            if (_isClosed)
            {
                return;
            }

            if (_isApplyQueued)
            {
                // Keep only the newest theme choice while a previous controller-to-brush
                // handoff is waiting for the current dispatcher callback to unwind.
                _queuedSettings = settings;
                _queuedSolidAttachRetries = settings.Kind == BackdropControllerKind.Solid
                    ? SolidAttachRetryCount
                    : 0;
                return;
            }

            Apply(
                target,
                settings,
                deferSolidAttach: true,
                solidAttachRetriesRemaining: SolidAttachRetryCount);
        }

        public void Close(ICompositionSupportsSystemBackdrop target)
        {
            if (_isClosed)
            {
                return;
            }

            _isClosed = true;
            _queuedSettings = null;
            DetachBackdrop(target);
        }

        private void Apply(
            ICompositionSupportsSystemBackdrop target,
            BackdropSettings settings,
            bool deferSolidAttach,
            int solidAttachRetriesRemaining)
        {
            _configuration.Theme = ResolveTheme(_xamlRoot);

            if (_appliedSettings == settings)
            {
                return;
            }

            if (settings.Kind == BackdropControllerKind.Solid && _solidColorBrush is not null && _backdropHasTarget)
            {
                try
                {
                    _solidColorBrush.Color = settings.SolidColor;
                    _appliedSettings = settings;
                    return;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to update solid system backdrop tint", ex);
                }
            }

            DetachBackdrop(target);

            // SystemBackdropElement can temporarily reject a direct composition brush while
            // the controller removed above is still unwinding its native backdrop link.
            // Let that handoff finish before assigning the color brush.
            if (deferSolidAttach &&
                settings.Kind == BackdropControllerKind.Solid &&
                QueueApply(target, settings, SolidAttachRetryCount))
            {
                return;
            }

            try
            {
                switch (settings.Kind)
                {
                    case BackdropControllerKind.Solid:
                        AttachSolidColorBrush(target, settings);
                        break;

                    case BackdropControllerKind.Mica:
                    case BackdropControllerKind.MicaAlt:
                        AttachMicaController(target, settings);
                        break;

                    case BackdropControllerKind.Acrylic:
                    case BackdropControllerKind.AcrylicThin:
                    default:
                        AttachAcrylicController(target, settings);
                        break;
                }

                _appliedSettings = settings;
            }
            catch (UnauthorizedAccessException ex) when (settings.Kind == BackdropControllerKind.Solid)
            {
                DetachBackdrop(target);

                if (solidAttachRetriesRemaining > 0 &&
                    QueueApply(target, settings, solidAttachRetriesRemaining - 1))
                {
                    return;
                }

                Logger.LogWarning(
                    $"Solid backdrop target remained unavailable after the native handoff; using the fallback background. HRESULT: 0x{ex.HResult:X8}.");
            }
            catch (Exception ex)
            {
                // A failed controller or brush remains owned by this target state and is closed
                // immediately on the XAML thread. The SystemBackdrop target stays rooted.
                DetachBackdrop(target);
                Logger.LogError("Failed to apply composition-backed system backdrop", ex);
            }
        }

        private bool QueueApply(
            ICompositionSupportsSystemBackdrop target,
            BackdropSettings settings,
            int solidAttachRetriesRemaining)
        {
            if (_isClosed)
            {
                return false;
            }

            _queuedSettings = settings;
            _queuedSolidAttachRetries = solidAttachRetriesRemaining;

            if (_isApplyQueued)
            {
                return true;
            }

            _isApplyQueued = true;
            if (_xamlRoot.Content.DispatcherQueue.TryEnqueue(() => ApplyQueued(target)))
            {
                return true;
            }

            _isApplyQueued = false;
            _queuedSettings = null;
            _queuedSolidAttachRetries = 0;
            return false;
        }

        private void ApplyQueued(ICompositionSupportsSystemBackdrop target)
        {
            _isApplyQueued = false;

            var settings = _queuedSettings;
            var solidAttachRetriesRemaining = _queuedSolidAttachRetries;
            _queuedSettings = null;
            _queuedSolidAttachRetries = 0;

            if (_isClosed || settings is null)
            {
                return;
            }

            try
            {
                Apply(
                    target,
                    settings.Value,
                    deferSolidAttach: false,
                    solidAttachRetriesRemaining: solidAttachRetriesRemaining);
            }
            catch (Exception ex)
            {
                DetachBackdrop(target);
                Logger.LogError("Failed to apply queued system backdrop", ex);
            }
            finally
            {
                _backdropAttachmentChanged();
            }
        }

        private void DetachBackdrop(ICompositionSupportsSystemBackdrop target)
        {
            _appliedSettings = null;

            var solidColorCompositor = _solidColorCompositor;
            var solidColorBrush = _solidColorBrush;
            var micaController = _micaController;
            var acrylicController = _acrylicController;
            var backdropHasTarget = _backdropHasTarget;

            _solidColorCompositor = null;
            _solidColorBrush = null;
            _micaController = null;
            _acrylicController = null;
            _backdropHasTarget = false;

            if (solidColorBrush is not null)
            {
                RemoveTargetAndDispose(solidColorBrush, target, backdropHasTarget);
            }

            if (solidColorCompositor is not null)
            {
                Dispose(solidColorCompositor);
            }

            if (micaController is not null)
            {
                RemoveTargetAndDispose(micaController, target, backdropHasTarget);
            }

            if (acrylicController is not null)
            {
                RemoveTargetAndDispose(acrylicController, target, backdropHasTarget);
            }
        }

        private void AttachSolidColorBrush(ICompositionSupportsSystemBackdrop target, BackdropSettings settings)
        {
            // SystemBackdrop uses Windows.UI.Composition brushes even though its target is
            // projected through Microsoft.UI.Composition. Create and retain the matching
            // compositor on the owning XAML thread so neither projection reaches finalization.
            var compositor = new WindowsCompositionCompositor();
            _solidColorCompositor = compositor;
            var brush = compositor.CreateColorBrush(settings.SolidColor);
            _solidColorBrush = brush;
            _backdropHasTarget = true;
            target.SystemBackdrop = brush;
        }

        private void AttachMicaController(ICompositionSupportsSystemBackdrop target, BackdropSettings settings)
        {
            if (!MicaController.IsSupported())
            {
                return;
            }

            var controller = new MicaController
            {
                Kind = settings.Kind == BackdropControllerKind.MicaAlt ? MicaKind.BaseAlt : MicaKind.Base,
            };
            _micaController = controller;

            if (settings.ApplyTint)
            {
                controller.TintColor = settings.TintColor;
                controller.TintOpacity = settings.TintOpacity;
                controller.FallbackColor = settings.FallbackColor;
                controller.LuminosityOpacity = settings.LuminosityOpacity;
            }

            controller.SetSystemBackdropConfiguration(_configuration);
            _backdropHasTarget = true;
            controller.AddSystemBackdropTarget(target);
        }

        private void AttachAcrylicController(ICompositionSupportsSystemBackdrop target, BackdropSettings settings)
        {
            if (!DesktopAcrylicController.IsSupported())
            {
                return;
            }

            var controller = new DesktopAcrylicController
            {
                Kind = settings.Kind == BackdropControllerKind.AcrylicThin
                    ? DesktopAcrylicKind.Thin
                    : DesktopAcrylicKind.Default,
                TintColor = settings.TintColor,
                TintOpacity = settings.TintOpacity,
                FallbackColor = settings.FallbackColor,
                LuminosityOpacity = settings.LuminosityOpacity,
            };
            _acrylicController = controller;

            controller.SetSystemBackdropConfiguration(_configuration);
            _backdropHasTarget = true;
            controller.AddSystemBackdropTarget(target);
        }

        private static void RemoveTargetAndDispose(WindowsCompositionColorBrush brush, ICompositionSupportsSystemBackdrop target, bool backdropHasTarget)
        {
            try
            {
                if (backdropHasTarget)
                {
                    target.SystemBackdrop = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to remove solid system backdrop target", ex);
            }
            finally
            {
                try
                {
                    brush.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to dispose solid system backdrop brush", ex);
                }
            }
        }

        private static void Dispose(WindowsCompositionCompositor compositor)
        {
            try
            {
                compositor.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to dispose solid system backdrop compositor", ex);
            }
        }

        private static void RemoveTargetAndDispose(MicaController controller, ICompositionSupportsSystemBackdrop target, bool backdropHasTarget)
        {
            try
            {
                if (backdropHasTarget)
                {
                    controller.RemoveSystemBackdropTarget(target);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to remove Mica system backdrop target", ex);
            }
            finally
            {
                try
                {
                    controller.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to dispose Mica system backdrop controller", ex);
                }
            }
        }

        private static void RemoveTargetAndDispose(DesktopAcrylicController controller, ICompositionSupportsSystemBackdrop target, bool backdropHasTarget)
        {
            try
            {
                if (backdropHasTarget)
                {
                    controller.RemoveSystemBackdropTarget(target);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to remove acrylic system backdrop target", ex);
            }
            finally
            {
                try
                {
                    controller.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to dispose acrylic system backdrop controller", ex);
                }
            }
        }
    }
}
