#include "pch.h"

#include "Randomizer.h"

std::vector<RandomizerOptions> parseRandomizerOptions(const std::wstring& replaceWith)
{
    static const std::wregex randAlnumRegex(LR"(rstringalnum=(\d+))");
    static const std::wregex randAlphaRegex(LR"(rstringalpha=(-?\d+))");
    static const std::wregex randDigitRegex(LR"(rstringdigit=(\d+))");
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
        if (std::regex_search(matchString, subMatch, randAlnumRegex))
        {
            int length = std::stoi(subMatch.str(1));
            option.alnum = true;
            option.length = length;
        }
        if (std::regex_search(matchString, subMatch, randAlphaRegex))
        {
            int length = std::stoi(subMatch.str(1));
            option.alpha = true;
            option.length = length;
        }
        if (std::regex_search(matchString, subMatch, randDigitRegex))
        {
            int length = std::stoi(subMatch.str(1));
            option.digit = true;
            option.length = length;
        }
        if (std::regex_search(matchString, subMatch, randUuidRegex))
        {
            option.uuid = true;
        }
        if (option.isValid())
        {
            options.push_back(option);
        }
    }

    return options;
}
