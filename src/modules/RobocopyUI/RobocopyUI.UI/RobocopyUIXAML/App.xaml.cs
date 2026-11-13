// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using Microsoft.UI.Xaml;
using RobocopyUI.Models;

namespace RobocopyUI
{
    public partial class App
    {
        private ShellPage? _window;

        public static ConcurrentDictionary<string, OptionContent> Options { get; } = new ConcurrentDictionary<string, OptionContent>();

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new ShellPage();
            _window.Activate();
        }
    }
}
