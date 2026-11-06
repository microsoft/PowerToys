// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include <algorithm>
#include <cstdint>
#include <cstddef>
#include <vector>

namespace WindowTabs
{
    // OS-independent model. A window belongs to at most one group; selection is a
    // stable identity, so reordering cannot accidentally switch applications.
    using WindowId = uintptr_t;
    struct Group
    {
        std::vector<WindowId> tabs;
        WindowId active = 0;

        bool Contains(WindowId id) const
        {
            return std::find(tabs.begin(), tabs.end(), id) != tabs.end();
        }

        bool Add(WindowId id)
        {
            if (!id || Contains(id) || tabs.size() >= 32)
            {
                return false;
            }
            tabs.push_back(id);
            if (!active)
            {
                active = id;
            }
            return true;
        }

        bool Select(WindowId id)
        {
            if (!Contains(id))
            {
                return false;
            }
            active = id;
            return true;
        }

        void Remove(WindowId id)
        {
            const auto it = std::find(tabs.begin(), tabs.end(), id);
            if (it == tabs.end())
            {
                return;
            }
            const auto index = static_cast<size_t>(it - tabs.begin());
            tabs.erase(it);
            if (active == id)
            {
                active = tabs.empty() ? 0 : tabs[(std::min)(index, tabs.size() - 1)];
            }
        }

        bool Reorder(WindowId id, size_t index)
        {
            const auto it = std::find(tabs.begin(), tabs.end(), id);
            if (it == tabs.end() || index >= tabs.size())
            {
                return false;
            }
            tabs.erase(it);
            tabs.insert(tabs.begin() + index, id);
            return true;
        }

        WindowId Cycle(bool forward) const
        {
            if (tabs.empty())
            {
                return 0;
            }
            const auto it = std::find(tabs.begin(), tabs.end(), active);
            const auto index = static_cast<size_t>(it - tabs.begin());
            return tabs[(index + (forward ? 1 : tabs.size() - 1)) % tabs.size()];
        }
    };
}
