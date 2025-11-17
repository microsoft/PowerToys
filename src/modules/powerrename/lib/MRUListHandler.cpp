#include "pch.h"
#include "MRUListHandler.h"
#include "Helpers.h"

#include <dll/PowerRenameConstants.h>
#include <common/SettingsAPI/settings_helpers.h>

namespace
{
    const wchar_t c_mruList[] = L"MRUList";
    const wchar_t c_insertionIdx[] = L"InsertionIdx";
    const wchar_t c_maxMRUSize[] = L"MaxMRUSize";
}

MRUListHandler::MRUListHandler(unsigned int size, const std::wstring& filePath, const std::wstring& regPath) :
    pushIdx(0),
    nextIdx(1),
    size(size),
    jsonFilePath(PTSettingsHelper::get_module_save_folder_location(PowerRenameConstants::ModuleKey) + filePath),
    registryFilePath(regPath)
{
    items.resize(size);
    Load();
}

void MRUListHandler::Push(const std::wstring& data)
{
    if (Exists(data))
    {
        // TODO: Already existing item should be put on top of MRU list.
        return;
    }
    items[pushIdx] = data;
    pushIdx = (pushIdx + 1) % size;
    Save();
}

bool MRUListHandler::Next(std::wstring& data)
{
    if (nextIdx == size + 1)
    {
        Reset();
        return false;
    }
    // Go backwards to consume latest items first.
    unsigned int idx = (pushIdx + size - nextIdx) % size;
    if (items[idx].empty())
    {
        Reset();
        return false;
    }
    data = items[idx];
    ++nextIdx;
    return true;
}

void MRUListHandler::Reset()
{
    nextIdx = 1;
}

const std::vector<std::wstring>& MRUListHandler::GetItems()
{
    return items;
}

void MRUListHandler::Load()
{
    if (!std::filesystem::exists(jsonFilePath))
    {
        MigrateFromRegistry();

        Save();
    }
    else
    {
        ParseJson();
    }
}

void MRUListHandler::Save()
{
    json::JsonObject jsonData;

    jsonData.SetNamedValue(c_maxMRUSize, json::value(size));
    jsonData.SetNamedValue(c_insertionIdx, json::value(pushIdx));
    jsonData.SetNamedValue(c_mruList, Serialize());

    json::to_file(jsonFilePath, jsonData);
}

json::JsonArray MRUListHandler::Serialize()
{
    json::JsonArray searchMRU{};

    std::wstring data{};
    for (const std::wstring& item : items)
    {
        searchMRU.Append(json::value(item));
    }

    return searchMRU;
}

void MRUListHandler::MigrateFromRegistry()
{
    std::wstring searchListKeys = GetRegString(c_mruList, registryFilePath);
    std::sort(std::begin(searchListKeys), std::end(searchListKeys));
    for (const wchar_t& key : searchListKeys)
    {
        Push(GetRegString(std::wstring(1, key), registryFilePath));
    }
}

void MRUListHandler::ParseJson()
{
    auto json = json::from_file(jsonFilePath);
    if (json)
    {
        const json::JsonObject& jsonObject = json.value();
        try
        {
            unsigned int oldSize{ size };
            if (json::has(jsonObject, c_maxMRUSize, json::JsonValueType::Number))
            {
                oldSize = static_cast<unsigned int>(jsonObject.GetNamedNumber(c_maxMRUSize));
            }
            unsigned int oldPushIdx{ 0 };
            if (json::has(jsonObject, c_insertionIdx, json::JsonValueType::Number))
            {
                oldPushIdx = static_cast<unsigned int>(jsonObject.GetNamedNumber(c_insertionIdx));
                if (oldPushIdx < 0 || oldPushIdx >= oldSize)
                {
                    oldPushIdx = 0;
                }
            }
            if (json::has(jsonObject, c_mruList, json::JsonValueType::Array))
            {
                auto jsonArray = jsonObject.GetNamedArray(c_mruList);
                if (oldSize == size)
                {
                    for (uint32_t i = 0; i < jsonArray.Size(); ++i)
                    {
                        items[i] = std::wstring(jsonArray.GetStringAt(i));
                    }
                    pushIdx = oldPushIdx;
                }
                else
                {
                    std::vector<std::wstring> temp;
                    for (unsigned int i = 0; i < std::min(jsonArray.Size(), size); ++i)
                    {
                        int idx = (oldPushIdx + oldSize - (i + 1)) % oldSize;
                        temp.push_back(std::wstring(jsonArray.GetStringAt(idx)));
                    }
                    if (size > oldSize)
                    {
                        std::reverse(std::begin(temp), std::end(temp));
                        pushIdx = static_cast<unsigned int>(temp.size());
                        temp.resize(size);
                    }
                    else
                    {
                        temp.resize(size);
                        std::reverse(std::begin(temp), std::end(temp));
                    }
                    items = std::move(temp);
                    Save();
                }
            }
        }
        catch (const winrt::hresult_error&)
        {
        }
    }
}

bool MRUListHandler::Exists(const std::wstring& data)
{
    return std::find(std::begin(items), std::end(items), data) != std::end(items);
}
