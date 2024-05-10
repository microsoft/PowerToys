#include "pch.h"

#include "Randomizer.h"

std::vector<RandomizerOptions> parseRandomizerOptions(const std::wstring& replaceWith)
{
    static const std::wregex randBasicRegex(LR"(rstring=(\d+))");
    static const std::wregex randCharRegex(LR"(rstringchar=(-?\d+))");
    static const std::wregex randNumRegex(LR"(rstringnum=(\d+))");
    static const std::wregex randUuidRegex(LR"(ruuidv4)");

    std::string buf;
    std::vector<RandomizerOptions> options;
    std::wregex randGroupRegex(LR"(\$\{.*?\})");

    for (std::wsregex_iterator i{ begin(replaceWith), end(replaceWith), randGroupRegex }, end; i != end; ++i)
    {
        std::wsmatch match = *i;
        std::wstring matchString = match.str();

        RandomizerOptions option;
        option.replaceStrSpan.offset = match.position();
        option.replaceStrSpan.length = match.length();

        std::wsmatch subMatch;
        if (std::regex_search(matchString, subMatch, randBasicRegex))
        {
            int length = std::stoi(subMatch.str(1));
            option.basic = true;
            option.length = length;
        }
        if (std::regex_search(matchString, subMatch, randCharRegex))
        {
            int length = std::stoi(subMatch.str(1));
            option.characters = true;
            option.length = length;
        }
        if (std::regex_search(matchString, subMatch, randNumRegex))
        {
            int length = std::stoi(subMatch.str(1));
            option.numbers = true;
            option.length = length;
        }
        if (std::regex_search(matchString, subMatch, randUuidRegex))
        {
            option.uuid = true;
        }

        options.push_back(option);
    }

    return options;
}
