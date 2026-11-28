// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WorkspacesLauncherUI.Models;
using VirtualKey = Windows.System.VirtualKey;

namespace WorkspacesLauncherUI
{
    public sealed partial class SignatureWarningWindow : Window, ISignatureWarningWindow
    {
        private readonly SignatureWarningRequest _request;
        private readonly IntPtr _owner;
        private bool _readyForChoice;
        private bool _dismissedByOwner;
        private bool _closed;
        private bool _rendered;
        private ContentDialog _copyError;

        public SignatureWarningWindow(SignatureWarningRequest request)
        {
            _request = request;
            InitializeComponent();
            Root.DataContext = request;
            Title = request.Title;
            Root.Language = CultureInfo.CurrentUICulture.IetfLanguageTag;
            Root.FlowDirection = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            var display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
            var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
            var width = Math.Min((int)(600 * scale), display.WorkArea.Width);
            var height = Math.Min((int)(700 * scale), display.WorkArea.Height);
            AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
            AppWindow.Move(new Windows.Graphics.PointInt32(
                display.WorkArea.X + ((display.WorkArea.Width - width) / 2),
                display.WorkArea.Y + ((display.WorkArea.Height - height) / 2)));
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = true;
            }

            var owner = (Application.Current as App)?.MainWindow;
            if (owner != null)
            {
                _owner = WinRT.Interop.WindowNative.GetWindowHandle(owner);
                SetWindowLongPtrW(WinRT.Interop.WindowNative.GetWindowHandle(this), -8, _owner);
            }

            Root.Loaded += (_, _) =>
            {
                SkipButton.Focus(FocusState.Programmatic);
                CompositionTarget.Rendering += OnRendering;
            };
            Closed += (_, _) =>
            {
                _closed = true;
                CompositionTarget.Rendering -= OnRendering;
                _copyError?.Hide();
                if (_owner != IntPtr.Zero)
                {
                    EnableWindow(_owner, true);
                }

                if (!_dismissedByOwner)
                {
                    ChoiceMade?.Invoke(this, false);
                }
            };
        }

        public event EventHandler Rendered;

        public event EventHandler<bool> ChoiceMade;

        public bool IsVisible => !_closed && AppWindow.IsVisible;

        public Visibility EscapedDisplayVisibility => _request.HasEscapedDisplayValues ? Visibility.Visible : Visibility.Collapsed;

        public void Show()
        {
            if (_owner != IntPtr.Zero)
            {
                EnableWindow(_owner, false);
            }

            Activate();
        }

        private void OnRendering(object sender, object args)
        {
            CompositionTarget.Rendering -= OnRendering;
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                if (!_closed && !_dismissedByOwner && !_rendered && IsVisible)
                {
                    _rendered = true;
                    Rendered?.Invoke(this, EventArgs.Empty);
                }
            });
        }

        public void EnableRunChoice()
        {
            if (!_dismissedByOwner && !_closed)
            {
                _readyForChoice = true;
                RunAnywayButton.IsEnabled = true;
            }
        }

        public void DismissWithoutResponse()
        {
            _dismissedByOwner = true;
            _readyForChoice = false;
            RunAnywayButton.IsEnabled = false;
            _copyError?.Hide();
            if (!_closed)
            {
                Close();
            }
        }

        private void RunAnyway_Click(object sender, RoutedEventArgs e)
        {
            if (_readyForChoice && !_dismissedByOwner && !_closed)
            {
                ChoiceMade?.Invoke(this, true);
            }
        }

        private void Skip_Click(object sender, RoutedEventArgs e) => Close();

        private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private async void CopyDetails_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var content = new DataPackage();
                content.SetText(_request.DetailsText);
                Clipboard.SetContent(content);
                Clipboard.Flush();
            }
            catch (Exception exception) when (exception is ExternalException || exception is InvalidOperationException)
            {
                Logger.LogError("Unable to copy Workspaces elevation warning details", exception);
                if (!_dismissedByOwner && !_closed)
                {
                    _copyError = new ContentDialog
                    {
                        XamlRoot = Root.XamlRoot,
                        Title = Title,
                        Content = _request.CopyDetailsErrorText,
                        CloseButtonText = _request.SkipText,
                    };
                    await _copyError.ShowAsync();
                    _copyError = null;
                }
            }
        }

        [LibraryImport("user32.dll")]
        private static partial uint GetDpiForWindow(IntPtr window);

        [LibraryImport("user32.dll")]
        private static partial IntPtr SetWindowLongPtrW(IntPtr window, int index, IntPtr value);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool EnableWindow(IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool enable);
    }
}
