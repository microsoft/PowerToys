#pragma once

#include "pch.h"

#include "Helpers.h"

#include <common\utils\string_utils.h>

struct ReplaceStrSpan
{
    size_t offset = 0;
    size_t length = 0;
};

struct RandomizerOptions
{
    std::optional<int> length;
    std::optional<boolean> alnum;
    std::optional<boolean> alpha;
    std::optional<boolean> digit;
    std::optional<boolean> uuid;
    ReplaceStrSpan replaceStrSpan;

    bool isValid() const
    {
        return alnum.has_value() || alpha.has_value() || digit.has_value() || uuid.has_value();
    }
};

std::vector<RandomizerOptions> parseRandomizerOptions(const std::wstring& replaceWith);

struct Randomizer
{
    RandomizerOptions options;

    inline Randomizer(RandomizerOptions opts) :
        options(opts) {}

    std::string randomize() const
    {
        std::string chars;

        if (options.uuid.value_or(false))
        {
            return unwide(CreateGuidStringWithoutBrackets());
        }
        if (options.alnum.value_or(false))
        {
            chars += "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        }
        if (options.alpha.value_or(false))
        {
            chars += "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        }
        if (options.digit.value_or(false))
        {
            chars += "0123456789";
        }
        if (chars.empty())
        {
            return "";
        }

        std::string result;
        std::random_device rd;
        std::mt19937 generator(rd());
        std::uniform_int_distribution<> distribution(0, static_cast<int>(chars.size()) - 1);

        for (int i = 0; i < options.length.value_or(10); ++i)
        {
            result += chars[distribution(generator)];
        }

        return result;
    }
};