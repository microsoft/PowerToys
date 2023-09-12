#pragma once

#include "pch.h"

struct EnumSpan
{
    size_t offset = 0;
    size_t length = 0;
};

struct EnumOptions
{
    std::optional<int> start;
    std::optional<int> increment;
    std::optional<uint32_t> padding;

    EnumSpan replaceStrSpan;

    std::strong_ordering operator<=>(const EnumOptions& rhs) const noexcept
    {
        return std::make_tuple(start, increment, padding) <=> std::make_tuple(rhs.start, rhs.increment, rhs.padding);
    }

    bool operator==(const EnumOptions& rhs) const noexcept
    {
        return std::make_tuple(start, increment, padding) == std::make_tuple(rhs.start, rhs.increment, rhs.padding);
    }
};

std::vector<EnumOptions> parseEnumOptions(const std::wstring& replaceWith);

struct Enumerator
{
    inline Enumerator(EnumOptions options) :
        start{ options.start.value_or(0) }, increment{ options.increment.value_or(1) }, padding{ options.padding.value_or(0) % MAX_PATH }, replaceStrSpan{ options.replaceStrSpan }
    {
    }

    inline int32_t enumerate(const unsigned long index) const { return start + static_cast<int32_t>(index * increment); }

    inline size_t printTo(wchar_t* buf, const size_t bufSize, const unsigned long index) const
    {
        const int32_t enumeratedIndex = enumerate(index);
        wchar_t format[32];
        swprintf_s(format, sizeof(format) / sizeof(wchar_t), L"%%0%ud", padding);

        //swprintf panics when the buffer is too small, so we're checking the required buffer size ahead of printing to it and falling back to 0 padding in case it cannot fit. Note that we must use swprintf with nullptr buf, because the _s version panics on it as well.
        const size_t requiredBufSize = swprintf(nullptr, 0, format, enumeratedIndex) + 1ull;
        const bool fitsBuf = requiredBufSize < bufSize;
        if (!fitsBuf)
        {
            swprintf_s(format, sizeof(format) / sizeof(wchar_t), L"%%%ud", 0);
        }

        return swprintf_s(buf, bufSize, format, enumeratedIndex);
    }

    EnumSpan replaceStrSpan;

private:
    int start;
    int increment;
    uint32_t padding;
};
