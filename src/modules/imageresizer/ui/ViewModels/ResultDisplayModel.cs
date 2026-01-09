// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using ImageResizer.Models.ResizeResults;
using ImageResizer.Properties;

namespace ImageResizer.ViewModels;

public sealed class ResultDisplayModel
{
    private static readonly string AccessibleTypeError = Resources.Results_ErrorItemType_AccessibilityText;
    private static readonly string AccessibleTypeWarning = Resources.Results_WarningItemType_AccessibilityText;

    public ResizeResult Result { get; }

    public ResultDisplayModel(ResizeResult result)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    /// <summary>
    /// Gets the file name for display, trimming the path information.
    /// </summary>
    public string FileName => Path.GetFileName(Result.FilePath);

    /// <summary>
    /// Gets the file name of the destination image. For <see cref="FileReplaceFailedResult"/>
    /// results. Returns an empty string if the result is a different type.
    /// </summary>
    public string DestinationFileName => Result switch
    {
        FileReplaceFailedResult r => Path.GetFileName(r.ResizedFilePath),
        _ => string.Empty,
    };

    /// <summary>
    /// Gets the full file path of the original image file.
    /// </summary>
    public string FilePath => Result.FilePath;

    /// <summary>
    /// Gets the exception message if the result was an error or warning; otherwise returns
    /// <see langword="null"/>.
    /// </summary>
    public string ResultMessage => Result switch
    {
        ErrorResult e => e.Exception.Message,
        FileReplaceFailedResult r => r.Exception.Message,
        FileRecycleFailedResult r => r.Exception.Message,
        _ => null,
    };

    /// <summary>
    /// Gets the localised item type of a result, e.g. "Warning" or "Error". Provides context for
    /// accessibility tools.
    /// </summary>
    public string AccessibleType => Result switch
    {
        ErrorResult => AccessibleTypeError,
        FileRecycleFailedResult or FileReplaceFailedResult => AccessibleTypeWarning,
        _ => AccessibleTypeWarning,
    };

    /// <summary>
    /// Gets a concise item summary string for accessibility tools.
    /// </summary>
    public string AccessibleDescription => Result switch
    {
        ErrorResult e => $"{FileName}: {e.Exception.Message}",
        FileReplaceFailedResult r => $"{FileName}: {r.Exception.Message}",
        FileRecycleFailedResult r => $"{FileName}: {r.Exception.Message}",
        _ => FileName,
    };
}
