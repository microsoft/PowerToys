// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Properties;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper
{
    /// <summary>
    /// Helper class to easier work with results
    /// </summary>
    internal static class ResultHelper
    {
        /// <summary>
        /// Return a list with <see cref="Result"/>s, based on the given list.
        /// </summary>
        /// <param name="list">The original result list to convert.</param>
        /// <param name="iconPath">The path to the icon of each entry.</param>
        /// <returns>A list with <see cref="Result"/>.</returns>
        internal static List<Result> GetResultList(
            in IEnumerable<WindowsSetting> list,
            string query,
            in string iconPath)
        {
            var resultList = new List<Result>(list.Count());

            foreach (var entry in list)
            {
                var result = new Result
                {
                    Action = (_) => DoOpenSettingsAction(entry),
                    IcoPath = iconPath,
                    SubTitle = entry.JoinedFullSettingsPath,
                    Title = entry.Name,
                    ContextData = entry,
                };

                AddOptionalToolTip(entry, result);

                resultList.Add(result);
            }

            SetScores(resultList, query);

            return resultList;
        }

        /// <summary>
        /// Add a tool-tip to the given <see cref="Result"/>, based o the given <see cref="IWindowsSetting"/>.
        /// </summary>
        /// <param name="entry">The <see cref="WindowsSetting"/> that contain informations for the tool-tip.</param>
        /// <param name="result">The <see cref="Result"/> that need a tool-tip.</param>
        private static void AddOptionalToolTip(WindowsSetting entry, Result result)
        {
            var toolTipText = new StringBuilder();

            toolTipText.AppendLine($"{Resources.Application}: {entry.Type}");

            if (entry.Areas != null && entry.Areas.Any())
            {
                toolTipText.AppendLine($"{Resources.Area}: {entry.JoinedAreaPath}");
            }

            if (entry.AltNames != null && entry.AltNames.Any())
            {
                var altList = entry.AltNames.Aggregate((current, next) => $"{current}, {next}");

                toolTipText.AppendLine($"{Resources.AlternativeName}: {altList}");
            }

            toolTipText.Append($"{Resources.Command}: {entry.Command}");

            if (!string.IsNullOrEmpty(entry.Note))
            {
                toolTipText.AppendLine(string.Empty);
                toolTipText.AppendLine(string.Empty);
                toolTipText.Append($"{Resources.Note}: {entry.Note}");
            }

            result.ToolTipData = new ToolTipData(entry.Name, toolTipText.ToString());
        }

        /// <summary>
        /// Open the settings page of the given <see cref="IWindowsSetting"/>.
        /// </summary>
        /// <param name="entry">The <see cref="WindowsSetting"/> that contain the information to open the setting on command level.</param>
        /// <returns><see langword="true"/> if the settings could be opened, otherwise <see langword="false"/>.</returns>
        private static bool DoOpenSettingsAction(WindowsSetting entry)
        {
            ProcessStartInfo processStartInfo;

            var command = entry.Command;

            if (command.Contains("%windir%", StringComparison.InvariantCultureIgnoreCase))
            {
                var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                command = command.Replace("%windir%", windowsFolder, StringComparison.InvariantCultureIgnoreCase);
            }

            if (command.Contains(' '))
            {
                var commandSplit = command.Split(' ');
                var file = commandSplit.FirstOrDefault();
                var arguments = command[file.Length..].TrimStart();

                processStartInfo = new ProcessStartInfo(file, arguments)
                {
                    UseShellExecute = false,
                };
            }
            else
            {
                processStartInfo = new ProcessStartInfo(command)
                {
                    UseShellExecute = true,
                };
            }

            try
            {
                Process.Start(processStartInfo);
                return true;
            }
            catch (Exception exception)
            {
                Log.Exception("can't open settings", exception, typeof(ResultHelper));
                return false;
            }
        }

        /// <summary>
        /// Set the score (known as order number or ranking number)
        /// for all <see cref="Results"/> in the given list, based on the given query.
        /// </summary>
        /// <param name="resultList">A list with <see cref="Result"/>s that need scores.</param>
        /// <param name="query">The query to calculated the score for the <see cref="Result"/>s.</param>
        private static void SetScores(IEnumerable<Result> resultList, string query)
        {
            var lowScore = 1_000;
            var mediumScore = 5_000;
            var highScore = 10_000;

            foreach (var result in resultList)
            {
                if (!(result.ContextData is WindowsSetting windowsSetting))
                {
                    continue;
                }

                if (windowsSetting.Name.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
                {
                    result.Score = highScore--;
                    continue;
                }

                // If query starts with second or next word of name, set score.
                if (windowsSetting.Name.Contains($" {query}", StringComparison.CurrentCultureIgnoreCase))
                {
                    result.Score = mediumScore--;
                    continue;
                }

                if (windowsSetting.Areas is null)
                {
                    result.Score = lowScore--;
                    continue;
                }

                if (windowsSetting.Areas.Any(x => x.StartsWith(query, StringComparison.CurrentCultureIgnoreCase)))
                {
                    result.Score = lowScore--;
                    continue;
                }

                if (windowsSetting.AltNames is null)
                {
                    result.Score = lowScore--;
                    continue;
                }

                if (windowsSetting.AltNames.Any(x => x.StartsWith(query, StringComparison.CurrentCultureIgnoreCase)))
                {
                    result.Score = mediumScore--;
                    continue;
                }

                result.Score = lowScore--;
            }
        }

        /// <summary>
        /// Checks if a setting <see cref="WindowsSetting"/> matches the search string <see cref="Query.Search"/> to filter settings by settings path.
        /// This method is called from the <see cref="Predicate{T}"/> method in <see cref="Main.Query(Query)"/> if the search string <see cref="Query.Search"/> contains the character ">".
        /// </summary>
        /// <param name="found">The WindowsSetting's result that should be checked.</param>
        /// <param name="queryString">The searchString entered by the user <see cref="Query.Search"/>s.</param>
        internal static bool FilterBySettingsPath(in WindowsSetting found, in string queryString)
        {
            if (!queryString.Contains('>'))
            {
                return false;
            }

            var queryElements = queryString.Split('>');

            // check app name of the WindowsSetting
            if (!found.Type.StartsWith(queryElements[0], StringComparison.CurrentCultureIgnoreCase))
            {
                return false;
            }

            // Check area path of the WindowsSetting.
            if (!string.IsNullOrEmpty(queryElements[1]))
            {
                if (found.Areas is null)
                {
                    // The user entered at least one area name (queryElements[1]). But the WindowsSetting has no defined areas.
                    return false;
                }

                if ((queryElements.Length - 1) > found.Areas.Count())
                {
                    // The user entered more area names (queryElements[1] -> queryElements[n]) than defined for this WindowsSetting.
                    return false;
                }

                int areaCounter = 0;
                foreach (var qElement in queryElements.Skip(1))
                {
                    if (!found.Areas[areaCounter].StartsWith(qElement, StringComparison.CurrentCultureIgnoreCase))
                    {
                        // The user has entered an area name that not matches.
                        return false;
                    }

                    areaCounter++;
                }
            }

            // Return "true" if <found> matches <queryString>.
            return true;
        }
    }
}
