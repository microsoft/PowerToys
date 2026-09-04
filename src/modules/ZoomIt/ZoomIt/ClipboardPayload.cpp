//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Publishes a snip to the clipboard in multiple formats at once.
//
//==============================================================================
#include "pch.h"
#include "ClipboardPayload.h"
#include <chrono>

// Defined in Zoomit.cpp
int GetEncoderClsid( const WCHAR* format, CLSID* pClsid );
void OutputDebug( const TCHAR* format, ... );

namespace
{
    const wchar_t* const c_tempSubdirectory = L"ZoomIt";
    const wchar_t* const c_tempFilePrefix = L"ZoomIt-snip-";

    //
    // Registered clipboard formats are process-wide and only need to be
    // registered once.
    //
    UINT HtmlClipboardFormat()
    {
        static UINT format = RegisterClipboardFormat( L"HTML Format" );
        return format;
    }

    UINT PngClipboardFormat()
    {
        static UINT format = RegisterClipboardFormat( L"PNG" );
        return format;
    }

    UINT FileNameWClipboardFormat()
    {
        static UINT format = RegisterClipboardFormat( L"FileNameW" );
        return format;
    }

    UINT RtfClipboardFormat()
    {
        static UINT format = RegisterClipboardFormat( L"Rich Text Format" );
        return format;
    }

    //
    // Copies a block of memory into a moveable HGLOBAL suitable for
    // SetClipboardData. Returns nullptr on failure.
    //
    HGLOBAL CreateClipboardBlock( const void* data, size_t size )
    {
        if( size == 0 )
        {
            return nullptr;
        }

        HGLOBAL block = GlobalAlloc( GMEM_MOVEABLE, size );
        if( block == nullptr )
        {
            return nullptr;
        }

        void* destination = GlobalLock( block );
        if( destination == nullptr )
        {
            GlobalFree( block );
            return nullptr;
        }

        memcpy( destination, data, size );
        GlobalUnlock( block );
        return block;
    }

    //
    // Publishes a block, freeing it if the clipboard refuses to take ownership.
    //
    bool PublishBlock( UINT format, HGLOBAL block )
    {
        if( block == nullptr )
        {
            return false;
        }

        if( SetClipboardData( format, block ) == nullptr )
        {
            GlobalFree( block );
            return false;
        }
        return true;
    }

    bool PublishBytes( UINT format, const void* data, size_t size )
    {
        return PublishBlock( format, CreateClipboardBlock( data, size ) );
    }

    std::string Utf8FromWide( const std::wstring& text )
    {
        if( text.empty() )
        {
            return {};
        }

        int size = WideCharToMultiByte( CP_UTF8, 0, text.c_str(), static_cast<int>(text.size()),
                                        nullptr, 0, nullptr, nullptr );
        if( size <= 0 )
        {
            return {};
        }

        std::string result( static_cast<size_t>(size), '\0' );
        WideCharToMultiByte( CP_UTF8, 0, text.c_str(), static_cast<int>(text.size()),
                             result.data(), size, nullptr, nullptr );
        return result;
    }

    std::string HtmlEscape( const std::string& text )
    {
        std::string result;
        result.reserve( text.size() );
        for( char character : text )
        {
            switch( character )
            {
            case '&': result += "&amp;"; break;
            case '<': result += "&lt;"; break;
            case '>': result += "&gt;"; break;
            case '"': result += "&quot;"; break;
            case '\n': result += "<br>"; break;
            case '\r': break;
            default: result += character; break;
            }
        }
        return result;
    }

    //
    // Formats a CF_HTML header offset, which is a fixed width decimal number.
    //
    void PatchOffset( std::string& html, const char* marker, size_t offset )
    {
        size_t position = html.find( marker );
        if( position == std::string::npos )
        {
            return;
        }

        position += strlen( marker );
        char value[11];
        sprintf_s( value, "%010zu", offset );
        html.replace( position, 10, value );
    }

    //
    // Returns %TEMP%\ZoomIt, creating it if necessary. Empty on failure.
    //
    std::filesystem::path SnipTempDirectory()
    {
        wchar_t tempPath[MAX_PATH + 1]{};
        if( GetTempPathW( ARRAYSIZE( tempPath ), tempPath ) == 0 )
        {
            return {};
        }

        std::error_code error;
        std::filesystem::path directory = std::filesystem::path( tempPath ) / c_tempSubdirectory;
        std::filesystem::create_directories( directory, error );
        if( error )
        {
            return {};
        }
        return directory;
    }

