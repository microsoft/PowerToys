// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Peek.FilePreviewer.Controls
{
    [INotifyPropertyChanged]
    public unsafe sealed partial class ShellPreviewHandlerControl : UserControl
    {
        // Mica fallback colors
        private static readonly COLORREF LightThemeBgColor = new(0x00f3f3f3);
        private static readonly COLORREF DarkThemeBgColor = new(0x00202020);

        private static readonly HBRUSH LightThemeBgBrush = PInvoke.CreateSolidBrush(LightThemeBgColor);
        private static readonly HBRUSH DarkThemeBgBrush = PInvoke.CreateSolidBrush(DarkThemeBgColor);

        [ObservableProperty]
        private IPreviewHandler? source;

        private HWND containerHwnd;
        private WNDPROC containerWndProc;
        private HBRUSH containerBgBrush;
        private RECT controlRect;

        public event EventHandler? HandlerLoaded;

        public event EventHandler? HandlerError;

        public static readonly DependencyProperty HandlerVisibilityProperty = DependencyProperty.Register(
            nameof(HandlerVisibility),
            typeof(Visibility),
            typeof(ShellPreviewHandlerControl),
            new PropertyMetadata(Visibility.Collapsed, new PropertyChangedCallback((d, e) => ((ShellPreviewHandlerControl)d).OnHandlerVisibilityChanged())));

        // Must have its own visibility property so resize events can still fire
        public Visibility HandlerVisibility
        {
            get { return (Visibility)GetValue(HandlerVisibilityProperty); }
            set { SetValue(HandlerVisibilityProperty, value); }
        }

        public ShellPreviewHandlerControl()
        {
            InitializeComponent();

            containerWndProc = ContainerWndProc;
        }

        partial void OnSourceChanged(IPreviewHandler? value)
        {
            if (Source != null)
            {
                UpdatePreviewerTheme();

                try
                {
                    // Attach the preview handler to the container window
                    Source.SetWindow(containerHwnd, (RECT*)Unsafe.AsPointer(ref controlRect));
                    Source.DoPreview();

                    HandlerLoaded?.Invoke(this, EventArgs.Empty);
                }
                catch
                {
                    HandlerError?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void OnHandlerVisibilityChanged()
        {
            if (HandlerVisibility == Visibility.Visible)
            {
                PInvoke.ShowWindow(containerHwnd, SHOW_WINDOW_CMD.SW_SHOW);
                IsEnabled = true;

                // Clears the background from the last previewer
                // The brush can only be drawn here because flashes will occur during resize
                PInvoke.SetClassLongPtr(containerHwnd, GET_CLASS_LONG_INDEX.GCLP_HBRBACKGROUND, containerBgBrush);
                PInvoke.UpdateWindow(containerHwnd);
                PInvoke.SetClassLongPtr(containerHwnd, GET_CLASS_LONG_INDEX.GCLP_HBRBACKGROUND, IntPtr.Zero);
                PInvoke.InvalidateRect(containerHwnd, (RECT*)null, true);
            }
            else
            {
                PInvoke.ShowWindow(containerHwnd, SHOW_WINDOW_CMD.SW_HIDE);
                IsEnabled = false;
            }
        }

        private void UpdatePreviewerTheme()
        {
            COLORREF bgColor, fgColor;
            switch (ActualTheme)
            {
                case ElementTheme.Light:
                    bgColor = LightThemeBgColor;
                    fgColor = new COLORREF(0x00000000); // Black

                    containerBgBrush = LightThemeBgBrush;
                    break;

                case ElementTheme.Dark:
                default:
                    bgColor = DarkThemeBgColor;
                    fgColor = new COLORREF(0x00FFFFFF); // White

                    containerBgBrush = DarkThemeBgBrush;
                    break;
            }

            if (Source is IPreviewHandlerVisuals visuals)
            {
                visuals.SetBackgroundColor(bgColor);
                visuals.SetTextColor(fgColor);

                // Changing the previewer colors might not always redraw itself
                PInvoke.InvalidateRect(containerHwnd, (RECT*)null, true);
            }
        }

        private LRESULT ContainerWndProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
        {
            // Here for future use :)
            return PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            fixed (char* pContainerClassName = "PeekShellPreviewHandlerContainer")
            {
                PInvoke.RegisterClass(new WNDCLASSW()
                {
                    lpfnWndProc = containerWndProc,
                    lpszClassName = pContainerClassName,
                });

                // Create the container window to host the preview handler
                containerHwnd = PInvoke.CreateWindowEx(
                    WINDOW_EX_STYLE.WS_EX_LAYERED,
                    pContainerClassName,
                    null,
                    WINDOW_STYLE.WS_CHILD,
                    0, // X
                    0, // Y
                    0, // Width
                    0, // Height
                    (HWND)Win32Interop.GetWindowFromWindowId(XamlRoot.ContentIslandEnvironment.AppWindowId), // Peek UI window
                    HMENU.Null,
                    HINSTANCE.Null);

                // Allows the preview handlers to display properly
                PInvoke.SetLayeredWindowAttributes(containerHwnd, default, byte.MaxValue, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
            }
        }

        private void UserControl_EffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            var dpi = (float)PInvoke.GetDpiForWindow(containerHwnd) / 96;

            // Resize the container window
            PInvoke.SetWindowPos(
                containerHwnd,
                (HWND)0, // HWND_TOP
                (int)(Math.Abs(args.EffectiveViewport.X) * dpi),
                (int)(Math.Abs(args.EffectiveViewport.Y) * dpi),
                (int)(ActualWidth * dpi),
                (int)(ActualHeight * dpi),
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

            // Resize the preview handler window
            controlRect.right = (int)(ActualWidth * dpi);
            controlRect.bottom = (int)(ActualHeight * dpi);
            try
            {
                Source?.SetRect((RECT*)Unsafe.AsPointer(ref controlRect));
            }
            catch
            {
            }

            // Resizing the previewer might not always redraw itself
            PInvoke.InvalidateRect(containerHwnd, (RECT*)null, false);
        }

        private void UserControl_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                Source?.SetFocus();
            }
            catch
            {
            }
        }
    }
}
