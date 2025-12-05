#include <pch.h>

#include "Enumerating.h"

#include <common\utils\string_utils.h>

std::vector<EnumOptions> parseEnumOptions(const std::wstring& replaceWith)
{
    static const std::wregex enumStartRegex(LR"(start=(-?\d+))");
    static const std::wregex enumIncrementRegex(LR"(increment=(-?\d+))");
    static const std::wregex enumPaddingRegex(LR"(padding=(\d+))");

    std::string buf;
    std::vector<EnumOptions> options;
    std::wregex enumGroupRegex(LR"(\$\{.*?\})");
    for (std::wsregex_iterator i{ begin(replaceWith), end(replaceWith), enumGroupRegex }, end; i != end; ++i)
    {
        std::wsmatch match = *i;
        std::wstring matchString = match.str();

        EnumOptions option;
        option.replaceStrSpan.offset = match.position();
        option.replaceStrSpan.length = match.length();

        std::wsmatch subMatch;
        if (std::regex_search(matchString, subMatch, enumStartRegex))
        {
            buf = unwide(subMatch[1].str());
            std::from_chars(buf.data(), buf.data() + buf.size(), option.start.emplace());
        }
        if (std::regex_search(matchString, subMatch, enumIncrementRegex))
        {
            buf = unwide(subMatch[1].str());
            std::from_chars(buf.data(), buf.data() + buf.size(), option.increment.emplace());
        }
        if (std::regex_search(matchString, subMatch, enumPaddingRegex))
        {
            buf = unwide(subMatch[1].str());
            std::from_chars(buf.data(), buf.data() + buf.size(), option.padding.emplace());
        }

        options.emplace_back(std::move(option));
    }

    return options;
}
