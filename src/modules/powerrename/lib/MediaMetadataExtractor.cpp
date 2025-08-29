#include "pch.h"
#include "MediaMetadataExtractor.h"
#include <fstream>
#include <vector>
#include <sstream>
#include <iomanip>

// TinyEXIF includes
#include "TinyEXIF.h"

using namespace PowerRenameLib;

// PIMPL implementation
class MediaMetadataExtractor::Impl
{
public:
    Impl()
    {
        // TinyEXIF doesn't require initialization
    }

    ~Impl()
    {
        // TinyEXIF doesn't require cleanup
    }

    std::vector<uint8_t> ReadFileToBuffer(const std::wstring& filePath)
    {
        std::ifstream file(filePath, std::ios::binary);
        if (!file.is_open())
        {
            return {};
        }

        file.seekg(0, std::ios::end);
        size_t size = file.tellg();
        file.seekg(0, std::ios::beg);

        std::vector<uint8_t> buffer(size);
        file.read(reinterpret_cast<char*>(buffer.data()), size);
        file.close();
        
        return buffer;
    }

    std::wstring ConvertToWString(const std::string& str)
    {
        if (str.empty()) return L"";
        
        int size_needed = MultiByteToWideChar(CP_UTF8, 0, &str[0], static_cast<int>(str.size()), NULL, 0);
        std::wstring wstrTo(size_needed, 0);
        MultiByteToWideChar(CP_UTF8, 0, &str[0], static_cast<int>(str.size()), &wstrTo[0], size_needed);
        return wstrTo;
    }

    std::string ConvertToString(const std::wstring& wstr)
    {
        if (wstr.empty()) return "";
        
        int size_needed = WideCharToMultiByte(CP_UTF8, 0, &wstr[0], static_cast<int>(wstr.size()), NULL, 0, NULL, NULL);
        std::string strTo(size_needed, 0);
        WideCharToMultiByte(CP_UTF8, 0, &wstr[0], static_cast<int>(wstr.size()), &strTo[0], size_needed, NULL, NULL);
        return strTo;
    }
};

MediaMetadataExtractor::MediaMetadataExtractor() : m_pImpl(std::make_unique<Impl>())
{
}

MediaMetadataExtractor::~MediaMetadataExtractor() = default;

MediaMetadataExtractor::ImageMetadata MediaMetadataExtractor::ExtractEXIFMetadata(const std::wstring& filePath)
{
    ImageMetadata metadata{};

    try
    {
        // Read file into buffer
        auto buffer = m_pImpl->ReadFileToBuffer(filePath);
        if (buffer.empty())
        {
            return metadata;
        }

        // Parse EXIF data using TinyEXIF
        TinyEXIF::EXIFInfo exifInfo;
        int parseResult = exifInfo.parseFrom(buffer.data(), static_cast<unsigned int>(buffer.size()));
        
        if (parseResult == TinyEXIF::PARSE_SUCCESS)
        {
            // Extract basic image info
            metadata.width = static_cast<int>(exifInfo.ImageWidth);
            metadata.height = static_cast<int>(exifInfo.ImageHeight);

            // Extract camera info
            if (!exifInfo.Make.empty() && !exifInfo.Model.empty())
            {
                metadata.cameraModel = m_pImpl->ConvertToWString(exifInfo.Make + " " + exifInfo.Model);
            }

            // Extract date taken
            if (!exifInfo.DateTime.empty())
            {
                metadata.dateTaken = m_pImpl->ConvertToWString(exifInfo.DateTime);
            }

            // Extract camera settings
            metadata.iso = static_cast<int>(exifInfo.ISOSpeedRatings);
            metadata.aperture = exifInfo.FNumber;
            metadata.shutterSpeed = exifInfo.ExposureTime;
            metadata.focalLength = exifInfo.FocalLength;

            // Extract GPS location if available
            if (exifInfo.GeoLocation.hasLatLon())
            {
                std::ostringstream location;
                location << std::fixed << std::setprecision(6) 
                         << exifInfo.GeoLocation.Latitude << ", " 
                         << exifInfo.GeoLocation.Longitude;
                metadata.gpsLocation = m_pImpl->ConvertToWString(location.str());
            }

            // Extract copyright
            if (!exifInfo.Copyright.empty())
            {
                metadata.copyright = m_pImpl->ConvertToWString(exifInfo.Copyright);
            }
        }
    }
    catch (...)
    {
        // Handle any exceptions during EXIF parsing
        // Return empty metadata on error
    }

    return metadata;
}

std::wstring MediaMetadataExtractor::FormatMetadataForRename(const ImageMetadata& metadata, const std::wstring& pattern)
{
    if (pattern == L"camera")
    {
        return metadata.cameraModel.empty() ? L"Unknown" : metadata.cameraModel;
    }
    else if (pattern == L"date")
    {
        if (!metadata.dateTaken.empty())
        {
            // Convert "YYYY:MM:DD HH:MM:SS" to "YYYY-MM-DD"
            std::wstring date = metadata.dateTaken;
            std::replace(date.begin(), date.end(), L':', L'-');
            size_t spacePos = date.find(L' ');
            if (spacePos != std::wstring::npos)
            {
                date = date.substr(0, spacePos);
            }
            return date;
        }
        return L"Unknown";
    }
    else if (pattern == L"location")
    {
        return metadata.gpsLocation.empty() ? L"Unknown" : metadata.gpsLocation;
    }
    else if (pattern == L"artist")
    {
        return metadata.artist.empty() ? L"Unknown" : metadata.artist;
    }
    else if (pattern == L"dimensions")
    {
        if (metadata.width > 0 && metadata.height > 0)
        {
            return std::to_wstring(metadata.width) + L"x" + std::to_wstring(metadata.height);
        }
        return L"Unknown";
    }
    else if (pattern == L"iso")
    {
        return metadata.iso > 0 ? L"ISO" + std::to_wstring(metadata.iso) : L"Unknown";
    }
    
    return L"Unknown";
}
