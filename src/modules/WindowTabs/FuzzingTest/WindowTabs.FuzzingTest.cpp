// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <windows.h>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <set>
#include "../GroupModel.h"
#include "../Session.h"

extern "C" int LLVMFuzzerTestOneInput(const uint8_t* data, size_t size)
{
    static const bool initialized = [] { winrt::init_apartment(); return true; }();
    (void)initialized;
    WindowTabs::Group group;
    for (size_t index = 0; index + 2 < size; index += 3)
    {
        const auto id = static_cast<WindowTabs::WindowId>(data[index + 1]);
        switch (data[index] % 6)
        {
        case 0:
            group.Add(id);
            break;
        case 1:
            group.Remove(id);
            break;
        case 2:
            group.Select(id);
            break;
        case 3:
            group.Reorder(id, data[index + 2]);
            break;
        case 4:
            group.Select(group.Cycle(true));
            break;
        case 5:
            group.Select(group.Cycle(false));
            break;
        }
        const std::set<WindowTabs::WindowId> unique(group.tabs.begin(), group.tabs.end());
        if (unique.size() != group.tabs.size() || group.tabs.size() > 32 || (!group.tabs.empty() && !group.Contains(group.active)) || (group.tabs.empty() && group.active))
            std::abort();
    }
    if (size <= 1024 * 1024)
    {
        try
        {
            auto text = winrt::to_hstring(std::string_view(reinterpret_cast<const char*>(data), size));
            const auto saved = WindowTabs::ParseSession(text);
            const auto roundTrip = WindowTabs::ParseSession(WindowTabs::SerializeSession(saved));
            if (saved.size() != roundTrip.size())
                std::abort();
        }
        catch (const winrt::hresult_error&)
        {
        } // Invalid UTF-8 is rejected.
    }
    return 0;
}

#ifndef DISABLE_FOR_FUZZING
int main()
{
    const uint8_t seed[] = { 0, 1, 0, 0, 2, 0, 2, 2, 0, 3, 1, 1, 1, 2, 0 };
    return LLVMFuzzerTestOneInput(seed, sizeof(seed));
}
#endif
