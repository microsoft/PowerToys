// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using RobocopyUI.Helpers;

namespace RobocopyUI.Models;

/// <summary>
/// Optional Simple-mode toggles that map onto a small set of high-value robocopy switches.
/// </summary>
public sealed class SimpleCopyOptions
{
    /// <summary>Keep NTFS permissions (<c>/SEC</c>).</summary>
    public bool KeepPermissions { get; set; }

    /// <summary>Keep all file properties including owner and auditing (<c>/COPYALL</c>).</summary>
    public bool KeepAllProperties { get; set; }

    /// <summary>Skip source files older than the destination copy (<c>/XO</c>).</summary>
    public bool SkipNewerAtDestination { get; set; }

    /// <summary>File names or wildcards to exclude (<c>/XF</c>).</summary>
    public string ExcludeFileTypes { get; set; } = string.Empty;

    /// <summary>Directory names or wildcards to exclude (<c>/XD</c>).</summary>
    public string ExcludeFolders { get; set; } = string.Empty;

    /// <summary>Use multiple threads (<c>/MT</c>).</summary>
    public bool CopyFaster { get; set; }

    /// <summary>Thread count when <see cref="CopyFaster"/> is on.</summary>
    public int ThreadCount { get; set; } = SimpleCopyTask.DefaultThreadCount;

    /// <summary>Retry failed copies (<c>/R</c> and <c>/W</c>).</summary>
    public bool RetryFailedFiles { get; set; }

    /// <summary>Retry count when <see cref="RetryFailedFiles"/> is on.</summary>
    public int RetryCount { get; set; } = SimpleCopyTask.DefaultRetryCount;

    /// <summary>Seconds to wait between retries.</summary>
    public int RetryWaitSeconds { get; set; } = SimpleCopyTask.DefaultRetryWaitSeconds;

    /// <summary>List what would be copied without copying (<c>/L</c>).</summary>
    public bool PreviewOnly { get; set; }

    /// <summary>Write output to a log file (<c>/LOG</c>).</summary>
    public bool WriteLog { get; set; }

    /// <summary>Log file path when <see cref="WriteLog"/> is on.</summary>
    public string LogPath { get; set; } = string.Empty;
}
