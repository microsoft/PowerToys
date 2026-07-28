#include "pch.h"
#include "NumericRenameMapping.h"
#include <climits>

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

    std::wstring FirstCsvField(const std::wstring& row)
    {
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
                break;
            }
            else
            {
                field.push_back(row[i]);
            }
        }
        while (!field.empty() && iswspace(field.back())) field.pop_back();
        size_t first = 0;
        while (first < field.size() && iswspace(field[first])) ++first;
        return field.substr(first);
    }
}

namespace PowerRenameLib
{
    HRESULT LoadNumericRenameMappingFromCsv(PCWSTR path, std::vector<std::wstring>& names)
    {
        names.clear();
        std::vector<unsigned char> bytes;
        RETURN_IF_FAILED(ReadFileBytes(path, bytes));
        std::wstring text;
        RETURN_IF_FAILED(DecodeCsvBytes(bytes, text));

        size_t rowStart = 0;
        while (rowStart <= text.size())
        {
            const size_t rowEnd = text.find_first_of(L"\r\n", rowStart);
            const size_t end = rowEnd == std::wstring::npos ? text.size() : rowEnd;
            const std::wstring name = FirstCsvField(text.substr(rowStart, end - rowStart));
            if (!name.empty()) names.push_back(name);
            if (rowEnd == std::wstring::npos) break;
            rowStart = rowEnd + 1;
            if (text[rowEnd] == L'\r' && rowStart < text.size() && text[rowStart] == L'\n') ++rowStart;
        }
        return names.empty() ? E_INVALIDARG : S_OK;
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
