#include "pch.h"
#include "ExifReader.h"
#include <propvarutil.h>
#include <sstream>
#include <iomanip>

ExifReader::ExifReader() : m_hasExifData(false)
{
    InitializeWIC();
}

ExifReader::~ExifReader()
{
    ClearExifData();
}

HRESULT ExifReader::InitializeWIC()
{
    return CoCreateInstance(
        CLSID_WICImagingFactory,
        nullptr,
        CLSCTX_INPROC_SERVER,
        IID_PPV_ARGS(&m_pWICFactory));
}

HRESULT ExifReader::ReadExifData(_In_ PCWSTR filePath)
{
    ClearExifData();
    
    if (!m_pWICFactory)
    {
        return E_FAIL;
    }

    return ExtractExifFromFile(filePath);
}

HRESULT ExifReader::ExtractExifFromFile(_In_ PCWSTR filePath)
{
    Microsoft::WRL::ComPtr<IWICBitmapDecoder> pDecoder;
    HRESULT hr = m_pWICFactory->CreateDecoderFromFilename(
        filePath,
        nullptr,
        GENERIC_READ,
        WICDecodeMetadataCacheOnLoad,
        &pDecoder);

    if (FAILED(hr))
    {
        return hr;
    }

    Microsoft::WRL::ComPtr<IWICBitmapFrameDecode> pFrame;
    hr = pDecoder->GetFrame(0, &pFrame);
    if (FAILED(hr))
    {
        return hr;
    }

    Microsoft::WRL::ComPtr<IWICMetadataQueryReader> pReader;
    hr = pFrame->GetMetadataQueryReader(&pReader);
    if (FAILED(hr))
    {
        return hr;
    }

    // Extract common EXIF data
    std::wstring value;
    
    // Camera information
    if (SUCCEEDED(ReadMetadataProperty(pReader.Get(), L"/app1/ifd/exif/{ushort=271}", value)))
        m_exifData[L"CameraMake"] = value;
    
    if (SUCCEEDED(ReadMetadataProperty(pReader.Get(), L"/app1/ifd/exif/{ushort=272}", value)))
        m_exifData[L"CameraModel"] = value;

    if (SUCCEEDED(ReadMetadataProperty(pReader.Get(), L"/app1/ifd/exif/{ushort=42036}", value)))
        m_exifData[L"LensModel"] = value;

    // Shooting parameters
    PROPVARIANT prop;
    PropVariantInit(&prop);
    
    if (SUCCEEDED(pReader->GetMetadataByName(L"/app1/ifd/exif/{ushort=33434}", &prop)))
    {
        m_exifData[L"ExposureTime"] = FormatExposureTime(prop);
        PropVariantClear(&prop);
    }

    if (SUCCEEDED(pReader->GetMetadataByName(L"/app1/ifd/exif/{ushort=33437}", &prop)))
    {
        m_exifData[L"FNumber"] = FormatFNumber(prop);
        PropVariantClear(&prop);
    }

    if (SUCCEEDED(pReader->GetMetadataByName(L"/app1/ifd/exif/{ushort=34855}", &prop)))
    {
        if (prop.vt == VT_UI2)
            m_exifData[L"ISO"] = std::to_wstring(prop.uiVal);
        PropVariantClear(&prop);
    }

    if (SUCCEEDED(pReader->GetMetadataByName(L"/app1/ifd/exif/{ushort=37386}", &prop)))
    {
        m_exifData[L"FocalLength"] = FormatRational(prop);
        PropVariantClear(&prop);
    }

    // Date/Time
    if (SUCCEEDED(ReadMetadataProperty(pReader.Get(), L"/app1/ifd/exif/{ushort=36867}", value)))
        m_exifData[L"ExifDateTaken"] = value;

    if (SUCCEEDED(ReadMetadataProperty(pReader.Get(), L"/app1/ifd/exif/{ushort=306}", value)))
        m_exifData[L"ExifDateTime"] = value;

    // Image dimensions
    UINT width = 0, height = 0;
    if (SUCCEEDED(pFrame->GetSize(&width, &height)))
    {
        m_exifData[L"ImageWidth"] = std::to_wstring(width);
        m_exifData[L"ImageHeight"] = std::to_wstring(height);
    }

    // Orientation
    if (SUCCEEDED(pReader->GetMetadataByName(L"/app1/ifd/{ushort=274}", &prop)))
    {
        if (prop.vt == VT_UI2)
            m_exifData[L"Orientation"] = std::to_wstring(prop.uiVal);
        PropVariantClear(&prop);
    }

    // GPS data (if available)
    if (SUCCEEDED(ReadMetadataProperty(pReader.Get(), L"/app1/ifd/gps/{ushort=2}", value)))
        m_exifData[L"GPSLatitude"] = value;

    if (SUCCEEDED(ReadMetadataProperty(pReader.Get(), L"/app1/ifd/gps/{ushort=4}", value)))
        m_exifData[L"GPSLongitude"] = value;

    if (SUCCEEDED(ReadMetadataProperty(pReader.Get(), L"/app1/ifd/gps/{ushort=6}", value)))
        m_exifData[L"GPSAltitude"] = value;

    m_hasExifData = !m_exifData.empty();
    return m_hasExifData ? S_OK : S_FALSE;
}

