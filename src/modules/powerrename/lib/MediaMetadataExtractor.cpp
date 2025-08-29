#include "pch.h"
#include "MediaMetadataExtractor.h"
#include <fstream>
#include <vector>
#include <sstream>
#include <iomanip>
#include <comdef.h>
#include <mutex>
#include <unordered_map>
#include <chrono>
#include <algorithm>

using namespace PowerRenameLib;

namespace
{
    // WIC metadata property paths - comprehensive mapping
    const std::map<std::wstring, std::wstring> COMMON_EXIF_PATHS = {
        {L"Make", L"/app1/ifd/{ushort=271}"},
        {L"Model", L"/app1/ifd/{ushort=272}"},
        {L"DateTime", L"/app1/ifd/{ushort=306}"},
        {L"DateTimeOriginal", L"/app1/ifd/exif/{ushort=36867}"},
        {L"DateTimeDigitized", L"/app1/ifd/exif/{ushort=36868}"},
        {L"ISO", L"/app1/ifd/exif/{ushort=34855}"},
        {L"FNumber", L"/app1/ifd/exif/{ushort=33437}"},
        {L"ExposureTime", L"/app1/ifd/exif/{ushort=33434}"},
        {L"FocalLength", L"/app1/ifd/exif/{ushort=37386}"},
        {L"ExposureBias", L"/app1/ifd/exif/{ushort=37380}"},
        {L"WhiteBalance", L"/app1/ifd/exif/{ushort=37384}"},
        {L"Flash", L"/app1/ifd/exif/{ushort=37385}"},
        {L"Orientation", L"/app1/ifd/{ushort=274}"},
        {L"XResolution", L"/app1/ifd/{ushort=282}"},
        {L"YResolution", L"/app1/ifd/{ushort=283}"},
        {L"Software", L"/app1/ifd/{ushort=305}"},
        {L"Artist", L"/app1/ifd/{ushort=315}"},
        {L"Copyright", L"/app1/ifd/{ushort=33432}"},
        {L"ColorSpace", L"/app1/ifd/exif/{ushort=40961}"},
        {L"PixelXDimension", L"/app1/ifd/exif/{ushort=40962}"},
        {L"PixelYDimension", L"/app1/ifd/exif/{ushort=40963}"},
        {L"SceneCaptureType", L"/app1/ifd/exif/{ushort=41990}"},
        {L"MeteringMode", L"/app1/ifd/exif/{ushort=37383}"},
        {L"LightSource", L"/app1/ifd/exif/{ushort=37384}"}
    };

    const std::map<std::wstring, std::wstring> GPS_PATHS = {
        {L"GPSLatitude", L"/app1/ifd/gps/{ushort=2}"},
        {L"GPSLatitudeRef", L"/app1/ifd/gps/{ushort=1}"},
        {L"GPSLongitude", L"/app1/ifd/gps/{ushort=4}"},
        {L"GPSLongitudeRef", L"/app1/ifd/gps/{ushort=3}"},
        {L"GPSAltitude", L"/app1/ifd/gps/{ushort=6}"},
        {L"GPSAltitudeRef", L"/app1/ifd/gps/{ushort=5}"},
        {L"GPSTimeStamp", L"/app1/ifd/gps/{ushort=7}"},
        {L"GPSDateStamp", L"/app1/ifd/gps/{ushort=29}"}
    };

    const std::map<std::wstring, std::wstring> IPTC_PATHS = {
        {L"Title", L"/app13/irb/8bimiptc/iptc/object name"},
        {L"Caption", L"/app13/irb/8bimiptc/iptc/caption"},
        {L"Keywords", L"/app13/irb/8bimiptc/iptc/keywords"},
        {L"Category", L"/app13/irb/8bimiptc/iptc/category"},
        {L"Credit", L"/app13/irb/8bimiptc/iptc/credit"},
        {L"Source", L"/app13/irb/8bimiptc/iptc/source"},
        {L"Byline", L"/app13/irb/8bimiptc/iptc/by-line"},
        {L"BylineTitle", L"/app13/irb/8bimiptc/iptc/by-line title"},
        {L"City", L"/app13/irb/8bimiptc/iptc/city"},
        {L"ProvinceState", L"/app13/irb/8bimiptc/iptc/province or state"},
        {L"CountryName", L"/app13/irb/8bimiptc/iptc/country or primary location name"}
    };

