#include "pch.h"
#include "NumericRenameMapping.h"
#include <climits>
#include <string_view>
#include <zip.h>

namespace
{
    HRESULT ReadFileBytes(PCWSTR path, std::vector<unsigned char>& bytes)
    {
        std::ifstream input(path, std::ios::binary | std::ios::ate);
        if (!input)
        {
            return E_FAIL;
        }
        const auto size = input.tellg();
        if (size < 0)
        {
            return E_FAIL;
        }
        bytes.resize(static_cast<size_t>(size));
        input.seekg(0, std::ios::beg);
        if (!bytes.empty() && !input.read(reinterpret_cast<char*>(bytes.data()), size))
        {
            return E_FAIL;
        }
        return S_OK;
    }

    HRESULT DecodeCsvBytes(const std::vector<unsigned char>& bytes, std::wstring& text)
    {
        if (bytes.size() >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            const auto* data = reinterpret_cast<const wchar_t*>(bytes.data() + 2);
            text.assign(data, data + (bytes.size() - 2) / sizeof(wchar_t));
            return S_OK;
        }
        if (bytes.size() >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            text.clear();
            for (size_t i = 2; i + 1 < bytes.size(); i += 2)
            {
                text.push_back(static_cast<wchar_t>((bytes[i] << 8) | bytes[i + 1]));
            }
            return S_OK;
        }

        const size_t offset = bytes.size() >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        const auto* data = reinterpret_cast<const char*>(bytes.data() + offset);
        const int length = static_cast<int>(bytes.size() - offset);
        if (length == 0)
        {
            text.clear();
            return S_OK;
        }
        const int wideLength = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, data, length, nullptr, 0);
        if (wideLength == 0)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        text.resize(wideLength);
        MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, data, length, text.data(), wideLength);
        return S_OK;
    }

    std::wstring TrimField(const std::wstring& field)
    {
        size_t first = 0;
        while (first < field.size() && iswspace(field[first])) ++first;
        size_t last = field.size();
        while (last > first && iswspace(field[last - 1])) --last;
        return field.substr(first, last - first);
    }

    std::vector<std::wstring> ParseCsvFields(const std::wstring& row)
    {
        std::vector<std::wstring> fields;
        std::wstring field;
        bool quoted = false;
        for (size_t i = 0; i < row.size(); ++i)
        {
            if (row[i] == L'"')
            {
                if (quoted && i + 1 < row.size() && row[i + 1] == L'"')
                {
                    field.push_back(L'"');
                    ++i;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (row[i] == L',' && !quoted)
            {
                fields.push_back(TrimField(field));
                field.clear();
            }
            else
            {
                field.push_back(row[i]);
            }
        }
        fields.push_back(TrimField(field));
        return fields;
    }

    std::wstring DecodeXmlEntities(const std::wstring& value)
    {
        std::wstring result;
        result.reserve(value.size());
        for (size_t i = 0; i < value.size(); ++i)
        {
            if (value[i] != L'&')
            {
                result.push_back(value[i]);
                continue;
            }

            const size_t end = value.find(L';', i + 1);
            if (end == std::wstring::npos)
            {
                result.push_back(value[i]);
                continue;
            }
            const std::wstring entity = value.substr(i + 1, end - i - 1);
            if (entity == L"amp") result.push_back(L'&');
            else if (entity == L"lt") result.push_back(L'<');
            else if (entity == L"gt") result.push_back(L'>');
            else if (entity == L"quot") result.push_back(L'"');
            else if (entity == L"apos") result.push_back(L'\'');
            else if (entity.rfind(L"#x", 0) == 0)
            {
                wchar_t* parsedEnd = nullptr;
                const auto codePoint = wcstoul(entity.c_str() + 2, &parsedEnd, 16);
                if (parsedEnd && *parsedEnd == L'\0') result.push_back(static_cast<wchar_t>(codePoint));
            }
            else if (entity.rfind(L"#", 0) == 0)
            {
                wchar_t* parsedEnd = nullptr;
                const auto codePoint = wcstoul(entity.c_str() + 1, &parsedEnd, 10);
                if (parsedEnd && *parsedEnd == L'\0') result.push_back(static_cast<wchar_t>(codePoint));
            }
            else
            {
                result.append(value, i, end - i + 1);
            }
            i = end;
        }
        return result;
    }

    std::wstring XmlTextElements(const std::wstring& xml)
    {
        std::wstring result;
        size_t position = 0;
        while ((position = xml.find(L"<t", position)) != std::wstring::npos)
        {
            const wchar_t next = position + 2 < xml.size() ? xml[position + 2] : L'\0';
            if (next != L'>' && !iswspace(next))
            {
                position += 2;
                continue;
            }
            const size_t openEnd = xml.find(L'>', position);
            const size_t close = openEnd == std::wstring::npos ? std::wstring::npos : xml.find(L"</t>", openEnd + 1);
            if (close == std::wstring::npos) break;
            result += DecodeXmlEntities(xml.substr(openEnd + 1, close - openEnd - 1));
            position = close + 4;
        }
        return result;
    }

    std::wstring XmlAttribute(const std::wstring& tag, const wchar_t* name)
    {
        const std::wstring prefix = std::wstring(name) + L"=\"";
        const size_t start = tag.find(prefix);
        if (start == std::wstring::npos) return {};
        const size_t valueStart = start + prefix.size();
        const size_t end = tag.find(L'"', valueStart);
        return end == std::wstring::npos ? std::wstring{} : tag.substr(valueStart, end - valueStart);
    }

    bool TryParseNumericKey(const std::wstring& value, unsigned long long& number)
    {
        number = 0;
        if (value.empty()) return false;
        for (const wchar_t character : value)
        {
            if (character < L'0' || character > L'9') return false;
            const unsigned int digit = static_cast<unsigned int>(character - L'0');
            if (number > (ULLONG_MAX - digit) / 10) return false;
            number = number * 10 + digit;
        }
        return true;
    }

    HRESULT AddNumericMapping(
        PowerRenameLib::NumericRenameMapping& mappings,
        unsigned long long key,
        const std::wstring& name)
    {
        if (!PowerRenameLib::IsValidNumericRenameName(name.c_str())) return E_INVALIDARG;
        return mappings.emplace(key, name).second ? S_OK : E_INVALIDARG;
    }

    HRESULT BuildNumericMapping(
        const std::vector<std::vector<std::wstring>>& rows,
        PowerRenameLib::NumericRenameMapping& mappings)
    {
        mappings.clear();

        bool hasKeyAndNameRows = false;
        for (const auto& row : rows)
        {
            if (row.size() > 1 && !row[1].empty())
            {
                hasKeyAndNameRows = true;
                break;
            }
        }

        if (hasKeyAndNameRows)
        {
            bool firstRow = true;
            for (const auto& row : rows)
            {
                if (row.empty() || (row.size() == 1 && row[0].empty())) continue;
                unsigned long long key = 0;
                if (!TryParseNumericKey(row[0], key))
                {
                    if (firstRow)
                    {
                        firstRow = false;
                        continue;
                    }
                    mappings.clear();
                    return E_INVALIDARG;
                }
                firstRow = false;
                if (row.size() < 2 || row[1].empty() || FAILED(AddNumericMapping(mappings, key, row[1])))
                {
                    mappings.clear();
                    return E_INVALIDARG;
                }
            }
        }
        else
        {
            unsigned long long key = 1;
            for (const auto& row : rows)
            {
                if (row.empty() || row[0].empty()) continue;
                if (FAILED(AddNumericMapping(mappings, key, row[0])))
                {
                    mappings.clear();
                    return E_INVALIDARG;
                }
                if (key == ULLONG_MAX)
                {
                    mappings.clear();
                    return E_INVALIDARG;
                }
                ++key;
            }
        }

        return mappings.empty() ? E_INVALIDARG : S_OK;
    }

    HRESULT ReadZipEntry(zip_t* archive, const char* name, std::string& contents)
    {
        zip_stat_t stat{};
        if (zip_stat(archive, name, 0, &stat) != 0 || stat.size > SIZE_MAX)
        {
            return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
        }
        zip_file_t* file = zip_fopen(archive, name, 0);
        if (!file) return E_FAIL;
        contents.resize(static_cast<size_t>(stat.size));
        const zip_int64_t read = zip_fread(file, contents.data(), contents.size());
        zip_fclose(file);
        return read == static_cast<zip_int64_t>(contents.size()) ? S_OK : E_FAIL;
    }

    std::wstring FirstXlsxCellValue(const std::wstring& cell, const std::vector<std::wstring>& sharedStrings)
    {
        const size_t openEnd = cell.find(L'>');
        if (openEnd == std::wstring::npos) return {};
        const std::wstring type = XmlAttribute(cell.substr(0, openEnd + 1), L"t");
        const size_t valueStart = cell.find(L"<v>", openEnd);
        const size_t valueEnd = valueStart == std::wstring::npos ? std::wstring::npos : cell.find(L"</v>", valueStart);
        const std::wstring value = valueStart == std::wstring::npos || valueEnd == std::wstring::npos ? std::wstring{} : DecodeXmlEntities(cell.substr(valueStart + 3, valueEnd - valueStart - 3));

        if (type == L"s")
        {
            wchar_t* end = nullptr;
            const unsigned long index = wcstoul(value.c_str(), &end, 10);
            return end && *end == L'\0' && index < sharedStrings.size() ? sharedStrings[index] : std::wstring{};
        }
        if (type == L"inlineStr")
        {
            const size_t inlineStart = cell.find(L"<is>", openEnd);
            const size_t inlineEnd = inlineStart == std::wstring::npos ? std::wstring::npos : cell.find(L"</is>", inlineStart);
            return inlineStart == std::wstring::npos || inlineEnd == std::wstring::npos ? std::wstring{} : XmlTextElements(cell.substr(inlineStart, inlineEnd - inlineStart + 5));
        }
        return value;
    }

    HRESULT DecodeXlsxXml(const std::string& bytes, std::wstring& text)
    {
        if (bytes.empty()) return E_INVALIDARG;
        const int wideLength = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, bytes.data(), static_cast<int>(bytes.size()), nullptr, 0);
        if (wideLength == 0) return HRESULT_FROM_WIN32(GetLastError());
        text.resize(wideLength);
        MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, bytes.data(), static_cast<int>(bytes.size()), text.data(), wideLength);
        return S_OK;
    }
}

