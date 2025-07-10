using Bootstrapper.Models;
using Bootstrapper.Models.Util;
using System;
using WixToolset.BootstrapperApplicationApi;

namespace Bootstrapper.Phases;

internal class LoggingDetectPhase : DetectPhase
{
  private readonly Log _logger;

  public LoggingDetectPhase(Model model)
    : base(model)
  {
    _logger = model.Log;
  }

  /// <inheritdoc />
  public override void OnStartup(object sender, StartupEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnStartup)} -------v");

      base.OnStartup(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnStartup)} -------^");
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
  public override void OnShutdown(object sender, ShutdownEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnShutdown)} -------v");

      base.OnShutdown(sender, e);

      _logger.Write($"{nameof(e.Action)} = {e.Action}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnShutdown)} -------^");
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
  public override void OnDetectBegin(object sender, DetectBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectBegin)} -------v");
      _logger.Write($"{nameof(e.PackageCount)} = {e.PackageCount}", true);
      _logger.Write($"{nameof(e.Cached)} = {e.Cached}", true);
      _logger.Write($"{nameof(e.RegistrationType)}  = {e.RegistrationType}", true);

      base.OnDetectBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectBegin)} -------^");
    }
    catch (PhaseException)
    { }
    catch (Exception ex)
    {
      _logger.Write(ex);
      e.HResult = ex.HResult;
    }
  }

  /// <inheritdoc />
  public override void OnDetectComplete(object sender, DetectCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
      _logger.Write($"{nameof(e.EligibleForCleanup)} = {e.EligibleForCleanup}", true);

      base.OnDetectComplete(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectComplete)} -------^");
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
  public override void OnDetectRelatedBundle(object sender, DetectRelatedBundleEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectRelatedBundle)} -------v");
      _logger.Write($"{nameof(e.ProductCode)} = {e.ProductCode}", true);
      _logger.Write($"{nameof(e.Version)} = {e.Version}", true);
      _logger.Write($"{nameof(e.BundleTag)} = {e.BundleTag}", true);
      _logger.Write($"{nameof(e.PerMachine)} = {e.PerMachine}", true);
      _logger.Write($"{nameof(e.RelationType)} = {e.RelationType}", true);
      _logger.Write($"{nameof(e.MissingFromCache)} = {e.MissingFromCache}", true);

      base.OnDetectRelatedBundle(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectRelatedBundle)} -------^");
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
  public override void OnDetectRelatedBundlePackage(object sender, DetectRelatedBundlePackageEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectRelatedBundlePackage)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.ProductCode)} = {e.ProductCode}", true);
      _logger.Write($"{nameof(e.Version)} = {e.Version}", true);
      _logger.Write($"{nameof(e.RelationType)} = {e.RelationType}", true);
      _logger.Write($"{nameof(e.PerMachine)} = {e.PerMachine}", true);

      base.OnDetectRelatedBundlePackage(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectRelatedBundlePackage)} -------^");
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
  public override void OnDetectRelatedMsiPackage(object sender, DetectRelatedMsiPackageEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectRelatedMsiPackage)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.UpgradeCode)} = {e.UpgradeCode}", true);
      _logger.Write($"{nameof(e.ProductCode)} = {e.ProductCode}", true);
      _logger.Write($"{nameof(e.Version)} = {e.Version}", true);
      _logger.Write($"{nameof(e.PerMachine)} = {e.PerMachine}", true);
      _logger.Write($"{nameof(e.Operation)} = {e.Operation}", true);

      base.OnDetectRelatedMsiPackage(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectRelatedMsiPackage)} -------^");
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
  public override void OnDetectUpdate(object sender, DetectUpdateEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectUpdate)} -------v");
      _logger.Write($"{nameof(e.Title)} = {e.Title}", true);
      _logger.Write($"{nameof(e.Summary)} = {e.Summary}", true);
      _logger.Write($"{nameof(e.Version)} = {e.Version}", true);
      _logger.Write($"{nameof(e.UpdateLocation)} = {e.UpdateLocation}", true);

      base.OnDetectUpdate(sender, e);

      _logger.Write($"{nameof(e.StopProcessingUpdates)} = {e.StopProcessingUpdates}");
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectUpdate)} -------^");
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
  public override void OnDetectUpdateBegin(object sender, DetectUpdateBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectUpdateBegin)} -------v");
      _logger.Write($"{nameof(e.UpdateLocation)} = {e.UpdateLocation}", true);

      base.OnDetectUpdateBegin(sender, e);

      _logger.Write($"{nameof(e.Skip)} = {e.Skip}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectUpdateBegin)} -------^");
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
  public override void OnDetectUpdateComplete(object sender, DetectUpdateCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectUpdateComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);

      base.OnDetectUpdateComplete(sender, e);

      _logger.Write($"{nameof(e.IgnoreError)} = {e.IgnoreError}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectUpdateComplete)} -------^");
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
  public override void OnDetectForwardCompatibleBundle(object sender, DetectForwardCompatibleBundleEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectForwardCompatibleBundle)} -------v");
      _logger.Write($"{nameof(e.BundleId)} = {e.BundleId}", true);
      _logger.Write($"{nameof(e.Version)} = {e.Version}", true);
      _logger.Write($"{nameof(e.BundleTag)} = {e.BundleTag}", true);
      _logger.Write($"{nameof(e.PerMachine)} = {e.PerMachine}", true);
      _logger.Write($"{nameof(e.RelationType)} = {e.RelationType}", true);

      base.OnDetectForwardCompatibleBundle(sender, e);

      _logger.Write($"{nameof(e.MissingFromCache)} = {e.MissingFromCache}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectForwardCompatibleBundle)} -------^");
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
  public override void OnDetectPackageBegin(object sender, DetectPackageBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectPackageBegin)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);

      base.OnDetectPackageBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectPackageBegin)} -------^");
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
  public override void OnDetectPackageComplete(object sender, DetectPackageCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectPackageComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.State)} = {e.State}", true);
      _logger.Write($"{nameof(e.Cached)} = {e.Cached}", true);

      base.OnDetectPackageComplete(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectPackageComplete)} -------^");
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
  public override void OnDetectPatchTarget(object sender, DetectPatchTargetEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectPatchTarget)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.ProductCode)} = {e.ProductCode}", true);
      _logger.Write($"{nameof(e.State)} = {e.State}", true);

      base.OnDetectPatchTarget(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectPatchTarget)} -------^");
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
  public override void OnDetectCompatibleMsiPackage(object sender, DetectCompatibleMsiPackageEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectCompatibleMsiPackage)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.CompatiblePackageId)} = {e.CompatiblePackageId}", true);
      _logger.Write($"{nameof(e.CompatiblePackageVersion)} = {e.CompatiblePackageVersion}", true);

      base.OnDetectCompatibleMsiPackage(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectCompatibleMsiPackage)} -------^");
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
  public override void OnDetectMsiFeature(object sender, DetectMsiFeatureEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectMsiFeature)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.FeatureId)} = {e.FeatureId}", true);
      _logger.Write($"{nameof(e.State)} = {e.State}", true);

      base.OnDetectMsiFeature(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(DetectPhase)}: {nameof(OnDetectMsiFeature)} -------^");
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