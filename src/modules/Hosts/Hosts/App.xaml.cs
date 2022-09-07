// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Hosts.Helpers;
using Microsoft.UI.Xaml;

namespace Hosts
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            var thread = new Thread(() =>
            {
                try
                {
                    Directory.GetFiles(Path.GetDirectoryName(HostsService.HostsFilePath), $"*{HostsService.BackupSuffix}*")
                        .Select(f => new FileInfo(f))
                        .Where(f => f.CreationTime < DateTime.Now.AddDays(-15))
                        .ToList()
                        .ForEach(f => f.Delete());
                }
                catch
                {
                }
            });

            thread.Start();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }

        private Window _window;
    }
}
