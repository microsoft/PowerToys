#include "pch.h"
#include "MediaMetadataExtractor.h"
#include <algorithm>

using namespace PowerRenameLib;

// RenamePatternProvider implementation
std::vector<std::wstring> WICMetadataExtractor::RenamePatternProvider::GetAvailablePatterns(const ImageInfo& imageInfo)
{
    std::vector<std::wstring> patterns;

    // Basic patterns always available
    patterns.push_back(L"filename");
    patterns.push_back(L"extension");

    if (imageInfo.width > 0 && imageInfo.height > 0)
    {
        patterns.push_back(L"dimensions");
        patterns.push_back(L"width");
        patterns.push_back(L"height");
    }

    if (!imageInfo.containerFormat.empty())
    {
        patterns.push_back(L"format");
    }

    // EXIF-based patterns
    if (!imageInfo.exifData.empty())
    {
        if (imageInfo.exifData.find(L"Make") != imageInfo.exifData.end() ||
            imageInfo.exifData.find(L"Model") != imageInfo.exifData.end())
        {
            patterns.push_back(L"camera");
        }

        if (imageInfo.exifData.find(L"DateTimeOriginal") != imageInfo.exifData.end() ||
            imageInfo.exifData.find(L"DateTime") != imageInfo.exifData.end())
        {
            patterns.push_back(L"date");
            patterns.push_back(L"datetime");
        }

        if (imageInfo.exifData.find(L"ISO") != imageInfo.exifData.end())
        {
            patterns.push_back(L"iso");
        }

        if (imageInfo.exifData.find(L"FNumber") != imageInfo.exifData.end())
        {
            patterns.push_back(L"aperture");
        }

        if (imageInfo.exifData.find(L"ExposureTime") != imageInfo.exifData.end())
        {
            patterns.push_back(L"shutter");
        }

        if (imageInfo.exifData.find(L"FocalLength") != imageInfo.exifData.end())
        {
            patterns.push_back(L"focal");
        }

        patterns.push_back(L"camera_settings");
        patterns.push_back(L"exposure_settings");
    }

    // GPS patterns
    if (!imageInfo.gpsData.empty())
    {
        patterns.push_back(L"location");
        patterns.push_back(L"gps");
    }

    // IPTC patterns
    if (!imageInfo.iptcData.empty())
    {
        if (imageInfo.iptcData.find(L"Title") != imageInfo.iptcData.end())
        {
            patterns.push_back(L"title");
        }

        if (imageInfo.iptcData.find(L"Keywords") != imageInfo.iptcData.end())
        {
            patterns.push_back(L"keywords");
        }

        if (imageInfo.iptcData.find(L"Byline") != imageInfo.iptcData.end())
        {
            patterns.push_back(L"author");
        }

        if (imageInfo.iptcData.find(L"Caption") != imageInfo.iptcData.end())
        {
            patterns.push_back(L"caption");
        }
    }

    return patterns;
}