    // Helper function to convert PROPVARIANT to MetadataValue
    WICMetadataExtractor::MetadataValue PropVariantToMetadataValue(const PROPVARIANT& pv)
    {
        switch (pv.vt)
        {
        case VT_LPWSTR:
            return pv.pwszVal ? std::wstring(pv.pwszVal) : std::wstring{};
        case VT_BSTR:
            return pv.bstrVal ? std::wstring(pv.bstrVal) : std::wstring{};
        case VT_LPSTR:
            if (pv.pszVal)
            {
                int size_needed = MultiByteToWideChar(CP_UTF8, 0, pv.pszVal, -1, NULL, 0);
                if (size_needed > 0)
                {
                    std::wstring result(static_cast<size_t>(size_needed) - 1, 0);
                    MultiByteToWideChar(CP_UTF8, 0, pv.pszVal, -1, &result[0], size_needed);
                    return result;
                }
            }
            return std::wstring{};
        case VT_I1:
            return static_cast<int32_t>(pv.cVal);
        case VT_I2:
            return static_cast<int32_t>(pv.iVal);
        case VT_I4:
            return pv.lVal;
        case VT_UI1:
            return static_cast<uint32_t>(pv.bVal);
        case VT_UI2:
            return static_cast<uint32_t>(pv.uiVal);
        case VT_UI4:
            return pv.ulVal;
        case VT_R4:
            return static_cast<double>(pv.fltVal);
        case VT_R8:
            return pv.dblVal;
        case VT_BOOL:
            return pv.boolVal != VARIANT_FALSE;
        case VT_UI1 | VT_VECTOR:
            if (pv.caub.pElems && pv.caub.cElems > 0)
            {
                return std::vector<uint8_t>(pv.caub.pElems, pv.caub.pElems + pv.caub.cElems);
            }
            return std::vector<uint8_t>{};
        default:
            // Try to convert to string as fallback
            PWSTR pszValue = nullptr;
            if (SUCCEEDED(PropVariantToStringAlloc(pv, &pszValue)) && pszValue)
            {
                std::wstring result = pszValue;
                CoTaskMemFree(pszValue);
                return result;
            }
            return std::wstring{};
        }
    }

    // Cache for metadata to improve performance
    class MetadataCache
    {
    private:
        mutable std::mutex m_mutex;
        std::unordered_map<std::wstring, WICMetadataExtractor::ImageInfo> m_cache;
        bool m_enabled = true;
        size_t m_maxSize = 100; // Maximum cached items

    public:
        void Put(const std::wstring& filePath, const WICMetadataExtractor::ImageInfo& info)
        {
            if (!m_enabled) return;

            std::lock_guard<std::mutex> lock(m_mutex);
            if (m_cache.size() >= m_maxSize)
            {
                // Simple LRU: remove first item
                m_cache.erase(m_cache.begin());
            }
            m_cache[filePath] = info;
        }

        std::optional<WICMetadataExtractor::ImageInfo> Get(const std::wstring& filePath) const
        {
            if (!m_enabled) return std::nullopt;

            std::lock_guard<std::mutex> lock(m_mutex);
            auto it = m_cache.find(filePath);
            return (it != m_cache.end()) ? std::make_optional(it->second) : std::nullopt;
        }

        void Clear()
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            m_cache.clear();
        }

        void SetEnabled(bool enabled)
        {
            m_enabled = enabled;
        }

        size_t Size() const
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            return m_cache.size();
        }
    };
}