namespace PowerRenameLib
{
    HRESULT LoadNumericRenameMappingFromCsv(PCWSTR path, NumericRenameMapping& mappings)
    {
        mappings.clear();
        std::vector<unsigned char> bytes;
        HRESULT hr = ReadFileBytes(path, bytes);
        if (FAILED(hr)) return hr;
        std::wstring text;
        hr = DecodeCsvBytes(bytes, text);
        if (FAILED(hr)) return hr;

        std::vector<std::vector<std::wstring>> rows;
        size_t rowStart = 0;
        while (rowStart <= text.size())
        {
            const size_t rowEnd = text.find_first_of(L"\r\n", rowStart);
            const size_t end = rowEnd == std::wstring::npos ? text.size() : rowEnd;
            const auto fields = ParseCsvFields(text.substr(rowStart, end - rowStart));
            bool hasValue = false;
            for (const auto& field : fields)
            {
                hasValue = hasValue || !field.empty();
            }
            if (hasValue) rows.push_back(fields);
            if (rowEnd == std::wstring::npos) break;
            rowStart = rowEnd + 1;
            if (text[rowEnd] == L'\r' && rowStart < text.size() && text[rowStart] == L'\n') ++rowStart;
        }
        return BuildNumericMapping(rows, mappings);
    }

