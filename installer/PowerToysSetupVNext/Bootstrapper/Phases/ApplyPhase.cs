using Bootstrapper.Models;
using Bootstrapper.Models.Util;
using System;
using System.Linq;
using System.Windows;
using WixToolset.BootstrapperApplicationApi;

namespace Bootstrapper.Phases;

internal class ApplyPhase
{
  private readonly Model _model;

  public ApplyPhase(Model model)
  {
    _model = model;
  }

  public event EventHandler<EventArgs> ApplyPhaseComplete;

  /// <summary>
  ///   Fired when the engine has begun installing the bundle.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  /// <exception cref="PhaseException"></exception>
  public virtual void OnApplyBegin(object sender, ApplyBeginEventArgs e)
  {
    try
    {
      _model.State.PhaseResult = 0;
      _model.State.ErrorMessage = string.Empty;
      if (e.Cancel)
        return;

      _model.State.BaStatus = BaStatus.Applying;
    }
    catch (Exception ex)
    {
      _model.Log.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <summary>
  ///   Fired when the engine has completed installing the bundle.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  /// <exception cref="PhaseException"></exception>
  public virtual void OnApplyComplete(object sender, ApplyCompleteEventArgs e)
  {
    try
    {
      _model.State.PhaseResult = e.Status;

      // Set the state to applied or failed unless the user cancelled.
      if (_model.State.BaStatus == BaStatus.Cancelled || e.Status == ErrorHelper.CancelHResult)
      {
        _model.State.BaStatus = BaStatus.Cancelled;
        _model.Log.Write("User cancelled");
      }
      else if (ErrorHelper.HResultIsFailure(e.Status))
      {
        _model.State.BaStatus = BaStatus.Failed;
        var msg = $"Apply failed - {ErrorHelper.HResultToMessage(e.Status)}";
        if (string.IsNullOrEmpty(_model.State.ErrorMessage))
          _model.State.ErrorMessage = msg;

        _model.Log.Write(msg);

        if (e.Restart == ApplyRestart.RestartRequired && _model.UiFacade.IsUiShown)
          _model.UiFacade.ShowMessageBox(Constants.RebootMessage, MessageBoxButton.OK, MessageBoxImage.Stop, MessageBoxResult.OK);
      }
      else
        _model.State.BaStatus = BaStatus.Applied;
    }
    catch (Exception ex)
    {
      _model.Log.Write(ex);
      throw new PhaseException(ex);
    }
    finally
    {
      if (_model.State.Display == Display.Full)
        ApplyPhaseComplete?.Invoke(this, EventArgs.Empty);
      else
        _model.UiFacade.ShutDown();
    }
  }

  /// <summary>
  ///   Fired when the engine has encountered an error.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  /// <exception cref="PhaseException"></exception>
  public virtual void OnError(object sender, ErrorEventArgs e)
  {
    try
    {
      if (e.ErrorCode == ErrorHelper.CancelCode)
        _model.State.BaStatus = BaStatus.Cancelled;
      else
      {
        _model.State.ErrorMessage = e.ErrorMessage;
        _model.Log.Write("Error encountered");
        _model.Log.Write(e.ErrorMessage, true);
        _model.Log.Write($"Type: {e.ErrorType}", true);
        _model.Log.Write($"Code: {e.ErrorCode}", true);
        if (!string.IsNullOrWhiteSpace(e.PackageId))
          _model.Log.Write($"Package: {e.PackageId}", true);

        var data = e.Data?.Where(d => !string.IsNullOrWhiteSpace(d)).ToArray() ?? Array.Empty<string>();
        if (data.Length > 0)
        {
          _model.Log.Write("Data:", true);
          foreach (var d in data)
            _model.Log.Write($"    {d}", true);
        }

        if (_model.UiFacade.IsUiShown)
        {
          // Show an error dialog.
          var button = MessageBoxButton.OK;
          var buttonHint = e.UIHint & 0xF;

          if (buttonHint >= 0 && buttonHint <= 4)
            button = (MessageBoxButton)buttonHint;

          var response = _model.UiFacade.ShowMessageBox(e.ErrorMessage, button, MessageBoxImage.Error, MessageBoxResult.None);

          if (buttonHint == (int)button)
          {
            // If WiX supplied a hint, return the result
            e.Result = (Result)response;
            _model.Log.Write($"User response: {response}");
          }
        }
      }
    }
    catch (Exception ex)
    {
      _model.Log.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <summary>
  ///   Fired when the plan determined that nothing should happen to prevent downgrading.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnApplyDowngrade(object sender, ApplyDowngradeEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has begun installing packages.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnExecuteBegin(object sender, ExecuteBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed installing packages.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnExecuteComplete(object sender, ExecuteCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired by the engine while executing a package.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnExecuteProgress(object sender, ExecuteProgressEventArgs e)
  { }

  ///// <summary>
  /////   Fired when the engine has begun to set up the update package.
  ///// </summary>
  ///// <param name="sender"></param>
  ///// <param name="e"></param>
  //public virtual void OnSetUpdateBegin(object sender, SetUpdateBeginEventArgs e)
  //{ }

  ///// <summary>
  /////   Fired when the engine has completed setting up the update package.
  ///// </summary>
  ///// <param name="sender"></param>
  ///// <param name="e"></param>
  ///// <exception cref="PhaseException"></exception>
  //public virtual void OnSetUpdateComplete(object sender, SetUpdateCompleteEventArgs e)
  //{ }

  /// <summary>
  ///   Fired when the engine executes one or more patches targeting a product.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnExecutePatchTarget(object sender, ExecutePatchTargetEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine is about to begin an MSI transaction.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnBeginMsiTransactionBegin(object sender, BeginMsiTransactionBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed beginning an MSI transaction.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnBeginMsiTransactionComplete(object sender, BeginMsiTransactionCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine is about to commit an MSI transaction.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCommitMsiTransactionBegin(object sender, CommitMsiTransactionBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed comitting an MSI transaction.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCommitMsiTransactionComplete(object sender, CommitMsiTransactionCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine is about to rollback an MSI transaction.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnRollbackMsiTransactionBegin(object sender, RollbackMsiTransactionBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed rolling back an MSI transaction.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnRollbackMsiTransactionComplete(object sender, RollbackMsiTransactionCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has begun acquiring the payload or container.
  ///   The BA can change the source using
  ///   <see cref="M:WixToolset.Mba.Core.IEngine.SetLocalSource(System.String,System.String,System.String)" />
  ///   or
  ///   <see
  ///     cref="M:WixToolset.Mba.Core.IEngine.SetDownloadSource(System.String,System.String,System.String,System.String,System.String)" />
  ///   .
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCacheAcquireBegin(object sender, CacheAcquireBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed the acquisition of the payload or container.
  ///   The BA can change the source using
  ///   <see cref="M:WixToolset.Mba.Core.IEngine.SetLocalSource(System.String,System.String,System.String)" />
  ///   or
  ///   <see
  ///     cref="M:WixToolset.Mba.Core.IEngine.SetDownloadSource(System.String,System.String,System.String,System.String,System.String)" />
  ///   .
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCacheAcquireComplete(object sender, CacheAcquireCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired by the engine to allow the BA to override the acquisition action.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCacheAcquireResolving(object sender, CacheAcquireResolvingEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has progress acquiring the payload or container.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCacheAcquireProgress(object sender, CacheAcquireProgressEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has begun caching the installation sources.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCacheBegin(object sender, CacheBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired after the engine has cached the installation sources.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCacheComplete(object sender, CacheCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine begins the verification of the payload or container that was already in the package cache.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCacheContainerOrPayloadVerifyBegin(object sender, CacheContainerOrPayloadVerifyBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed the verification of the payload or container that was already in the package
  ///   cache.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCacheContainerOrPayloadVerifyComplete(object sender, CacheContainerOrPayloadVerifyCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has progress verifying the payload or container that was already in the package cache.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCacheContainerOrPayloadVerifyProgress(object sender, CacheContainerOrPayloadVerifyProgressEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has begun caching a specific package.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCachePackageBegin(object sender, CachePackageBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed caching a specific package.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCachePackageComplete(object sender, CachePackageCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine failed validating a package in the package cache that is non-vital to execution.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCachePackageNonVitalValidationFailure(object sender, CachePackageNonVitalValidationFailureEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine begins the extraction of the payload from the container.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCachePayloadExtractBegin(object sender, CachePayloadExtractBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed the extraction of the payload from the container.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCachePayloadExtractComplete(object sender, CachePayloadExtractCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has progress extracting the payload from the container.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCachePayloadExtractProgress(object sender, CachePayloadExtractProgressEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine begins the verification of the acquired payload or container.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCacheVerifyBegin(object sender, CacheVerifyBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed the verification of the acquired payload or container.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCacheVerifyComplete(object sender, CacheVerifyCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has progress verifying the payload or container.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnCacheVerifyProgress(object sender, CacheVerifyProgressEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has begun installing a specific package.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnExecutePackageBegin(object sender, ExecutePackageBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed installing a specific package.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnExecutePackageComplete(object sender, ExecutePackageCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine is about to pause Windows automatic updates.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnPauseAutomaticUpdatesBegin(object sender, PauseAutomaticUpdatesBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed pausing Windows automatic updates.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnPauseAutomaticUpdatesComplete(object sender, PauseAutomaticUpdatesCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine is about to take a system restore point.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnSystemRestorePointBegin(object sender, SystemRestorePointBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed taking a system restore point.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnSystemRestorePointComplete(object sender, SystemRestorePointCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine is about to start the elevated process.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnElevateBegin(object sender, ElevateBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed starting the elevated process.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnElevateComplete(object sender, ElevateCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine is about to launch the preapproved executable.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnLaunchApprovedExeBegin(object sender, LaunchApprovedExeBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed launching the preapproved executable.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnLaunchApprovedExeComplete(object sender, LaunchApprovedExeCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has begun registering the location and visibility of the bundle.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnRegisterBegin(object sender, RegisterBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed registering the location and visibility of the bundle.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnRegisterComplete(object sender, RegisterCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine unregisters the bundle.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnUnregisterBegin(object sender, UnregisterBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine unregistration is complete.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnUnregisterComplete(object sender, UnregisterCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has changed progress for the bundle installation.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnProgress(object sender, ProgressEventArgs e)
  { }

  /// <summary>
  ///   Fired when Windows Installer sends an installation message.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnExecuteMsiMessage(object sender, ExecuteMsiMessageEventArgs e)
  { }

  /// <summary>
  ///   Fired when a package that spawned a process is cancelled.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnExecuteProcessCancel(object sender, ExecuteProcessCancelEventArgs e)
  { }

  /// <summary>
  ///   Fired when a package sends a files in use installation message.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnExecuteFilesInUse(object sender, ExecuteFilesInUseEventArgs e)
  { }
}