// PIMPL implementation
class WICMetadataExtractor::Impl
{
private:
    bool m_comInitialized = false;
    CComPtr<IWICImagingFactory> m_pWicFactory;
    MetadataCache m_cache;

public:
    Impl()
    {
        // Initialize COM
        HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
        if (SUCCEEDED(hr))
        {
            m_comInitialized = true;
        }

        // Create WIC factory
        hr = CoCreateInstance(
            CLSID_WICImagingFactory,
            nullptr,
            CLSCTX_INPROC_SERVER,
            IID_IWICImagingFactory,
            reinterpret_cast<LPVOID*>(&m_pWicFactory)
        );

        if (FAILED(hr))
        {
            m_pWicFactory = nullptr;
        }
    }

    ~Impl()
    {
        m_pWicFactory = nullptr;
        if (m_comInitialized)
        {
            CoUninitialize();
        }
    }

    bool IsWicInitialized() const
    {
        return GetWicFactory() != nullptr;
    }

    MetadataCache& GetCache() { return m_cache; }
    IWICImagingFactory* GetWicFactory() const { return m_pWicFactory; }

    std::optional<ImageInfo> ExtractImageInfo(const std::wstring& filePath, const ExtractionOptions& options)
    {
        if (!IsWicInitialized())
        {
            return std::nullopt;
        }

        // Check cache first
        if (options.cacheMetadata)
        {
            auto cached = m_cache.Get(filePath);
            if (cached.has_value())
            {
                return cached;
            }
        }

        try
        {
            CComPtr<IWICBitmapDecoder> pDecoder;
            HRESULT hr = GetWicFactory()->CreateDecoderFromFilename(
                filePath.c_str(),
                nullptr,
                GENERIC_READ,
                WICDecodeMetadataCacheOnLoad,
                &pDecoder
            );

            if (FAILED(hr))
            {
                return std::nullopt;
            }

            ImageInfo info;

            // Get container format
            GUID containerFormat;
            if (SUCCEEDED(pDecoder->GetContainerFormat(&containerFormat)))
            {
                info.containerFormat = GetFormatNameFromGuid(containerFormat);
            }

            // Get the first frame
            CComPtr<IWICBitmapFrameDecode> pFrame;
            hr = pDecoder->GetFrame(0, &pFrame);
            if (FAILED(hr))
            {
                return std::nullopt;
            }

            // Get basic image properties
            hr = pFrame->GetSize(&info.width, &info.height);
            if (FAILED(hr))
            {
                return std::nullopt;
            }

            // Get pixel format
            WICPixelFormatGUID pixelFormat;
            if (SUCCEEDED(pFrame->GetPixelFormat(&pixelFormat)))
            {
                info.pixelFormat = GetPixelFormatName(pixelFormat);
                
                CComPtr<IWICComponentInfo> pComponentInfo;
                hr = GetWicFactory()->CreateComponentInfo(pixelFormat, &pComponentInfo);
                if (SUCCEEDED(hr))
                {
                    CComPtr<IWICPixelFormatInfo> pPixelFormatInfo;
                    hr = pComponentInfo->QueryInterface(IID_IWICPixelFormatInfo, reinterpret_cast<void**>(&pPixelFormatInfo));
                    if (SUCCEEDED(hr))
                    {
                        hr = pPixelFormatInfo->GetBitsPerPixel(&info.bitsPerPixel);
                    }
                }
            }

            // Get metadata reader
            CComPtr<IWICMetadataQueryReader> pQueryReader;
            hr = pFrame->GetMetadataQueryReader(&pQueryReader);
            if (SUCCEEDED(hr))
            {
                // Extract EXIF metadata
                if (options.includeExif)
                {
                    ExtractMetadataGroup(pQueryReader, COMMON_EXIF_PATHS, info.exifData);
                }

                // Extract GPS metadata
                if (options.includeGps)
                {
                    ExtractMetadataGroup(pQueryReader, GPS_PATHS, info.gpsData);
                }

                // Extract IPTC metadata
                if (options.includeIptc)
                {
                    ExtractMetadataGroup(pQueryReader, IPTC_PATHS, info.iptcData);
                }

                // Extract XMP metadata (if available)
                if (options.includeXmp)
                {
                    ExtractXmpMetadata(pQueryReader, info.xmpData);
                }

                // Extract all available metadata paths for comprehensive coverage
                if (options.includeCustom)
                {
                    ExtractAllAvailableMetadata(pQueryReader, info.customData);
                }
            }

            // Cache the result
            if (options.cacheMetadata)
            {
                m_cache.Put(filePath, info);
            }

            return info;
        }
        catch (...)
        {
            return std::nullopt;
        }
    }

