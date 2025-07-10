using System;
using WixToolset.BootstrapperApplicationApi;

namespace Bootstrapper.Models.Util;

internal class DetectPhaseCompleteEventArgs : EventArgs
{
  public DetectPhaseCompleteEventArgs(LaunchAction followupAction)
  {
    FollowupAction = followupAction;
  }

  /// <summary>
  ///   Indicates which action is being planned and executed after the detect phase has
  ///   completed. If <see cref="LaunchAction.Unknown" />, then no action is planned, and
  ///   the UI should prompt the user for the action (install, uninstall, updated, repair, etc.).
  /// </summary>
  public LaunchAction FollowupAction { get; }
}