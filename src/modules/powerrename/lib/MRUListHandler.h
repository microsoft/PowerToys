#pragma once
#include "pch.h"

#include <filesystem>
#include <string>

#include <common/utils/json.h>

class MRUListHandler
{
public:
    MRUListHandler(unsigned int size, const std::wstring& filePath, const std::wstring& regPath);

    void Push(const std::wstring& data);
    bool Next(std::wstring& data);

    void Reset();

    const std::vector<std::wstring>& GetItems();
private:
    void Load();
    void Save();
    void MigrateFromRegistry();
    json::JsonArray Serialize();
    void ParseJson();

    bool Exists(const std::wstring& data);

    std::vector<std::wstring> items;
    unsigned int pushIdx;
    unsigned int nextIdx;
    unsigned int size;
    const std::wstring jsonFilePath;
    const std::wstring registryFilePath;
};
