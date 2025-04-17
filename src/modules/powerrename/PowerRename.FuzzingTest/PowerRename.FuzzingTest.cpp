// Test.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <iostream>
#include <PowerRenameRegEx.h>

extern "C" int LLVMFuzzerTestOneInput(const uint8_t* data, size_t size)
{
    if (size < 4)
        return 0;

    size_t offset = 0;

    auto read_length = [&](size_t max_len) -> size_t {
        if (offset + 1 > size)
            return 0;
        uint8_t len = data[offset++];
        return (std::min)(static_cast<size_t>(len), max_len);
    };

    auto read_wstring = [&](size_t max_len) -> std::wstring {
        size_t len = read_length(max_len);
        if (offset + len > size)
            len = size - offset;
        std::string utf8(reinterpret_cast<const char*>(data + offset), len);
        offset += len;

        int wide_len = MultiByteToWideChar(CP_UTF8, 0, utf8.c_str(), -1, nullptr, 0);
        if (wide_len == 0)
            return L"";

        std::wstring wide(wide_len, L'\0');
        MultiByteToWideChar(CP_UTF8, 0, utf8.c_str(), -1, &wide[0], wide_len);
        return wide;
    };

    std::wstring input = read_wstring(100);
    std::wstring find = read_wstring(50);
    std::wstring replace = read_wstring(100);

    CComPtr<IPowerRenameRegEx> renamer;
    CPowerRenameRegEx::s_CreateInstance(&renamer);

    renamer->PutFlags(UseRegularExpressions | CaseSensitive);

    renamer->PutSearchTerm(find.c_str());
    renamer->PutReplaceTerm(replace.c_str());

    BSTR result{};
    unsigned long index = 0;

    renamer->Replace(input.c_str(), &result, index);

    if (result)
        SysFreeString(result);

    return 0;
}