    std::optional<FormatInfo> GetFormatInfo(const std::wstring& filePath)
    {
        if (!IsWicInitialized())
        {
            return std::nullopt;
        }

        try
        {
            CComPtr<IWICBitmapDecoder> pDecoder;
            HRESULT hr = GetWicFactory()->CreateDecoderFromFilename(
                filePath.c_str(),
                nullptr,
                GENERIC_READ,
                WICDecodeMetadataCacheOnDemand,
                &pDecoder
            );

            if (FAILED(hr))
            {
                return std::nullopt;
            }

            FormatInfo formatInfo;
            
            GUID containerFormat;
            if (SUCCEEDED(pDecoder->GetContainerFormat(&containerFormat)))
            {
                formatInfo.formatName = GetFormatNameFromGuid(containerFormat);
                formatInfo.fileExtensions = GetFileExtensionsFromGuid(containerFormat);
                formatInfo.mimeTypes = GetMimeTypesFromGuid(containerFormat);
            }

            // Check if metadata is supported
            CComPtr<IWICBitmapFrameDecode> pFrame;
            hr = pDecoder->GetFrame(0, &pFrame);
            if (SUCCEEDED(hr))
            {
                CComPtr<IWICMetadataQueryReader> pQueryReader;
                formatInfo.supportsMetadata = SUCCEEDED(pFrame->GetMetadataQueryReader(&pQueryReader));
            }

            // Check multi-frame support
            UINT frameCount = 0;
            if (SUCCEEDED(pDecoder->GetFrameCount(&frameCount)))
            {
                formatInfo.supportsMultiFrame = frameCount > 1;
            }

            return formatInfo;
        }
        catch (...)
        {
            return std::nullopt;
        }
    }

    std::vector<FormatInfo> GetSupportedFormats()
    {
        std::vector<FormatInfo> formats;
        
        if (!IsWicInitialized())
        {
            return formats;
        }

        try
        {
            CComPtr<IEnumUnknown> pEnum;
            HRESULT hr = GetWicFactory()->CreateComponentEnumerator(
                WICDecoder,
                WICComponentEnumerateDefault,
                &pEnum
            );

            if (FAILED(hr))
            {
                return formats;
            }

            IUnknown* pUnknown = nullptr;
            ULONG fetched = 0;
            
            while (pEnum->Next(1, &pUnknown, &fetched) == S_OK && fetched == 1)
            {
                CComPtr<IWICBitmapDecoderInfo> pDecoderInfo;
                hr = pUnknown->QueryInterface(IID_IWICBitmapDecoderInfo, reinterpret_cast<void**>(&pDecoderInfo));
                pUnknown->Release();
                
                if (SUCCEEDED(hr))
                {
                    FormatInfo formatInfo;
                    
                    UINT length = 0;
                    hr = pDecoderInfo->GetFriendlyName(0, nullptr, &length);
                    if (SUCCEEDED(hr) && length > 0)
                    {
                        std::vector<WCHAR> buffer(length);
                        hr = pDecoderInfo->GetFriendlyName(length, buffer.data(), &length);
                        if (SUCCEEDED(hr))
                        {
                            formatInfo.formatName = buffer.data();
                        }
                    }

                    // Get file extensions
                    length = 0;
                    hr = pDecoderInfo->GetFileExtensions(0, nullptr, &length);
                    if (SUCCEEDED(hr) && length > 0)
                    {
                        std::vector<WCHAR> buffer(length);
                        hr = pDecoderInfo->GetFileExtensions(length, buffer.data(), &length);
                        if (SUCCEEDED(hr))
                        {
                            formatInfo.fileExtensions = buffer.data();
                        }
                    }

                    // Get MIME types
                    length = 0;
                    hr = pDecoderInfo->GetMimeTypes(0, nullptr, &length);
                    if (SUCCEEDED(hr) && length > 0)
                    {
                        std::vector<WCHAR> buffer(length);
                        hr = pDecoderInfo->GetMimeTypes(length, buffer.data(), &length);
                        if (SUCCEEDED(hr))
                        {
                            std::wstring mimeTypesStr = buffer.data();
                            // Split by comma
                            std::wstringstream ss(mimeTypesStr);
                            std::wstring item;
                            while (std::getline(ss, item, L','))
                            {
                                // Trim whitespace
                                item.erase(0, item.find_first_not_of(L" \t"));
                                item.erase(item.find_last_not_of(L" \t") + 1);
                                if (!item.empty())
                                {
                                    formatInfo.mimeTypes.push_back(item);
                                }
                            }
                        }
                    }

                    formatInfo.supportsMetadata = true; // Assume metadata support for now
                    formatInfo.supportsMultiFrame = false; // Will be determined per file

                    formats.push_back(formatInfo);
                }
            }
        }
        catch (...)
        {
            // Return partial results on error
        }

        return formats;
    }

