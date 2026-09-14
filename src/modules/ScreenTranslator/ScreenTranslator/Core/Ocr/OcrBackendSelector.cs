// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using ManagedCommon;

namespace ScreenTranslator.Core.Ocr;

/// <summary>
/// Capability-based OCR backend selector.
/// Prefers Microsoft.Windows.AI.Imaging.TextRecognizer when available/ready, falling back to Windows.Media.Ocr.
/// </summary>
public sealed class OcrBackendSelector
{
    private readonly IOcrCapabilityDetector _capabilityDetector;
    private readonly IOcrBackend? _injectedAiBackend;
    private readonly IOcrBackend _legacyBackend;
    private readonly object _lock = new();
    private IOcrBackend? _selectedBackend;

    public IOcrBackend? ActiveBackend => _selectedBackend;

    public OcrBackendSelector(
        IOcrCapabilityDetector? capabilityDetector = null,
        IOcrBackend? aiBackend = null,
        IOcrBackend? legacyBackend = null)
    {
        _capabilityDetector = capabilityDetector ?? new WindowsAiCapabilityDetector();
        _injectedAiBackend = aiBackend;
        _legacyBackend = legacyBackend ?? new WindowsMediaOcrBackend();
    }

    public async Task<IOcrBackend> GetOrSelectBackendAsync()
    {
        lock (_lock)
        {
            if (_selectedBackend != null)
            {
                return _selectedBackend;
            }
        }

        IOcrBackend chosen;
        try
        {
            bool aiEligible = false;
            if (_capabilityDetector.IsWindowsAiReady())
            {
                aiEligible = true;
            }
            else if (_capabilityDetector.IsWindowsAiSupported() && await _capabilityDetector.EnsureWindowsAiReadyAsync().ConfigureAwait(false))
            {
                aiEligible = true;
            }

            if (aiEligible)
            {
                if (_injectedAiBackend != null)
                {
                    chosen = _injectedAiBackend;
                }
                else
                {
                    var createdAi = await WindowsAiTextRecognizerOcrBackend.TryCreateAsync().ConfigureAwait(false);
                    chosen = createdAi ?? _legacyBackend;
                }
            }
            else
            {
                chosen = _legacyBackend;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Exception probing Windows AI OCR backend: {ex.Message}. Falling back to standard OCR.");
            chosen = _legacyBackend;
        }

        lock (_lock)
        {
            if (_selectedBackend == null)
            {
                _selectedBackend = chosen;
                Logger.LogInfo($"ScreenTranslator selected OCR backend: {chosen.BackendName}");
            }

            return _selectedBackend;
        }
    }
}
