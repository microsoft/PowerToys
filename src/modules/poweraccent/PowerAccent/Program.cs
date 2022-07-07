using System;
using System.CommandLine;
using System.CommandLine.Invocation;
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

    [STAThread]
    static void Main(string[] args)
    {
        Arguments(args);
        InitEvents();

        _application = new App();
        _application.InitializeComponent();
        _application.Run();
        (_application.MainWindow as Selector).HideTaskbarIcon();
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
    }

    private static int Arguments(string[] args)
    {
        Option<int> pidOption = new(
            aliases: new[] { "--pid", "-p" },
            getDefaultValue: () => 0,
            description: $"Bind the execution of {PROGRAM_NAME} to another process.")
        {
            Argument = new Argument<int>(() => 0)
            {
                Arity = ArgumentArity.ZeroOrOne,
            },
        };

        pidOption.Required = false;

        RootCommand rootCommand = new RootCommand
        {
            pidOption,
        };

        rootCommand.Description = PROGRAM_NAME;

        rootCommand.Handler = CommandHandler.Create<int>(HandleCommandLineArguments);

        return rootCommand.InvokeAsync(args).Result;
    }

    private static void HandleCommandLineArguments(int pid)
    {
        if (pid != 0)
        {
            Task.Run(() =>
            {
                RunnerHelper.WaitForPowerToysRunner(pid, Terminate);
            });
        }
    }

    private static void Terminate()
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            Application.Current.Shutdown();
        });
    }
}