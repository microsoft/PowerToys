using Bootstrapper.Models.Util;
using System;
using System.Text;
using System.Threading;
using WixToolset.BootstrapperApplicationApi;

namespace Bootstrapper.Models.State;

/// <summary>
///   Provides thread safe access to all state shared by the BA
/// </summary>
internal class AppState
{
  private readonly IBootstrapperCommand _commandInfo;
  private readonly object _lock = new();
  private long _baStatus;
  private long _plannedAction;
  private long _relatedBundleStatus;
  private string _relatedBundleId;
  private string _relatedBundleVersion;
  private long _phaseResult;
  private string _errorMessage;
  private long _cancelRequested;
  private string _relatedBundleName;

  public AppState(IEngine engine, IBootstrapperCommand commandInfo)
  {
    _commandInfo = commandInfo;
    Bundle = new BootstrapperApplicationData().Bundle;
    BundleVersion = engine.GetVariableVersion(Constants.VersionVariable);
    _baStatus = (long)BaStatus.Initializing;
    _relatedBundleStatus = (long)BundleStatus.Unknown;
    _plannedAction = (long)LaunchAction.Unknown;
  }

  /// <summary>
  ///   Information about the packages included in the bundle.
  /// </summary>
  public IBundleInfo Bundle { get; }

  /// <summary>
  ///   Version of the bundle that this BA will deploy.
  /// </summary>
  public string BundleVersion { get; }

  /// <summary>
  ///   The display level of the BA.
  /// </summary>
  public Display Display => _commandInfo.Display;

  /// <summary>
  ///   Whether a version of this bundle was previously installed, and if so, whether this bundle is newer or older than the
  ///   installed version.
  /// </summary>
  public BundleStatus RelatedBundleStatus
  {
    get => (BundleStatus)Interlocked.Read(ref _relatedBundleStatus);
    set => Interlocked.Exchange(ref _relatedBundleStatus, (long)value);
  }


  /// <summary>
  ///   Package ID of the bundle that was discovered during the detection phase.
  ///   Will be <see langword="null" /> if not installed.
  /// </summary>
  public string RelatedBundleId
  {
    get
    {
      lock (_lock)
        return _relatedBundleId;
    }
    set
    {
      lock (_lock)
        _relatedBundleId = value;
    }
  }

  public string RelatedBundleName
  {
    get
    {
      lock (_lock)
        return _relatedBundleName;
    }
    set
    {
      lock (_lock)
        _relatedBundleName = value;
    }
  }

  /// <summary>
  ///   Version of the bundle that was discovered during the detection phase.
  ///   Will be <see langword="null" /> if not installed.
  /// </summary>
  public string RelatedBundleVersion
  {
    get
    {
      lock (_lock)
        return _relatedBundleVersion;
    }
    set
    {
      lock (_lock)
        _relatedBundleVersion = value;
    }
  }

  /// <summary>
  ///   Current status of the BA.
  /// </summary>
  public BaStatus BaStatus
  {
    get => (BaStatus)Interlocked.Read(ref _baStatus);
    set => Interlocked.Exchange(ref _baStatus, (long)value);
  }

  /// <summary>
  ///   Final result of the last phase that ran.
  /// </summary>
  public int PhaseResult
  {
    get => (int)Interlocked.Read(ref _phaseResult);
    set => Interlocked.Exchange(ref _phaseResult, value);
  }

  /// <summary>
  ///   Action selected when beginning the plan phase.
  /// </summary>
  public LaunchAction PlannedAction
  {
    get => (LaunchAction)Interlocked.Read(ref _plannedAction);
    set => Interlocked.Exchange(ref _plannedAction, (long)value);
  }

  /// <summary>
  ///   Can be set to <see langword="true" /> to cancel the plan and apply phases.
  /// </summary>
  public bool CancelRequested
  {
    get => Interlocked.Read(ref _cancelRequested) == 1L;
    set => Interlocked.Exchange(ref _cancelRequested, Convert.ToInt32(value));
  }

  /// <summary>
  ///   Stores any error encountered during the apply phase.
  /// </summary>
  public string ErrorMessage
  {
    get
    {
      lock (_lock)
        return _errorMessage;
    }
    set
    {
      lock (_lock)
        _errorMessage = value;
    }
  }

  /// <summary>
  ///   Gets the display name for a package if possible.
  /// </summary>
  /// <param name="packageId">Identity of the package.</param>
  /// <returns>Display name of the package if found or the package id if not.</returns>
  public string GetPackageName(string packageId)
  {
    var packageName = string.Empty;

    if (packageId == RelatedBundleId)
      packageName = RelatedBundleName;
    else if (Bundle.Packages.TryGetValue(packageId, out var package) && !string.IsNullOrWhiteSpace(package.DisplayName))
      packageName = package.DisplayName;

    return packageName;
  }

  public override string ToString()
  {
    var tocSb = new StringBuilder();
    string error;
    if (string.IsNullOrWhiteSpace(ErrorMessage))
      error = "None";
    else
      error = ErrorMessage;

    string installedVersion;
    if (string.IsNullOrWhiteSpace(RelatedBundleVersion))
      installedVersion = "Not installed";
    else
      installedVersion = RelatedBundleVersion;

    tocSb.AppendLine($"Bootstrapper status: {BaStatus}");
    tocSb.AppendLine($"Bundle status: {RelatedBundleStatus}");
    tocSb.AppendLine($"Bundle version: {BundleVersion}");
    tocSb.AppendLine($"Installed version: {installedVersion}");
    tocSb.AppendLine($"Planned action: {PlannedAction}");
    tocSb.AppendLine($"Result of last phase: {PhaseResult}");
    tocSb.AppendLine($"Bundle error message: {error}");
    tocSb.AppendLine($"Display: {_commandInfo.Display}");
    tocSb.AppendLine($"Command line: {_commandInfo.CommandLine}");
    tocSb.AppendLine($"Command line action: {_commandInfo.Action}");
    tocSb.AppendLine($"Command line resume: {_commandInfo.Resume}");
    return tocSb.ToString();
  }
}