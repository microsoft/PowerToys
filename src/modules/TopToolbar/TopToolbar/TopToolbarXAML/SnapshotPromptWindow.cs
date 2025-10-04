// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinUIEx;

namespace TopToolbar
{
    internal sealed class SnapshotPromptWindow : WindowEx
    {
        private const int WindowWidth = 420;
        private const int WindowHeight = 220;

        private readonly TaskCompletionSource<string> _resultSource = new();
        private readonly TextBox _nameBox;
        private readonly TextBlock _errorText;
        private readonly Button _saveButton;

        private SnapshotPromptWindow()
        {
            Title = "Create workspace snapshot";
            IsTitleBarVisible = true;

            var root = new Grid
            {
                Padding = new Thickness(24),
            };

            var stack = new StackPanel
            {
                Spacing = 16,
            };
            root.Children.Add(stack);

            stack.Children.Add(new TextBlock
            {
                Text = "Enter a name for the new workspace snapshot.",
                TextWrapping = TextWrapping.Wrap,
            });

            _nameBox = new TextBox
            {
                PlaceholderText = "Workspace name",
                MinWidth = 260,
            };
            _nameBox.TextChanged += (_, __) => OnNameChanged();
            _nameBox.KeyDown += OnNameBoxKeyDown;
            stack.Children.Add(_nameBox);

            _errorText = new TextBlock
            {
                Foreground = new SolidColorBrush(Colors.Red),
                Visibility = Visibility.Collapsed,
                TextWrapping = TextWrapping.Wrap,
            };
            stack.Children.Add(_errorText);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                MinWidth = 88,
            };
            cancelButton.Click += (_, __) => Cancel();

            _saveButton = new Button
            {
                Content = "Save",
                MinWidth = 88,
                IsEnabled = false,
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

        private void OnNameBoxKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
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

            if (owner != null)
            {
                try
                {
                    var ownerPosition = owner.AppWindow.Position;
                    var ownerSize = owner.AppWindow.Size;
                    var desiredSize = new SizeInt32(WindowWidth, WindowHeight);
                    window.AppWindow.Resize(desiredSize);

                    var offsetX = ownerPosition.X + Math.Max(0, (ownerSize.Width - desiredSize.Width) / 2);
                    var offsetY = ownerPosition.Y + Math.Max(0, (ownerSize.Height - desiredSize.Height) / 2);
                    window.AppWindow.Move(new PointInt32(offsetX, offsetY));
                }
                catch
                {
                }
            }

            window.Activate();
            return await window._resultSource.Task.ConfigureAwait(true);
        }
    }
}
