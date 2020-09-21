// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;
using Wox.Infrastructure.Logger;

namespace Microsoft.Plugin.Program.Logger
{
    /// <summary>
    /// The Program plugin has seen many issues recorded in the Wox repo related to various loading of Windows programs.
    /// This is a dedicated logger for this Program plugin with the aim to output a more friendlier message and clearer
    /// log that will allow debugging to be quicker and easier.
    /// </summary>
    internal static class ProgramLogger
    {
        /// <summary>
        /// Logs an exception
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        internal static void LogException(string classname, string callingMethodName, string loadingProgramPath, string interpretationMessage, Exception e)
        {
            Debug.WriteLine($"ERROR{classname}|{callingMethodName}|{loadingProgramPath}|{interpretationMessage}");

            var possibleResolution = "Not yet known";
            var errorStatus = "UNKNOWN";

            if (IsKnownWinProgramError(e, callingMethodName) || IsKnownUWPProgramError(e, callingMethodName))
            {
                possibleResolution = "Can be ignored and Wox should still continue, however the program may not be loaded";
                errorStatus = "KNOWN";
            }

            var calledMethod = e.TargetSite != null ? e.TargetSite.ToString() : e.StackTrace;

            calledMethod = string.IsNullOrEmpty(calledMethod) ? "Not available" : calledMethod;
            var msg = $"Error status: {errorStatus}"
                         + $"\nProgram path: {loadingProgramPath}"
                         + $"\nException thrown in called method: {calledMethod}"
                         + $"\nPossible interpretation of the error: {interpretationMessage}"
                         + $"\nPossible resolution: {possibleResolution}";

            // removed looping logic since that is inside Log class
            Log.Exception(classname, msg, e, callingMethodName);
        }

        /// <summary>
        /// Please follow exception format: |class name|calling method name|loading program path|user friendly message that explains the error
        /// => Example: |Win32|LnkProgram|c:\..\chrome.exe|Permission denied on directory, but Wox should continue
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        internal static void LogException(string message, Exception e)
        {
            var parts = message.Split('|');
            if (parts.Length < 4)
            {
                Log.Exception($"|ProgramLogger|LogException|Fail to log exception in program logger, parts length is too small: {parts.Length}, message: {message}", e);
            }

            var classname = parts[1];
            var callingMethodName = parts[2];
            var loadingProgramPath = parts[3];
            var interpretationMessage = parts[4];

            LogException(classname, callingMethodName, loadingProgramPath, interpretationMessage, e);
        }

        private static bool IsKnownWinProgramError(Exception e, string callingMethodName)
        {
            if (e.TargetSite?.Name == "GetDescription" && callingMethodName == "LnkProgram")
            {
                return true;
            }

            if (e is SecurityException || e is UnauthorizedAccessException || e is DirectoryNotFoundException)
            {
                return true;
            }

            return false;
        }

        private static bool IsKnownUWPProgramError(Exception e, string callingMethodName)
        {
            if (((e.HResult == -2147024774 || e.HResult == -2147009769) && callingMethodName == "ResourceFromPri")
                || (e.HResult == -2147024894 && (callingMethodName == "LogoPathFromUri" || callingMethodName == "ImageFromPath"))
                || (e.HResult == -2147024864 && callingMethodName == "InitializeAppInfo"))
            {
                return true;
            }

            if (callingMethodName == "XmlNamespaces")
            {
                return true;
            }

            return false;
        }
    }
}