    bool HasMetadataType(const std::wstring& filePath, const std::wstring& metadataType)
    {
        if (!IsWicInitialized())
        {
            return false;
        }

        try
        {
            CComPtr<IWICBitmapDecoder> pDecoder;
            HRESULT hr = GetWicFactory()->CreateDecoderFromFilename(
                filePath.c_str(),
                nullptr,
                GENERIC_READ,
                WICDecodeMetadataCacheOnDemand,
                &pDecoder
            );

            if (FAILED(hr))
            {
                return false;
            }

            CComPtr<IWICBitmapFrameDecode> pFrame;
            hr = pDecoder->GetFrame(0, &pFrame);
            if (FAILED(hr))
            {
                return false;
            }

            CComPtr<IWICMetadataQueryReader> pQueryReader;
            hr = pFrame->GetMetadataQueryReader(&pQueryReader);
            if (FAILED(hr))
            {
                return false;
            }

            // Check specific metadata type paths
            if (metadataType == L"exif")
            {
                PROPVARIANT pv;
                PropVariantInit(&pv);
                bool hasExif = SUCCEEDED(pQueryReader->GetMetadataByName(L"/app1/ifd/exif", &pv));
                PropVariantClear(&pv);
                return hasExif;
            }
            else if (metadataType == L"iptc")
            {
                PROPVARIANT pv;
                PropVariantInit(&pv);
                bool hasIptc = SUCCEEDED(pQueryReader->GetMetadataByName(L"/app13/irb/8bimiptc", &pv));
                PropVariantClear(&pv);
                return hasIptc;
            }
            else if (metadataType == L"xmp")
            {
                PROPVARIANT pv;
                PropVariantInit(&pv);
                bool hasXmp = SUCCEEDED(pQueryReader->GetMetadataByName(L"/xmp", &pv));
                PropVariantClear(&pv);
                return hasXmp;
            }
            else if (metadataType == L"gps")
            {
                PROPVARIANT pv;
                PropVariantInit(&pv);
                bool hasGps = SUCCEEDED(pQueryReader->GetMetadataByName(L"/app1/ifd/gps", &pv));
                PropVariantClear(&pv);
                return hasGps;
            }

            return false;
        }
        catch (...)
        {
            return false;
        }
    }

