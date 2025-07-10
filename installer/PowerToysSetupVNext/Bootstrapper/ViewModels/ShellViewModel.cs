using Bootstrapper.Models;
using Bootstrapper.Models.Util;
using Bootstrapper.ViewModels.Util;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using WixToolset.BootstrapperApplicationApi;

namespace Bootstrapper.ViewModels;

internal class ShellViewModel : ViewModelBase
{
  private readonly Model _model;
  private readonly CancelViewModel _cancelVm;
  private readonly IDelegateCommand _installCommand;
  private readonly IDelegateCommand _updateCommand;
  private readonly IDelegateCommand _uninstallCommand;
  private bool _isRepairAvailable;
  private IDelegateCommand _executeCommand;
  private string _executeDescription;
  private string _message;

  public ShellViewModel(Model model)
  {
    _model = model;
    _cancelVm = new CancelViewModel(model);

    _installCommand = new DelegateCommand(Install, CanInstall);
    _uninstallCommand = new DelegateCommand(Uninstall, CanUninstall);
    _updateCommand = new DelegateCommand(Update, CanUpdate);
    RepairCommand = new DelegateCommand(Repair, CanRepair);
    ExitCommand = new DelegateCommand(Exit, CanExit);
    ShowLogCommand = new DelegateCommand(ShowLog, CanShowLog);

    ConfigVm = new ConfigViewModel(model);
    ConfigVm.PropertyChanged += ConfigVm_PropertyChanged;
    ProgressVm = new ProgressViewModel();
  }

  public ConfigViewModel ConfigVm { get; }
  public ProgressViewModel ProgressVm { get; }
  public IDelegateCommand ShowLogCommand { get; }
  public IDelegateCommand ExitCommand { get; }
  public IDelegateCommand RepairCommand { get; }
  public IDelegateCommand CancelCommand => _cancelVm.CancelCommand;

  /// <summary>
  ///   Is installer waiting for user input?
  /// </summary>
  public bool IsWaiting => _model.State.BaStatus == BaStatus.Waiting;

  /// <summary>
  ///   Is the UI running in passive mode, only displaying a progress bar?
  /// </summary>
  public bool IsPassive => _model.State.Display == Display.Passive;

  /// <summary>
  ///   The command that will install or uninstall the software
  /// </summary>
  public IDelegateCommand ExecuteCommand
  {
    get => _executeCommand;
    set
    {
      if (_executeCommand == value)
        return;

      _executeCommand = value;
      OnPropertyChanged();
    }
  }

  /// <summary>
  ///   A brief, one-word description of what will happen when the <see cref="ExecuteCommand" /> is run.
  ///   Should be appropriate for button text.
  /// </summary>
  public string ExecuteDescription
  {
    get => _executeDescription;
    set
    {
      if (_executeDescription == value)
        return;

      _executeDescription = value;
      OnPropertyChanged();
    }
  }

  /// <summary>
  ///   Display the Repair button?
  /// </summary>
  public bool IsRepairAvailable
  {
    get => _isRepairAvailable;
    set
    {
      if (_isRepairAvailable == value)
        return;

      _isRepairAvailable = value;
      OnPropertyChanged();
    }
  }

  /// <summary>
  ///   A message to display to the user.
  /// </summary>
  public string Message
  {
    get => _message;
    set
    {
      if (_message == value)
        return;

      _message = value;
      OnPropertyChanged();
    }
  }

