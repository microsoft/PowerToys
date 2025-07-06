// Test.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <iostream>
#include <fstream>
#include <PowerRenameRegEx.h>

extern "C" int LLVMFuzzerTestOneInput(const uint8_t* data, size_t size)
{
    if (size < 6)
        return 0;

    size_t offset = 0;

    size_t input_len = size / 3;
    size_t find_len = size / 3;
    size_t replace_len = size - input_len - find_len;

    auto read_wstring = [&](size_t len) -> std::wstring {
        std::wstring result;
        if (offset + len > size)
            len = size - offset;

        result.assign(reinterpret_cast<const wchar_t*>(data + offset), len / sizeof(wchar_t));
        offset += len;
        return result;
    };

    std::wstring input = read_wstring(input_len);
    std::wstring find = read_wstring(find_len);
    std::wstring replace = read_wstring(replace_len);

    if (find.empty() || replace.empty())
        return 0;

    CComPtr<IPowerRenameRegEx> renamer;
    CPowerRenameRegEx::s_CreateInstance(&renamer);

    renamer->PutFlags(UseRegularExpressions | CaseSensitive);

    renamer->PutSearchTerm(find.c_str());
    renamer->PutReplaceTerm(replace.c_str());

    PWSTR result = nullptr;
    unsigned long index = 0;
    HRESULT hr = renamer->Replace(input.c_str(), &result, index);
    if (SUCCEEDED(hr) && result != nullptr)
    {
        CoTaskMemFree(result);
    }

    return 0;
}

#ifndef DISABLE_FOR_FUZZING

int main(int argc, char** argv)
{
    const char8_t raw[] = u8"test_string";

    std::vector<uint8_t> data(reinterpret_cast<const uint8_t*>(raw), reinterpret_cast<const uint8_t*>(raw) + sizeof(raw) - 1);

    LLVMFuzzerTestOneInput(data.data(), data.size());
    return 0;
}

#endif
