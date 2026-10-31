// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>What can be done about one clipboard format when borrowing the clipboard.</summary>
internal enum ClipboardFormatHandling
{
    /// <summary>Plain memory the snapshot can copy byte for byte and publish again.</summary>
    Copy,

    /// <summary>
    /// A GDI handle Windows re-synthesises from a device-independent bitmap. Safe to leave out
    /// of the snapshot, but only when a DIB is actually present to synthesise it from.
    /// </summary>
    SynthesizedFromDib,

    /// <summary>
    /// A handle or application-owned resource we can't reproduce, so the clipboard isn't taken at all.
    /// </summary>
    Unreproducible,
}

/// <summary>Decides what a clipboard snapshot can put back; free of Win32 so the rule is testable.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Stylistically, clipboard formats use Win32 SDK names.")]
internal static class ClipboardFormats
{
    public const uint CF_TEXT = 1;
    public const uint CF_BITMAP = 2;
    public const uint CF_METAFILEPICT = 3;
    public const uint CF_DIB = 8;
    public const uint CF_PALETTE = 9;
    public const uint CF_UNICODETEXT = 13;
    public const uint CF_ENHMETAFILE = 14;
    public const uint CF_HDROP = 15;
    public const uint CF_LOCALE = 16;
    public const uint CF_DIBV5 = 17;
    public const uint CF_OWNERDISPLAY = 0x0080;
    public const uint CF_DSPBITMAP = 0x0082;
    public const uint CF_DSPMETAFILEPICT = 0x0083;
    public const uint CF_DSPENHMETAFILE = 0x008E;

    // Handles in these ranges belong to the application that published them.
    public const uint CF_PRIVATEFIRST = 0x0200;
    public const uint CF_PRIVATELAST = 0x02FF;
    public const uint CF_GDIOBJFIRST = 0x0300;
    public const uint CF_GDIOBJLAST = 0x03FF;

    public static ClipboardFormatHandling Classify(uint format) => format switch
    {
        // Bitmap handles Windows synthesizes from CF_DIB; the caller checks that a DIB is present.
        CF_BITMAP or CF_DSPBITMAP => ClipboardFormatHandling.SynthesizedFromDib,

        // Metafile and palette handles (CF_METAFILEPICT's memory holds a handle too) and owner-drawn
        // content can't be copied as bytes.
        CF_METAFILEPICT or CF_ENHMETAFILE or CF_DSPMETAFILEPICT or CF_DSPENHMETAFILE
            or CF_PALETTE or CF_OWNERDISPLAY => ClipboardFormatHandling.Unreproducible,

        >= CF_PRIVATEFIRST and <= CF_PRIVATELAST => ClipboardFormatHandling.Unreproducible,
        >= CF_GDIOBJFIRST and <= CF_GDIOBJLAST => ClipboardFormatHandling.Unreproducible,

        // Everything else — the text formats, CF_DIB/CF_DIBV5, CF_HDROP, CF_LOCALE and every
        // registered format such as HTML Format or "Preferred DropEffect" — is an HGLOBAL.
        _ => ClipboardFormatHandling.Copy,
    };

    /// <summary>
    /// Works out which of <paramref name="formats"/> a snapshot must copy to put the clipboard back exactly.
    /// Returns null, a refusal, when something can't be reproduced; an empty result means the clipboard was
    /// empty and restoring should clear it.
    /// </summary>
    public static IReadOnlyList<uint>? PlanCapture(IReadOnlyList<uint> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);

        bool hasDib = false;
        foreach (uint format in formats)
        {
            if (format is CF_DIB or CF_DIBV5)
            {
                hasDib = true;
                break;
            }
        }

        var capture = new List<uint>(formats.Count);
        foreach (uint format in formats)
        {
            switch (Classify(format))
            {
                case ClipboardFormatHandling.Copy:
                    capture.Add(format);
                    break;

                case ClipboardFormatHandling.SynthesizedFromDib:
                    if (!hasDib)
                    {
                        return null;
                    }

                    break;

                default:
                    return null;
            }
        }

        return capture;
    }
}