    std::optional<MetadataValue> GetMetadataByPath(const std::wstring& filePath, const std::wstring& propertyPath)
    {
        if (!IsWicInitialized())
        {
            return std::nullopt;
        }

        try
        {
            CComPtr<IWICBitmapDecoder> pDecoder;
            HRESULT hr = GetWicFactory()->CreateDecoderFromFilename(
                filePath.c_str(),
                nullptr,
                GENERIC_READ,
                WICDecodeMetadataCacheOnDemand,
                &pDecoder
            );

            if (FAILED(hr))
            {
                return std::nullopt;
            }

            CComPtr<IWICBitmapFrameDecode> pFrame;
            hr = pDecoder->GetFrame(0, &pFrame);
            if (FAILED(hr))
            {
                return std::nullopt;
            }

            CComPtr<IWICMetadataQueryReader> pQueryReader;
            hr = pFrame->GetMetadataQueryReader(&pQueryReader);
            if (FAILED(hr))
            {
                return std::nullopt;
            }

            PROPVARIANT pv;
            PropVariantInit(&pv);
            hr = pQueryReader->GetMetadataByName(propertyPath.c_str(), &pv);
            
            if (SUCCEEDED(hr))
            {
                auto result = PropVariantToMetadataValue(pv);
                PropVariantClear(&pv);
                return result;
            }

            PropVariantClear(&pv);
            return std::nullopt;
        }
        catch (...)
        {
            return std::nullopt;
        }
    }

private:
    void ExtractMetadataGroup(CComPtr<IWICMetadataQueryReader> pQueryReader,
                             const std::map<std::wstring, std::wstring>& paths,
                             std::map<std::wstring, MetadataValue>& output)
    {
        for (const auto& [key, path] : paths)
        {
            PROPVARIANT pv;
            PropVariantInit(&pv);
            if (SUCCEEDED(pQueryReader->GetMetadataByName(path.c_str(), &pv)))
            {
                output[key] = PropVariantToMetadataValue(pv);
            }
            PropVariantClear(&pv);
        }
    }

    void ExtractXmpMetadata(CComPtr<IWICMetadataQueryReader> pQueryReader,
                           std::map<std::wstring, MetadataValue>& output)
    {
        // XMP metadata extraction - simplified implementation
        // In a full implementation, you would parse the XMP XML
        PROPVARIANT pv;
        PropVariantInit(&pv);
        if (SUCCEEDED(pQueryReader->GetMetadataByName(L"/xmp", &pv)))
        {
            output[L"XMP_Raw"] = PropVariantToMetadataValue(pv);
        }
        PropVariantClear(&pv);
    }

    void ExtractAllAvailableMetadata(CComPtr<IWICMetadataQueryReader> pQueryReader,
                                    std::map<std::wstring, MetadataValue>& output)
    {
        // This would enumerate all available metadata paths
        // For now, we'll add some common additional paths
        std::vector<std::wstring> additionalPaths = {
            L"/app1/ifd/{ushort=256}",  // ImageWidth
            L"/app1/ifd/{ushort=257}",  // ImageLength
            L"/app1/ifd/{ushort=258}",  // BitsPerSample
            L"/app1/ifd/{ushort=259}",  // Compression
            L"/app1/ifd/{ushort=262}",  // PhotometricInterpretation
            L"/app1/ifd/{ushort=277}",  // SamplesPerPixel
        };

        for (const auto& path : additionalPaths)
        {
            PROPVARIANT pv;
            PropVariantInit(&pv);
            if (SUCCEEDED(pQueryReader->GetMetadataByName(path.c_str(), &pv)))
            {
                output[path] = PropVariantToMetadataValue(pv);
            }
            PropVariantClear(&pv);
        }
    }

    std::wstring GetFormatNameFromGuid(const GUID& guid)
    {
        // Map common format GUIDs to friendly names
        if (guid == GUID_ContainerFormatJpeg) return L"JPEG";
        if (guid == GUID_ContainerFormatPng) return L"PNG";
        if (guid == GUID_ContainerFormatGif) return L"GIF";
        if (guid == GUID_ContainerFormatBmp) return L"BMP";
        if (guid == GUID_ContainerFormatTiff) return L"TIFF";
        if (guid == GUID_ContainerFormatIco) return L"ICO";
        if (guid == GUID_ContainerFormatWmp) return L"WMP";
        
        // Convert GUID to string for unknown formats
        OLECHAR* guidString = nullptr;
        if (SUCCEEDED(StringFromCLSID(guid, &guidString)))
        {
            std::wstring result = guidString;
            CoTaskMemFree(guidString);
            return result;
        }
        
        return L"Unknown";
    }

