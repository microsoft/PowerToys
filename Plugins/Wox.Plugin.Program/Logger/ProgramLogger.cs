using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;
using Wox.Infrastructure;

namespace Wox.Plugin.Program.Logger
{
    /// <summary>
    /// The Program plugin has seen many issues recorded in the Wox repo related to various loading of Windows programs.
    /// This is a dedicated logger for this Program plugin with the aim to output a more friendlier message and clearer
    /// log that will allow debugging to be quicker and easier.
    /// </summary>
    internal static class ProgramLogger
    {
        public const string DirectoryName = "Logs";

        static ProgramLogger()
        {
            var path = Path.Combine(Constant.DataDirectory, DirectoryName, Constant.Version);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var configuration = new LoggingConfiguration();
            var target = new FileTarget();
            configuration.AddTarget("file", target);
            target.FileName = path.Replace(@"\", "/") + "/${shortdate}.txt";
#if DEBUG
            var rule = new LoggingRule("*", LogLevel.Debug, target);
#else
            var rule = new LoggingRule("*", LogLevel.Error, target);
#endif
            configuration.LoggingRules.Add(rule);
            LogManager.Configuration = configuration;
        }

        /// <summary>
        /// Please follow exception format: |class name|calling method name|loading program path|user friendly message that explains the error
        /// => Example: |Win32|LnkProgram|c:\..\chrome.exe|Permission denied on directory, but Wox should continue
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        internal static void LogException(string message, Exception e)
        {
            //Index 0 is always empty.
            var parts = message.Split('|');
            var classname = parts[1];
            var callingMethodName = parts[2];
            var loadingProgramPath = parts[3];
            var interpretationMessage = parts[4];

            Debug.WriteLine($"ERROR{message}");

            var logger = LogManager.GetLogger("");

            var innerExceptionNumber = 1;

            var possibleResolution = "Not yet known";
            var errorStatus = "UNKNOWN";

            logger.Error("------------- BEGIN Wox.Plugin.Program exception -------------");

            do
            {
                if (IsKnownWinProgramError(e, callingMethodName) || IsKnownUWPProgramError(e, callingMethodName))
                {
                    possibleResolution = "Can be ignored and Wox should still continue, however the program may not be loaded";
                    errorStatus = "KNOWN";
                }

                var calledMethod = e.TargetSite != null ? e.TargetSite.ToString() : e.StackTrace;

                calledMethod = string.IsNullOrEmpty(calledMethod) ? "Not available" : calledMethod;

                logger.Error($"\nException full name: {e.GetType().FullName}"
                                + $"\nError status: {errorStatus}"
                                + $"\nClass name: {classname}"
                                + $"\nCalling method: {callingMethodName}"
                                + $"\nProgram path: {loadingProgramPath}"
                                + $"\nInnerException number: {innerExceptionNumber}"
                                + $"\nException message: {e.Message}"
                                + $"\nException error type: HResult {e.HResult}"
                                + $"\nException thrown in called method: {calledMethod}"
                                + $"\nPossible interpretation of the error: {interpretationMessage}"
                                + $"\nPossible resolution: {possibleResolution}");

                innerExceptionNumber++;
                e = e.InnerException;
            } while (e != null);

            logger.Error("------------- END Wox.Plugin.Program exception -------------");
        }

        private static bool IsKnownWinProgramError(Exception e, string callingMethodName)
        {
            if (e.TargetSite?.Name == "GetDescription" && callingMethodName == "LnkProgram")
                return true;

            if (e is SecurityException || e is UnauthorizedAccessException || e is DirectoryNotFoundException)
                return true;

            return false;
        }

        private static bool IsKnownUWPProgramError(Exception e, string callingMethodName)
        {
            if (((e.HResult == -2147024774 || e.HResult == -2147009769) && callingMethodName == "ResourceFromPri")
                || (e.HResult == -2147024894 && (callingMethodName == "LogoPathFromUri" || callingMethodName == "ImageFromPath")))
                return true;

            if (callingMethodName == "XmlNamespaces")
                return true;

            return false;
        }
    }
}