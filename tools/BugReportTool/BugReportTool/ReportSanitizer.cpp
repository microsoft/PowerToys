// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "ReportSanitizer.h"

#include <algorithm>
#include <array>
#include <cstdio>
#include <cwchar>
#include <exception>
#include <fstream>
#include <optional>
#include <regex>
#include <string>
#include <string_view>

namespace
{
    constexpr std::string_view propertyNameToSanitize = "\"SecurityKey\"";
    constexpr std::string_view propertyWhitespace = " \t\r\n\f\v";
    constexpr std::string_view privateValue = "\"<private_data>\"";
    constexpr std::size_t readBufferSize = 64 * 1024;

    struct PropertyScanResult
    {
        std::uintmax_t count = 0;
        std::uintmax_t firstOffset = 0;
    };

    enum class SanitizeResult
    {
        Unchanged,
        Sanitized,
        NeedsFallback,
    };

    PropertyScanResult ScanPropertiesToSanitize(std::ifstream& input)
    {
        std::array<char, readBufferSize> buffer;
        PropertyScanResult properties;
        std::size_t matchedNameLength = 0;
        std::optional<std::uintmax_t> pendingPropertyOffset;
        std::uintmax_t offset = 0;
        do
        {
            input.read(buffer.data(), buffer.size());
            if (input.fail() && !input.eof())
            {
                throw std::ios::failure("Failed to scan report file");
            }
            for (const auto character : std::string_view(buffer.data(), static_cast<std::size_t>(input.gcount())))
            {
                const auto characterOffset = offset++;
                if (pendingPropertyOffset)
                {
                    if (propertyWhitespace.find(character) != std::string_view::npos)
                    {
                        continue;
                    }
                    if (character == ':')
                    {
                        if (properties.count == 0)
                        {
                            properties.firstOffset = *pendingPropertyOffset;
                        }
                        ++properties.count;
                    }
                    pendingPropertyOffset.reset();
                }

                if (character == propertyNameToSanitize[matchedNameLength])
                {
                    ++matchedNameLength;
                }
                else
                {
                    matchedNameLength = character == propertyNameToSanitize.front() ? 1 : 0;
                }
                if (matchedNameLength == propertyNameToSanitize.size())
                {
                    pendingPropertyOffset = characterOffset + 1 - propertyNameToSanitize.size();
                    matchedNameLength = 0;
                }
            }
        } while (!input.eof());

        return properties;
    }

