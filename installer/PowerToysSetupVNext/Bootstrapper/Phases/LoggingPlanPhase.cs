using Bootstrapper.Models;
using Bootstrapper.Models.Util;
using System;
using WixToolset.BootstrapperApplicationApi;

namespace Bootstrapper.Phases;

internal class LoggingPlanPhase : PlanPhase
{
  private readonly Log _logger;

  public LoggingPlanPhase(Model model)
    : base(model)
  {
    _logger = model.Log;
  }

  /// <inheritdoc />
  public override void OnPlanBegin(object sender, PlanBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanBegin)} -------v");
      _logger.Write($"{nameof(e.PackageCount)} = {e.PackageCount}", true);

      base.OnPlanBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanBegin)} -------^");
    }
    catch (PhaseException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <inheritdoc />
  public override void OnPlanComplete(object sender, PlanCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);

      base.OnPlanComplete(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanComplete)} -------^");
    }
    catch (PhaseException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <inheritdoc />
  public override void OnPlanPackageBegin(object sender, PlanPackageBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanPackageBegin)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.CurrentState)} = {e.CurrentState}", true);
      _logger.Write($"{nameof(e.RecommendedState)} = {e.RecommendedState}", true);
      _logger.Write($"{nameof(e.RepairCondition)} = {e.RepairCondition}", true);
      _logger.Write($"{nameof(e.Cached)} = {e.Cached}", true);
      _logger.Write($"{nameof(e.RecommendedCacheType)} = {e.RecommendedCacheType}", true);
      _logger.Write($"{nameof(e.InstallCondition)} = {e.InstallCondition}", true);

      base.OnPlanPackageBegin(sender, e);

      _logger.Write($"{nameof(e.State)} = {e.State}", true);
      _logger.Write($"{nameof(e.CacheType)} = {e.CacheType}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanPackageBegin)} -------^");
    }
    catch (PhaseException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <inheritdoc />
  public override void OnPlanPackageComplete(object sender, PlanPackageCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanPackageComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.Requested)} = {e.Requested}", true);

      base.OnPlanPackageComplete(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanPackageComplete)} -------^");
    }
    catch (PhaseException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <inheritdoc />
  public override void OnPlanCompatibleMsiPackageBegin(object sender, PlanCompatibleMsiPackageBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanCompatibleMsiPackageBegin)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.CompatiblePackageId)} = {e.CompatiblePackageId}", true);
      _logger.Write($"{nameof(e.CompatiblePackageVersion)} = {e.CompatiblePackageVersion}", true);
      _logger.Write($"{nameof(e.RecommendedRemove)} = {e.RecommendedRemove}", true);

      base.OnPlanCompatibleMsiPackageBegin(sender, e);

      _logger.Write($"{nameof(e.RequestRemove)} = {e.RequestRemove}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanCompatibleMsiPackageBegin)} -------^");
    }
    catch (PhaseException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <inheritdoc />
  public override void OnPlanCompatibleMsiPackageComplete(object sender, PlanCompatibleMsiPackageCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanCompatibleMsiPackageComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.CompatiblePackageId)} = {e.CompatiblePackageId}", true);
      _logger.Write($"{nameof(e.RequestedRemove)} = {e.RequestedRemove}", true);

      base.OnPlanCompatibleMsiPackageComplete(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanCompatibleMsiPackageComplete)} -------^");
    }
    catch (PhaseException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <inheritdoc />
  public override void OnPlanRollbackBoundary(object sender, PlanRollbackBoundaryEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanRollbackBoundary)} -------v");
      _logger.Write($"{nameof(e.RollbackBoundaryId)} = {e.RollbackBoundaryId}", true);
      _logger.Write($"{nameof(e.RecommendedTransaction)} = {e.RecommendedTransaction}", true);

      base.OnPlanRollbackBoundary(sender, e);

      _logger.Write($"{nameof(e.Transaction)} = {e.Transaction}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanRollbackBoundary)} -------^");
    }
    catch (PhaseException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <inheritdoc />
  public override void OnPlanRelatedBundle(object sender, PlanRelatedBundleEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanRelatedBundle)} -------v");
      _logger.Write($"{nameof(e.BundleId)} = {e.BundleId}", true);
      _logger.Write($"{nameof(e.RecommendedState)} = {e.RecommendedState}", true);

      base.OnPlanRelatedBundle(sender, e);

      _logger.Write($"{nameof(e.State)} = {e.State}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanRelatedBundle)} -------^");
    }
    catch (PhaseException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <inheritdoc />
  public override void OnPlanRelatedBundleType(object sender, PlanRelatedBundleTypeEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanRelatedBundleType)} -------v");
      _logger.Write($"{nameof(e.BundleId)} = {e.BundleId}", true);
      _logger.Write($"{nameof(e.RecommendedType)} = {e.RecommendedType}", true);

      base.OnPlanRelatedBundleType(sender, e);

      _logger.Write($"{nameof(e.Type)} = {e.Type}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanRelatedBundleType)} -------^");
    }
    catch (PhaseException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <inheritdoc />
  public override void OnPlanRestoreRelatedBundle(object sender, PlanRestoreRelatedBundleEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanRestoreRelatedBundle)} -------v");
      _logger.Write($"{nameof(e.BundleId)} = {e.BundleId}", true);
      _logger.Write($"{nameof(e.RecommendedState)} = {e.RecommendedState}", true);

      base.OnPlanRestoreRelatedBundle(sender, e);

      _logger.Write($"{nameof(e.State)} = {e.State}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanRestoreRelatedBundle)} -------^");
    }
    catch (PhaseException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <inheritdoc />
  public override void OnPlanForwardCompatibleBundle(object sender, PlanForwardCompatibleBundleEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanForwardCompatibleBundle)} -------v");
      _logger.Write($"{nameof(e.BundleId)} = {e.BundleId}", true);
      _logger.Write($"{nameof(e.Version)} = {e.Version}", true);
      _logger.Write($"{nameof(e.PerMachine)} = {e.PerMachine}", true);
      _logger.Write($"{nameof(e.RelationType)} = {e.RelationType}", true);
      _logger.Write($"{nameof(e.BundleTag)} = {e.BundleTag}", true);
      _logger.Write($"{nameof(e.RecommendedIgnoreBundle)} = {e.RecommendedIgnoreBundle}", true);

      base.OnPlanForwardCompatibleBundle(sender, e);

      _logger.Write($"{nameof(e.IgnoreBundle)} = {e.IgnoreBundle}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanForwardCompatibleBundle)} -------^");
    }
    catch (PhaseException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <inheritdoc />
  public override void OnPlanMsiPackage(object sender, PlanMsiPackageEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanMsiPackage)} -------v");
      _logger.Write($"{nameof(e.Action)} = {e.Action}", true);
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.RecommendedFileVersioning)} = {e.RecommendedFileVersioning}", true);
      _logger.Write($"{nameof(e.ShouldExecute)} = {e.ShouldExecute}", true);

      base.OnPlanMsiPackage(sender, e);

      _logger.Write($"{nameof(e.ActionMsiProperty)} = {e.ActionMsiProperty}", true);
      _logger.Write($"{nameof(e.FileVersioning)} = {e.FileVersioning}", true);
      _logger.Write($"{nameof(e.DisableExternalUiHandler)} = {e.DisableExternalUiHandler}", true);
      _logger.Write($"{nameof(e.UiLevel)} = {e.UiLevel}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanMsiPackage)} -------^");
    }
    catch (PhaseException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <inheritdoc />
  public override void OnPlanMsiFeature(object sender, PlanMsiFeatureEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanMsiFeature)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.FeatureId)} = {e.FeatureId}", true);
      _logger.Write($"{nameof(e.RecommendedState)} = {e.RecommendedState}", true);

      base.OnPlanMsiFeature(sender, e);

      _logger.Write($"{nameof(e.State)} = {e.State}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanMsiFeature)} -------^");
    }
    catch (PhaseException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <inheritdoc />
  public override void OnPlanPatchTarget(object sender, PlanPatchTargetEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanPatchTarget)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.ProductCode)} = {e.ProductCode}", true);
      _logger.Write($"{nameof(e.RecommendedState)} = {e.RecommendedState}", true);

      base.OnPlanPatchTarget(sender, e);

      _logger.Write($"{nameof(e.State)} = {e.State}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlanPatchTarget)} -------^");
    }
    catch (PhaseException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <inheritdoc />
  public override void OnPlannedPackage(object sender, PlannedPackageEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlannedPackage)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.Execute)} = {e.Execute}", true);
      _logger.Write($"{nameof(e.Rollback)} = {e.Rollback}", true);
      _logger.Write($"{nameof(e.Cache)} = {e.Cache}", true);
      _logger.Write($"{nameof(e.Uncache)} = {e.Uncache}", true);

      base.OnPlannedPackage(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlannedPackage)} -------^");
    }
    catch (PhaseException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Write(ex);
      throw new PhaseException(ex);
    }
  }

  /// <inheritdoc />
  public override void OnPlannedCompatiblePackage(object sender, PlannedCompatiblePackageEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlannedCompatiblePackage)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.CompatiblePackageId)} = {e.CompatiblePackageId}", true);
      _logger.Write($"{nameof(e.Remove)} = {e.Remove}", true);

      base.OnPlannedCompatiblePackage(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(PlanPhase)}: {nameof(OnPlannedCompatiblePackage)} -------^");
    }
    catch (PhaseException)
    {
      throw;
    }
    catch (Exception ex)
    {
      _logger.Write(ex);
      throw new PhaseException(ex);
    }
  }
}