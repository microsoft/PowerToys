// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include "GroupModel.h"
#include <algorithm>
#include <cmath>
#include <string>
#include <vector>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Foundation.Collections.h>

namespace WindowTabs
{
    struct SavedWindow
    {
        std::wstring executable;
        std::wstring windowClass;
        std::wstring title;
        bool operator==(const SavedWindow&) const = default;
    };

    struct SavedGroup
    {
        std::vector<SavedWindow> tabs;
        size_t selected = 0;
    };

    inline std::vector<WindowId> MatchGroup(const SavedGroup& group, const std::vector<std::pair<WindowId, SavedWindow>>& candidates)
    {
        std::vector<WindowId> result;
        for (const auto& tab : group.tabs)
        {
            WindowId match = 0;
            for (const auto& [id, identity] : candidates)
            {
                if (identity == tab)
                {
                    if (match || !id)
                        return {};
                    match = id;
                }
            }
            if (!match || std::find(result.begin(), result.end(), match) != result.end())
                return {};
            result.push_back(match);
        }
        return result;
    }

    // No HWNDs, arguments or commands are persisted. Matching requires all three
    // fields and a unique live candidate; ambiguous matches are left untouched.
    inline std::vector<SavedGroup> ParseSession(std::wstring_view text)
    {
        using namespace winrt::Windows::Data::Json;
        if (text.size() > 512 * 1024)
        {
            return {};
        }
        try
        {
            auto root = JsonObject::Parse(winrt::hstring(text));
            if (root.GetNamedNumber(L"version") != 1)
            {
                return {};
            }
            auto groups = root.GetNamedArray(L"groups");
            if (groups.Size() > 64)
            {
                return {};
            }
            std::vector<SavedGroup> result;
            std::vector<SavedWindow> used;
            for (const auto& value : groups)
            {
                auto item = value.GetObject();
                auto tabs = item.GetNamedArray(L"tabs");
                const double selected = item.GetNamedNumber(L"selected");
                if (tabs.Size() < 2 || tabs.Size() > 32 || !std::isfinite(selected) || selected < 0 || selected >= tabs.Size() || std::floor(selected) != selected)
                {
                    return {};
                }
                SavedGroup group;
                group.selected = static_cast<size_t>(selected);
                for (const auto& tab : tabs)
                {
                    auto object = tab.GetObject();
                    SavedWindow window{ object.GetNamedString(L"executable").c_str(), object.GetNamedString(L"class").c_str(), object.GetNamedString(L"title").c_str() };
                    if (window.executable.empty() || window.executable.size() > 32768 || window.windowClass.empty() || window.windowClass.size() > 256 || window.title.empty() || window.title.size() > 4096 || std::find(used.begin(), used.end(), window) != used.end())
                    {
                        return {};
                    }
                    used.push_back(window);
                    group.tabs.push_back(std::move(window));
                }
                result.push_back(std::move(group));
            }
            return result;
        }
        catch (const winrt::hresult_error&)
        {
            return {};
        }
    }

    inline std::wstring SerializeSession(const std::vector<SavedGroup>& groups)
    {
        using namespace winrt::Windows::Data::Json;
        JsonArray items;
        for (const auto& group : groups)
        {
            JsonArray tabs;
            for (const auto& window : group.tabs)
            {
                JsonObject tab;
                tab.Insert(L"executable", JsonValue::CreateStringValue(window.executable));
                tab.Insert(L"class", JsonValue::CreateStringValue(window.windowClass));
                tab.Insert(L"title", JsonValue::CreateStringValue(window.title));
                tabs.Append(tab);
            }
            JsonObject item;
            item.Insert(L"tabs", tabs);
            item.Insert(L"selected", JsonValue::CreateNumberValue(static_cast<double>(group.selected)));
            items.Append(item);
        }
        JsonObject root;
        root.Insert(L"version", JsonValue::CreateNumberValue(1));
        root.Insert(L"groups", items);
        return root.Stringify().c_str();
    }
}