    //
    // Writes the PNG bytes to a uniquely named file in the temp directory and
    // returns its path, or an empty path on failure.
    //
    std::filesystem::path WriteTempPng( const std::vector<BYTE>& pngBytes )
    {
        std::filesystem::path directory = SnipTempDirectory();
        if( directory.empty() )
        {
            return {};
        }

        SYSTEMTIME now{};
        GetLocalTime( &now );
        wchar_t name[MAX_PATH]{};
        swprintf_s( name, L"%s%04u%02u%02u-%02u%02u%02u-%03u.png", c_tempFilePrefix,
                    now.wYear, now.wMonth, now.wDay, now.wHour, now.wMinute, now.wSecond, now.wMilliseconds );

        std::filesystem::path file = directory / name;
        std::ofstream stream( file, std::ios::binary | std::ios::trunc );
        if( !stream.is_open() )
        {
            return {};
        }

        stream.write( reinterpret_cast<const char*>(pngBytes.data()), static_cast<std::streamsize>(pngBytes.size()) );
        if( !stream.good() )
        {
            stream.close();
            std::error_code error;
            std::filesystem::remove( file, error );
            return {};
        }
        return file;
    }

    //
    // Builds a CF_DIB block describing the bitmap.
    //
    HGLOBAL CreateDibBlock( HBITMAP bitmap )
    {
        BITMAP bitmapInfo{};
        if( GetObject( bitmap, sizeof( bitmapInfo ), &bitmapInfo ) == 0 )
        {
            return nullptr;
        }

        BITMAPINFOHEADER header{};
        header.biSize = sizeof( BITMAPINFOHEADER );
        header.biWidth = bitmapInfo.bmWidth;
        header.biHeight = bitmapInfo.bmHeight;
        header.biPlanes = 1;
        header.biBitCount = 32;
        header.biCompression = BI_RGB;

        HDC screenDc = GetDC( nullptr );
        if( screenDc == nullptr )
        {
            return nullptr;
        }
        auto releaseDc = wil::scope_exit( [screenDc] { ReleaseDC( nullptr, screenDc ); } );

        // Query the required size for the pixel data.
        BITMAPINFO bitmapInfoHeader{};
        bitmapInfoHeader.bmiHeader = header;
        if( GetDIBits( screenDc, bitmap, 0, static_cast<UINT>(bitmapInfo.bmHeight),
                       nullptr, &bitmapInfoHeader, DIB_RGB_COLORS ) == 0 )
        {
            return nullptr;
        }

        header = bitmapInfoHeader.bmiHeader;
        header.biCompression = BI_RGB;
        if( header.biSizeImage == 0 )
        {
            header.biSizeImage = static_cast<DWORD>(((bitmapInfo.bmWidth * 32 + 31) / 32) * 4 * bitmapInfo.bmHeight);
        }

        size_t totalSize = sizeof( BITMAPINFOHEADER ) + header.biSizeImage;
        HGLOBAL block = GlobalAlloc( GMEM_MOVEABLE, totalSize );
        if( block == nullptr )
        {
            return nullptr;
        }

        BYTE* destination = static_cast<BYTE*>(GlobalLock( block ));
        if( destination == nullptr )
        {
            GlobalFree( block );
            return nullptr;
        }

        memcpy( destination, &header, sizeof( header ) );
        bitmapInfoHeader.bmiHeader = header;
        if( GetDIBits( screenDc, bitmap, 0, static_cast<UINT>(bitmapInfo.bmHeight),
                       destination + sizeof( header ), &bitmapInfoHeader, DIB_RGB_COLORS ) == 0 )
        {
            GlobalUnlock( block );
            GlobalFree( block );
            return nullptr;
        }

        GlobalUnlock( block );
        return block;
    }

    //
    // Builds a CF_HDROP block naming a single file.
    //
    HGLOBAL CreateDropBlock( const std::wstring& filePath )
    {
        size_t size = sizeof( DROPFILES ) + sizeof( wchar_t ) * ( filePath.size() + 2 );
        HGLOBAL block = GlobalAlloc( GHND, size );
        if( block == nullptr )
        {
            return nullptr;
        }

        DROPFILES* dropFiles = static_cast<DROPFILES*>(GlobalLock( block ));
        if( dropFiles == nullptr )
        {
            GlobalFree( block );
            return nullptr;
        }

        dropFiles->pFiles = sizeof( DROPFILES );
        dropFiles->fWide = TRUE;
        wcscpy_s( reinterpret_cast<wchar_t*>(&dropFiles[1]), filePath.size() + 1, filePath.c_str() );
        GlobalUnlock( block );
        return block;
    }
}

