using Bootstrapper.Models.State;
using Bootstrapper.Models.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using WixToolset.BootstrapperApplicationApi;

namespace Bootstrapper.Models;

/// <summary>
///   A model that exposes all the functionality of the BA.
/// </summary>
internal class Model
{
  public Model(IEngine engine, IBootstrapperCommand commandInfo, WpfFacade uiFacade)
  {
    Engine = engine;
    CommandInfo = commandInfo;
    UiFacade = uiFacade;
    Log = new Log(Engine);
    State = new AppState(Engine, CommandInfo);
  }

  /// <summary>
  ///   Contains shared state used by the BA. All members are thread safe unless noted otherwise.
  /// </summary>
  public AppState State { get; }

  /// <summary>
  ///   Command line parameters and other command info passed from the engine.
  /// </summary>
  public IBootstrapperCommand CommandInfo { get; }

  /// <summary>
  ///   Read from and write to the bundle's log.
  /// </summary>
  public Log Log { get; }


  /// <summary>
  ///   A facade exposing the limited UI functionality needed by the BA.
  /// </summary>
  public WpfFacade UiFacade { get; }

  /// <summary>
  ///   WiX engine
  /// </summary>
  public IEngine Engine { get; }

  /// <summary>
  ///   Starts the plan and apply phases with the given action.
  /// </summary>
  /// <param name="action">Action to plan</param>
  public void PlanAndApply(LaunchAction action)
  {
    State.PlannedAction = action;
    State.BaStatus = BaStatus.Planning;
    State.CancelRequested = false;
    Engine.Plan(action);
  }

  /// <summary>
  ///   Reads the log file and displays it in the user's default text editor.
  /// </summary>
  public void ShowLog()
  {
    try
    {
      var fileName = Path.GetFullPath($"{Engine.GetVariableString(Constants.BundleLogName)}.view.txt");
      Log.WriteLogFile(State, fileName);
      if (File.Exists(fileName))
      {
        Process process = null;
        try
        {
          process = new Process();
          process.StartInfo.FileName = fileName;
          process.StartInfo.UseShellExecute = true;
          process.StartInfo.Verb = "open";
          process.Start();
        }
        finally
        {
          process?.Dispose();
        }
      }
    }
    catch (Exception ex)
    {
      UiFacade.ShowMessageBox($"Unable to display log: {ex.Message}", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
    }
  }

  /// <summary>
  /// </summary>
  public void SaveEmbeddedLog(int exitCode)
  {
    try
    {
      if (State.Display == Display.Embedded)
      {
        Log.Write($"Exit code: 0x{exitCode:X}");
        Log.WriteLogFile(State, Log.EmbeddedLogFileName());
      }
    }
    catch
    {
      // ignore
    }
  }
}