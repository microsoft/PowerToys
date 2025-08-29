#include "pch.h"
#include "MediaMetadataExtractor.h"

using namespace PowerRenameLib;

// MetadataFormatter implementation
std::wstring WICMetadataExtractor::MetadataFormatter::FormatValue(const MetadataValue& value)
{
    return std::visit([](auto&& arg) -> std::wstring {
        using T = std::decay_t<decltype(arg)>;
        if constexpr (std::is_same_v<T, std::wstring>)
        {
            return arg;
        }
        else if constexpr (std::is_same_v<T, int32_t>)
        {
            return std::to_wstring(arg);
        }
        else if constexpr (std::is_same_v<T, uint32_t>)
        {
            return std::to_wstring(arg);
        }
        else if constexpr (std::is_same_v<T, double>)
        {
            std::wostringstream oss;
            oss << std::fixed << std::setprecision(2) << arg;
            return oss.str();
        }
        else if constexpr (std::is_same_v<T, bool>)
        {
            return arg ? L"True" : L"False";
        }
        else if constexpr (std::is_same_v<T, std::vector<uint8_t>>)
        {
            return L"[Binary Data: " + std::to_wstring(arg.size()) + L" bytes]";
        }
        else
        {
            return L"Unknown";
        }
    }, value);
}

std::wstring WICMetadataExtractor::MetadataFormatter::FormatGpsCoordinates(
    const MetadataValue& latitude, 
    const MetadataValue& longitude,
    const MetadataValue& latRef,
    const MetadataValue& lonRef)
{
    try
    {
        double lat = 0.0, lon = 0.0;
        std::wstring latRefStr, lonRefStr;

        // Extract latitude
        if (std::holds_alternative<double>(latitude))
        {
            lat = std::get<double>(latitude);
        }
        else if (std::holds_alternative<std::vector<uint8_t>>(latitude))
        {
            // Handle rational array format (degrees, minutes, seconds)
            const auto& data = std::get<std::vector<uint8_t>>(latitude);
            if (data.size() >= 24) // 3 rationals * 8 bytes each
            {
                // This is a simplified parser - real implementation would handle endianness
                // and properly parse the rational format
                lat = 0.0; // Placeholder - would need proper rational parsing
            }
        }

        // Extract longitude
        if (std::holds_alternative<double>(longitude))
        {
            lon = std::get<double>(longitude);
        }
        else if (std::holds_alternative<std::vector<uint8_t>>(longitude))
        {
            // Similar handling as latitude
            lon = 0.0; // Placeholder
        }

        // Extract reference directions
        if (std::holds_alternative<std::wstring>(latRef))
        {
            latRefStr = std::get<std::wstring>(latRef);
        }
        if (std::holds_alternative<std::wstring>(lonRef))
        {
            lonRefStr = std::get<std::wstring>(lonRef);
        }

        // Apply reference directions
        if (latRefStr == L"S") lat = -lat;
        if (lonRefStr == L"W") lon = -lon;

        std::wostringstream oss;
        oss << std::fixed << std::setprecision(6) << lat << L", " << lon;
        return oss.str();
    }
    catch (...)
    {
        return L"Invalid GPS Data";
    }
}

std::wstring WICMetadataExtractor::MetadataFormatter::FormatCameraSettings(
    const std::map<std::wstring, MetadataValue>& exifData)
{
    std::wostringstream oss;
    bool first = true;

    auto addSetting = [&](const std::wstring& key, const std::wstring& prefix = L"", const std::wstring& suffix = L"")
    {
        auto it = exifData.find(key);
        if (it != exifData.end())
        {
            if (!first) oss << L", ";
            oss << prefix << FormatValue(it->second) << suffix;
            first = false;
        }
    };

    addSetting(L"ISO", L"ISO");
    addSetting(L"FNumber", L"f/");
    addSetting(L"ExposureTime", L"", L"s");
    addSetting(L"FocalLength", L"", L"mm");

    return oss.str();
}

std::wstring WICMetadataExtractor::MetadataFormatter::FormatDateTime(
    const MetadataValue& dateTime, 
    const std::wstring& format)
{
    if (!std::holds_alternative<std::wstring>(dateTime))
    {
        return L"";
    }

    std::wstring dateStr = std::get<std::wstring>(dateTime);
    if (dateStr.empty())
    {
        return L"";
    }

    // Convert "YYYY:MM:DD HH:MM:SS" to requested format
    if (format == L"yyyy-MM-dd")
    {
        std::replace(dateStr.begin(), dateStr.end(), L':', L'-');
        size_t spacePos = dateStr.find(L' ');
        if (spacePos != std::wstring::npos)
        {
            return dateStr.substr(0, spacePos);
        }
    }
    else if (format == L"yyyy-MM-dd_HH-mm-ss")
    {
        std::replace(dateStr.begin(), dateStr.end(), L':', L'-');
        std::replace(dateStr.begin(), dateStr.end(), L' ', L'_');
        // Fix time part
        size_t underscorePos = dateStr.find(L'_');
        if (underscorePos != std::wstring::npos)
        {
            std::wstring timePart = dateStr.substr(underscorePos + 1);
            std::replace(timePart.begin(), timePart.end(), L'-', L':');
            // Re-fix time separators
            size_t firstColon = timePart.find(L':');
            if (firstColon != std::wstring::npos)
            {
                size_t secondColon = timePart.find(L':', firstColon + 1);
                if (secondColon != std::wstring::npos)
                {
                    timePart[firstColon] = L'-';
                    timePart[secondColon] = L'-';
                }
            }
            return dateStr.substr(0, underscorePos + 1) + timePart;
        }
    }

    return dateStr;
}

std::wstring WICMetadataExtractor::MetadataFormatter::FormatExposureSettings(
    const MetadataValue& aperture,
    const MetadataValue& shutter,
    const MetadataValue& iso)
{
    std::wostringstream oss;
    
    if (std::holds_alternative<double>(aperture))
    {
        double fNum = std::get<double>(aperture);
        if (fNum > 0.0)
        {
            oss << L"f/" << std::fixed << std::setprecision(1) << fNum;
        }
    }

    if (std::holds_alternative<double>(shutter))
    {
        double shutterSpeed = std::get<double>(shutter);
        if (shutterSpeed > 0.0)
        {
            if (!oss.str().empty()) oss << L" ";
            if (shutterSpeed >= 1.0)
            {
                oss << std::fixed << std::setprecision(1) << shutterSpeed << L"s";
            }
            else
            {
                oss << L"1/" << static_cast<int>(1.0 / shutterSpeed) << L"s";
            }
        }
    }

    if (std::holds_alternative<int32_t>(iso) || std::holds_alternative<uint32_t>(iso))
    {
        int isoValue = std::holds_alternative<int32_t>(iso) ? 
                      std::get<int32_t>(iso) : 
                      static_cast<int>(std::get<uint32_t>(iso));
        if (isoValue > 0)
        {
            if (!oss.str().empty()) oss << L" ";
            oss << L"ISO" << isoValue;
        }
    }

    return oss.str();
}
