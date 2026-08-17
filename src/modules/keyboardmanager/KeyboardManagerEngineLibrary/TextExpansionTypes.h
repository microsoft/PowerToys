#pragma once

#include <chrono>
#include <cstdint>
#include <functional>
#include <optional>
#include <span>
#include <string>
#include <vector>

#include <keyboardmanager/common/Shortcut.h>

enum class TextExpansionResult : uint8_t
{
    Prepared,
    Replaced,
    NoMatch,
    UnsupportedContext,
    FailedUnchanged,
    FailedChangedOrUnknown,
};

struct TextExpansionCandidate
{
    std::wstring sourceText;
    std::wstring replacementText;
    size_t profileIndex = 0;
};

struct TextExpansionRequest
{
    Shortcut activationShortcut;
    std::vector<DWORD> activationModifierKeys;
    std::vector<TextExpansionCandidate> candidates;
    std::chrono::steady_clock::time_point deadline;
};

enum class TextExpansionPreparationStatus : uint8_t
{
    Prepared,
    NoMatch,
    UnsupportedContext,
    FailedUnchanged,
    FailedChangedOrUnknown,
};

struct TextExpansionPreparationResult
{
    TextExpansionPreparationStatus status = TextExpansionPreparationStatus::UnsupportedContext;
    std::wstring replacementText;
    size_t profileIndex = 0;
};

// Returns the original candidate index. Longest suffix wins; an equal duplicate
// keeps the earlier profile entry.
std::optional<size_t> SelectTextExpansionCandidate(
    std::span<const TextExpansionCandidate> candidates,
    std::wstring_view caretPrefix) noexcept;

constexpr bool IsTextExpansionReplacementExact(
    std::wstring_view textFromOriginalSourceStartToCaret,
    std::wstring_view replacementText) noexcept
{
    return textFromOriginalSourceStartToCaret == replacementText;
}
