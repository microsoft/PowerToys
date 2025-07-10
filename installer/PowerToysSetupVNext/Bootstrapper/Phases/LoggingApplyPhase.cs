using Bootstrapper.Models;
using Bootstrapper.Models.Util;
using System;
using System.Linq;
using WixToolset.BootstrapperApplicationApi;

namespace Bootstrapper.Phases;

internal class LoggingApplyPhase : ApplyPhase
{
  private readonly Log _logger;

  public LoggingApplyPhase(Model model)
    : base(model)
  {
    _logger = model.Log;
  }

  /// <inheritdoc />
  public override void OnApplyBegin(object sender, ApplyBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnApplyBegin)} -------v");
      _logger.Write($"{nameof(e.PhaseCount)} = {e.PhaseCount}", true);

      base.OnApplyBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnApplyBegin)} -------^");
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
  public override void OnApplyComplete(object sender, ApplyCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnApplyComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
      _logger.Write($"{nameof(e.Restart)} = {e.Restart}", true);
      _logger.Write($"{nameof(e.Recommendation)} = {e.Recommendation}", true);

      base.OnApplyComplete(sender, e);

      _logger.Write($"{nameof(e.Action)} = {e.Action}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnApplyComplete)} -------^");
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
  public override void OnApplyDowngrade(object sender, ApplyDowngradeEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnApplyDowngrade)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
      _logger.Write($"{nameof(e.Recommendation)} = {e.Recommendation}", true);

      base.OnApplyDowngrade(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnApplyDowngrade)} -------^");
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
  public override void OnExecuteBegin(object sender, ExecuteBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecuteBegin)} -------v");
      _logger.Write($"{nameof(e.PackageCount)} = {e.PackageCount}", true);

      base.OnExecuteBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecuteBegin)} -------^");
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
  public override void OnExecuteComplete(object sender, ExecuteCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecuteComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);

      base.OnExecuteComplete(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecuteComplete)} -------^");
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
  public override void OnExecuteProgress(object sender, ExecuteProgressEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecuteProgress)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.OverallPercentage)} = {e.OverallPercentage}", true);
      _logger.Write($"{nameof(e.ProgressPercentage)} = {e.ProgressPercentage}", true);

      base.OnExecuteProgress(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecuteProgress)} -------^");
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

  ///// <inheritdoc />
  //public override void OnSetUpdateBegin(object sender, SetUpdateBeginEventArgs e)
  //{
  //  try
  //  {
  //    _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnSetUpdateBegin)} -------v");

  //    base.OnSetUpdateBegin(sender, e);

  //    _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
  //    _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnSetUpdateBegin)} -------^");
  //  }
  //  catch (PhaseException)
  //  {
  //    throw;
  //  }
  //  catch (Exception ex)
  //  {
  //    _logger.Write(ex);
  //    throw new PhaseException(ex);
  //  }
  //}

  ///// <inheritdoc />
  //public override void OnSetUpdateComplete(object sender, SetUpdateCompleteEventArgs e)
  //{
  //  try
  //  {
  //    _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnSetUpdateComplete)} -------v");
  //    _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
  //    _logger.Write($"{nameof(e.PreviousPackageId)} = {e.PreviousPackageId}", true);
  //    _logger.Write($"{nameof(e.NewPackageId)} = {e.NewPackageId}", true);

  //    base.OnSetUpdateComplete(sender, e);

  //    _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
  //    _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnSetUpdateComplete)} -------^");
  //  }
  //  catch (PhaseException)
  //  {
  //    throw;
  //  }
  //  catch (Exception ex)
  //  {
  //    _logger.Write(ex);
  //    throw new PhaseException(ex);
  //  }
  //}

  /// <inheritdoc />
  public override void OnExecutePatchTarget(object sender, ExecutePatchTargetEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecutePatchTarget)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.TargetProductCode)} = {e.TargetProductCode}", true);

      base.OnExecutePatchTarget(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecutePatchTarget)} -------^");
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
  public override void OnBeginMsiTransactionBegin(object sender, BeginMsiTransactionBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnBeginMsiTransactionBegin)} -------v");
      _logger.Write($"{nameof(e.TransactionId)} = {e.TransactionId}", true);

      base.OnBeginMsiTransactionBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnBeginMsiTransactionBegin)} -------^");
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
  public override void OnBeginMsiTransactionComplete(object sender, BeginMsiTransactionCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnBeginMsiTransactionComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
      _logger.Write($"{nameof(e.TransactionId)} = {e.TransactionId}", true);

      base.OnBeginMsiTransactionComplete(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnBeginMsiTransactionComplete)} -------^");
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
  public override void OnCommitMsiTransactionBegin(object sender, CommitMsiTransactionBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCommitMsiTransactionBegin)} -------v");
      _logger.Write($"{nameof(e.TransactionId)} = {e.TransactionId}", true);

      base.OnCommitMsiTransactionBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCommitMsiTransactionBegin)} -------^");
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
  public override void OnCommitMsiTransactionComplete(object sender, CommitMsiTransactionCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCommitMsiTransactionComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
      _logger.Write($"{nameof(e.TransactionId)} = {e.TransactionId}", true);
      _logger.Write($"{nameof(e.Recommendation)} = {e.Recommendation}", true);

      base.OnCommitMsiTransactionComplete(sender, e);

      _logger.Write($"{nameof(e.Action)} = {e.Action}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCommitMsiTransactionComplete)} -------^");
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
  public override void OnRollbackMsiTransactionBegin(object sender, RollbackMsiTransactionBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnRollbackMsiTransactionBegin)} -------v");
      _logger.Write($"{nameof(e.TransactionId)} = {e.TransactionId}", true);

      base.OnRollbackMsiTransactionBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnRollbackMsiTransactionBegin)} -------^");
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
  public override void OnRollbackMsiTransactionComplete(object sender, RollbackMsiTransactionCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnRollbackMsiTransactionComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
      _logger.Write($"{nameof(e.TransactionId)} = {e.TransactionId}", true);
      _logger.Write($"{nameof(e.Restart)} = {e.Restart}", true);
      _logger.Write($"{nameof(e.Recommendation)} = {e.Recommendation}", true);

      base.OnRollbackMsiTransactionComplete(sender, e);

      _logger.Write($"{nameof(e.Action)} = {e.Action}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnRollbackMsiTransactionComplete)} -------^");
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
  public override void OnCacheAcquireBegin(object sender, CacheAcquireBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheAcquireBegin)} -------v");
      _logger.Write($"{nameof(e.PayloadId)} = {e.PayloadId}", true);
      _logger.Write($"{nameof(e.PayloadContainerId)} = {e.PayloadContainerId}", true);
      _logger.Write($"{nameof(e.PackageOrContainerId)} = {e.PackageOrContainerId}", true);
      _logger.Write($"{nameof(e.DownloadUrl)} = {e.DownloadUrl}", true);
      _logger.Write($"{nameof(e.Source)} = {e.Source}", true);
      _logger.Write($"{nameof(e.Recommendation)} = {e.Recommendation}", true);

      base.OnCacheAcquireBegin(sender, e);

      _logger.Write($"{nameof(e.Action)} = {e.Action}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheAcquireBegin)} -------^");
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
  public override void OnCacheAcquireComplete(object sender, CacheAcquireCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheAcquireComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
      _logger.Write($"{nameof(e.PayloadId)} = {e.PayloadId}", true);
      _logger.Write($"{nameof(e.PackageOrContainerId)} = {e.PackageOrContainerId}", true);
      _logger.Write($"{nameof(e.Recommendation)} = {e.Recommendation}", true);

      base.OnCacheAcquireComplete(sender, e);

      _logger.Write($"{nameof(e.Action)} = {e.Action}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheAcquireComplete)} -------^");
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
  public override void OnCacheAcquireResolving(object sender, CacheAcquireResolvingEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheAcquireResolving)} -------v");
      _logger.Write($"{nameof(e.PayloadId)} = {e.PayloadId}", true);
      _logger.Write($"{nameof(e.PayloadContainerId)} = {e.PayloadContainerId}", true);
      _logger.Write($"{nameof(e.PackageOrContainerId)} = {e.PackageOrContainerId}", true);
      _logger.Write($"{nameof(e.DownloadUrl)} = {e.DownloadUrl}", true);
      _logger.Write($"{nameof(e.FoundLocal)} = {e.FoundLocal}", true);
      _logger.Write($"{nameof(e.RecommendedSearchPath)} = {e.RecommendedSearchPath}", true);
      _logger.Write($"{nameof(e.SearchPaths)} (count = {e.SearchPaths?.Length ?? 0})", true);
      if (e.SearchPaths != null)
      {
        for (var i = 0; i < e.SearchPaths.Length; i++)
          _logger.Write($"    {i} = {e.SearchPaths[i]}", true);
      }

      _logger.Write($"{nameof(e.ChosenSearchPath)} = {e.ChosenSearchPath}", true);
      _logger.Write($"{nameof(e.Action)} = {e.Action}", true);
      _logger.Write($"{nameof(e.Recommendation)} = {e.Recommendation}", true);

      base.OnCacheAcquireResolving(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheAcquireResolving)} -------^");
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
  public override void OnCacheAcquireProgress(object sender, CacheAcquireProgressEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheAcquireProgress)} -------v");
      LogCacheProgress(e);

      base.OnCacheAcquireProgress(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheAcquireProgress)} -------^");
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
  public override void OnCacheBegin(object sender, CacheBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheBegin)} -------v");

      base.OnCacheBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheBegin)} -------^");
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
  public override void OnCacheComplete(object sender, CacheCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);

      base.OnCacheComplete(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheComplete)} -------^");
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
  public override void OnCachePackageBegin(object sender, CachePackageBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCachePackageBegin)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.Vital)} = {e.Vital}", true);
      _logger.Write($"{nameof(e.PackageCacheSize)} = {e.PackageCacheSize}", true);
      _logger.Write($"{nameof(e.CachePayloads)} = {e.CachePayloads}", true);

      base.OnCachePackageBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCachePackageBegin)} -------^");
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
  public override void OnCachePackageComplete(object sender, CachePackageCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCachePackageComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.Recommendation)} = {e.Recommendation}", true);

      base.OnCachePackageComplete(sender, e);

      _logger.Write($"{nameof(e.Action)} = {e.Action}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCachePackageComplete)} -------^");
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
  public override void OnCachePackageNonVitalValidationFailure(object sender, CachePackageNonVitalValidationFailureEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCachePackageNonVitalValidationFailure)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.Recommendation)} = {e.Recommendation}", true);

      base.OnCachePackageNonVitalValidationFailure(sender, e);

      _logger.Write($"{nameof(e.Action)} = {e.Action}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCachePackageNonVitalValidationFailure)} -------^");
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
  public override void OnCacheContainerOrPayloadVerifyBegin(object sender, CacheContainerOrPayloadVerifyBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheContainerOrPayloadVerifyBegin)} -------v");
      _logger.Write($"{nameof(e.PayloadId)} = {e.PayloadId}", true);
      _logger.Write($"{nameof(e.PackageOrContainerId)} = {e.PackageOrContainerId}", true);

      base.OnCacheContainerOrPayloadVerifyBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheContainerOrPayloadVerifyBegin)} -------^");
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
  public override void OnCacheContainerOrPayloadVerifyComplete(object sender, CacheContainerOrPayloadVerifyCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheContainerOrPayloadVerifyComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
      _logger.Write($"{nameof(e.PayloadId)} = {e.PayloadId}", true);
      _logger.Write($"{nameof(e.PackageOrContainerId)} = {e.PackageOrContainerId}", true);

      base.OnCacheContainerOrPayloadVerifyComplete(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheContainerOrPayloadVerifyComplete)} -------^");
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
  public override void OnCachePayloadExtractBegin(object sender, CachePayloadExtractBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCachePayloadExtractBegin)} -------v");
      _logger.Write($"{nameof(e.ContainerId)} = {e.ContainerId}", true);
      _logger.Write($"{nameof(e.PayloadId)} = {e.PayloadId}", true);

      base.OnCachePayloadExtractBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCachePayloadExtractBegin)} -------^");
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
  public override void OnCachePayloadExtractComplete(object sender, CachePayloadExtractCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCachePayloadExtractComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
      _logger.Write($"{nameof(e.ContainerId)} = {e.ContainerId}", true);
      _logger.Write($"{nameof(e.PayloadId)} = {e.PayloadId}", true);

      base.OnCachePayloadExtractComplete(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCachePayloadExtractComplete)} -------^");
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
  public override void OnCachePayloadExtractProgress(object sender, CachePayloadExtractProgressEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCachePayloadExtractProgress)} -------v");
      LogCacheProgress(e);

      base.OnCachePayloadExtractProgress(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCachePayloadExtractProgress)} -------^");
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
  public override void OnCacheVerifyBegin(object sender, CacheVerifyBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheVerifyBegin)} -------v");
      _logger.Write($"{nameof(e.PayloadId)} = {e.PayloadId}", true);
      _logger.Write($"{nameof(e.PackageOrContainerId)} = {e.PackageOrContainerId}", true);

      base.OnCacheVerifyBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheVerifyBegin)} -------^");
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
  public override void OnCacheVerifyComplete(object sender, CacheVerifyCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheVerifyComplete)} -------v");
      _logger.Write($"{nameof(e.PayloadId)} = {e.PayloadId}", true);
      _logger.Write($"{nameof(e.PackageOrContainerId)} = {e.PackageOrContainerId}", true);
      _logger.Write($"{nameof(e.Recommendation)} = {e.Recommendation}", true);

      base.OnCacheVerifyComplete(sender, e);

      _logger.Write($"{nameof(e.Action)} = {e.Action}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheVerifyComplete)} -------^");
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
  public override void OnCacheVerifyProgress(object sender, CacheVerifyProgressEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheVerifyProgress)} -------v");
      _logger.Write($"{nameof(e.Step)} = {e.Step}", true);
      LogCacheProgress(e);

      base.OnCacheVerifyProgress(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheVerifyProgress)} -------^");
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
  public override void OnCacheContainerOrPayloadVerifyProgress(object sender, CacheContainerOrPayloadVerifyProgressEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheContainerOrPayloadVerifyProgress)} -------v");
      LogCacheProgress(e);

      base.OnCacheContainerOrPayloadVerifyProgress(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnCacheContainerOrPayloadVerifyProgress)} -------^");
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
  public override void OnExecutePackageBegin(object sender, ExecutePackageBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecutePackageBegin)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.Action)} = {e.Action}", true);
      _logger.Write($"{nameof(e.UiLevel)} = {e.UiLevel}", true);
      _logger.Write($"{nameof(e.DisableExternalUiHandler)} = {e.DisableExternalUiHandler}", true);
      _logger.Write($"{nameof(e.ShouldExecute)} = {e.ShouldExecute}", true);

      base.OnExecutePackageBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecutePackageBegin)} -------^");
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
  public override void OnExecutePackageComplete(object sender, ExecutePackageCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecutePackageComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.Restart)} = {e.Restart}", true);
      _logger.Write($"{nameof(e.Recommendation)} = {e.Recommendation}", true);

      base.OnExecutePackageComplete(sender, e);

      _logger.Write($"{nameof(e.Action)} = {e.Action}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecutePackageComplete)} -------^");
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
  public override void OnPauseAutomaticUpdatesBegin(object sender, PauseAutomaticUpdatesBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnPauseAutomaticUpdatesBegin)} -------v");

      base.OnPauseAutomaticUpdatesBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnPauseAutomaticUpdatesBegin)} -------^");
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
  public override void OnPauseAutomaticUpdatesComplete(object sender, PauseAutomaticUpdatesCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnPauseAutomaticUpdatesComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);

      base.OnPauseAutomaticUpdatesComplete(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnPauseAutomaticUpdatesComplete)} -------^");
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
  public override void OnSystemRestorePointBegin(object sender, SystemRestorePointBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnSystemRestorePointBegin)} -------v");

      base.OnSystemRestorePointBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnSystemRestorePointBegin)} -------^");
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
  public override void OnSystemRestorePointComplete(object sender, SystemRestorePointCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnSystemRestorePointComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);

      base.OnSystemRestorePointComplete(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnSystemRestorePointComplete)} -------^");
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
  public override void OnElevateBegin(object sender, ElevateBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnElevateBegin)} -------v");

      base.OnElevateBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnElevateBegin)} -------^");
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
  public override void OnElevateComplete(object sender, ElevateCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnElevateComplete)} -------v");

      base.OnElevateComplete(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnElevateComplete)} -------^");
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
  public override void OnLaunchApprovedExeBegin(object sender, LaunchApprovedExeBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnLaunchApprovedExeBegin)} -------v");

      base.OnLaunchApprovedExeBegin(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnLaunchApprovedExeBegin)} -------^");
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
  public override void OnLaunchApprovedExeComplete(object sender, LaunchApprovedExeCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnLaunchApprovedExeComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);
      _logger.Write($"{nameof(e.ProcessId)} = {e.ProcessId}", true);

      base.OnLaunchApprovedExeComplete(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnLaunchApprovedExeComplete)} -------^");
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
  public override void OnRegisterBegin(object sender, RegisterBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnRegisterBegin)} -------v");
      _logger.Write($"{nameof(e.RecommendedRegistrationType)} = {e.RecommendedRegistrationType}", true);

      base.OnRegisterBegin(sender, e);

      _logger.Write($"{nameof(e.RegistrationType)} = {e.RegistrationType}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnRegisterBegin)} -------^");
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
  public override void OnRegisterComplete(object sender, RegisterCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnRegisterComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);

      base.OnRegisterComplete(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnRegisterComplete)} -------^");
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
  public override void OnUnregisterBegin(object sender, UnregisterBeginEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnUnregisterBegin)} -------v");
      _logger.Write($"{nameof(e.RecommendedRegistrationType)} = {e.RecommendedRegistrationType}", true);

      base.OnUnregisterBegin(sender, e);

      _logger.Write($"{nameof(e.RegistrationType)} = {e.RegistrationType}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnUnregisterBegin)} -------^");
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
  public override void OnUnregisterComplete(object sender, UnregisterCompleteEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnUnregisterComplete)} -------v");
      _logger.Write($"{nameof(e.Status)} = {ErrorHelper.HResultToMessage(e.Status)}", true);

      base.OnUnregisterComplete(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnUnregisterComplete)} -------^");
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
  public override void OnProgress(object sender, ProgressEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnProgress)} -------v");
      _logger.Write($"{nameof(e.ProgressPercentage)} = {e.ProgressPercentage}", true);
      _logger.Write($"{nameof(e.OverallPercentage)} = {e.OverallPercentage}", true);

      base.OnProgress(sender, e);

      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnProgress)} -------^");
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
  public override void OnExecuteMsiMessage(object sender, ExecuteMsiMessageEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecuteMsiMessage)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.MessageType)} = {e.MessageType}", true);
      _logger.Write($"{nameof(e.Message)} = {e.Message}", true);
      _logger.Write($"{nameof(e.UIHint)} = {e.UIHint}", true);
      _logger.Write($"{nameof(e.Recommendation)} = {e.Recommendation}", true);


      var data = e.Data?.Where(d => !string.IsNullOrWhiteSpace(d)).ToArray() ?? Array.Empty<string>();
      _logger.Write($"{nameof(e.Data)} (count = {data.Length})", true);
      foreach (var d in data)
        _logger.Write($"     {d}", true);

      base.OnExecuteMsiMessage(sender, e);

      _logger.Write($"{nameof(e.Result)} = {e.Result}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecuteMsiMessage)} -------^");
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
  public override void OnExecuteProcessCancel(object sender, ExecuteProcessCancelEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecuteProcessCancel)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.ProcessId)} = {e.ProcessId}", true);
      _logger.Write($"{nameof(e.Recommendation)} = {e.Recommendation}", true);

      base.OnExecuteProcessCancel(sender, e);

      _logger.Write($"{nameof(e.Action)} = {e.Action}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecuteProcessCancel)} -------^");
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
  public override void OnExecuteFilesInUse(object sender, ExecuteFilesInUseEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecuteFilesInUse)} -------v");
      _logger.Write($"{nameof(e.PackageId)} = {e.PackageId}", true);
      _logger.Write($"{nameof(e.Source)} = {e.Source}", true);
      _logger.Write($"{nameof(e.Recommendation)} = {e.Recommendation}", true);

      var files = e.Files?.Where(f => !string.IsNullOrWhiteSpace(f)).ToArray() ?? Array.Empty<string>();
      _logger.Write($"{nameof(e.Files)} (count = {files.Length})", true);
      foreach (var file in files)
        _logger.Write($"    {file}", true);


      base.OnExecuteFilesInUse(sender, e);

      _logger.Write($"{nameof(e.Result)} = {e.Result}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnExecuteFilesInUse)} -------^");
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
  public override void OnError(object sender, ErrorEventArgs e)
  {
    try
    {
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnError)} -------v");
      _logger.Write($"{nameof(e.UIHint)} = {e.UIHint}", true);
      _logger.Write($"{nameof(e.Recommendation)} = {e.Recommendation}", true);

      base.OnError(sender, e);

      _logger.Write($"{nameof(e.Result)} = {e.Result}", true);
      _logger.Write($"{nameof(e.HResult)} = {ErrorHelper.HResultToMessage(e.HResult)}", true);
      _logger.Write($"{nameof(ApplyPhase)}: {nameof(OnError)} -------^");
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

  private void LogCacheProgress(CacheProgressBaseEventArgs e)
  {
    _logger.Write($"{nameof(e.PayloadId)} = {e.PayloadId}", true);
    _logger.Write($"{nameof(e.PackageOrContainerId)} = {e.PackageOrContainerId}", true);
    _logger.Write($"{nameof(e.Total)} = {e.Total}", true);
    _logger.Write($"{nameof(e.Progress)} = {e.Progress}", true);
    _logger.Write($"{nameof(e.OverallPercentage)} = {e.OverallPercentage}", true);
  }
}