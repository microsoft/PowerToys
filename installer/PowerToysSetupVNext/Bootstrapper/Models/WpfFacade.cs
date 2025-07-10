using Bootstrapper.Models.State;
using Bootstrapper.Models.Util;
using Bootstrapper.ViewModels;
using Bootstrapper.Views;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using WixToolset.BootstrapperApplicationApi;

namespace Bootstrapper.Models;

internal class WpfFacade
{
  private readonly Log _log;
  private ShellViewModel _shellVm;
  private Window _shell;
  private Dispatcher _dispatcher;

  public WpfFacade(Log log, Display display)
  {
    _log = log ?? throw new ArgumentNullException(nameof(log));
    IsUiShown = display == Display.Full || display == Display.Passive;
  }

  /// <summary>
  ///   Dispatches progress reports to the UI. Will be <see langword="null" /> when there is no UI to receive these reports.
  /// </summary>
  public IProgress<ProgressReport> ProgressReporter { get; private set; }

  /// <summary>
  ///   A valid window handle required for the apply phase.
  /// </summary>
  public IntPtr ShellWindowHandle { get; private set; }

  /// <summary>
  ///   Returns <see langword="false" /> when the installer is running silently.
  /// </summary>
  public bool IsUiShown { get; }

  /// <summary>
  ///   Builds out needed UI elements and displays the shell window if not running silently.
  /// </summary>
  /// <param name="model"></param>
  public void Initialize(Model model)
  {
    _dispatcher = Dispatcher.CurrentDispatcher;
    _dispatcher.UnhandledException += Dispatcher_UnhandledException;
    _shell = new ShellView();
    ShellWindowHandle = new WindowInteropHelper(_shell).EnsureHandle();

    // Stop message loop when the window is closed.
    _shell.Closed += (sender, e) => _dispatcher.InvokeShutdown();

    if (!IsUiShown)
      return;

    _shellVm = new ShellViewModel(model);
    _shell.DataContext = _shellVm;
    ProgressReporter = new Progress<ProgressReport>(r => _shellVm.ProgressVm.ProcessProgressReport(r));

    _log.Write("Displaying UI.");
    _shell.Show();
  }

  /// <summary>
  ///   Starts the message loop for the UI framework. This is a blocking call that is exited by calling.
  ///   <see cref="ShutDown" />.
  /// </summary>
  /// <exception cref="InvalidOperationException"></exception>
  public void RunMessageLoop()
  {
    if (_shell == null)
      throw new InvalidOperationException($"{nameof(Initialize)} must be called before the message loop can be started");

    Dispatcher.Run();
  }

  /// <summary>
  ///   Executes the given action on "the UI thread".
  /// </summary>
  /// <param name="action">Action to execute</param>
  /// <param name="blockUntilComplete">
  ///   If <see langword="true" />, then this call will block until the action completes.
  ///   If <see langword="false" />, this call immediately returns after queuing the action
  ///   for processing by the message loop.
  /// </param>
  public void Dispatch(Action action, bool blockUntilComplete = true)
  {
    if (blockUntilComplete)
    {
      // No need to dispatch if already running on the UI thread
      if (_dispatcher.CheckAccess())
        action();
      else
        _dispatcher.Invoke(DispatcherPriority.Normal, action);
    }
    else
      _dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
  }

  /// <summary>
  ///   Closes any displayed windows and stops the message loop.
  /// </summary>
  public void ShutDown()
  {
    Dispatch(CloseShell);
  }

  /// <summary>
  ///   Submits a request to the UI to refresh all commands.
  /// </summary>
  public void Refresh()
  {
    if (IsUiShown)
      Dispatch(CommandManager.InvalidateRequerySuggested);
  }

  /// <summary>
  ///   Displays a message box with the given criteria
  /// </summary>
  /// <param name="message"></param>
  /// <param name="buttons"></param>
  /// <param name="image"></param>
  /// <param name="defaultResult"></param>
  /// <returns></returns>
  public MessageBoxResult ShowMessageBox(string message, MessageBoxButton buttons, MessageBoxImage image, MessageBoxResult defaultResult)
  {
    var result = defaultResult;

    if (IsUiShown)
    {
      Dispatch(
        () => result = MessageBox.Show(
          _shell,
          message,
          "Installation",
          buttons,
          image,
          defaultResult));
    }
    else
    {
      Dispatch(
        () => result = MessageBox.Show(
          message,
          "Installation",
          buttons,
          image,
          defaultResult));
    }

    return result;
  }

  /// <summary>
  ///   Informs the UI that the detect phase has completed.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public void OnDetectPhaseComplete(object sender, DetectPhaseCompleteEventArgs e)
  {
    if (_shellVm != null)
    {
      _log.Write($"{nameof(WpfFacade)}: Notified that detect is complete. Dispatching UI refresh tasks.");
      Dispatch(() => _shellVm.AfterDetect(e.FollowupAction), false);
    }
  }

  /// <summary>
  ///   Informs the UI that the apply phase has completed
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public void OnApplyPhaseComplete(object sender, EventArgs e)
  {
    if (_shellVm != null)
    {
      _log.Write($"{nameof(WpfFacade)}: Notified that installation has completed. Dispatching UI refresh tasks.");
      Dispatch(_shellVm.AfterApply, false);
    }
  }

  private void CloseShell()
  {
    if (IsUiShown)
      _shell.Close();

    _dispatcher.InvokeShutdown();
  }

  private void Dispatcher_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
  {
    e.Handled = true;
    _log.Write(e.Exception);

    if (IsUiShown)
      ShowMessageBox($"An error occurred:\r\n{e.Exception.Message}", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
  }
}