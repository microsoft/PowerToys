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

    /// <summary>
    /// Whether the user's clipboard should get the exclusion formats when it is put back. Windows takes the
    /// restore for a new copy, so without them every pasted expansion would add the user's last copy to Clipboard
    /// History again, sync it to Cloud Clipboard again and have clipboard managers log it again, though it was
    /// recorded when it was copied. An empty clipboard stays empty, and one that already carries any of the
    /// formats goes back with the markings it came with.
    /// </summary>
    /// <param name="formatOf">The registered format number of each of <see cref="FormatNames"/>.</param>
    public static bool ShouldMarkRestore(IReadOnlyList<ClipboardEntry> entries, Func<string, uint> formatOf)
    {
        if (entries.Count == 0)
        {
            return false;
        }

        foreach (string name in FormatNames)
        {
            uint format = formatOf(name);
            foreach (ClipboardEntry entry in entries)
            {
                if (entry.Format == format)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
