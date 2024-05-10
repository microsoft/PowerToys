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
    std::optional<boolean> basic;
    std::optional<boolean> characters;
    std::optional<boolean> numbers;
    std::optional<boolean> uuid;
    ReplaceStrSpan replaceStrSpan;
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
            std::string uuid = unwide(CreateGuidString());

            if (!uuid.empty() && uuid.front() == '{' && uuid.back() == '}')
            {
                uuid = uuid.substr(1, uuid.size() - 2);
            }

            return uuid;
        }
        if (options.basic.value_or(false))
        {
            chars += "abcdefghijklmnopqrstuvwxyz0123456789";
        }
        if (options.characters.value_or(false))
        {
            chars += "abcdefghijklmnopqrstuvwxyz";
        }
        if (options.numbers.value_or(false))
        {
            chars += "0123456789";
        }

        if (chars.size() > static_cast<size_t>(std::numeric_limits<int>::max()))
        {
            throw std::overflow_error("Size of chars exceeds maximum int value");
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