HRESULT ExifReader::ReadMetadataProperty(_In_ IWICMetadataQueryReader* pReader, _In_ PCWSTR query, _Out_ std::wstring& value)
{
    PROPVARIANT prop;
    PropVariantInit(&prop);
    
    HRESULT hr = pReader->GetMetadataByName(query, &prop);
    if (SUCCEEDED(hr))
    {
        if (prop.vt == VT_LPSTR && prop.pszVal)
        {
            // Convert ANSI to Unicode
            int len = MultiByteToWideChar(CP_ACP, 0, prop.pszVal, -1, nullptr, 0);
            if (len > 0)
            {
                std::vector<wchar_t> buffer(len);
                MultiByteToWideChar(CP_ACP, 0, prop.pszVal, -1, buffer.data(), len);
                value = buffer.data();
            }
        }
        else if (prop.vt == VT_LPWSTR && prop.pwszVal)
        {
            value = prop.pwszVal;
        }
        else if (prop.vt == VT_UI2)
        {
            value = std::to_wstring(prop.uiVal);
        }
        else if (prop.vt == VT_UI4)
        {
            value = std::to_wstring(prop.ulVal);
        }
        else if (prop.vt == (VT_UI4 | VT_VECTOR) && prop.caul.cElems >= 2)
        {
            // Rational number (numerator/denominator)
            double rational = static_cast<double>(prop.caul.pElems[0]) / static_cast<double>(prop.caul.pElems[1]);
            std::wostringstream oss;
            oss << std::fixed << std::setprecision(2) << rational;
            value = oss.str();
        }
        
        PropVariantClear(&prop);
    }
    
    return hr;
}

std::wstring ExifReader::FormatExposureTime(const PROPVARIANT& prop)
{
    if (prop.vt == (VT_UI4 | VT_VECTOR) && prop.caul.cElems >= 2)
    {
        ULONG numerator = prop.caul.pElems[0];
        ULONG denominator = prop.caul.pElems[1];
        
        if (numerator == 1)
        {
            return L"1/" + std::to_wstring(denominator);
        }
        else if (denominator == 1)
        {
            return std::to_wstring(numerator);
        }
        else
        {
            double exposure = static_cast<double>(numerator) / static_cast<double>(denominator);
            std::wostringstream oss;
            oss << std::fixed << std::setprecision(3) << exposure;
            return oss.str();
        }
    }
    return L"";
}

std::wstring ExifReader::FormatFNumber(const PROPVARIANT& prop)
{
    if (prop.vt == (VT_UI4 | VT_VECTOR) && prop.caul.cElems >= 2)
    {
        double fNumber = static_cast<double>(prop.caul.pElems[0]) / static_cast<double>(prop.caul.pElems[1]);
        std::wostringstream oss;
        oss << L"f/" << std::fixed << std::setprecision(1) << fNumber;
        return oss.str();
    }
    return L"";
}

std::wstring ExifReader::FormatRational(const PROPVARIANT& prop)
{
    if (prop.vt == (VT_UI4 | VT_VECTOR) && prop.caul.cElems >= 2)
    {
        double value = static_cast<double>(prop.caul.pElems[0]) / static_cast<double>(prop.caul.pElems[1]);
        std::wostringstream oss;
        oss << std::fixed << std::setprecision(1) << value;
        return oss.str();
    }
    return L"";
}

std::wstring ExifReader::FormatDateTime(const PROPVARIANT& prop)
{
    // EXIF DateTime format is "YYYY:MM:DD HH:MM:SS"
    // We'll return it as-is, but could format differently if needed
    if (prop.vt == VT_LPSTR && prop.pszVal)
    {
        int len = MultiByteToWideChar(CP_ACP, 0, prop.pszVal, -1, nullptr, 0);
        if (len > 0)
        {
            std::vector<wchar_t> buffer(len);
            MultiByteToWideChar(CP_ACP, 0, prop.pszVal, -1, buffer.data(), len);
            return buffer.data();
        }
    }
    return L"";
}

std::wstring ExifReader::GetExifValue(_In_ PCWSTR parameterName)
{
    auto it = m_exifData.find(parameterName);
    if (it != m_exifData.end())
    {
        return it->second;
    }
    return L"";
}

void ExifReader::ClearExifData()
{
    m_exifData.clear();
    m_hasExifData = false;
}