//----------------------------------------------------------------------------
//
// SnipClipboardInternal::Base64Encode
//
//----------------------------------------------------------------------------
std::string SnipClipboardInternal::Base64Encode( const BYTE* data, size_t size )
{
    static const char* const alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    std::string result;
    result.reserve( ( ( size + 2 ) / 3 ) * 4 );

    size_t index = 0;
    while( index + 2 < size )
    {
        const uint32_t triple = ( static_cast<uint32_t>(data[index]) << 16 ) |
                                ( static_cast<uint32_t>(data[index + 1]) << 8 ) |
                                static_cast<uint32_t>(data[index + 2]);
        result += alphabet[( triple >> 18 ) & 0x3F];
        result += alphabet[( triple >> 12 ) & 0x3F];
        result += alphabet[( triple >> 6 ) & 0x3F];
        result += alphabet[triple & 0x3F];
        index += 3;
    }

    const size_t remaining = size - index;
    if( remaining == 1 )
    {
        const uint32_t triple = static_cast<uint32_t>(data[index]) << 16;
        result += alphabet[( triple >> 18 ) & 0x3F];
        result += alphabet[( triple >> 12 ) & 0x3F];
        result += "==";
    }
    else if( remaining == 2 )
    {
        const uint32_t triple = ( static_cast<uint32_t>(data[index]) << 16 ) |
                                ( static_cast<uint32_t>(data[index + 1]) << 8 );
        result += alphabet[( triple >> 18 ) & 0x3F];
        result += alphabet[( triple >> 12 ) & 0x3F];
        result += alphabet[( triple >> 6 ) & 0x3F];
        result += '=';
    }

    return result;
}

//----------------------------------------------------------------------------
//
// SnipClipboardInternal::EncodeBitmapToPng
//
// Encodes an HBITMAP to PNG bytes with GDI+, matching SavePng's encoder.
//
//----------------------------------------------------------------------------
bool SnipClipboardInternal::EncodeBitmapToPng( HBITMAP bitmap, std::vector<BYTE>& pngBytes )
{
    pngBytes.clear();
    if( bitmap == nullptr )
    {
        return false;
    }

    CLSID pngClsid{};
    if( GetEncoderClsid( L"image/png", &pngClsid ) < 0 )
    {
        return false;
    }

    wil::com_ptr<IStream> stream;
    if( FAILED( CreateStreamOnHGlobal( nullptr, TRUE, stream.put() ) ) )
    {
        return false;
    }

    {
        Gdiplus::Bitmap gdiBitmap( bitmap, nullptr );
        if( gdiBitmap.GetLastStatus() != Gdiplus::Ok )
        {
            return false;
        }

        if( gdiBitmap.Save( stream.get(), &pngClsid, nullptr ) != Gdiplus::Ok )
        {
            return false;
        }
    }

    STATSTG statistics{};
    if( FAILED( stream->Stat( &statistics, STATFLAG_NONAME ) ) )
    {
        return false;
    }

    const ULONGLONG size = statistics.cbSize.QuadPart;
    if( size == 0 || size > MAXDWORD )
    {
        return false;
    }

    LARGE_INTEGER origin{};
    if( FAILED( stream->Seek( origin, STREAM_SEEK_SET, nullptr ) ) )
    {
        return false;
    }

    pngBytes.resize( static_cast<size_t>(size) );
    ULONG read = 0;
    if( FAILED( stream->Read( pngBytes.data(), static_cast<ULONG>(size), &read ) ) || read != size )
    {
        pngBytes.clear();
        return false;
    }

    return true;
}

//----------------------------------------------------------------------------
//
// SnipClipboardInternal::BuildHtmlClipboardFormat
//
// Builds a CF_HTML payload carrying the annotation text followed by the image
// as a data URI, so that rich editors receive both in a single paste.
//
//----------------------------------------------------------------------------
std::string SnipClipboardInternal::BuildHtmlClipboardFormat( const std::wstring& text,
                                                             const std::vector<BYTE>& pngBytes )
{
    std::string fragment;
    if( !text.empty() )
    {
        fragment += "<p>";
        fragment += HtmlEscape( Utf8FromWide( text ) );
        fragment += "</p>";
    }

    if( !pngBytes.empty() )
    {
        fragment += "<img src=\"data:image/png;base64,";
        fragment += Base64Encode( pngBytes.data(), pngBytes.size() );
        fragment += "\">";
    }

    if( fragment.empty() )
    {
        return {};
    }

    std::string html =
        "Version:0.9\r\n"
        "StartHTML:0000000000\r\n"
        "EndHTML:0000000000\r\n"
        "StartFragment:0000000000\r\n"
        "EndFragment:0000000000\r\n"
        "<html><body>\r\n"
        "<!--StartFragment-->";

    const size_t startHtml = html.find( "<html>" );
    const size_t startFragment = html.size();
    html += fragment;
    const size_t endFragment = html.size();
    html += "<!--EndFragment-->\r\n</body></html>";
    const size_t endHtml = html.size();

    PatchOffset( html, "StartHTML:", startHtml );
    PatchOffset( html, "EndHTML:", endHtml );
    PatchOffset( html, "StartFragment:", startFragment );
    PatchOffset( html, "EndFragment:", endFragment );

    return html;
}

