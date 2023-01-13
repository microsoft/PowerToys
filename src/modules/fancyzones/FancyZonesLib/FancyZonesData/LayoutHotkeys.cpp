#include "../pch.h"
#include "LayoutHotkeys.h"

#include <common/logger/logger.h>

#include <FancyZonesLib/FancyZonesWinHookEventIDs.h>
#include <FancyZonesLib/JsonHelpers.h>
#include <FancyZonesLib/util.h>

namespace JsonUtils
{
    struct LayoutHotkeysJSON
    {
        GUID uuid;
        int key;

        static std::optional<LayoutHotkeysJSON> FromJson(const json::JsonObject& json)
        {
            try
            {
                LayoutHotkeysJSON result;

                std::wstring uuidStr = json.GetNamedString(NonLocalizable::LayoutHotkeysIds::LayoutUuidID).c_str();
                auto uuidOpt = FancyZonesUtils::GuidFromString(uuidStr);
                if (!uuidOpt)
                {
                    return std::nullopt;
                }

                result.uuid = uuidOpt.value();
                result.key = static_cast<int>(json.GetNamedNumber(NonLocalizable::LayoutHotkeysIds::KeyID));

                return result;
            }
            catch (const winrt::hresult_error&)
            {
                return std::nullopt;
            }
        }
    };

    LayoutHotkeys::THotkeyMap ParseJson(const json::JsonObject& json)
    {
        LayoutHotkeys::THotkeyMap map{};
        auto layoutHotkeys = json.GetNamedArray(NonLocalizable::LayoutHotkeysIds::LayoutHotkeysArrayID);

        for (uint32_t i = 0; i < layoutHotkeys.Size(); ++i)
        {
            if (auto obj = LayoutHotkeysJSON::FromJson(layoutHotkeys.GetObjectAt(i)); obj.has_value())
            {
                map[obj->key] = obj->uuid;
            }
        }

        return std::move(map);
    }
}


LayoutHotkeys::LayoutHotkeys()
{
    const std::wstring& settingsFileName = LayoutHotkeysFileName();
    m_fileWatcher = std::make_unique<FileWatcher>(settingsFileName, [&]() {
        PostMessageW(HWND_BROADCAST, WM_PRIV_LAYOUT_HOTKEYS_FILE_UPDATE, NULL, NULL);
    });
}

LayoutHotkeys& LayoutHotkeys::instance()
{
    static LayoutHotkeys self;
    return self;
}

void LayoutHotkeys::LoadData()
{
    auto data = json::from_file(LayoutHotkeysFileName());
    
    try
    {
        if (data)
        {
            m_hotkeyMap = JsonUtils::ParseJson(data.value());
        }
        else
        {
            m_hotkeyMap.clear();
            Logger::info(L"layout-hotkeys.json file is missing or malformed");
        }
    }
    catch (const winrt::hresult_error& e)
    {
        Logger::error(L"Parsing layout-hotkeys error: {}", e.message());
    }
}

std::optional<GUID> LayoutHotkeys::GetLayoutId(int key) const noexcept
{
    auto iter = m_hotkeyMap.find(key);
    if (iter != m_hotkeyMap.end())
    {
        return iter->second;
    }

    return std::nullopt;
}

size_t LayoutHotkeys::GetHotkeysCount() const noexcept
{
    return m_hotkeyMap.size();
}
