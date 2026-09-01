#include "pch.h"
#include "TextExpansionTypes.h"

#include <algorithm>
#include <map>

#include <keyboardmanager/common/Helpers.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>

namespace
{
    constexpr uint8_t LeftWinMask = 1u << 0;
    constexpr uint8_t RightWinMask = 1u << 1;
    constexpr uint8_t LeftCtrlMask = 1u << 2;
    constexpr uint8_t RightCtrlMask = 1u << 3;
    constexpr uint8_t LeftAltMask = 1u << 4;
    constexpr uint8_t RightAltMask = 1u << 5;
    constexpr uint8_t LeftShiftMask = 1u << 6;
    constexpr uint8_t RightShiftMask = 1u << 7;

    struct MutableNode
    {
        std::map<wchar_t, uint32_t> children;
        uint32_t terminalTable = UINT32_MAX;
    };

    constexpr bool ModifierMatches(
        const uint8_t modifierMask,
        const ModifierKey expected,
        const uint8_t leftMask,
        const uint8_t rightMask) noexcept
    {
        const bool leftDown = (modifierMask & leftMask) != 0;
        const bool rightDown = (modifierMask & rightMask) != 0;
        switch (expected)
        {
        case ModifierKey::Disabled:
            return !leftDown && !rightDown;
        case ModifierKey::Left:
            return leftDown && !rightDown;
        case ModifierKey::Right:
            return !leftDown && rightDown;
        case ModifierKey::Both:
            return leftDown || rightDown;
        default:
            return false;
        }
    }

    constexpr bool ActivationMatchesMask(const Shortcut& activation, const uint8_t modifierMask) noexcept
    {
        return !activation.HasChord() &&
               ModifierMatches(modifierMask, activation.winKey, LeftWinMask, RightWinMask) &&
               ModifierMatches(modifierMask, activation.ctrlKey, LeftCtrlMask, RightCtrlMask) &&
               ModifierMatches(modifierMask, activation.altKey, LeftAltMask, RightAltMask) &&
               ModifierMatches(modifierMask, activation.shiftKey, LeftShiftMask, RightShiftMask);
    }

    constexpr size_t Utf16ScalarCount(const std::wstring_view text) noexcept
    {
        size_t count = 0;
        for (size_t index = 0; index < text.size(); ++index)
        {
            const auto codeUnit = static_cast<uint16_t>(text[index]);
            if (codeUnit >= 0xD800 && codeUnit <= 0xDBFF && index + 1 < text.size())
            {
                const auto next = static_cast<uint16_t>(text[index + 1]);
                if (next >= 0xDC00 && next <= 0xDFFF)
                {
                    ++index;
                }
            }
            ++count;
        }
        return count;
    }
}

