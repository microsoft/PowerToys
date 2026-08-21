#include "pch.h"
#include "TextExpansionTypes.h"

std::optional<size_t> SelectTextExpansionCandidate(
    const std::span<const TextExpansionCandidate> candidates,
    const std::wstring_view caretPrefix) noexcept
{
    std::optional<size_t> selected;
    size_t selectedLength = 0;
    for (size_t index = 0; index < candidates.size(); ++index)
    {
        const auto& source = candidates[index].sourceText;
        if (source.size() < selectedLength || source.size() > caretPrefix.size() ||
            !caretPrefix.ends_with(source))
        {
            continue;
        }

        if (!selected || source.size() > selectedLength)
        {
            selected = index;
            selectedLength = source.size();
        }
    }
    return selected;
}
