using Bootstrapper.Models.State;
using Bootstrapper.Models.Util;
using System;
using System.IO;
using System.Linq;
using System.Text;
using WixToolset.BootstrapperApplicationApi;

namespace Bootstrapper.Models;

internal class Log
{
  private readonly IEngine _engine;

  public Log(IEngine engine)
  {
    _engine = engine;
  }

  public void Write(string message, bool indent = false, LogLevel level = LogLevel.Verbose)
  {
    var txt = message;
    if (indent)
      txt = $"{new string(' ', 10)}{txt}";

    _engine.Log(level, txt);
  }

  public void Write(Exception ex)
  {
    Write(ex.ToString(), false, LogLevel.Error);
  }

  public void RemoveEmbeddedLog()
  {
    try
    {
      // delete any previous embedded log
      var fileName = EmbeddedLogFileName();
      if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
        File.Delete(fileName);
    }
    catch
    {
      // ignore
    }
  }

  public string EmbeddedLogFileName()
  {
    var folder = Path.GetDirectoryName(_engine.GetVariableString(Constants.BundleLogName));
    if (string.IsNullOrEmpty(folder))
      return string.Empty;

    return Path.Combine(folder, $"{_engine.GetVariableString(Constants.BundleNameVariable)}_embedded.txt");
  }

  public void WriteLogFile(AppState state, string fileName)
  {
    try
    {
      if (string.IsNullOrEmpty(fileName))
        return;

      var log = Read(state);
      if (string.IsNullOrEmpty(log))
        throw new InvalidOperationException("Reading log produced an empty string");

      var logSb = new StringBuilder();
      if (fileName == EmbeddedLogFileName())
      {
        // indent embedded log
        var logArr = log.Split('\n');
        foreach (var line in logArr)
        {
          logSb.Append(new string(' ', 8));
          logSb.AppendLine(line.Trim());
        }

        File.WriteAllText(fileName, logSb.ToString());
      }
      else
        File.WriteAllText(fileName, log);
    }
    catch (Exception ex)
    {
      Write($"Unable to write to {fileName}");
      Write(ex);
    }
  }

  /// <summary>
  ///   Formats app state, bundle log and any package logs that have been written into a single log
  /// </summary>
  /// <exception cref="InstallerVariableNotFoundException">
  ///   Thrown when the log cannot be read because a variable is missing
  /// </exception>
  private string Read(AppState state)
  {
    if (!_engine.ContainsVariable(Constants.BundleLogName))
      throw new InstallerVariableNotFoundException(Constants.BundleLogName);

    var tocSb = new StringBuilder();
    tocSb.AppendLine("Table of Contents");

    // Caches log file text. Will be appended to TOC once all logs have been read.
    var logSb = new StringBuilder();

    var tocIndex = 1;

    // Add state to TOC
    tocSb.AppendLine($"{tocIndex}. State");

    // Add state to log
    logSb.AppendLine();
    logSb.AppendLine();
    logSb.AppendLine("1. State");
    logSb.AppendLine(Constants.Line);
    logSb.AppendLine(state.ToString());

    // Add TOC entry for bundle
    tocIndex++;
    var bundleLogFile = Path.GetFullPath(_engine.GetVariableString(Constants.BundleLogName));
    tocSb.AppendLine($"{tocIndex}. Bundle Log (original location = \"{bundleLogFile}\")");
    // Add bundle log text
    logSb.Append(ReadLogFile(bundleLogFile, "Bundle Log", tocIndex));

    var packageIds = state.Bundle.Packages.Values.Select(i => i.Id).ToArray();
    foreach (var packageId in packageIds)
    {
      try
      {
        var packageLogFile = PackageLogFile(packageId);
        if (string.IsNullOrEmpty(packageLogFile))
          continue;

        // Add package to TOC
        var logName = state.GetPackageName(packageId);
        tocIndex++;
        tocSb.AppendLine($"{tocIndex}. {logName} (original location = \"{packageLogFile}\")");

        // Add package log
        logSb.Append(ReadLogFile(packageLogFile, logName, tocIndex));
      }
      catch (Exception ex)
      {
        logSb.AppendLine();
        logSb.AppendLine();
        logSb.AppendLine(ex.ToString());
      }
    }

    if (!string.IsNullOrEmpty(state.RelatedBundleId))
    {
      var fileName = EmbeddedLogFileName();
      if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
      {
        tocIndex++;
        var productName = _engine.GetVariableString(Constants.BundleNameVariable);
        tocSb.AppendLine($"{tocIndex}. {productName} bundle (original location = \"{fileName}\")");
        logSb.Append(ReadLogFile(fileName, productName, tocIndex));
      }
    }

    tocSb.Append(logSb);
    return tocSb.ToString();
  }

  private string PackageLogFile(string packageId)
  {
    var logLocationVar = $"{Constants.BundleLogName}_{packageId}";
    // Variable won't exist until package has been run
    if (!_engine.ContainsVariable(logLocationVar))
      return null;

    var logLocation = _engine.GetVariableString(logLocationVar);
    if (string.IsNullOrWhiteSpace(logLocation))
      return null;

    var logFile = Path.GetFullPath(logLocation);
    if (string.IsNullOrEmpty(logFile) || !File.Exists(logFile))
      return null;

    return logFile;
  }

  private string ReadLogFile(string fileName, string logName, int tocIndex)
  {
    string logText;
    var bakFile = Path.GetFullPath($"{fileName}.view.bak");
    // Copy the log file to avoid file contention.
    File.Copy(fileName, bakFile);

    try
    {
      logText = File.ReadAllText(bakFile);
    }
    catch (Exception ex)
    {
      logText = $"Unable to read file {bakFile} ({ex.Message})";
    }
    finally
    {
      File.Delete(bakFile);
    }

    var sb = new StringBuilder();
    sb.AppendLine();
    sb.AppendLine();
    sb.AppendLine($"{tocIndex}. {logName}");
    sb.AppendLine(Constants.Line);
    sb.AppendLine(logText);
    return sb.ToString();
  }
}