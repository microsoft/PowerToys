// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace ScreenTranslator.Core.Ocr;

/// <summary>
/// Probes and prepares Windows AI / NPU OCR capabilities.
/// </summary>
public interface IOcrCapabilityDetector
{
    bool IsWindowsAiSupported();

    bool IsWindowsAiReady();

    Task<bool> EnsureWindowsAiReadyAsync();
}
