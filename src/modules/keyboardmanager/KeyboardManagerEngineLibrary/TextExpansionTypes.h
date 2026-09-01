#pragma once

#include <array>
#include <bitset>
#include <cstdint>
#include <memory>
#include <optional>
#include <string>
#include <string_view>
#include <vector>

#include <keyboardmanager/common/MappingConfiguration.h>

enum class TextExpansionResult : uint8_t
{
    Prepared,
    Replaced,
    NoMatch,
    UnsupportedContext,
    FailedUnchanged,
    FailedChangedOrUnknown,
};

namespace TextExpansionModifiers
{
    // Keep side-specific modifier state in one byte so low-level hook matching and
    // pending-press bookkeeping never need dynamically allocated collections.
    inline constexpr uint8_t LeftWin = 1u << 0;
    inline constexpr uint8_t RightWin = 1u << 1;
    inline constexpr uint8_t LeftCtrl = 1u << 2;
    inline constexpr uint8_t RightCtrl = 1u << 3;
    inline constexpr uint8_t LeftAlt = 1u << 4;
    inline constexpr uint8_t RightAlt = 1u << 5;
    inline constexpr uint8_t LeftShift = 1u << 6;
    inline constexpr uint8_t RightShift = 1u << 7;
    inline constexpr uint8_t All =
        LeftWin | RightWin | LeftCtrl | RightCtrl |
        LeftAlt | RightAlt | LeftShift | RightShift;
    inline constexpr std::array<uint8_t, 8> Bits{
        LeftWin,
        RightWin,
        LeftCtrl,
        RightCtrl,
        LeftAlt,
        RightAlt,
        LeftShift,
        RightShift,
    };
    inline constexpr std::array<DWORD, 8> Keys{
        VK_LWIN,
        VK_RWIN,
        VK_LCONTROL,
        VK_RCONTROL,
        VK_LMENU,
        VK_RMENU,
        VK_LSHIFT,
        VK_RSHIFT,
    };

    constexpr uint8_t BitForKey(const DWORD key) noexcept
    {
        switch (key)
        {
        case VK_LWIN:
            return LeftWin;
        case VK_RWIN:
            return RightWin;
        case VK_LCONTROL:
            return LeftCtrl;
        case VK_RCONTROL:
            return RightCtrl;
        case VK_LMENU:
            return LeftAlt;
        case VK_RMENU:
            return RightAlt;
        case VK_LSHIFT:
            return LeftShift;
        case VK_RSHIFT:
            return RightShift;
        default:
            return 0;
        }
    }

    constexpr size_t Count(const uint8_t mask) noexcept
    {
        size_t count = 0;
        for (uint8_t remaining = mask; remaining != 0; remaining >>= 1)
        {
            count += remaining & 1u;
        }
        return count;
    }
}

// Immutable configuration-time index. Each action key owns one reverse UTF-16
// trie. Terminal nodes contain an O(1) table from the exact left/right modifier
// state to the earliest matching profile rule, so hook-time lookup is bounded by
// the maximum source length rather than by the total number of configured rules.
class TextExpansionIndex final
{
public:
    struct IndexedRule
    {
        std::wstring sourceText;
        std::wstring replacementText;
        size_t backspaceCount = 0;
        size_t profileIndex = 0;
    };

    explicit TextExpansionIndex(const TextExpansionTable& sourceRules);

    bool Empty() const noexcept;
    bool HasActivation(DWORD actionKey, uint8_t modifierMask) const noexcept;
    std::optional<size_t> FindLongestMatch(
        DWORD actionKey,
        uint8_t modifierMask,
        std::wstring_view trackedText,
        size_t* traversedCodeUnits = nullptr) const noexcept;
    const IndexedRule* GetRule(size_t ruleIndex) const noexcept;

private:
    static constexpr uint32_t InvalidIndex = UINT32_MAX;

    struct Edge
    {
        wchar_t codeUnit = L'\0';
        uint32_t childNode = InvalidIndex;
    };

    struct Node
    {
        uint32_t firstEdge = 0;
        uint32_t edgeCount = 0;
        uint32_t terminalTable = InvalidIndex;
    };

    struct ActionSlot
    {
        uint32_t rootNode = InvalidIndex;
        std::bitset<256> modifierMasks;
    };

    std::array<ActionSlot, 256> actionSlots;
    std::vector<Node> nodes;
    std::vector<Edge> edges;
    std::vector<std::array<uint32_t, 256>> terminalRuleTables;
    std::vector<IndexedRule> rules;
};

struct TextExpansionRequest
{
    std::shared_ptr<const TextExpansionIndex> index;
    DWORD actionKey = 0;
    uint8_t modifierMask = 0;
};

struct TextExpansionRecoveryRequest
{
    DWORD actionKey = 0;
    DWORD actionScanCode = 0;
    bool actionExtended = false;
    DWORD replayKey = 0;
    DWORD replayScanCode = 0;
    bool replayExtended = false;
    uint8_t releasedActivationModifierMask = 0;
};