  /// <summary>
  ///   Call after the detect phase completes to refresh the UI.
  /// </summary>
  /// <param name="followupAction">
  ///   Indicates which action will be planned when the BA is running silently or in passive mode.
  ///   Pass <see cref="LaunchAction.Unknown" /> for full UI mode.
  /// </param>
  public void AfterDetect(LaunchAction followupAction)
  {
    try
    {
      OnPropertyChanged(nameof(IsWaiting));

      if (_model.State.RelatedBundleStatus == BundleStatus.OlderInstalled || followupAction == LaunchAction.UpdateReplace || followupAction == LaunchAction.UpdateReplaceEmbedded)
      {
        ExecuteCommand = _updateCommand;
        ExecuteDescription = "Update";
      }
      else if (_model.State.RelatedBundleStatus == BundleStatus.Current || followupAction == LaunchAction.Uninstall || followupAction == LaunchAction.UnsafeUninstall)
      {
        ExecuteCommand = _uninstallCommand;
        ExecuteDescription = "Uninstall";
      }
      else
      {
        ExecuteCommand = _installCommand;
        ExecuteDescription = "Install";
      }

      IsRepairAvailable = _model.State.RelatedBundleStatus == BundleStatus.Current;
      AssignMessage();
      ConfigVm.AfterDetect();
    }
    catch (Exception ex)
    {
      _model.Log.Write(ex);
      Message = $"Error: {ex.Message}";
      _model.UiFacade.ShowMessageBox($"Error: {ex.Message}", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
    }
    finally
    {
      CommandManager.InvalidateRequerySuggested();
    }
  }

  /// <summary>
  ///   Call after the apply phase completes to refresh the UI.
  /// </summary>
  public void AfterApply()
  {
    try
    {
      ProgressVm.Reset();
      AssignMessage();
    }
    catch (Exception ex)
    {
      _model.Log.Write(ex);
      Message = $"Error: {ex.Message}";
      _model.UiFacade.ShowMessageBox($"Error: {ex.Message}", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
    }
    finally
    {
      CommandManager.InvalidateRequerySuggested();
    }
  }

  private void Install()
  {
    _model.PlanAndApply(LaunchAction.Install);
    OnPropertyChanged(nameof(IsWaiting));
    CommandManager.InvalidateRequerySuggested();
  }

  private bool CanInstall()
  {
    return _model.State.RelatedBundleStatus == BundleStatus.NotInstalled && CanPlanAndApply();
  }

  private void Update()
  {
    // Any older bundles that were discovered have already been scheduled for uninstall, so an "upgrade" will be a fresh installation.
    _model.PlanAndApply(LaunchAction.Install);
    OnPropertyChanged(nameof(IsWaiting));
    CommandManager.InvalidateRequerySuggested();
  }

  private bool CanUpdate()
  {
    return _model.State.RelatedBundleStatus == BundleStatus.OlderInstalled && CanPlanAndApply();
  }

  private void Uninstall()
  {
    _model.PlanAndApply(LaunchAction.Uninstall);
    OnPropertyChanged(nameof(IsWaiting));
    CommandManager.InvalidateRequerySuggested();
  }

  private bool CanUninstall()
  {
    return _model.State.RelatedBundleStatus == BundleStatus.Current && CanPlanAndApply();
  }

  private void Repair()
  {
    _model.PlanAndApply(LaunchAction.Repair);
    OnPropertyChanged(nameof(IsWaiting));
    CommandManager.InvalidateRequerySuggested();
  }

  private bool CanRepair()
  {
    return _model.State.RelatedBundleStatus == BundleStatus.Current && CanPlanAndApply();
  }

  private void Exit()
  {
    _model.UiFacade.ShutDown();
  }

  private bool CanExit()
  {
    return _model.State.BaStatus == BaStatus.Failed || _model.State.BaStatus == BaStatus.Cancelled || _model.State.BaStatus == BaStatus.Applied || _model.State.BaStatus == BaStatus.Waiting;
    ;
  }

  private void ShowLog()
  {
    _model.ShowLog();
  }

  private bool CanShowLog()
  {
    return _model.State.BaStatus == BaStatus.Failed || _model.State.BaStatus == BaStatus.Cancelled || _model.State.BaStatus == BaStatus.Applied || _model.State.BaStatus == BaStatus.Waiting;
    ;
  }

  private void ConfigVm_PropertyChanged(object sender, PropertyChangedEventArgs e)
  {
    RepairCommand.RaiseCanExecuteChanged();
    ExecuteCommand?.RaiseCanExecuteChanged();
  }

  private bool CanPlanAndApply()
  {
    // Ensure ConfigVm is not displaying any data validation errors.
    return _model.State.BaStatus == BaStatus.Waiting && string.IsNullOrEmpty(ConfigVm.Error);
  }

  /// <summary>
  ///   Will assign a value to <see cref="Message" /> based on the current state.
  ///   This should be called after Detect, and again after Apply.
  /// </summary>
  /// <exception cref="ArgumentOutOfRangeException"></exception>
  private void AssignMessage()
  {
    switch (_model.State.BaStatus)
    {
      case BaStatus.Cancelled:
        Message = "User cancelled";
        break;

      case BaStatus.Failed:
        if (!string.IsNullOrWhiteSpace(_model.State.ErrorMessage))
          Message = $"Failed: {_model.State.ErrorMessage}";
        else if (_model.State.CancelRequested)
          Message = "User cancelled";
        else
          Message = "An error occurred. See log for details.";

        break;

      case BaStatus.Planning:
      case BaStatus.Applying:
      case BaStatus.Waiting:
        // BA will be in one of these states after successfully completing the detect phase.
        if (string.IsNullOrEmpty(_model.State.RelatedBundleVersion))
          Message = $"Installing v{_model.State.BundleVersion}";
        else
        {
          switch (_model.State.RelatedBundleStatus)
          {
            case BundleStatus.Unknown:
            case BundleStatus.NotInstalled:
              Message = $"Installing v{_model.State.BundleVersion}";
              break;

            case BundleStatus.OlderInstalled:
              Message = $"Updating v{_model.State.RelatedBundleVersion} to {_model.State.BundleVersion}";
              break;

            case BundleStatus.Current:
              Message = $"v{_model.State.BundleVersion} is currently installed";
              break;

            case BundleStatus.NewerInstalled:
              Message = $"There is already a newer version (v{_model.State.RelatedBundleVersion}) installed on this machine.";
              break;

            default:
              throw new ArgumentOutOfRangeException(nameof(_model.State.RelatedBundleStatus));
          }
        }

        break;

      case BaStatus.Applied:
        switch (_model.State.PlannedAction)
        {
          case LaunchAction.Layout:
            Message = $"v{_model.State.BundleVersion} successfully laid out";
            break;

          case LaunchAction.UnsafeUninstall:
          case LaunchAction.Uninstall:
            Message = $"v{_model.State.BundleVersion} successfully removed";
            break;

          case LaunchAction.Modify:
            Message = $"v{_model.State.BundleVersion} successfully modified";
            break;

          case LaunchAction.Repair:
            Message = $"v{_model.State.BundleVersion} successfully repaired";
            break;

          case LaunchAction.UpdateReplace:
          case LaunchAction.UpdateReplaceEmbedded:
            Message = $"v{_model.State.RelatedBundleVersion} successfully updated to {_model.State.BundleVersion}";
            break;

          case LaunchAction.Unknown:
          case LaunchAction.Help:
          case LaunchAction.Cache:
          case LaunchAction.Install:
          default:
            Message = $"v{_model.State.BundleVersion} successfully installed";
            break;
        }

        break;

      case BaStatus.Initializing:
      case BaStatus.Detecting:
        // No reason to display a message
        break;

      default:
        throw new ArgumentOutOfRangeException(nameof(_model.State.BaStatus));
    }
  }
}