// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// The clipboard formats that mark content as not-for-keeping, so pasted expansions (including
/// <c>{{clipboard}}</c> content a password manager marked excluded) stay out of Clipboard History and Cloud
/// Clipboard. These are conventions, not enforcement: a third-party clipboard manager may ignore them, and
/// only the typing backend (<c>POWERTOYS_TEXT_EXPANDER_INJECTION_BACKEND=type</c>) keeps expansions off the clipboard.
/// </summary>
internal static class ClipboardPrivacy
{
    /// <summary>Asks clipboard monitors not to process this content at all.</summary>
    public const string ExcludeFromMonitorProcessing = "ExcludeClipboardContentFromMonitorProcessing";

    /// <summary>A DWORD 0 keeps the content out of Clipboard History (Win+V).</summary>
    public const string CanIncludeInClipboardHistory = "CanIncludeInClipboardHistory";

    /// <summary>A DWORD 0 keeps the content from being uploaded to Cloud Clipboard.</summary>
    public const string CanUploadToCloudClipboard = "CanUploadToCloudClipboard";

    /// <summary>Gets every exclusion format, in the order they should be published.</summary>
    public static IReadOnlyList<string> FormatNames { get; } =
    [
        ExcludeFromMonitorProcessing,
        CanIncludeInClipboardHistory,
        CanUploadToCloudClipboard,
    ];

    /// <summary>
    /// A little-endian DWORD zero for every exclusion format (the monitor-processing format ignores its data).
    /// </summary>
    public static byte[] DenyPayload() => [0, 0, 0, 0];
}
