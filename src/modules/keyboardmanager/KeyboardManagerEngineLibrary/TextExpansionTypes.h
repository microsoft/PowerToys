#pragma once

#include <cstdint>
#include <optional>
#include <span>
#include <string>
#include <string_view>
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
};

// Returns the original candidate index. Longest suffix wins; an equal duplicate
// keeps the earlier profile entry.
std::optional<size_t> SelectTextExpansionCandidate(
    std::span<const TextExpansionCandidate> candidates,
    std::wstring_view trackedText) noexcept;
