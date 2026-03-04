// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Models;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Services.PythonScripts;

public interface IPythonScriptService
{
    /// <summary>
    /// Windows mode: the script directly manipulates the clipboard. C# waits for the process to exit.
    /// </summary>
    Task ExecuteWindowsScriptAsync(string scriptPath, ClipboardFormat detectedFormat, CancellationToken cancellationToken, IProgress<double> progress);

    /// <summary>
    /// WSL mode: C# passes data via JSON stdin, receives a DataPackage from JSON stdout.
    /// </summary>
    Task<DataPackage> ExecuteWslScriptAsync(string scriptPath, DataPackageView clipboardData, ClipboardFormat detectedFormat, CancellationToken cancellationToken, IProgress<double> progress);

    /// <summary>
    /// Parses the @advancedpaste: header comments from a Python script file.
    /// </summary>
    PythonScriptMetadata ReadMetadata(string scriptPath);

    /// <summary>
    /// Discovers all .py scripts in <paramref name="folderPath"/> and returns their metadata.
    /// </summary>
    IReadOnlyList<PythonScriptMetadata> DiscoverScripts(string folderPath);

    /// <summary>
    /// Finds the Python executable to use. Returns null if none is found.
    /// </summary>
    string TryFindPythonExecutable(string overridePath = null);

    /// <summary>
    /// Returns true if wsl.exe is available on this machine.
    /// </summary>
    bool IsWslAvailable();

    /// <summary>
    /// Checks which of the declared requirements are not yet importable.
    /// Returns an empty list if all packages are installed.
    /// </summary>
    Task<IReadOnlyList<PythonRequirement>> GetMissingRequirementsAsync(
        PythonScriptMetadata metadata,
        CancellationToken cancellationToken);

    /// <summary>
    /// Installs the given packages via pip / pip3.
    /// </summary>
    Task InstallRequirementsAsync(
        IReadOnlyList<PythonRequirement> requirements,
        string platform,
        CancellationToken cancellationToken);
}
