using Bootstrapper.Models;
using Bootstrapper.Models.State;
using System;
using WixToolset.BootstrapperApplicationApi;

namespace Bootstrapper.Phases;

internal class ProgressHandler
{
  private readonly Model _model;
  private readonly object _progressLock = new();
  private int _progressStages;
  private int _cacheProgress;
  private int _executeProgress;

  public ProgressHandler(Model model)
  {
    _model = model;
  }

  public void OnPlanBegin(object sender, PlanBeginEventArgs e)
  {
    try
    {
      var action = _model.State.PlannedAction.ToString().ToLower();
      ReportProgress($"Planning {action}");
    }
    catch (Exception ex)
    {
      _model.Log.Write(ex);
      throw;
    }
  }

  public void OnApplyBegin(object sender, ApplyBeginEventArgs e)
  {
    try
    {
      lock (_progressLock)
        _progressStages = e.PhaseCount;

      ReportProgress("Applying changes");
    }
    catch (Exception ex)
    {
      _model.Log.Write(ex);
      throw;
    }
  }

  public void OnCacheAcquireProgress(object sender, CacheAcquireProgressEventArgs e)
  {
    try
    {
      lock (_progressLock)
        _cacheProgress = e.OverallPercentage;

      ReportProgress("Retrieving", e.PackageOrContainerId);
    }
    catch (Exception ex)
    {
      _model.Log.Write(ex);
      throw;
    }
  }

  public void OnCachePayloadExtractProgress(object sender, CachePayloadExtractProgressEventArgs e)
  {
    try
    {
      lock (_progressLock)
        _cacheProgress = e.OverallPercentage;

      ReportProgress("Extracting", e.PackageOrContainerId);
    }
    catch (Exception ex)
    {
      _model.Log.Write(ex);
      throw;
    }
  }

  public void OnCacheVerifyProgress(object sender, CacheVerifyProgressEventArgs e)
  {
    try
    {
      lock (_progressLock)
        _cacheProgress = e.OverallPercentage;

      ReportProgress("Verifying", e.PackageOrContainerId);
    }
    catch (Exception ex)
    {
      _model.Log.Write(ex);
      throw;
    }
  }

  public void OnCacheContainerOrPayloadVerifyProgress(object sender, CacheContainerOrPayloadVerifyProgressEventArgs e)
  {
    try
    {
      lock (_progressLock)
        _cacheProgress = e.OverallPercentage;

      ReportProgress("Verifying", e.PackageOrContainerId);
    }
    catch (Exception ex)
    {
      _model.Log.Write(ex);
    }
  }

  public void OnCacheComplete(object sender, CacheCompleteEventArgs e)
  {
    try
    {
      lock (_progressLock)
        _cacheProgress = 100;

      ReportProgress();
    }
    catch (Exception ex)
    {
      _model.Log.Write(ex);
      throw;
    }
  }

  public void OnApplyExecuteProgress(object sender, ExecuteProgressEventArgs e)
  {
    try
    {
      int overallProgress;
      lock (_progressLock)
      {
        _executeProgress = e.OverallPercentage;
        overallProgress = CalculateProgress();
      }

      ReportProgress(null, e.PackageId);

      if (_model.State.Display == Display.Embedded)
        _model.Engine.SendEmbeddedProgress(e.ProgressPercentage, overallProgress);
    }
    catch (Exception ex)
    {
      _model.Log.Write(ex);
      throw;
    }
  }

  public void OnExecutePackageComplete(object sender, ExecutePackageCompleteEventArgs e)
  {
    try
    {
      // clear display
      ReportProgress(string.Empty, string.Empty);
    }
    catch (Exception ex)
    {
      _model.Log.Write(ex);
      throw;
    }
  }

  private void ReportProgress(string message = null, string packageId = null)
  {
    if (_model.UiFacade.ProgressReporter != null)
    {
      var report = new ProgressReport
      {
        Message = message,
        Progress = CalculateProgress()
      };

      if (packageId != null)
        report.PackageName = _model.State.GetPackageName(packageId);

      _model.UiFacade.ProgressReporter.Report(report);
    }
  }

  private int CalculateProgress()
  {
    lock (_progressLock)
    {
      if (_progressStages > 0)
        return (_cacheProgress + _executeProgress) / _progressStages;

      return 0;
    }
  }
}