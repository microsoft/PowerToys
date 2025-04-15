// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ManagedCommon;
using static Peek.Common.Helpers.ResourceLoaderInstance;

namespace Peek.UI.Helpers;

public static class DeleteErrorMessageHelper
{
    /// <summary>
    /// The "Could not delete 'filename'." message, which begins every user-facing error string.
    /// </summary>
    private static readonly CompositeFormat UserMessagePrefix =
        CompositeFormat.Parse(ResourceLoader.GetString("DeleteFileError_Prefix") + " ");

    /// <summary>
    /// The message displayed if the delete failed but the error code isn't covered in the
    /// <see cref="DeleteFileErrors"/> collection.
    /// </summary>
    private static readonly string GenericErrorMessage = ResourceLoader.GetString("DeleteFileError_Generic");

    /// <summary>
    /// The collection of the most common error codes with their matching log messages and user-
    /// facing descriptions.
    /// </summary>
    private static readonly Dictionary<int, (string LogMessage, string UserMessage)> DeleteFileErrors = new()
    {
        {
            2,
            (
                "The system cannot find the file specified.",
                ResourceLoader.GetString("DeleteFileError_NotFound")
            )
        },
        {
            3,
            (
                "The system cannot find the path specified.",
                ResourceLoader.GetString("DeleteFileError_NotFound")
            )
        },
        {
            5,
            (
                "Access is denied.",
                ResourceLoader.GetString("DeleteFileError_AccessDenied")
            )
        },
        {
            19,
            (
                "The media is write protected.",
                ResourceLoader.GetString("DeleteFileError_WriteProtected")
            )
        },
        {
            32,
            (
                "The process cannot access the file because it is being used by another process.",
                ResourceLoader.GetString("DeleteFileError_FileInUse")
            )
        },
        {
            33,
            (
                "The process cannot access the file because another process has locked a portion of the file.",
                ResourceLoader.GetString("DeleteFileError_FileInUse")
            )
        },
    };

    /// <summary>
    /// Logs an error message in response to a failed file deletion attempt.
    /// </summary>
    /// <param name="errorCode">The error code returned from the delete call.</param>
    public static void LogError(int errorCode) =>
        Logger.LogError(DeleteFileErrors.TryGetValue(errorCode, out var messages) ?
            messages.LogMessage :
            $"Error {errorCode} occurred while deleting the file.");

    /// <summary>
    /// Gets the message to display in the UI for a specific delete error code.
    /// </summary>
    /// <param name="filename">The name of the file which could not be deleted.</param>
    /// <param name="errorCode">The error code result from the delete call.</param>
    /// <returns>A string containing the message to show in the user interface.</returns>
    public static string GetUserErrorMessage(string filename, int errorCode)
    {
        string prefix = string.Format(CultureInfo.InvariantCulture, UserMessagePrefix, filename);

        return DeleteFileErrors.TryGetValue(errorCode, out var messages) ?
            prefix + messages.UserMessage :
            prefix + GenericErrorMessage;
    }
}