std::wstring WICMetadataExtractor::RenamePatternProvider::ResolvePattern(
    const std::wstring& pattern, 
    const ImageInfo& imageInfo)
{
    if (pattern == L"dimensions")
    {
        return std::to_wstring(imageInfo.width) + L"x" + std::to_wstring(imageInfo.height);
    }
    else if (pattern == L"width")
    {
        return std::to_wstring(imageInfo.width);
    }
    else if (pattern == L"height")
    {
        return std::to_wstring(imageInfo.height);
    }
    else if (pattern == L"format")
    {
        return imageInfo.containerFormat;
    }
    else if (pattern == L"pixelformat")
    {
        return imageInfo.pixelFormat;
    }
    else if (pattern == L"bitsperpixel")
    {
        return std::to_wstring(imageInfo.bitsPerPixel);
    }
    else if (pattern == L"camera")
    {
        std::wstring make, model;
        auto makeIt = imageInfo.exifData.find(L"Make");
        auto modelIt = imageInfo.exifData.find(L"Model");
        
        if (makeIt != imageInfo.exifData.end())
        {
            make = MetadataFormatter::FormatValue(makeIt->second);
        }
        if (modelIt != imageInfo.exifData.end())
        {
            model = MetadataFormatter::FormatValue(modelIt->second);
        }

        if (!make.empty() && !model.empty())
        {
            return make + L" " + model;
        }
        else if (!model.empty())
        {
            return model;
        }
        else if (!make.empty())
        {
            return make;
        }
        return L"Unknown";
    }
    else if (pattern == L"date")
    {
        auto dateIt = imageInfo.exifData.find(L"DateTimeOriginal");
        if (dateIt == imageInfo.exifData.end())
        {
            dateIt = imageInfo.exifData.find(L"DateTime");
        }
        
        if (dateIt != imageInfo.exifData.end())
        {
            return MetadataFormatter::FormatDateTime(dateIt->second, L"yyyy-MM-dd");
        }
        return L"Unknown";
    }
    else if (pattern == L"datetime")
    {
        auto dateIt = imageInfo.exifData.find(L"DateTimeOriginal");
        if (dateIt == imageInfo.exifData.end())
        {
            dateIt = imageInfo.exifData.find(L"DateTime");
        }
        
        if (dateIt != imageInfo.exifData.end())
        {
            return MetadataFormatter::FormatDateTime(dateIt->second, L"yyyy-MM-dd_HH-mm-ss");
        }
        return L"Unknown";
    }
    else if (pattern == L"iso")
    {
        auto isoIt = imageInfo.exifData.find(L"ISO");
        if (isoIt != imageInfo.exifData.end())
        {
            return L"ISO" + MetadataFormatter::FormatValue(isoIt->second);
        }
        return L"Unknown";
    }
    else if (pattern == L"aperture")
    {
        auto apertureIt = imageInfo.exifData.find(L"FNumber");
        if (apertureIt != imageInfo.exifData.end())
        {
            return L"f" + MetadataFormatter::FormatValue(apertureIt->second);
        }
        return L"Unknown";
    }
    else if (pattern == L"shutter")
    {
        auto shutterIt = imageInfo.exifData.find(L"ExposureTime");
        if (shutterIt != imageInfo.exifData.end())
        {
            if (std::holds_alternative<double>(shutterIt->second))
            {
                double speed = std::get<double>(shutterIt->second);
                if (speed >= 1.0)
                {
                    return MetadataFormatter::FormatValue(shutterIt->second) + L"s";
                }
                else
                {
                    return L"1-" + std::to_wstring(static_cast<int>(1.0 / speed)) + L"s";
                }
            }
            return MetadataFormatter::FormatValue(shutterIt->second);
        }
        return L"Unknown";
    }
    else if (pattern == L"focal")
    {
        auto focalIt = imageInfo.exifData.find(L"FocalLength");
        if (focalIt != imageInfo.exifData.end())
        {
            return MetadataFormatter::FormatValue(focalIt->second) + L"mm";
        }
        return L"Unknown";
    }
    else if (pattern == L"camera_settings")
    {
        return MetadataFormatter::FormatCameraSettings(imageInfo.exifData);
    }
    else if (pattern == L"exposure_settings")
    {
        auto apertureIt = imageInfo.exifData.find(L"FNumber");
        auto shutterIt = imageInfo.exifData.find(L"ExposureTime");
        auto isoIt = imageInfo.exifData.find(L"ISO");
        
        MetadataValue aperture = apertureIt != imageInfo.exifData.end() ? apertureIt->second : MetadataValue{};
        MetadataValue shutter = shutterIt != imageInfo.exifData.end() ? shutterIt->second : MetadataValue{};
        MetadataValue iso = isoIt != imageInfo.exifData.end() ? isoIt->second : MetadataValue{};
        
        return MetadataFormatter::FormatExposureSettings(aperture, shutter, iso);
    }
    else if (pattern == L"location" || pattern == L"gps")
    {
        auto latIt = imageInfo.gpsData.find(L"GPSLatitude");
        auto lonIt = imageInfo.gpsData.find(L"GPSLongitude");
        auto latRefIt = imageInfo.gpsData.find(L"GPSLatitudeRef");
        auto lonRefIt = imageInfo.gpsData.find(L"GPSLongitudeRef");
        
        if (latIt != imageInfo.gpsData.end() && lonIt != imageInfo.gpsData.end())
        {
            MetadataValue latRef = latRefIt != imageInfo.gpsData.end() ? latRefIt->second : MetadataValue{};
            MetadataValue lonRef = lonRefIt != imageInfo.gpsData.end() ? lonRefIt->second : MetadataValue{};
            
            return MetadataFormatter::FormatGpsCoordinates(latIt->second, lonIt->second, latRef, lonRef);
        }
        return L"Unknown";
    }
    else if (pattern == L"title")
    {
        auto titleIt = imageInfo.iptcData.find(L"Title");
        if (titleIt != imageInfo.iptcData.end())
        {
            return MetadataFormatter::FormatValue(titleIt->second);
        }
        return L"Unknown";
    }
    else if (pattern == L"keywords")
    {
        auto keywordsIt = imageInfo.iptcData.find(L"Keywords");
        if (keywordsIt != imageInfo.iptcData.end())
        {
            std::wstring keywords = MetadataFormatter::FormatValue(keywordsIt->second);
            // Replace spaces and commas with underscores for filename compatibility
            std::replace(keywords.begin(), keywords.end(), L' ', L'_');
            std::replace(keywords.begin(), keywords.end(), L',', L'_');
            return keywords;
        }
        return L"Unknown";
    }
    else if (pattern == L"author")
    {
        auto authorIt = imageInfo.iptcData.find(L"Byline");
        if (authorIt != imageInfo.iptcData.end())
        {
            return MetadataFormatter::FormatValue(authorIt->second);
        }
        return L"Unknown";
    }
    else if (pattern == L"caption")
    {
        auto captionIt = imageInfo.iptcData.find(L"Caption");
        if (captionIt != imageInfo.iptcData.end())
        {
            std::wstring caption = MetadataFormatter::FormatValue(captionIt->second);
            // Truncate caption if too long for filename
            if (caption.length() > 50)
            {
                caption = caption.substr(0, 47) + L"...";
            }
            // Replace invalid filename characters
            const std::wstring invalidChars = L"<>:\"/\\|?*";
            for (wchar_t c : invalidChars)
            {
                std::replace(caption.begin(), caption.end(), c, L'_');
            }
            return caption;
        }
        return L"Unknown";
    }

    return L"Unknown";
}