    HRESULT LoadNumericRenameMappingFromText(PCWSTR path, NumericRenameMapping& mappings)
    {
        mappings.clear();
        std::vector<unsigned char> bytes;
        HRESULT hr = ReadFileBytes(path, bytes);
        if (FAILED(hr)) return hr;
        std::wstring text;
        hr = DecodeCsvBytes(bytes, text);
        if (FAILED(hr)) return hr;

        std::vector<std::vector<std::wstring>> rows;
        size_t rowStart = 0;
        while (rowStart <= text.size())
        {
            const size_t rowEnd = text.find_first_of(L"\r\n", rowStart);
            const size_t end = rowEnd == std::wstring::npos ? text.size() : rowEnd;
            const std::wstring name = text.substr(rowStart, end - rowStart);
            if (!name.empty()) rows.push_back({ name });
            if (rowEnd == std::wstring::npos) break;
            rowStart = rowEnd + 1;
            if (text[rowEnd] == L'\r' && rowStart < text.size() && text[rowStart] == L'\n') ++rowStart;
        }
        return BuildNumericMapping(rows, mappings);
    }

    HRESULT LoadNumericRenameMappingFromXlsx(PCWSTR path, NumericRenameMapping& mappings)
    {
        mappings.clear();

        std::vector<unsigned char> archiveBytes;
        HRESULT hr = ReadFileBytes(path, archiveBytes);
        if (FAILED(hr) || archiveBytes.empty()) return FAILED(hr) ? hr : E_INVALIDARG;

        zip_error_t zipError{};
        zip_source_t* source = zip_source_buffer_create(archiveBytes.data(), archiveBytes.size(), 0, nullptr);
        if (!source) return E_FAIL;
        zip_t* archive = zip_open_from_source(source, ZIP_RDONLY, &zipError);
        if (!archive)
        {
            zip_source_free(source);
            return E_FAIL;
        }

        std::vector<std::wstring> sharedStrings;
        std::string sharedBytes;
        if (SUCCEEDED(ReadZipEntry(archive, "xl/sharedStrings.xml", sharedBytes)))
        {
            std::wstring sharedXml;
            if (SUCCEEDED(DecodeXlsxXml(sharedBytes, sharedXml)))
            {
                size_t position = 0;
                while ((position = sharedXml.find(L"<si", position)) != std::wstring::npos)
                {
                    const size_t end = sharedXml.find(L"</si>", position);
                    if (end == std::wstring::npos) break;
                    sharedStrings.push_back(XmlTextElements(sharedXml.substr(position, end - position + 5)));
                    position = end + 5;
                }
            }
        }

        std::string worksheetBytes;
        hr = ReadZipEntry(archive, "xl/worksheets/sheet1.xml", worksheetBytes);
        if (SUCCEEDED(hr))
        {
            std::wstring worksheet;
            hr = DecodeXlsxXml(worksheetBytes, worksheet);
            if (SUCCEEDED(hr))
            {
                std::vector<std::vector<std::wstring>> rows;
                size_t position = 0;
                while ((position = worksheet.find(L"<row", position)) != std::wstring::npos)
                {
                    const size_t rowEnd = worksheet.find(L"</row>", position);
                    if (rowEnd == std::wstring::npos) break;
                    const std::wstring row = worksheet.substr(position, rowEnd - position + 6);
                    std::vector<std::wstring> values;
                    size_t cellStart = 0;
                    while (values.size() < 2 && (cellStart = row.find(L"<c", cellStart)) != std::wstring::npos)
                    {
                        const wchar_t next = cellStart + 2 < row.size() ? row[cellStart + 2] : L'\0';
                        if (next != L'>' && !iswspace(next))
                        {
                            cellStart += 2;
                            continue;
                        }
                        const size_t cellEnd = row.find(L"</c>", cellStart);
                        if (cellEnd == std::wstring::npos) break;
                        values.push_back(FirstXlsxCellValue(row.substr(cellStart, cellEnd - cellStart + 4), sharedStrings));
                        cellStart = cellEnd + 4;
                    }
                    bool hasValue = false;
                    for (const auto& value : values) hasValue = hasValue || !value.empty();
                    if (hasValue) rows.push_back(std::move(values));
                    position = rowEnd + 6;
                }
                hr = BuildNumericMapping(rows, mappings);
            }
        }
        zip_close(archive);
        return hr;
    }

    bool IsValidNumericRenameName(PCWSTR name)
    {
        if (!name || *name == L'\0')
        {
            return false;
        }

        for (const wchar_t character : std::wstring_view{ name })
        {
            if (character == L'<' || character == L'>' || character == L':' || character == L'"' ||
                character == L'\\' || character == L'/' || character == L'|' || character == L'?' || character == L'*')
            {
                return false;
            }
        }

        return true;
    }

    bool TryGetNumericFileStem(PCWSTR fileName, unsigned long long& number)
    {
        number = 0;
        if (!fileName || *fileName == L'\0') return false;
        std::wstring stem(fileName);
        const size_t extension = stem.find_last_of(L'.');
        if (extension != std::wstring::npos) stem.resize(extension);
        if (stem.empty()) return false;
        for (const wchar_t ch : stem) if (ch < L'0' || ch > L'9') return false;
        for (const wchar_t ch : stem)
        {
            const unsigned int digit = static_cast<unsigned int>(ch - L'0');
            if (number > (ULLONG_MAX - digit) / 10)
            {
                return false;
            }
            number = number * 10 + digit;
        }
        return true;
    }
}
