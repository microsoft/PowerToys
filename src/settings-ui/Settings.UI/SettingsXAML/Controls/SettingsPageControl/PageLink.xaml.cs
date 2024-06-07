// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class PageLink : UserControl
    {
        public PageLink()
        {
            this.InitializeComponent();
        }

        public string Text { get; set; }

        public Uri Link { get; set; }

        public ICommand Command { get; set; }

        public object CommandParameter { get; set; }

        private async void OnClick(object sender, RoutedEventArgs e)
        {
            if (Command != null && Command.CanExecute(CommandParameter))
            {
                if (Command is AsyncRelayCommand asyncCommand)
                {
                    await asyncCommand.ExecuteAsync(CommandParameter);
                }
                else
                {
                    Command.Execute(CommandParameter);
                }

                // Check if CommandParameter has been updated
                if (CommandParameter is string uriString && !string.IsNullOrEmpty(uriString))
                {
                    _ = Launcher.LaunchUriAsync(new Uri(uriString));
                }
                else if (Link != null)
                {
                    _ = Launcher.LaunchUriAsync(Link);
                }
            }
            else if (Link != null)
            {
                var uri = CommandParameter as string ?? Link.ToString();
                _ = Launcher.LaunchUriAsync(new Uri(uri));
            }
        }
    }
}