//----------------------------------------------------------------------------
//
// SnipClipboardInternal::BuildRtfClipboardFormat
//
// Builds a "Rich Text Format" payload carrying the annotation text followed by
// the image as an embedded PNG. Word, Outlook, WordPad and other RTF aware
// editors accept this when they ignore CF_HTML.
//
//----------------------------------------------------------------------------
std::string SnipClipboardInternal::BuildRtfClipboardFormat( const std::wstring& text,
                                                            const std::vector<BYTE>& pngBytes,
                                                            int widthPixels, int heightPixels )
{
    if( text.empty() && pngBytes.empty() )
    {
        return {};
    }

    std::string rtf = "{\\rtf1\\ansi\\ansicpg1252\\deff0{\\fonttbl{\\f0\\fnil Segoe UI;}}\\f0\\fs20 ";

    for( const wchar_t ch : text )
    {
        switch( ch )
        {
        case L'\\': rtf += "\\\\"; break;
        case L'{': rtf += "\\{"; break;
        case L'}': rtf += "\\}"; break;
        case L'\r': break;
        case L'\n': rtf += "\\par "; break;
        default:
            if( ch < 0x80 )
            {
                rtf += static_cast<char>(ch);
            }
            else
            {
                // RTF signed 16 bit unicode escape with an ASCII fallback.
                char escape[24]{};
                sprintf_s( escape, "\\u%d?", static_cast<int>(static_cast<short>(ch)) );
                rtf += escape;
            }
            break;
        }
    }

    if( !pngBytes.empty() && widthPixels > 0 && heightPixels > 0 )
    {
        if( !text.empty() )
        {
            rtf += "\\par ";
        }

        // RTF picture dimensions are expressed in twips (1/1440 inch) assuming
        // the usual 96 DPI logical resolution.
        char header[192]{};
        sprintf_s( header, "{\\pict\\pngblip\\picw%d\\pich%d\\picwgoal%d\\pichgoal%d ",
                   widthPixels, heightPixels,
                   widthPixels * 15, heightPixels * 15 );
        rtf += header;

        static const char* const c_hex = "0123456789abcdef";
        rtf.reserve( rtf.size() + pngBytes.size() * 2 + 32 );
        for( size_t i = 0; i < pngBytes.size(); ++i )
        {
            rtf += c_hex[pngBytes[i] >> 4];
            rtf += c_hex[pngBytes[i] & 0x0F];
        }

        rtf += '}';
    }

    rtf += '}';
    return rtf;
}

