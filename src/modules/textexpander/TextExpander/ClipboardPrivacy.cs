// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// The clipboard formats that mark content as not-for-keeping.
///
/// <para>
/// Windows treats the clipboard as something to remember and to move around: Clipboard History
/// keeps the last 25 entries, and Cloud Clipboard uploads them to the user's Microsoft account so
/// they arrive on their other machines. Both apply to anything published without an opt-out —
/// which is what this app was doing for every single expansion. A signature, an address, an
/// account number: all of it, synced off the device, because the app happened to paste rather
/// than type. Restoring the user's clipboard afterwards does not undo that; by then the history
/// service has already taken its copy.
/// </para>
///
/// <para>
/// The worst case is <c>{{clipboard}}</c>. A password manager copies a secret and marks it
/// excluded precisely so it is not retained; this app then reads it and republishes it as an
/// ordinary, unmarked clipboard entry, stripping the protection its owner asked for. Setting
/// these formats on our own write is what stops that.
/// </para>
///
/// <para>
/// <b>Honest limitation:</b> these are conventions, not enforcement. Windows' own history and
/// cloud sync honour them, and well-behaved clipboard managers do too, but a third-party manager
/// is free to ignore them and capture the content anyway. Nothing published to the clipboard can
/// be made truly private. Users who need that guarantee should set
/// <c>POWERTOYS_TEXT_EXPANDER_INJECTION_BACKEND=type</c>, which keeps expansions off the clipboard entirely.
/// </para>
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
    /// The payload each exclusion format carries: a little-endian <c>DWORD</c> zero, meaning
    /// "no". <c>ExcludeClipboardContentFromMonitorProcessing</c> is documented as ignoring its
    /// data, so the same four bytes serve for all three.
    /// </summary>
    public static byte[] DenyPayload() => [0, 0, 0, 0];
}
