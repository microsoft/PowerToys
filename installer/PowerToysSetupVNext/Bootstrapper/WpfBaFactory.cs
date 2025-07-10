using Bootstrapper.Models;
using Bootstrapper.Phases;
using System;
using WixToolset.BootstrapperApplicationApi;

namespace Bootstrapper;

internal class WpfBaFactory
{
  public Model Create(IDefaultBootstrapperApplication ba, IEngine engine, IBootstrapperCommand commandInfo)
  {
    try
    {
      var uiFacade = new WpfFacade(new Log(engine), commandInfo.Display);
      var model = new Model(engine, commandInfo, uiFacade);

      SubscribeCancelEvents(ba, model);
      SubscribeProgressEvents(ba, model);

      SubscribeDetectEvents(ba, model);
      SubscribePlanEvents(ba, model);
      SubscribeApplyEvents(ba, model);

      model.Log.RemoveEmbeddedLog();
      return model;
    }
    catch (Exception ex)
    {
      engine.Log(LogLevel.Error, ex.ToString());
      throw;
    }
  }

  private void SubscribeDetectEvents(IDefaultBootstrapperApplication ba, Model model)
  {
    // Adds a lot of logging, but reviewing the output can be educational
    var debug = new LoggingDetectPhase(model);
    var release = new DetectPhase(model);


#if DEBUG
    var detectPhase = debug;
#else
    var detectPhase = release;
#endif

    detectPhase.DetectPhaseComplete += model.UiFacade.OnDetectPhaseComplete;

    ba.Startup += detectPhase.OnStartup;
    ba.Shutdown += detectPhase.OnShutdown;
    ba.DetectBegin += detectPhase.OnDetectBegin;
    ba.DetectComplete += detectPhase.OnDetectComplete;
    ba.DetectRelatedBundle += detectPhase.OnDetectRelatedBundle;
    ba.DetectRelatedBundlePackage += detectPhase.OnDetectRelatedBundlePackage;
    ba.DetectRelatedMsiPackage += detectPhase.OnDetectRelatedMsiPackage;
    ba.DetectUpdate += detectPhase.OnDetectUpdate;
    ba.DetectUpdateBegin += detectPhase.OnDetectUpdateBegin;
    ba.DetectUpdateComplete += detectPhase.OnDetectUpdateComplete;
    ba.DetectForwardCompatibleBundle += detectPhase.OnDetectForwardCompatibleBundle;
    ba.DetectPackageBegin += detectPhase.OnDetectPackageBegin;
    ba.DetectPackageComplete += detectPhase.OnDetectPackageComplete;
    ba.DetectPatchTarget += detectPhase.OnDetectPatchTarget;
    ba.DetectCompatibleMsiPackage += detectPhase.OnDetectCompatibleMsiPackage;
    ba.DetectMsiFeature += detectPhase.OnDetectMsiFeature;
  }

  private void SubscribePlanEvents(IDefaultBootstrapperApplication ba, Model model)
  {
    // Adds a lot of logging, but reviewing the output can be educational
    var debug = new LoggingPlanPhase(model);
    var release = new PlanPhase(model);

#if DEBUG
    var planPhase = debug;
#else
    var planPhase = release;
#endif

    planPhase.PlanPhaseFailed += model.UiFacade.OnApplyPhaseComplete;

    ba.PlanBegin += planPhase.OnPlanBegin;
    ba.PlanComplete += planPhase.OnPlanComplete;
    ba.PlanPackageBegin += planPhase.OnPlanPackageBegin;
    ba.PlanPackageComplete += planPhase.OnPlanPackageComplete;
    ba.PlanRollbackBoundary += planPhase.OnPlanRollbackBoundary;
    ba.PlanRelatedBundle += planPhase.OnPlanRelatedBundle;
    ba.PlanRelatedBundleType += planPhase.OnPlanRelatedBundleType;
    ba.PlanRestoreRelatedBundle += planPhase.OnPlanRestoreRelatedBundle;
    ba.PlanForwardCompatibleBundle += planPhase.OnPlanForwardCompatibleBundle;
    ba.PlanCompatibleMsiPackageBegin += planPhase.OnPlanCompatibleMsiPackageBegin;
    ba.PlanCompatibleMsiPackageComplete += planPhase.OnPlanCompatibleMsiPackageComplete;
    ba.PlanMsiPackage += planPhase.OnPlanMsiPackage;
    ba.PlanPatchTarget += planPhase.OnPlanPatchTarget;
    ba.PlanMsiFeature += planPhase.OnPlanMsiFeature;
    ba.PlannedPackage += planPhase.OnPlannedPackage;
    ba.PlannedCompatiblePackage += planPhase.OnPlannedCompatiblePackage;
  }

