#pragma once

#include <common/utils/json.h>

#include <map>
#include <mutex>
#include <set>
#include <string>

class PowerToysActionRegistry
{
public:
    static PowerToysActionRegistry& Instance();

    json::JsonArray ListActions();

    json::JsonObject InvokeAction(const std::wstring& action_id, const std::wstring& serialized_args);

private:
    struct RegisteredAction
    {
        std::wstring module_key;
        json::JsonObject descriptor;
    };

    std::mutex mutex;
    std::map<std::wstring, RegisteredAction> actions;
    std::set<std::wstring> duplicate_action_ids;

    static json::JsonObject ErrorResult(const std::wstring& error_code, const std::wstring& message);

    void RefreshLocked();
};