    SanitizeResult SanitizeRecognizedValues(std::string& text, std::uintmax_t expectedPropertyCount)
    {
        static const std::regex keyProperty(R"("SecurityKey"\s*:\s*)");
        static const std::regex keyValue(R"re("(?:[^"\\]|\\.)*"|\{\s*"value"\s*:\s*"(?:[^"\\]|\\.)*"\s*\})re");
        std::size_t offset = 0;
        std::uintmax_t redactedCount = 0;
        bool changed = false;
        std::smatch property;
        while (std::regex_search(text.cbegin() + offset, text.cend(), property, keyProperty))
        {
            const auto valueOffset = offset + property.position() + property.length();
            std::smatch value;
            if (!std::regex_search(text.cbegin() + valueOffset, text.cend(), value, keyValue, std::regex_constants::match_continuous))
            {
                return SanitizeResult::NeedsFallback;
            }
            ++redactedCount;
            const auto valueLength = static_cast<std::size_t>(value.length());
            if (text.compare(valueOffset, valueLength, privateValue) != 0)
            {
                text.replace(valueOffset, valueLength, privateValue);
                changed = true;
            }
            offset = valueOffset + privateValue.size();
        }
        if (redactedCount != expectedPropertyCount)
        {
            return SanitizeResult::NeedsFallback;
        }
        return changed ? SanitizeResult::Sanitized : SanitizeResult::Unchanged;
    }

    void ReplaceReportTailWithPlaceholder(const std::filesystem::path& file, std::uintmax_t keyOffset)
    {
        // The same on-disk fallback handles oversized files and values regex could not redact.
        // Discarding the tail can leave report JSON incomplete.
        std::filesystem::resize_file(file, keyOffset);
        std::ofstream output;
        output.exceptions(std::ios::failbit | std::ios::badbit);
        output.open(file, std::ios::binary | std::ios::app);
        output << propertyNameToSanitize << ':' << privateValue;
        output.close();
    }

    void SanitizeFile(const std::filesystem::directory_entry& entry)
    {
        std::ifstream input;
        input.exceptions(std::ios::failbit | std::ios::badbit);
        input.open(entry.path(), std::ios::binary);
        input.exceptions(std::ios::badbit);
        const auto properties = ScanPropertiesToSanitize(input);
        if (properties.count == 0)
        {
            return;
        }

        auto result = SanitizeResult::NeedsFallback;
        std::string text;
        if (entry.file_size() <= ReportSanitizer::MaxRegexFileSize)
        {
            input.clear();
            input.seekg(0);
            if (!input)
            {
                throw std::ios::failure("Failed to rewind report file");
            }
            text.reserve(static_cast<std::size_t>(std::min<std::uintmax_t>(entry.file_size(), ReportSanitizer::MaxRegexFileSize + 1)));
            std::array<char, readBufferSize> buffer;
            while (input && text.size() <= ReportSanitizer::MaxRegexFileSize)
            {
                const auto count = std::min(buffer.size(), ReportSanitizer::MaxRegexFileSize + 1 - text.size());
                input.read(buffer.data(), static_cast<std::streamsize>(count));
                text.append(buffer.data(), static_cast<std::size_t>(input.gcount()));
            }
            if (input.fail() && !input.eof())
            {
                throw std::ios::failure("Failed to read report file");
            }
            if (text.size() <= ReportSanitizer::MaxRegexFileSize)
            {
                try
                {
                    result = SanitizeRecognizedValues(text, properties.count);
                }
                catch (const std::regex_error& error)
                {
                    wprintf_s(L"Regex could not sanitize report file %ls; using tail replacement: %hs\n", entry.path().c_str(), error.what());
                }
            }
        }
        input.close();

        if (result == SanitizeResult::NeedsFallback)
        {
            ReplaceReportTailWithPlaceholder(entry.path(), properties.firstOffset);
        }
        else if (result == SanitizeResult::Sanitized)
        {
            std::ofstream output;
            output.exceptions(std::ios::failbit | std::ios::badbit);
            output.open(entry.path(), std::ios::binary | std::ios::trunc);
            output << text;
            output.close();
        }
    }

    void RemoveFailedReportPath(
        const std::filesystem::path& path,
        const std::filesystem::path& reportDirectory,
        const std::exception& failure,
        std::vector<std::filesystem::path>& removedPaths)
    {
        wprintf_s(L"Failed to sanitize report path %ls: %hs\n", path.lexically_relative(reportDirectory).c_str(), failure.what());
        std::error_code error;
        auto removed = std::filesystem::remove(path, error);
        if (error)
        {
            removed = std::filesystem::remove_all(path) != 0;
        }
        if (removed)
        {
            removedPaths.push_back(path);
        }
    }

    void SanitizeDirectory(
        const std::filesystem::path& directory,
        const std::filesystem::path& reportDirectory,
        std::vector<std::filesystem::path>& removedPaths)
    {
        std::vector<std::filesystem::directory_entry> entries;
        try
        {
            for (const auto& entry : std::filesystem::directory_iterator(directory))
            {
                entries.push_back(entry);
            }
        }
        catch (const std::filesystem::filesystem_error& error)
        {
            if (directory == reportDirectory)
            {
                throw;
            }
            RemoveFailedReportPath(directory, reportDirectory, error, removedPaths);
            return;
        }

        for (const auto& entry : entries)
        {
            bool isDirectory;
            try
            {
                isDirectory = entry.is_directory();
            }
            catch (const std::filesystem::filesystem_error& error)
            {
                RemoveFailedReportPath(entry.path(), reportDirectory, error, removedPaths);
                continue;
            }
            if (isDirectory)
            {
                SanitizeDirectory(entry.path(), reportDirectory, removedPaths);
                continue;
            }

            const auto extension = entry.path().extension();
            if (_wcsicmp(extension.c_str(), L".json") != 0 && _wcsicmp(extension.c_str(), L".log") != 0 && _wcsicmp(extension.c_str(), L".txt") != 0)
            {
                continue;
            }
            try
            {
                if (entry.is_regular_file())
                {
                    SanitizeFile(entry);
                }
            }
            catch (const std::exception& error)
            {
                RemoveFailedReportPath(entry.path(), reportDirectory, error, removedPaths);
            }
        }
    }
}

std::vector<std::filesystem::path> ReportSanitizer::Sanitize(const std::filesystem::path& reportDirectory)
{
    std::vector<std::filesystem::path> removedPaths;
    SanitizeDirectory(reportDirectory, reportDirectory, removedPaths);
    return removedPaths;
}