  private void SubscribeApplyEvents(IDefaultBootstrapperApplication ba, Model model)
  {
    // Adds a lot of logging, but reviewing the output can be educational
    var debug = new LoggingApplyPhase(model);
    var release = new ApplyPhase(model);

#if DEBUG
    var applyPhase = debug;
#else
    var applyPhase = release;
#endif

    applyPhase.ApplyPhaseComplete += model.UiFacade.OnApplyPhaseComplete;

    ba.ApplyBegin += applyPhase.OnApplyBegin;
    ba.ApplyComplete += applyPhase.OnApplyComplete;
    ba.ApplyDowngrade += applyPhase.OnApplyDowngrade;
    ba.ExecuteBegin += applyPhase.OnExecuteBegin;
    ba.ExecuteComplete += applyPhase.OnExecuteComplete;
    //ba.SetUpdateBegin += applyPhase.OnSetUpdateBegin;
    //ba.SetUpdateComplete += applyPhase.OnSetUpdateComplete;
    ba.ElevateBegin += applyPhase.OnElevateBegin;
    ba.ElevateComplete += applyPhase.OnElevateComplete;
    ba.ExecutePatchTarget += applyPhase.OnExecutePatchTarget;
    ba.BeginMsiTransactionBegin += applyPhase.OnBeginMsiTransactionBegin;
    ba.BeginMsiTransactionComplete += applyPhase.OnBeginMsiTransactionComplete;
    ba.CommitMsiTransactionBegin += applyPhase.OnCommitMsiTransactionBegin;
    ba.CommitMsiTransactionComplete += applyPhase.OnCommitMsiTransactionComplete;
    ba.RollbackMsiTransactionBegin += applyPhase.OnRollbackMsiTransactionBegin;
    ba.RollbackMsiTransactionComplete += applyPhase.OnRollbackMsiTransactionComplete;
    ba.CacheBegin += applyPhase.OnCacheBegin;
    ba.CacheComplete += applyPhase.OnCacheComplete;
    ba.CacheAcquireBegin += applyPhase.OnCacheAcquireBegin;
    ba.CacheAcquireComplete += applyPhase.OnCacheAcquireComplete;
    ba.CacheAcquireResolving += applyPhase.OnCacheAcquireResolving;
    ba.CacheAcquireProgress += applyPhase.OnCacheAcquireProgress;
    ba.CacheContainerOrPayloadVerifyBegin += applyPhase.OnCacheContainerOrPayloadVerifyBegin;
    ba.CacheContainerOrPayloadVerifyComplete += applyPhase.OnCacheContainerOrPayloadVerifyComplete;
    ba.CacheContainerOrPayloadVerifyProgress += applyPhase.OnCacheContainerOrPayloadVerifyProgress;
    ba.CachePackageBegin += applyPhase.OnCachePackageBegin;
    ba.CachePackageComplete += applyPhase.OnCachePackageComplete;
    ba.CachePackageNonVitalValidationFailure += applyPhase.OnCachePackageNonVitalValidationFailure;
    ba.CachePayloadExtractBegin += applyPhase.OnCachePayloadExtractBegin;
    ba.CachePayloadExtractComplete += applyPhase.OnCachePayloadExtractComplete;
    ba.CachePayloadExtractProgress += applyPhase.OnCachePayloadExtractProgress;
    ba.CacheVerifyBegin += applyPhase.OnCacheVerifyBegin;
    ba.CacheVerifyComplete += applyPhase.OnCacheVerifyComplete;
    ba.CacheVerifyProgress += applyPhase.OnCacheVerifyProgress;
    ba.ExecutePackageBegin += applyPhase.OnExecutePackageBegin;
    ba.ExecutePackageComplete += applyPhase.OnExecutePackageComplete;
    ba.ExecuteProgress += applyPhase.OnExecuteProgress;
    ba.PauseAutomaticUpdatesBegin += applyPhase.OnPauseAutomaticUpdatesBegin;
    ba.PauseAutomaticUpdatesComplete += applyPhase.OnPauseAutomaticUpdatesComplete;
    ba.SystemRestorePointBegin += applyPhase.OnSystemRestorePointBegin;
    ba.SystemRestorePointComplete += applyPhase.OnSystemRestorePointComplete;
    ba.LaunchApprovedExeBegin += applyPhase.OnLaunchApprovedExeBegin;
    ba.LaunchApprovedExeComplete += applyPhase.OnLaunchApprovedExeComplete;
    ba.RegisterBegin += applyPhase.OnRegisterBegin;
    ba.RegisterComplete += applyPhase.OnRegisterComplete;
    ba.UnregisterBegin += applyPhase.OnUnregisterBegin;
    ba.UnregisterComplete += applyPhase.OnUnregisterComplete;
    ba.Progress += applyPhase.OnProgress;
    ba.ExecuteMsiMessage += applyPhase.OnExecuteMsiMessage;
    ba.ExecuteProcessCancel += applyPhase.OnExecuteProcessCancel;
    ba.ExecuteFilesInUse += applyPhase.OnExecuteFilesInUse;
    ba.Error += applyPhase.OnError;
  }