TextExpansionIndex::TextExpansionIndex(const TextExpansionTable& sourceRules)
{
    std::vector<MutableNode> mutableNodes;
    const auto addNode = [&mutableNodes]() {
        mutableNodes.emplace_back();
        return static_cast<uint32_t>(mutableNodes.size() - 1);
    };

    rules.reserve(sourceRules.size());
    for (size_t profileIndex = 0; profileIndex < sourceRules.size(); ++profileIndex)
    {
        const auto& sourceRule = sourceRules[profileIndex];
        const DWORD actionKey = Helpers::ClearKeyNumpadOrigin(sourceRule.activation.GetActionKey());
        if (!sourceRule.enabled || sourceRule.sourceText.empty() || sourceRule.replacementText.empty() ||
            sourceRule.activation.HasChord() || actionKey == 0 || actionKey > 0xFF)
        {
            continue;
        }

        auto& actionSlot = actionSlots[actionKey];
        if (actionSlot.rootNode == InvalidIndex)
        {
            actionSlot.rootNode = addNode();
        }

        uint32_t nodeIndex = actionSlot.rootNode;
        for (auto unit = sourceRule.sourceText.rbegin(); unit != sourceRule.sourceText.rend(); ++unit)
        {
            const auto existing = mutableNodes[nodeIndex].children.find(*unit);
            if (existing != mutableNodes[nodeIndex].children.end())
            {
                nodeIndex = existing->second;
                continue;
            }

            const uint32_t childNode = addNode();
            mutableNodes[nodeIndex].children.emplace(*unit, childNode);
            nodeIndex = childNode;
        }

        auto& terminalTableIndex = mutableNodes[nodeIndex].terminalTable;
        if (terminalTableIndex == InvalidIndex)
        {
            terminalTableIndex = static_cast<uint32_t>(terminalRuleTables.size());
            terminalRuleTables.emplace_back();
            terminalRuleTables.back().fill(InvalidIndex);
        }

        const uint32_t indexedRule = static_cast<uint32_t>(rules.size());
        rules.push_back({
            .sourceText = sourceRule.sourceText,
            .replacementText = sourceRule.replacementText,
            .backspaceCount = Utf16ScalarCount(sourceRule.sourceText),
            .profileIndex = profileIndex,
        });

        auto& terminalRules = terminalRuleTables[terminalTableIndex];
        for (size_t mask = 0; mask < terminalRules.size(); ++mask)
        {
            const auto modifierMask = static_cast<uint8_t>(mask);
            if (!ActivationMatchesMask(sourceRule.activation, modifierMask))
            {
                continue;
            }

            actionSlot.modifierMasks.set(mask);
            // Rules are visited in profile order. Keep the first exact-source tie.
            if (terminalRules[mask] == InvalidIndex)
            {
                terminalRules[mask] = indexedRule;
            }
        }
    }

    nodes.resize(mutableNodes.size());
    for (size_t nodeIndex = 0; nodeIndex < mutableNodes.size(); ++nodeIndex)
    {
        const auto& mutableNode = mutableNodes[nodeIndex];
        auto& node = nodes[nodeIndex];
        node.firstEdge = static_cast<uint32_t>(edges.size());
        node.edgeCount = static_cast<uint32_t>(mutableNode.children.size());
        node.terminalTable = mutableNode.terminalTable;
        for (const auto& [codeUnit, childNode] : mutableNode.children)
        {
            edges.push_back({ codeUnit, childNode });
        }
    }
}

bool TextExpansionIndex::Empty() const noexcept
{
    return rules.empty();
}

bool TextExpansionIndex::HasActivation(const DWORD actionKey, const uint8_t modifierMask) const noexcept
{
    return actionKey <= 0xFF && actionSlots[actionKey].modifierMasks.test(modifierMask);
}

std::optional<size_t> TextExpansionIndex::FindLongestMatch(
    const DWORD actionKey,
    const uint8_t modifierMask,
    const std::wstring_view trackedText,
    size_t* traversedCodeUnits) const noexcept
{
    if (traversedCodeUnits)
    {
        *traversedCodeUnits = 0;
    }
    if (!HasActivation(actionKey, modifierMask))
    {
        return std::nullopt;
    }

    uint32_t nodeIndex = actionSlots[actionKey].rootNode;
    uint32_t selectedRule = InvalidIndex;
    size_t traversed = 0;
    for (auto unit = trackedText.rbegin();
         unit != trackedText.rend() && traversed < KeyboardManagerConstants::MaxTextExpansionSourceLength;
         ++unit)
    {
        ++traversed;
        if (traversedCodeUnits)
        {
            *traversedCodeUnits = traversed;
        }

        const auto& node = nodes[nodeIndex];
        const auto first = edges.begin() + node.firstEdge;
        const auto last = first + node.edgeCount;
        const auto edge = std::lower_bound(
            first,
            last,
            *unit,
            [](const Edge& candidate, const wchar_t codeUnit) {
                return candidate.codeUnit < codeUnit;
            });
        if (edge == last || edge->codeUnit != *unit)
        {
            break;
        }

        nodeIndex = edge->childNode;
        const auto terminalTable = nodes[nodeIndex].terminalTable;
        if (terminalTable != InvalidIndex)
        {
            const auto rule = terminalRuleTables[terminalTable][modifierMask];
            if (rule != InvalidIndex)
            {
                selectedRule = rule;
            }
        }
    }

    return selectedRule == InvalidIndex ? std::nullopt : std::optional<size_t>{ selectedRule };
}

const TextExpansionIndex::IndexedRule* TextExpansionIndex::GetRule(const size_t ruleIndex) const noexcept
{
    return ruleIndex < rules.size() ? &rules[ruleIndex] : nullptr;
}
