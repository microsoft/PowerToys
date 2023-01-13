// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;
using Wox.Plugin.Logger;

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
        /// Logs an warning
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        internal static void Warn(string message, Exception ex, Type fullClassName, string loadingProgramPath, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            string calledMethod = "Not available";

            if (ex != null)
            {
                string exceptionCalledMethod = ex.TargetSite != null ? ex.TargetSite.ToString() : ex.StackTrace;
                if (!string.IsNullOrEmpty(exceptionCalledMethod))
                {
                    calledMethod = exceptionCalledMethod;
                }
            }

            var msg = $"\n\t\tProgram path: {loadingProgramPath}"
                      + $"\n\t\tException thrown in called method: {calledMethod}"
                      + $"\n\t\tPossible interpretation of the error: {message}";

            // removed looping logic since that is inside Log class
            Log.Warn(msg, fullClassName, methodName, sourceFilePath, sourceLineNumber);
        }

        /// <summary>
        /// Logs an exception
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        internal static void Exception(string message, Exception ex, Type fullClassName, string loadingProgramPath, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            // internal static void LogException(string classname, string callingMethodName, string loadingProgramPath, string interpretationMessage, Exception e)
            var possibleResolution = "Not yet known";
            var errorStatus = "UNKNOWN";

            if (IsKnownWinProgramError(ex, methodName) || IsKnownUWPProgramError(ex, methodName))
            {
                possibleResolution = "Can be ignored and PowerToys Run should still continue, however the program may not be loaded";
                errorStatus = "KNOWN";
            }

            var calledMethod = ex.TargetSite != null ? ex.TargetSite.ToString() : ex.StackTrace;

            calledMethod = string.IsNullOrEmpty(calledMethod) ? "Not available" : calledMethod;
            var msg = $"\tError status: {errorStatus}"
                         + $"\n\t\tProgram path: {loadingProgramPath}"
                         + $"\n\t\tException thrown in called method: {calledMethod}"
                         + $"\n\t\tPossible interpretation of the error: {message}"
                         + $"\n\t\tPossible resolution: {possibleResolution}";

            // removed looping logic since that is inside Log class
            Log.Exception(msg, ex, fullClassName, methodName, sourceFilePath, sourceLineNumber);
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