  private void SubscribeCancelEvents(IDefaultBootstrapperApplication ba, Model model)
  {
    var cancelHandler = new CancelHandler(model);

    ba.ElevateBegin += cancelHandler.CheckForCancel;
    ba.PlanBegin += cancelHandler.CheckForCancel;
    ba.PlanPackageBegin += cancelHandler.CheckForCancel;
    ba.PlanPatchTarget += cancelHandler.CheckForCancel;
    ba.PlanMsiFeature += cancelHandler.CheckForCancel;
    ba.PlanMsiPackage += cancelHandler.CheckForCancel;
    ba.PlanCompatibleMsiPackageBegin += cancelHandler.CheckForCancel;
    ba.PlanForwardCompatibleBundle += cancelHandler.CheckForCancel;
    ba.PlanRollbackBoundary += cancelHandler.CheckForCancel;
    ba.PlanRelatedBundle += cancelHandler.CheckForCancel;
    ba.PlanRelatedBundleType += cancelHandler.CheckForCancel;
    ba.PlanRestoreRelatedBundle += cancelHandler.CheckForCancel;
    ba.ApplyBegin += cancelHandler.CheckForCancel;
    ba.LaunchApprovedExeBegin += cancelHandler.CheckForCancel;
    ba.ExecuteBegin += cancelHandler.CheckForCancel;
    ba.ExecutePackageBegin += cancelHandler.CheckForCancel;
    ba.ExecutePatchTarget += cancelHandler.CheckForCancel;
    ba.ExecuteProgress += cancelHandler.CheckForCancel;
    ba.BeginMsiTransactionBegin += cancelHandler.CheckForCancel;
    ba.CommitMsiTransactionBegin += cancelHandler.CheckForCancel;
    ba.CacheBegin += cancelHandler.CheckForCancel;
    ba.CacheAcquireBegin += cancelHandler.CheckForCancel;
    ba.CacheAcquireProgress += cancelHandler.CheckForCancel;
    ba.CacheAcquireResolving += cancelHandler.CheckForCancel;
    ba.CachePackageBegin += cancelHandler.CheckForCancel;
    ba.CacheContainerOrPayloadVerifyBegin += cancelHandler.CheckForCancel;
    ba.CacheContainerOrPayloadVerifyProgress += cancelHandler.CheckForCancel;
    ba.CachePayloadExtractBegin += cancelHandler.CheckForCancel;
    ba.CachePayloadExtractProgress += cancelHandler.CheckForCancel;
    ba.CacheVerifyBegin += cancelHandler.CheckForCancel;
    ba.CacheVerifyProgress += cancelHandler.CheckForCancel;
    ba.RegisterBegin += cancelHandler.CheckForCancel;
    ba.Progress += cancelHandler.CheckForCancel;

    ba.ExecuteMsiMessage += cancelHandler.CheckResult;
    ba.ExecuteFilesInUse += cancelHandler.CheckResult;
  }

  private void SubscribeProgressEvents(IDefaultBootstrapperApplication ba, Model model)
  {
    var progressHandler = new ProgressHandler(model);

    ba.PlanBegin += progressHandler.OnPlanBegin;
    ba.ApplyBegin += progressHandler.OnApplyBegin;
    ba.CacheAcquireProgress += progressHandler.OnCacheAcquireProgress;
    ba.CachePayloadExtractProgress += progressHandler.OnCachePayloadExtractProgress;
    ba.CacheVerifyProgress += progressHandler.OnCacheVerifyProgress;
    ba.CacheContainerOrPayloadVerifyProgress += progressHandler.OnCacheContainerOrPayloadVerifyProgress;
    ba.CacheComplete += progressHandler.OnCacheComplete;
    ba.ExecutePackageComplete += progressHandler.OnExecutePackageComplete;
    ba.ExecuteProgress += progressHandler.OnApplyExecuteProgress;
  }
}