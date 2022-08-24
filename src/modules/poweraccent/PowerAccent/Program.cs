// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#pragma warning disable SA1310 // FieldNamesMustNotContainUnderscore

using System;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using interop;
using ManagedCommon;
using PowerAccent.UI;

namespace PowerAccent;

internal static class Program
{
    private const string PROGRAM_NAME = "PowerAccent";
    private const string PROGRAM_APP_NAME = "PowerToys.PowerAccent";
    private static App _application;
    private static CancellationTokenSource _tokenSource = new CancellationTokenSource();

    [STAThread]
    public static void Main(string[] args)
    {
        _ = new Mutex(true, PROGRAM_APP_NAME, out bool instantiated);

        if (instantiated)
        {
            Arguments(args);

            InitEvents();

            _application = new App();
            _application.InitializeComponent();
            _application.Run();
        }
    }

    private static void InitEvents()
    {
        Task.Run(
            () =>
            {
                EventWaitHandle eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerAccentExitEvent());
                if (eventHandle.WaitOne())
                {
                    Terminate();
                }
            }, _tokenSource.Token);
    }

    private static int Arguments(string[] args)
    {
        Option<int> pidOption = new (
            aliases: new[] { "--pid", "-p" },
            getDefaultValue: () => 0,
            description: $"Bind the execution of {PROGRAM_NAME} to another process.");

        RootCommand rootCommand = new RootCommand
        {
            pidOption,
        };

        rootCommand.Description = PROGRAM_NAME;
        rootCommand.SetHandler(HandleCommandLineArguments, pidOption);
        return rootCommand.InvokeAsync(args).Result;
    }

    private static void HandleCommandLineArguments(int pid)
    {
        if (pid != 0)
        {
            Task.Run(
                () =>
                {
                    RunnerHelper.WaitForPowerToysRunner(pid, Terminate);
                }, _tokenSource.Token);
        }
    }

    private static void Terminate()
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _tokenSource.Cancel();
            Application.Current.Shutdown();
        });
    }
}
