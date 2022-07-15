using System;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using interop;
using ManagedCommon;
using PowerAccent.UI;

namespace PowerAccent;

static class Program
{
    private const string PROGRAM_NAME = "PowerAccent";
    private static App _application;
    private static Mutex _mutex = new Mutex(true, "PowerToys.PowerAccent");

    [STAThread]
    static void Main(string[] args)
    {
        Arguments(args);

        if (_mutex.WaitOne(TimeSpan.Zero, true))
        {
            InitEvents();

            _application = new App();
            _application.InitializeComponent();
            //(_application.MainWindow as Selector).HideTaskbarIcon();
            _application.Run();
        }
    }

    private static void InitEvents()
    {
        // Detect exit event from PowerToys
        new Thread(() =>
        {
            EventWaitHandle eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerAccentExitEvent());
            if (eventHandle.WaitOne())
            {
                Terminate();
            }
        }).Start();

        // wait for 
    }

    #region Arguments

    private static int Arguments(string[] args)
    {
        Option<int> pidOption = new(
            aliases: new[] { "--pid", "-p" },
            getDefaultValue: () => 0,
            description: $"Bind the execution of {PROGRAM_NAME} to another process.");

        Option<bool> openSettingsOption = new(
            aliases: new[] { "--settings", "-s" },
            getDefaultValue: () => false,
            description: $"Open Settings window.");

        RootCommand rootCommand = new RootCommand
        {
            pidOption,
            openSettingsOption,
        };

        rootCommand.Description = PROGRAM_NAME;
        rootCommand.SetHandler(HandleCommandLineArguments, pidOption, openSettingsOption);
        return rootCommand.InvokeAsync(args).Result;
    }

    private static void HandleCommandLineArguments(int pid, bool isOpenSettings)
    {
        if (pid != 0)
        {
            Task.Run(() =>
            {
                RunnerHelper.WaitForPowerToysRunner(pid, Terminate);
            });
        }

        if (isOpenSettings)
        {
            Settings settings = new Settings();
            settings.Show();
        }
    }

    #endregion

    private static void Terminate()
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            Application.Current.Shutdown();
        });
    }
}