//----------------------------------------------------------------------------
//
// PublishSnipClipboard
//
//----------------------------------------------------------------------------
bool PublishSnipClipboard( HWND owner, HBITMAP bitmap, const std::wstring& text, DWORD formats )
{
    const bool hasText = !text.empty();
    if( bitmap == nullptr && !hasText )
    {
        return false;
    }

    // Encode before taking the clipboard so it is held for as short a time as
    // possible. PNG encoding is only needed by the image, HTML and file formats.
    std::vector<BYTE> pngBytes;
    const bool needPng = bitmap != nullptr &&
                         ( formats & ( SNIP_CLIPBOARD_IMAGE | SNIP_CLIPBOARD_HTML |
                                       SNIP_CLIPBOARD_FILE | SNIP_CLIPBOARD_RTF ) ) != 0;
    if( needPng )
    {
        SnipClipboardInternal::EncodeBitmapToPng( bitmap, pngBytes );
    }

    std::wstring tempFile;
    if( ( formats & SNIP_CLIPBOARD_FILE ) && !pngBytes.empty() )
    {
        tempFile = WriteTempPng( pngBytes ).wstring();
    }

    std::string html;
    if( formats & SNIP_CLIPBOARD_HTML )
    {
        html = SnipClipboardInternal::BuildHtmlClipboardFormat(
            hasText ? text : std::wstring{},
            ( formats & SNIP_CLIPBOARD_IMAGE ) ? pngBytes : std::vector<BYTE>{} );
    }

    std::string rtf;
    if( formats & SNIP_CLIPBOARD_RTF )
    {
        BITMAP info{};
        const bool haveInfo = bitmap != nullptr &&
                              GetObject( bitmap, sizeof( info ), &info ) == sizeof( info );
        rtf = SnipClipboardInternal::BuildRtfClipboardFormat(
            hasText ? text : std::wstring{},
            ( formats & SNIP_CLIPBOARD_IMAGE ) ? pngBytes : std::vector<BYTE>{},
            haveInfo ? info.bmWidth : 0,
            haveInfo ? std::abs( info.bmHeight ) : 0 );
    }

    // A copy is placed on the clipboard so the caller keeps ownership of its
    // bitmap and can continue to use or delete it.
    wil::unique_hbitmap bitmapCopy;
    if( bitmap != nullptr && ( formats & SNIP_CLIPBOARD_IMAGE ) )
    {
        bitmapCopy.reset( static_cast<HBITMAP>(CopyImage( bitmap, IMAGE_BITMAP, 0, 0, 0 )) );
    }

    if( !OpenClipboard( owner ) )
    {
        OutputDebug( L"[Snip] OpenClipboard failed err=%lu\n", GetLastError() );
        return false;
    }

    EmptyClipboard();

    bool published = false;

    // The combined formats are published first: applications that enumerate the
    // clipboard in order and take the first format they understand then receive
    // the text and the picture together rather than only one of them.
    if( !html.empty() )
    {
        published |= PublishBytes( HtmlClipboardFormat(), html.c_str(), html.size() + 1 );
    }

    if( !rtf.empty() )
    {
        published |= PublishBytes( RtfClipboardFormat(), rtf.c_str(), rtf.size() + 1 );
    }

    if( ( formats & SNIP_CLIPBOARD_IMAGE ) && !pngBytes.empty() )
    {
        published |= PublishBytes( PngClipboardFormat(), pngBytes.data(), pngBytes.size() );
    }

    if( ( formats & SNIP_CLIPBOARD_IMAGE ) && bitmap != nullptr )
    {
        published |= PublishBlock( CF_DIB, CreateDibBlock( bitmap ) );
    }

    if( bitmapCopy )
    {
        if( SetClipboardData( CF_BITMAP, bitmapCopy.get() ) != nullptr )
        {
            // The clipboard owns the bitmap now.
            bitmapCopy.release();
            published = true;
        }
    }

    if( ( formats & SNIP_CLIPBOARD_FILE ) && !tempFile.empty() )
    {
        published |= PublishBlock( CF_HDROP, CreateDropBlock( tempFile ) );
        published |= PublishBytes( FileNameWClipboardFormat(), tempFile.c_str(),
                                   ( tempFile.size() + 1 ) * sizeof( wchar_t ) );
    }

    // Plain text is published last so that it is the final fallback.
    if( ( formats & SNIP_CLIPBOARD_TEXT ) && hasText )
    {
        published |= PublishBytes( CF_UNICODETEXT, text.c_str(), ( text.size() + 1 ) * sizeof( wchar_t ) );
    }

    CloseClipboard();

    OutputDebug( L"[Snip] Clipboard published formats=0x%08x text=%d png=%zu rtf=%zu file=%d result=%d\n",
                 formats, hasText ? 1 : 0, pngBytes.size(), rtf.size(), tempFile.empty() ? 0 : 1, published ? 1 : 0 );

    return published;
}

//----------------------------------------------------------------------------
//
// CleanupSnipTempFiles
//
// Removes snip PNGs older than a day so the temporary directory does not grow
// without bound. The most recent file is always kept because it may still be
// referenced by the clipboard.
//
//----------------------------------------------------------------------------
void CleanupSnipTempFiles()
{
    std::error_code error;
    std::filesystem::path directory = SnipTempDirectory();
    if( directory.empty() )
    {
        return;
    }

    const auto now = std::filesystem::file_time_type::clock::now();
    const auto maximumAge = std::chrono::hours( 24 );

    for( const auto& entry : std::filesystem::directory_iterator( directory, error ) )
    {
        if( error )
        {
            return;
        }

        if( !entry.is_regular_file( error ) )
        {
            continue;
        }

        const std::wstring name = entry.path().filename().wstring();
        if( name.rfind( c_tempFilePrefix, 0 ) != 0 )
        {
            continue;
        }

        const auto written = entry.last_write_time( error );
        if( error )
        {
            error.clear();
            continue;
        }

        if( now - written > maximumAge )
        {
            std::filesystem::remove( entry.path(), error );
            error.clear();
        }
    }
}
