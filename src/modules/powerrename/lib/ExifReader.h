#pragma once

#include <string>
#include <unordered_map>
#include <wincodec.h>
#include <wrl/client.h>

class ExifReader
{
public:
    ExifReader();
    ~ExifReader();

    HRESULT ReadExifData(_In_ PCWSTR filePath);
    std::wstring GetExifValue(_In_ PCWSTR parameterName);
    bool HasExifData() const { return m_hasExifData; }

private:
    Microsoft::WRL::ComPtr<IWICImagingFactory> m_pWICFactory;
    std::unordered_map<std::wstring, std::wstring> m_exifData;
    bool m_hasExifData;

    HRESULT InitializeWIC();
    HRESULT ExtractExifFromFile(_In_ PCWSTR filePath);
    HRESULT ReadMetadataProperty(_In_ IWICMetadataQueryReader* pReader, _In_ PCWSTR query, _Out_ std::wstring& value);
    std::wstring FormatExposureTime(_In_ const PROPVARIANT& prop);
    std::wstring FormatFNumber(_In_ const PROPVARIANT& prop);
    std::wstring FormatRational(_In_ const PROPVARIANT& prop);
    std::wstring FormatDateTime(_In_ const PROPVARIANT& prop);
    void ClearExifData();
};