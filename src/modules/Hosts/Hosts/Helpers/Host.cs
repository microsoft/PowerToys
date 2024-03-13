// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using Hosts.Settings;
using Hosts.ViewModels;
using Hosts.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hosts.Helpers
{
    public static class Host
    {
        public static IHost HostInstance
        {
            get;
        }

        public static T GetService<T>()
            where T : class
        {
            if (HostInstance!.Services.GetService(typeof(T)) is not T service)
            {
                throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
            }

            return service;
        }

        static Host()
        {
            HostInstance = Microsoft.Extensions.Hosting.Host.
                CreateDefaultBuilder().
                UseContentRoot(AppContext.BaseDirectory).
                ConfigureServices((context, services) =>
                {
                    // Core Services
                    services.AddSingleton<IFileSystem, FileSystem>();
                    services.AddSingleton<IHostsService, HostsService>();
                    services.AddSingleton<IUserSettings, UserSettings>();
                    services.AddSingleton<IElevationHelper, ElevationHelper>();

                    // Views and ViewModels
                    services.AddTransient<HostsMainPage>();
                    services.AddTransient<MainViewModel>();
                }).
                Build();
        }
    }
}
