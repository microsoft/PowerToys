// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;

namespace ScreenTranslator.Core.Ocr;

/// <summary>
/// Default detector checking Microsoft.Windows.AI.Imaging.TextRecognizer ready states on modern Copilot+ / NPU systems.
/// </summary>
public sealed class WindowsAiCapabilityDetector : IOcrCapabilityDetector
{
    public bool IsWindowsAiSupported()
    {
        try
        {
            var readyState = TextRecognizer.GetReadyState();
            return readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady;
        }
        catch (Exception ex)
        {
            Logger.LogInfo($"Windows AI TextRecognizer capability probe: not supported ({ex.Message}).");
            return false;
        }
    }

    public bool IsWindowsAiReady()
    {
        try
        {
            return TextRecognizer.GetReadyState() == AIFeatureReadyState.Ready;
        }
        catch (Exception ex)
        {
            Logger.LogInfo($"Windows AI TextRecognizer ready probe: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> EnsureWindowsAiReadyAsync()
    {
        try
        {
            var readyState = TextRecognizer.GetReadyState();
            if (readyState == AIFeatureReadyState.Ready)
            {
                return true;
            }

            if (readyState == AIFeatureReadyState.NotReady)
            {
                Logger.LogInfo("Windows AI TextRecognizer is NotReady; calling EnsureReadyAsync()...");
                var operation = await TextRecognizer.EnsureReadyAsync();
                if (operation.Status == AIFeatureReadyResultState.Success)
                {
                    Logger.LogInfo("Windows AI TextRecognizer EnsureReadyAsync succeeded.");
                    return true;
                }

                Logger.LogWarning($"Windows AI TextRecognizer EnsureReadyAsync finished with status: {operation.Status}, error: {operation.ErrorDisplayText}");
                return false;
            }

            return false;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Exception during EnsureWindowsAiReadyAsync: {ex.Message}");
            return false;
        }
    }
}