    std::wstring GetFileExtensionsFromGuid(const GUID& guid)
    {
        // Map format GUIDs to file extensions
        if (guid == GUID_ContainerFormatJpeg) return L".jpg,.jpeg";
        if (guid == GUID_ContainerFormatPng) return L".png";
        if (guid == GUID_ContainerFormatGif) return L".gif";
        if (guid == GUID_ContainerFormatBmp) return L".bmp";
        if (guid == GUID_ContainerFormatTiff) return L".tif,.tiff";
        if (guid == GUID_ContainerFormatIco) return L".ico";
        if (guid == GUID_ContainerFormatWmp) return L".wdp,.hdp,.jxr";
        
        return L"";
    }

    std::vector<std::wstring> GetMimeTypesFromGuid(const GUID& guid)
    {
        // Map format GUIDs to MIME types
        if (guid == GUID_ContainerFormatJpeg) return {L"image/jpeg"};
        if (guid == GUID_ContainerFormatPng) return {L"image/png"};
        if (guid == GUID_ContainerFormatGif) return {L"image/gif"};
        if (guid == GUID_ContainerFormatBmp) return {L"image/bmp"};
        if (guid == GUID_ContainerFormatTiff) return {L"image/tiff"};
        if (guid == GUID_ContainerFormatIco) return {L"image/x-icon"};
        if (guid == GUID_ContainerFormatWmp) return {L"image/vnd.ms-photo"};
        
        return {};
    }

    std::wstring GetPixelFormatName(const GUID& guid)
    {
        // Map pixel format GUIDs to friendly names
        if (guid == GUID_WICPixelFormat24bppRGB) return L"24bpp RGB";
        if (guid == GUID_WICPixelFormat32bppRGBA) return L"32bpp RGBA";
        if (guid == GUID_WICPixelFormat8bppGray) return L"8bpp Grayscale";
        if (guid == GUID_WICPixelFormat1bppIndexed) return L"1bpp Indexed";
        if (guid == GUID_WICPixelFormat8bppIndexed) return L"8bpp Indexed";
        
        // Convert GUID to string for unknown formats
        OLECHAR* guidString = nullptr;
        if (SUCCEEDED(StringFromCLSID(guid, &guidString)))
        {
            std::wstring result = guidString;
            CoTaskMemFree(guidString);
            return result;
        }
        
        return L"Unknown";
    }
};

// Constructor and Destructor
WICMetadataExtractor::WICMetadataExtractor() : m_pImpl(std::make_unique<Impl>())
{
}

WICMetadataExtractor::~WICMetadataExtractor() = default;

// Main interface implementation
std::optional<WICMetadataExtractor::ImageInfo> WICMetadataExtractor::ExtractImageInfo(
    const std::wstring& filePath, 
    const ExtractionOptions& options)
{
    return m_pImpl->ExtractImageInfo(filePath, options);
}

std::optional<WICMetadataExtractor::FormatInfo> WICMetadataExtractor::GetFormatInfo(const std::wstring& filePath)
{
    return m_pImpl->GetFormatInfo(filePath);
}

std::vector<WICMetadataExtractor::FormatInfo> WICMetadataExtractor::GetSupportedFormats()
{
    return m_pImpl->GetSupportedFormats();
}

bool WICMetadataExtractor::HasMetadataType(const std::wstring& filePath, const std::wstring& metadataType)
{
    return m_pImpl->HasMetadataType(filePath, metadataType);
}

std::optional<WICMetadataExtractor::MetadataValue> WICMetadataExtractor::GetMetadataByPath(
    const std::wstring& filePath, 
    const std::wstring& propertyPath)
{
    return m_pImpl->GetMetadataByPath(filePath, propertyPath);
}

void WICMetadataExtractor::ClearCache()
{
    m_pImpl->GetCache().Clear();
}

void WICMetadataExtractor::SetCacheEnabled(bool enabled)
{
    m_pImpl->GetCache().SetEnabled(enabled);
}

size_t WICMetadataExtractor::GetCacheSize() const
{
    return m_pImpl->GetCache().Size();
}
