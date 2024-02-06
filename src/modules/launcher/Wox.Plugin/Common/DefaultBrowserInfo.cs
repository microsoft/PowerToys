// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using Wox.Plugin.Common.Win32;
using Wox.Plugin.Logger;

namespace Wox.Plugin.Common
{
    /// <summary>
    /// Contains information (e.g. path to executable, name...) about the default browser.
    /// </summary>
    public static class DefaultBrowserInfo
    {
        private static readonly object _updateLock = new object();

        /// <summary>Gets the path to the MS Edge browser executable.</summary>
        public static string MSEdgePath =>
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) +
            @"\Microsoft\Edge\Application\msedge.exe";

        /// <summary>Gets the command line pattern of the MS Edge.</summary>
        public static string MSEdgeArgumentsPattern => "--single-argument %1";

        public const string MSEdgeName = "Microsoft Edge";

        /// <summary>Gets the path to default browser's executable.</summary>
        public static string Path { get; private set; }

        /// <summary>Gets <see cref="Path"/> since the icon is embedded in the executable.</summary>
        public static string IconPath => Path;

        /// <summary>Gets the user-friendly name of the default browser.</summary>
        public static string Name { get; private set; }

        /// <summary>Gets the command line pattern of the default browser.</summary>
        public static string ArgumentsPattern { get; private set; }

        public static bool IsDefaultBrowserSet { get => !string.IsNullOrEmpty(Path); }

        public const long UpdateTimeout = 300;

        private static long _lastUpdateTickCount = -UpdateTimeout;

        private static bool haveIRanUpdateOnce;

        /// <summary>
        /// Updates only if at least more than 300ms has passed since the last update, to avoid multiple calls to <see cref="Update"/>.
        /// (because of multiple plugins calling update at the same time.)
        /// </summary>
        public static void UpdateIfTimePassed()
        {
            long curTickCount = Environment.TickCount64;
            if (curTickCount - _lastUpdateTickCount >= UpdateTimeout)
            {
                _lastUpdateTickCount = curTickCount;
                Update();
            }
        }

        /// <summary>
        /// Consider using <see cref="UpdateIfTimePassed"/> to avoid updating multiple times.
        /// (because of multiple plugins calling update at the same time.)
        /// </summary>
        public static void Update()
        {
            lock (_updateLock)
            {
                if (!haveIRanUpdateOnce)
                {
                    Log.Warn("I've tried updating the chosen Web Browser info at least once.", typeof(DefaultBrowserInfo));
                    haveIRanUpdateOnce = true;
                }

                try
                {
                    string progId = GetRegistryValue(
                        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice",
                        "ProgId");
                    string appName = GetRegistryValue($@"HKEY_CLASSES_ROOT\{progId}\Application", "ApplicationName")
                        ?? GetRegistryValue($@"HKEY_CLASSES_ROOT\{progId}", "FriendlyTypeName");

                    if (appName != null)
                    {
                        // Handle indirect strings:
                        if (appName.StartsWith('@'))
                        {
                            appName = GetIndirectString(appName);
                        }

                        appName = appName
                            .Replace("URL", null, StringComparison.OrdinalIgnoreCase)
                            .Replace("HTML", null, StringComparison.OrdinalIgnoreCase)
                            .Replace("Document", null, StringComparison.OrdinalIgnoreCase)
                            .Replace("Web", null, StringComparison.OrdinalIgnoreCase)
                            .TrimEnd();
                    }

                    Name = appName;

                    string commandPattern = GetRegistryValue($@"HKEY_CLASSES_ROOT\{progId}\shell\open\command", null);

                    if (string.IsNullOrEmpty(commandPattern))
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(commandPattern),
                            "Default browser program command is not specified.");
                    }

                    if (commandPattern.StartsWith('@'))
                    {
                        commandPattern = GetIndirectString(commandPattern);
                    }

                    // HACK: for firefox installed through Microsoft store
                    // When installed through Microsoft Firefox the commandPattern does not have
                    // quotes for the path. As the Program Files does have a space
                    // the extracted path would be invalid, here we add the quotes to fix it
                    const string FirefoxExecutableName = "firefox.exe";
                    if (commandPattern.Contains(FirefoxExecutableName) && commandPattern.Contains(@"\WindowsApps\") && (!commandPattern.StartsWith('\"')))
                    {
                        var pathEndIndex = commandPattern.IndexOf(FirefoxExecutableName, StringComparison.Ordinal) + FirefoxExecutableName.Length;
                        commandPattern = commandPattern.Insert(pathEndIndex, "\"");
                        commandPattern = commandPattern.Insert(0, "\"");
                    }

                    if (commandPattern.StartsWith('\"'))
                    {
                        var endQuoteIndex = commandPattern.IndexOf('\"', 1);
                        if (endQuoteIndex != -1)
                        {
                            Path = commandPattern.Substring(1, endQuoteIndex - 1);
                            ArgumentsPattern = commandPattern.Substring(endQuoteIndex + 1).Trim();
                        }
                    }
                    else
                    {
                        var spaceIndex = commandPattern.IndexOf(' ');
                        if (spaceIndex != -1)
                        {
                            Path = commandPattern.Substring(0, spaceIndex);
                            ArgumentsPattern = commandPattern.Substring(spaceIndex + 1).Trim();
                        }
                    }

                    if (string.IsNullOrEmpty(Path))
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(Path),
                            "Default browser program path could not be determined.");
                    }
                }
                catch (Exception e)
                {
                    // fallback to MS Edge
                    Path = MSEdgePath;
                    Name = MSEdgeName;
                    ArgumentsPattern = MSEdgeArgumentsPattern;
                    Log.Exception("Exception when retrieving browser path/name. Path and Name are set to use Microsoft Edge.", e, typeof(DefaultBrowserInfo));
                }

                string GetRegistryValue(string registryLocation, string valueName)
                {
                    return Microsoft.Win32.Registry.GetValue(registryLocation, valueName, null) as string;
                }

                string GetIndirectString(string str)
                {
                    var stringBuilder = new StringBuilder(128);
                    if (NativeMethods.SHLoadIndirectString(
                            str,
                            stringBuilder,
                            (uint)stringBuilder.Capacity,
                            IntPtr.Zero)
                        == HRESULT.S_OK)
                    {
                        return stringBuilder.ToString();
                    }

                    throw new ArgumentNullException(nameof(str), "Could not load indirect string.");
                }
            }
        }
    }
}
