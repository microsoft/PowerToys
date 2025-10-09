// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;
using WinUIEx;

namespace TopToolbar
{
    internal sealed class SnapshotPromptWindow : WindowEx
    {
        private const int WindowWidth = 360;
        private const int WindowHeight = 230;

        private readonly TaskCompletionSource<string> _resultSource = new();
        private readonly TextBox _nameBox;
        private readonly TextBlock _errorText;
        private readonly Button _saveButton;

        private SnapshotPromptWindow()
        {
            Title = string.Empty;
            IsTitleBarVisible = false;
            ExtendsContentIntoTitleBar = true;
            SystemBackdrop = new TransparentTintBackdrop(Color.FromArgb(0, 0, 0, 0));

            if (AppWindow?.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsResizable = false;
            }

            var root = new Grid
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            };

            var chromeBorder = new Border
            {
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(22, 18, 22, 24),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
                Background = new LinearGradientBrush
                {
                    StartPoint = new Windows.Foundation.Point(0, 0),
                    EndPoint = new Windows.Foundation.Point(0, 1),
                    GradientStops =
                    {
                        new GradientStop { Color = Color.FromArgb(0xF0, 0x25, 0x28, 0x32), Offset = 0 },
                        new GradientStop { Color = Color.FromArgb(0xF0, 0x1C, 0x1E, 0x27), Offset = 1 },
                    },
                },
            };
            root.Children.Add(chromeBorder);

            var stack = new StackPanel
            {
                Spacing = 12,
            };
            chromeBorder.Child = stack;

            stack.Children.Add(new TextBlock
            {
                Text = "Workspace name",
                Margin = new Thickness(0, 0, 0, 2),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xF3, 0xF3, 0xF3)),
            });

            _nameBox = new TextBox
            {
                PlaceholderText = "Workspace name",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                FontSize = 13,
                MinWidth = 220,
            };
            _nameBox.TextChanged += (_, __) => OnNameChanged();
            _nameBox.KeyDown += OnNameBoxKeyDown;
            stack.Children.Add(_nameBox);

            _errorText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x64, 0x64)),
                Visibility = Visibility.Collapsed,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
            };
            stack.Children.Add(_errorText);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 12,
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                MinWidth = 88,
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
            };
            cancelButton.Click += (_, __) => Cancel();

            _saveButton = new Button
            {
                Content = "Save",
                MinWidth = 88,
                FontSize = 12,
                IsEnabled = false,
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x38, 0x8B, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x38, 0x8B, 0xFF)),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            };
            _saveButton.Click += (_, __) => Confirm();

            buttons.Children.Add(cancelButton);
            buttons.Children.Add(_saveButton);
            stack.Children.Add(buttons);

            Content = root;

            Activated += (_, args) =>
            {
                if (args.WindowActivationState != WindowActivationState.Deactivated)
                {
                    _ = _nameBox.Focus(FocusState.Programmatic);
                }
            };

            Closed += (_, __) =>
            {
                if (!_resultSource.Task.IsCompleted)
                {
                    _resultSource.TrySetResult(null);
                }
            };
        }

        private void OnNameChanged()
        {
            if (_errorText.Visibility == Visibility.Visible)
            {
                _errorText.Visibility = Visibility.Collapsed;
            }

            _saveButton.IsEnabled = !string.IsNullOrWhiteSpace(_nameBox.Text);
        }

        private void OnNameBoxKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                Confirm();
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                e.Handled = true;
                Cancel();
            }
        }

        private void Confirm()
        {
            var name = _nameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                _errorText.Text = "Workspace name is required.";
                _errorText.Visibility = Visibility.Visible;
                return;
            }

            _resultSource.TrySetResult(name);
            Close();
        }

        private void Cancel()
        {
            _resultSource.TrySetResult(null);
            Close();
        }

        public static async Task<string> ShowAsync(WindowEx owner)
        {
            var window = new SnapshotPromptWindow();
            window.AppWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));

            try
            {
                var displayArea = owner != null
                    ? DisplayArea.GetFromWindowId(owner.AppWindow.Id, DisplayAreaFallback.Primary)
                    : DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Primary);

                var workArea = displayArea.WorkArea;
                var offsetX = workArea.X + Math.Max(0, (workArea.Width - WindowWidth) / 2);
                var offsetY = workArea.Y + Math.Max(0, (workArea.Height - WindowHeight) / 2);
                window.AppWindow.Move(new PointInt32(offsetX, offsetY));
            }
            catch
            {
            }

            window.Activate();
            return await window._resultSource.Task.ConfigureAwait(true);
        }
    }
}
