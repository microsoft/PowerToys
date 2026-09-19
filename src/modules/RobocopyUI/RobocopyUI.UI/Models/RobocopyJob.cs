// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using RobocopyUI.Helpers;
using RobocopyUI.Services.AI;

namespace RobocopyUI.Models;

/// <summary>
/// Source of truth for the current robocopy command: paths plus selected switches.
/// Simple mode, Advanced checkboxes, AI plans, and loaded job files all write here.
/// </summary>
public sealed class RobocopyJob
{
    private static readonly HashSet<string> TaskSwitchNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "/E", "/S", "/MIR", "/MOVE", "/MOV",
    };

    private readonly List<RobocopyPlanOption> _options = [];
    private string _source = string.Empty;
    private string _destination = string.Empty;

    /// <summary>
    /// Raised after source, destination, or options change.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets or sets the source directory.
    /// </summary>
    public string Source
    {
        get => _source;
        set
        {
            if (_source == value)
            {
                return;
            }

            _source = value;
            OnChanged();
        }
    }

    /// <summary>
    /// Gets or sets the destination directory.
    /// </summary>
    public string Destination
    {
        get => _destination;
        set
        {
            if (_destination == value)
            {
                return;
            }

            _destination = value;
            OnChanged();
        }
    }

    /// <summary>
    /// Gets the selected switches after pruning.
    /// </summary>
    public IReadOnlyList<RobocopyPlanOption> Options => _options;

    /// <summary>
    /// Renders the full command line, including <c>robocopy.exe</c>.
    /// </summary>
    public string RenderCommandLine() => RobocopyCommand.Render(Source, Destination, _options);

    /// <summary>
    /// Renders just the arguments passed to <c>robocopy.exe</c>.
    /// </summary>
    public string RenderArguments(bool renderForRCJFile = false) => RobocopyCommand.RenderArguments(Source, Destination, _options, renderForRCJFile);

    /// <summary>
    /// Replaces every selected switch.
    /// </summary>
    public void ReplaceOptions(IEnumerable<RobocopyPlanOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options.Clear();
        _options.AddRange(options.Where(option => option is not null && !string.IsNullOrWhiteSpace(option.Name)));
        RobocopyCommand.Prune(_options);
        OnChanged();
    }

    /// <summary>
    /// Enables or updates a switch, then prunes implied duplicates.
    /// </summary>
    public void SetOption(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (TaskSwitchNames.Contains(name))
        {
            _options.RemoveAll(option => TaskSwitchNames.Contains(option.Name) && !string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        RemoveByName(name);
        _options.Add(new RobocopyPlanOption(name, value ?? string.Empty));
        RobocopyCommand.Prune(_options);
        OnChanged();
    }

    /// <summary>
    /// Turns a switch off.
    /// </summary>
    public void RemoveOption(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (RemoveByName(name) > 0)
        {
            OnChanged();
        }
    }

    /// <summary>
    /// Returns whether a switch is currently selected.
    /// </summary>
    public bool HasOption(string name) =>
        _options.Any(option => string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns the value for a switch, or an empty string when it is off.
    /// </summary>
    public string GetValue(string name) =>
        _options.FirstOrDefault(option => string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

    /// <summary>
    /// Applies a Simple-mode job, keeping any Advanced extras Simple does not own.
    /// </summary>
    public void ApplySimple(SimpleCopyTaskKind kind, SimpleCopyOptions options)
    {
        var merged = SimpleCopyTask.Merge(_options, kind, options);
        _options.Clear();
        _options.AddRange(merged);
        OnChanged();
    }

    /// <summary>
    /// Reads Simple-mode controls back from the current switches.
    /// </summary>
    public SimpleCopySnapshot InferSimple() => SimpleCopyTask.Infer(_options);

    private int RemoveByName(string name) =>
        _options.RemoveAll(option => string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase));

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
