// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;

using FancyZonesEditorCommon.Data;
using FancyZonesEditorCommon.Utils;

namespace FancyZonesCLI.Utils;

/// <summary>
/// Helper for requesting FancyZones to save editor-parameters.json and reading it reliably.
/// </summary>
internal static class EditorParametersRefresh
{
    public static EditorParameters.ParamsWrapper ReadEditorParametersWithRefresh(Action requestSave)
    {
        const int maxWaitMilliseconds = 500;
        const int pollIntervalMilliseconds = 50;

        string filePath = FancyZonesPaths.EditorParameters;
        DateTime lastWriteBefore = File.Exists(filePath) ? File.GetLastWriteTimeUtc(filePath) : DateTime.MinValue;

        requestSave();

        int elapsedMilliseconds = 0;
        while (elapsedMilliseconds < maxWaitMilliseconds)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    DateTime lastWriteNow = File.GetLastWriteTimeUtc(filePath);

                    // Prefer reading after the file is updated, but don't block forever if the
                    // timestamp resolution is coarse or FancyZones rewrites identical content.
                    if (lastWriteNow >= lastWriteBefore || elapsedMilliseconds > 100)
                    {
                        var editorParams = FancyZonesDataIO.ReadEditorParameters();
                        if (editorParams.Monitors != null && editorParams.Monitors.Count > 0)
                        {
                            return editorParams;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                // File may be mid-write/locked or temporarily invalid JSON; retry.
            }

            Thread.Sleep(pollIntervalMilliseconds);
            elapsedMilliseconds += pollIntervalMilliseconds;
        }

        var finalParams = FancyZonesDataIO.ReadEditorParameters();
        if (finalParams.Monitors == null || finalParams.Monitors.Count == 0)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, Properties.Resources.editor_params_timeout, maxWaitMilliseconds, Path.GetFileName(filePath)));
        }

        return finalParams;
    }
}
