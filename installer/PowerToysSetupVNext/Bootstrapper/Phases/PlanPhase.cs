using Bootstrapper.Models;
using Bootstrapper.Models.Util;
using System;
using WixToolset.BootstrapperApplicationApi;

namespace Bootstrapper.Phases;

internal class PlanPhase
{
  private readonly Model _model;

  public PlanPhase(Model model)
  {
    _model = model;
  }

  public event EventHandler<EventArgs> PlanPhaseFailed;

  /// <summary>
  ///   Fired when the engine has begun planning the installation.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  /// <exception cref="PhaseException"></exception>
  public virtual void OnPlanBegin(object sender, PlanBeginEventArgs e)
  {
    try
    {
      _model.State.PhaseResult = 0;
      _model.State.ErrorMessage = string.Empty;

      if (e.Cancel)
        return;

      _model.State.BaStatus = BaStatus.Planning;
    }
    catch (Exception ex)
    {
      _model.Log.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <summary>
  ///   Fired when the engine has completed planning the installation.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  /// <exception cref="PhaseException"></exception>
  public virtual void OnPlanComplete(object sender, PlanCompleteEventArgs e)
  {
    try
    {
      _model.State.PhaseResult = e.Status;

      if (_model.State.BaStatus == BaStatus.Cancelled || e.Status == ErrorHelper.CancelHResult)
      {
        _model.State.BaStatus = BaStatus.Cancelled;
        _model.Log.Write("User cancelled");
      }
      else if (ErrorHelper.HResultIsFailure(e.Status))
      {
        _model.State.BaStatus = BaStatus.Failed;
        var msg = $"Plan failed - {ErrorHelper.HResultToMessage(e.Status)}";
        if (string.IsNullOrEmpty(_model.State.ErrorMessage))
          _model.State.ErrorMessage = msg;

        _model.Log.Write(msg);

        if (_model.UiFacade.IsUiShown)
          PlanPhaseFailed?.Invoke(this, EventArgs.Empty);
        else
          _model.UiFacade.ShutDown();
      }
      else
      {
        _model.Log.Write("Plan succeeded, starting apply phase");
        _model.Engine.Apply(_model.UiFacade.ShellWindowHandle);
      }
    }
    catch (Exception ex)
    {
      _model.Log.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <summary>
  ///   Fired when the engine has begun getting the BA's input for planning a package.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnPlanPackageBegin(object sender, PlanPackageBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed getting the BA's input for planning a package.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnPlanPackageComplete(object sender, PlanPackageCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine plans a new, compatible package using the same provider key.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnPlanCompatibleMsiPackageBegin(object sender, PlanCompatibleMsiPackageBeginEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed planning the installation of a specific package.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnPlanCompatibleMsiPackageComplete(object sender, PlanCompatibleMsiPackageCompleteEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine is planning a rollback boundary.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnPlanRollbackBoundary(object sender, PlanRollbackBoundaryEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has begun planning for a related bundle.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnPlanRelatedBundle(object sender, PlanRelatedBundleEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has begun planning the related bundle relation type.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnPlanRelatedBundleType(object sender, PlanRelatedBundleTypeEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has begun planning an upgrade related bundle for restoring in case of failure.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnPlanRestoreRelatedBundle(object sender, PlanRestoreRelatedBundleEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine is about to plan a forward compatible bundle.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnPlanForwardCompatibleBundle(object sender, PlanForwardCompatibleBundleEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine is planning an MSI or MSP package.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnPlanMsiPackage(object sender, PlanMsiPackageEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine is about to plan a feature in an MSI package.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnPlanMsiFeature(object sender, PlanMsiFeatureEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine is about to plan a target of an MSP package.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnPlanPatchTarget(object sender, PlanPatchTargetEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed planning a package.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnPlannedPackage(object sender, PlannedPackageEventArgs e)
  { }

  /// <summary>
  ///   Fired when the engine has completed planning a compatible package.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  public virtual void OnPlannedCompatiblePackage(object sender, PlannedCompatiblePackageEventArgs e)
  { }
}