std::vector<std::wstring> WICMetadataExtractor::RenamePatternProvider::GetSmartSuggestions(const ImageInfo& imageInfo)
{
    std::vector<std::wstring> suggestions;

    // Smart suggestions based on available metadata
    auto availablePatterns = GetAvailablePatterns(imageInfo);

    // Photography-focused suggestions
    if (std::find(availablePatterns.begin(), availablePatterns.end(), L"date") != availablePatterns.end())
    {
        if (std::find(availablePatterns.begin(), availablePatterns.end(), L"camera") != availablePatterns.end())
        {
            suggestions.push_back(L"{date}_{camera}");
            suggestions.push_back(L"{camera}_{date}");
        }
        
        if (std::find(availablePatterns.begin(), availablePatterns.end(), L"exposure_settings") != availablePatterns.end())
        {
            suggestions.push_back(L"{date}_{exposure_settings}");
        }
        
        suggestions.push_back(L"{date}_{dimensions}");
        suggestions.push_back(L"{datetime}");
    }

    // Creative content suggestions
    if (std::find(availablePatterns.begin(), availablePatterns.end(), L"title") != availablePatterns.end())
    {
        suggestions.push_back(L"{title}_{date}");
        suggestions.push_back(L"{title}_{dimensions}");
        
        if (std::find(availablePatterns.begin(), availablePatterns.end(), L"author") != availablePatterns.end())
        {
            suggestions.push_back(L"{title}_{author}");
        }
    }

    // Technical/archival suggestions
    if (std::find(availablePatterns.begin(), availablePatterns.end(), L"camera_settings") != availablePatterns.end())
    {
        suggestions.push_back(L"{camera}_{camera_settings}");
        suggestions.push_back(L"{date}_{camera_settings}");
    }

    // Location-based suggestions
    if (std::find(availablePatterns.begin(), availablePatterns.end(), L"location") != availablePatterns.end())
    {
        suggestions.push_back(L"{date}_{location}");
        if (std::find(availablePatterns.begin(), availablePatterns.end(), L"title") != availablePatterns.end())
        {
            suggestions.push_back(L"{title}_{location}");
        }
    }

    // Keyword-based suggestions
    if (std::find(availablePatterns.begin(), availablePatterns.end(), L"keywords") != availablePatterns.end())
    {
        suggestions.push_back(L"{keywords}_{date}");
        suggestions.push_back(L"{date}_{keywords}");
    }

    // Default fallbacks
    suggestions.push_back(L"{format}_{dimensions}");
    suggestions.push_back(L"{width}x{height}");

    return suggestions;
}

std::map<std::wstring, std::wstring> WICMetadataExtractor::RenamePatternProvider::ResolvePatterns(
    const std::vector<std::wstring>& patterns, 
    const ImageInfo& imageInfo)
{
    std::map<std::wstring, std::wstring> results;
    
    for (const auto& pattern : patterns)
    {
        results[pattern] = ResolvePattern(pattern, imageInfo);
    }
    
    return results;
}
