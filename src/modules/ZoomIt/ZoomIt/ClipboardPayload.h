//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Publishes a snip to the clipboard in multiple formats at once, so that a
// single paste can carry both the captured image and an associated text
// annotation into whichever format the target application prefers.
//
//==============================================================================
#pragma once

#include "pch.h"
#include <string>
#include <vector>

//
// Bit mask selecting which clipboard formats to publish. The values are
// persisted in the ZoomIt settings, so do not renumber them.
//
enum SnipClipboardFormats : DWORD
{
    SNIP_CLIPBOARD_IMAGE = 0x01, // CF_BITMAP + CF_DIB + "PNG"
    SNIP_CLIPBOARD_TEXT = 0x02,  // CF_UNICODETEXT
    SNIP_CLIPBOARD_HTML = 0x04,  // CF_HTML with the text and an inline image
    SNIP_CLIPBOARD_FILE = 0x08,  // CF_HDROP + "FileNameW" referencing a temp PNG
    SNIP_CLIPBOARD_RTF = 0x10,   // "Rich Text Format" with the text and an embedded image

    SNIP_CLIPBOARD_ALL = SNIP_CLIPBOARD_IMAGE | SNIP_CLIPBOARD_TEXT |
                         SNIP_CLIPBOARD_HTML | SNIP_CLIPBOARD_FILE |
                         SNIP_CLIPBOARD_RTF,
};

//
// Publishes bitmap and/or text to the clipboard.
//
// The caller retains ownership of bitmap: everything placed on the clipboard is
// a copy. Formats that have no content (for example SNIP_CLIPBOARD_TEXT with an
// empty text) are silently skipped, so passing an empty text yields exactly the
// same clipboard contents as a plain image copy.
//
// Returns false only if the clipboard could not be opened or nothing at all
// could be published.
//
bool PublishSnipClipboard( HWND owner, HBITMAP bitmap, const std::wstring& text,
                           DWORD formats = SNIP_CLIPBOARD_ALL );

//
// Deletes snip PNGs left in the temporary directory by earlier sessions.
// Safe to call at any time; failures are ignored.
//
void CleanupSnipTempFiles();

//
// Exposed for unit tests.
//
namespace SnipClipboardInternal
{
    std::string Base64Encode( const BYTE* data, size_t size );
    std::string BuildHtmlClipboardFormat( const std::wstring& text, const std::vector<BYTE>& pngBytes );
    std::string BuildRtfClipboardFormat( const std::wstring& text, const std::vector<BYTE>& pngBytes,
                                         int widthPixels, int heightPixels );
    bool EncodeBitmapToPng( HBITMAP bitmap, std::vector<BYTE>& pngBytes );
}
