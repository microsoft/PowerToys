// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "SettingsLoader.h"
#include <iostream>
#include <fstream>
#include <sstream>
#include <filesystem>
#include <cwctype>
#include <Shlobj.h>

SettingsLoader::SettingsLoader()
{
}

SettingsLoader::~SettingsLoader()
{
}

std::wstring SettingsLoader::GetPowerToysSettingsRoot() const
{
    // Get %LOCALAPPDATA%
    PWSTR localAppDataPath = nullptr;
    HRESULT hr = SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &localAppDataPath);
    
    if (FAILED(hr) || !localAppDataPath)
    {
        std::wcerr << L"Error: Failed to get LOCALAPPDATA path\n";
        return L"";
    }

    std::wstring result(localAppDataPath);
    CoTaskMemFree(localAppDataPath);

    // Append PowerToys directory
    result += L"\\Microsoft\\PowerToys";
    return result;
}

std::wstring SettingsLoader::GetSettingsPath(const std::wstring& moduleName) const
{
    std::wstring root = GetPowerToysSettingsRoot();
    if (root.empty())
    {
        return L"";
    }

    // Construct path: %LOCALAPPDATA%\Microsoft\PowerToys\<ModuleName>\settings.json
    std::wstring settingsPath = root + L"\\" + moduleName + L"\\settings.json";
    return settingsPath;
}

std::wstring SettingsLoader::ReadFileContents(const std::wstring& filePath) const
{
    std::wifstream file(filePath, std::ios::binary);
    if (!file.is_open())
    {
        std::wcerr << L"Error: Could not open file: " << filePath << L"\n";
        return L"";
    }

    // Read the entire file
    std::wstringstream buffer;
    buffer << file.rdbuf();
    
    return buffer.str();
}

std::wstring SettingsLoader::LoadSettings(const std::wstring& moduleName, const std::wstring& moduleDllPath)
{
    const std::wstring powerToysPrefix = L"PowerToys.";
    
    // Build list of possible module name variations to try
    std::vector<std::wstring> moduleNameVariants;
    
    // Try exact name first
    moduleNameVariants.push_back(moduleName);
    
    // If doesn't start with "PowerToys.", try adding it
    if (moduleName.find(powerToysPrefix) != 0)
    {
        moduleNameVariants.push_back(powerToysPrefix + moduleName);
    }
    // If starts with "PowerToys.", try without it
    else
    {
        moduleNameVariants.push_back(moduleName.substr(powerToysPrefix.length()));
    }

    // FIRST: Try same directory as the module DLL
    if (!moduleDllPath.empty())
    {
        std::filesystem::path dllPath(moduleDllPath);
        std::filesystem::path dllDirectory = dllPath.parent_path();
        
        std::wstring localSettingsPath = (dllDirectory / L"settings.json").wstring();
        std::wcout << L"Trying settings path (module directory): " << localSettingsPath << L"\n";
        
        if (std::filesystem::exists(localSettingsPath))
        {
            std::wstring contents = ReadFileContents(localSettingsPath);
            if (!contents.empty())
            {
                std::wcout << L"Settings file loaded from module directory (" << contents.size() << L" characters)\n";
                return contents;
            }
        }
    }

    // SECOND: Try standard PowerToys settings locations
    for (const auto& variant : moduleNameVariants)
    {
        std::wstring settingsPath = GetSettingsPath(variant);
        
        std::wcout << L"Trying settings path: " << settingsPath << L"\n";

        // Check if file exists (case-sensitive path)
        if (std::filesystem::exists(settingsPath))
        {
            std::wstring contents = ReadFileContents(settingsPath);
            if (!contents.empty())
            {
                std::wcout << L"Settings file loaded (" << contents.size() << L" characters)\n";
                return contents;
            }
        }
        else
        {
            // Try case-insensitive search in the parent directory
            std::wstring root = GetPowerToysSettingsRoot();
            if (!root.empty() && std::filesystem::exists(root))
            {
                try
                {
                    // Search for a directory that matches case-insensitively
                    for (const auto& entry : std::filesystem::directory_iterator(root))
                    {
                        if (entry.is_directory())
                        {
                            std::wstring dirName = entry.path().filename().wstring();
                            
                            // Case-insensitive comparison
                            if (_wcsicmp(dirName.c_str(), variant.c_str()) == 0)
                            {
                                std::wstring actualSettingsPath = entry.path().wstring() + L"\\settings.json";
                                std::wcout << L"Found case-insensitive match: " << actualSettingsPath << L"\n";
                                
                                if (std::filesystem::exists(actualSettingsPath))
                                {
                                    std::wstring contents = ReadFileContents(actualSettingsPath);
                                    if (!contents.empty())
                                    {
                                        std::wcout << L"Settings file loaded (" << contents.size() << L" characters)\n";
                                        return contents;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (const std::filesystem::filesystem_error& e)
                {
                    std::wcerr << L"Error searching directory: " << e.what() << L"\n";
                }
            }
        }
    }

    std::wcerr << L"Error: Settings file not found in any expected location:\n";
    if (!moduleDllPath.empty())
    {
        std::filesystem::path dllPath(moduleDllPath);
        std::filesystem::path dllDirectory = dllPath.parent_path();
        std::wcerr << L"  - " << (dllDirectory / L"settings.json").wstring() << L" (module directory)\n";
    }
    for (const auto& variant : moduleNameVariants)
    {
        std::wcerr << L"  - " << GetSettingsPath(variant) << L"\n";
    }
    
    return L"";
}

std::wstring SettingsLoader::FindSettingsFilePath(const std::wstring& moduleName, const std::wstring& moduleDllPath)
{
    const std::wstring powerToysPrefix = L"PowerToys.";
    
    std::vector<std::wstring> moduleNameVariants;
    moduleNameVariants.push_back(moduleName);
    
    if (moduleName.find(powerToysPrefix) != 0)
    {
        moduleNameVariants.push_back(powerToysPrefix + moduleName);
    }
    else
    {
        moduleNameVariants.push_back(moduleName.substr(powerToysPrefix.length()));
    }

    // Try module directory first
    if (!moduleDllPath.empty())
    {
        std::filesystem::path dllPath(moduleDllPath);
        std::filesystem::path dllDirectory = dllPath.parent_path();
        std::wstring localSettingsPath = (dllDirectory / L"settings.json").wstring();
        
        if (std::filesystem::exists(localSettingsPath))
        {
            return localSettingsPath;
        }
    }

    // Try standard locations
    for (const auto& variant : moduleNameVariants)
    {
        std::wstring settingsPath = GetSettingsPath(variant);
        
        if (std::filesystem::exists(settingsPath))
        {
            return settingsPath;
        }
        
        // Case-insensitive search
        std::wstring root = GetPowerToysSettingsRoot();
        if (!root.empty() && std::filesystem::exists(root))
        {
            try
            {
                for (const auto& entry : std::filesystem::directory_iterator(root))
                {
                    if (entry.is_directory())
                    {
                        std::wstring dirName = entry.path().filename().wstring();
                        if (_wcsicmp(dirName.c_str(), variant.c_str()) == 0)
                        {
                            std::wstring actualSettingsPath = entry.path().wstring() + L"\\settings.json";
                            if (std::filesystem::exists(actualSettingsPath))
                            {
                                return actualSettingsPath;
                            }
                        }
                    }
                }
            }
            catch (...) {}
        }
    }

    return L"";
}

void SettingsLoader::DisplaySettingsInfo(const std::wstring& moduleName, const std::wstring& moduleDllPath)
{
    std::wcout << L"\n";
    std::wcout << L"\033[1;36m"; // Cyan bold
    std::wcout << L"+----------------------------------------------------------------+\n";
    std::wcout << L"|                    MODULE SETTINGS INFO                        |\n";
    std::wcout << L"+----------------------------------------------------------------+\n";
    std::wcout << L"\033[0m";

    std::wcout << L"\n\033[1mModule:\033[0m " << moduleName << L"\n";

    std::wstring settingsPath = FindSettingsFilePath(moduleName, moduleDllPath);
    
    if (settingsPath.empty())
    {
        std::wcout << L"\033[1;33mSettings file:\033[0m Not found\n";
        std::wcout << L"\nNo settings file found for this module.\n";
        return;
    }

    std::wcout << L"\033[1mSettings file:\033[0m " << settingsPath << L"\n\n";

    std::wstring settingsJson = ReadFileContents(settingsPath);
    if (settingsJson.empty())
    {
        std::wcout << L"Unable to read settings file.\n";
        return;
    }

    std::wcout << L"\033[1;32mCurrent Settings:\033[0m\n";
    std::wcout << L"-----------------------------------------------------------------\n";
    
    DisplayJsonProperties(settingsJson, 0);
    
    std::wcout << L"-----------------------------------------------------------------\n\n";
}

void SettingsLoader::DisplayJsonProperties(const std::wstring& settingsJson, int indent)
{
    // Simple JSON parser for display - handles the PowerToys settings format
    // Format: { "properties": { "key": { "value": ... }, ... } }
    // Also handles hotkey settings: { "key": { "win": true, "alt": true, "code": 85 } }
    
    std::string json(settingsJson.begin(), settingsJson.end());
    
    // Find "properties" section
    size_t propsStart = json.find("\"properties\"");
    if (propsStart == std::string::npos)
    {
        // If no properties section, just display the raw JSON
        std::wcout << settingsJson << L"\n";
        return;
    }
    
    // Find the opening brace after "properties":
    size_t braceStart = json.find('{', propsStart + 12);
    if (braceStart == std::string::npos) return;
    
    // Parse each property
    size_t pos = braceStart + 1;
    int braceCount = 1;
    
    while (pos < json.size() && braceCount > 0)
    {
        // Skip whitespace
        while (pos < json.size() && std::isspace(json[pos])) pos++;
        
        // Look for property name
        if (json[pos] == '"')
        {
            size_t nameStart = pos + 1;
            size_t nameEnd = json.find('"', nameStart);
            if (nameEnd == std::string::npos) break;
            
            std::string propName = json.substr(nameStart, nameEnd - nameStart);
            
            // Skip to the value object
            pos = json.find('{', nameEnd);
            if (pos == std::string::npos) break;
            
            size_t objStart = pos;
            
            // Check if this is a hotkey object (has "win", "code" etc. but no "value")
            if (IsHotkeyObject(json, objStart))
            {
                // Parse hotkey and display
                size_t objEnd;
                std::string hotkeyStr = ParseHotkeyObject(json, objStart, objEnd);
                
                std::wstring wPropName(propName.begin(), propName.end());
                std::wstring wHotkeyStr(hotkeyStr.begin(), hotkeyStr.end());
                
                std::wcout << L"  \033[1;34m" << wPropName << L"\033[0m: ";
                std::wcout << L"\033[1;36m" << wHotkeyStr << L"\033[0m\n";
                
                pos = objEnd + 1;
                continue;
            }
            
            // Regular property with "value" key
            int innerBraceCount = 1;
            pos++;
            
            std::string valueStr = "";
            bool foundValue = false;
            
            while (pos < json.size() && innerBraceCount > 0)
            {
                if (json[pos] == '{') innerBraceCount++;
                else if (json[pos] == '}') innerBraceCount--;
                else if (json[pos] == '"' && !foundValue)
                {
                    size_t keyStart = pos + 1;
                    size_t keyEnd = json.find('"', keyStart);
                    if (keyEnd != std::string::npos)
                    {
                        std::string key = json.substr(keyStart, keyEnd - keyStart);
                        if (key == "value")
                        {
                            // Find the colon and then the value
                            size_t colonPos = json.find(':', keyEnd);
                            if (colonPos != std::string::npos)
                            {
                                size_t valStart = colonPos + 1;
                                while (valStart < json.size() && std::isspace(json[valStart])) valStart++;
                                
                                // Determine value type and extract
                                if (json[valStart] == '"')
                                {
                                    size_t valEnd = json.find('"', valStart + 1);
                                    if (valEnd != std::string::npos)
                                    {
                                        valueStr = json.substr(valStart + 1, valEnd - valStart - 1);
                                        foundValue = true;
                                    }
                                }
                                else
                                {
                                    // Number or boolean
                                    size_t valEnd = valStart;
                                    while (valEnd < json.size() && json[valEnd] != ',' && json[valEnd] != '}' && !std::isspace(json[valEnd]))
                                    {
                                        valEnd++;
                                    }
                                    valueStr = json.substr(valStart, valEnd - valStart);
                                    foundValue = true;
                                }
                            }
                        }
                    }
                    pos = keyEnd + 1;
                    continue;
                }
                pos++;
            }
            
            // Print the property
            std::wstring wPropName(propName.begin(), propName.end());
            std::wstring wValueStr(valueStr.begin(), valueStr.end());
            
            std::wcout << L"  \033[1;34m" << wPropName << L"\033[0m: ";
            
            // Color-code based on value type
            if (valueStr == "true")
            {
                std::wcout << L"\033[1;32mtrue\033[0m";
            }
            else if (valueStr == "false")
            {
                std::wcout << L"\033[1;31mfalse\033[0m";
            }
            else if (!valueStr.empty() && (std::isdigit(valueStr[0]) || valueStr[0] == '-'))
            {
                std::wcout << L"\033[1;33m" << wValueStr << L"\033[0m";
            }
            else
            {
                std::wcout << L"\033[1;35m\"" << wValueStr << L"\"\033[0m";
            }
            std::wcout << L"\n";
        }
        else if (json[pos] == '{')
        {
            braceCount++;
            pos++;
        }
        else if (json[pos] == '}')
        {
            braceCount--;
            pos++;
        }
        else
        {
            pos++;
        }
    }
}

std::wstring SettingsLoader::GetSettingValue(const std::wstring& moduleName, const std::wstring& moduleDllPath, const std::wstring& key)
{
    std::wstring settingsPath = FindSettingsFilePath(moduleName, moduleDllPath);
    if (settingsPath.empty()) return L"";

    std::wstring settingsJson = ReadFileContents(settingsPath);
    if (settingsJson.empty()) return L"";

    // Simple JSON parser to find the specific key
    std::string json(settingsJson.begin(), settingsJson.end());
    std::string searchKey(key.begin(), key.end());
    
    // Look for "properties" -> key -> "value"
    std::string searchPattern = "\"" + searchKey + "\"";
    size_t keyPos = json.find(searchPattern);
    if (keyPos == std::string::npos) return L"";
    
    // Find "value" within this property's object
    size_t objStart = json.find('{', keyPos);
    if (objStart == std::string::npos) return L"";
    
    size_t valueKeyPos = json.find("\"value\"", objStart);
    if (valueKeyPos == std::string::npos) return L"";
    
    // Find the colon and extract value
    size_t colonPos = json.find(':', valueKeyPos);
    if (colonPos == std::string::npos) return L"";
    
    size_t valStart = colonPos + 1;
    while (valStart < json.size() && std::isspace(json[valStart])) valStart++;
    
    std::string valueStr;
    if (json[valStart] == '"')
    {
        size_t valEnd = json.find('"', valStart + 1);
        if (valEnd != std::string::npos)
        {
            valueStr = json.substr(valStart + 1, valEnd - valStart - 1);
        }
    }
    else
    {
        size_t valEnd = valStart;
        while (valEnd < json.size() && json[valEnd] != ',' && json[valEnd] != '}' && !std::isspace(json[valEnd]))
        {
            valEnd++;
        }
        valueStr = json.substr(valStart, valEnd - valStart);
    }
    
    return std::wstring(valueStr.begin(), valueStr.end());
}

bool SettingsLoader::SetSettingValue(const std::wstring& moduleName, const std::wstring& moduleDllPath, const std::wstring& key, const std::wstring& value)
{
    std::wstring settingsPath = FindSettingsFilePath(moduleName, moduleDllPath);
    if (settingsPath.empty())
    {
        std::wcerr << L"Error: Settings file not found\n";
        return false;
    }

    std::wstring settingsJson = ReadFileContents(settingsPath);
    if (settingsJson.empty())
    {
        std::wcerr << L"Error: Unable to read settings file\n";
        return false;
    }

    std::string json(settingsJson.begin(), settingsJson.end());
    std::string searchKey(key.begin(), key.end());
    std::string newValue(value.begin(), value.end());
    
    // Find the property
    std::string searchPattern = "\"" + searchKey + "\"";
    size_t keyPos = json.find(searchPattern);
    if (keyPos == std::string::npos)
    {
        // Setting not found - prompt user to add it
        std::wcout << L"\033[1;33mWarning:\033[0m Setting '" << key << L"' not found in settings file.\n";
        std::wcout << L"This could be a new setting or a typo.\n\n";
        
        if (PromptYesNo(L"Do you want to add this as a new setting?"))
        {
            std::string modifiedJson = AddNewProperty(json, searchKey, newValue);
            if (modifiedJson.empty())
            {
                std::wcerr << L"Error: Failed to add new property to settings file\n";
                return false;
            }
            
            std::wstring newJson(modifiedJson.begin(), modifiedJson.end());
            if (WriteFileContents(settingsPath, newJson))
            {
                std::wcout << L"\033[1;32m+\033[0m New setting '" << key << L"' added with value: " << value << L"\n";
                return true;
            }
            else
            {
                std::wcerr << L"Error: Failed to write settings file\n";
                return false;
            }
        }
        else
        {
            std::wcout << L"Operation cancelled.\n";
            return false;
        }
    }
    
    // Find "value" within this property's object
    size_t objStart = json.find('{', keyPos);
    if (objStart == std::string::npos) return false;
    
    size_t valueKeyPos = json.find("\"value\"", objStart);
    if (valueKeyPos == std::string::npos) return false;
    
    // Find the colon and the existing value
    size_t colonPos = json.find(':', valueKeyPos);
    if (colonPos == std::string::npos) return false;
    
    size_t valStart = colonPos + 1;
    while (valStart < json.size() && std::isspace(json[valStart])) valStart++;
    
    size_t valEnd;
    bool isString = (json[valStart] == '"');
    
    if (isString)
    {
        valEnd = json.find('"', valStart + 1);
        if (valEnd != std::string::npos) valEnd++; // Include closing quote
    }
    else
    {
        valEnd = valStart;
        while (valEnd < json.size() && json[valEnd] != ',' && json[valEnd] != '}' && !std::isspace(json[valEnd]))
        {
            valEnd++;
        }
    }
    
    // Determine if new value should be quoted
    bool newValueNeedsQuotes = false;
    if (newValue != "true" && newValue != "false")
    {
        // Check if it's a number
        bool isNumber = !newValue.empty();
        for (char c : newValue)
        {
            if (!std::isdigit(c) && c != '.' && c != '-')
            {
                isNumber = false;
                break;
            }
        }
        newValueNeedsQuotes = !isNumber;
    }
    
    std::string replacement;
    if (newValueNeedsQuotes)
    {
        replacement = "\"" + newValue + "\"";
    }
    else
    {
        replacement = newValue;
    }
    
    // Replace the value
    json = json.substr(0, valStart) + replacement + json.substr(valEnd);
    
    // Write back
    std::wstring newJson(json.begin(), json.end());
    if (WriteFileContents(settingsPath, newJson))
    {
        std::wcout << L"\033[1;32m?\033[0m Setting '" << key << L"' updated to: " << value << L"\n";
        return true;
    }
    else
    {
        std::wcerr << L"Error: Failed to write settings file\n";
        return false;
    }
}

bool SettingsLoader::WriteFileContents(const std::wstring& filePath, const std::wstring& contents) const
{
    std::ofstream file(filePath, std::ios::binary);
    if (!file.is_open())
    {
        return false;
    }
    
    std::string utf8Contents(contents.begin(), contents.end());
    file << utf8Contents;
    file.close();
    
    return true;
}

bool SettingsLoader::PromptYesNo(const std::wstring& prompt)
{
    std::wcout << prompt << L" [y/N]: ";
    std::wcout.flush();
    
    std::wstring input;
    std::getline(std::wcin, input);
    
    // Trim whitespace
    while (!input.empty() && iswspace(input.front())) input.erase(input.begin());
    while (!input.empty() && iswspace(input.back())) input.pop_back();
    
    // Check for yes responses
    return !input.empty() && (input[0] == L'y' || input[0] == L'Y');
}

std::string SettingsLoader::AddNewProperty(const std::string& json, const std::string& key, const std::string& value)
{
    // Find the "properties" section
    size_t propsPos = json.find("\"properties\"");
    if (propsPos == std::string::npos)
    {
        return "";
    }
    
    // Find the opening brace of properties object
    size_t propsStart = json.find('{', propsPos);
    if (propsStart == std::string::npos)
    {
        return "";
    }
    
    // Find the closing brace of properties object
    int braceCount = 1;
    size_t pos = propsStart + 1;
    size_t propsEnd = std::string::npos;
    
    while (pos < json.size() && braceCount > 0)
    {
        if (json[pos] == '{') braceCount++;
        else if (json[pos] == '}')
        {
            braceCount--;
            if (braceCount == 0) propsEnd = pos;
        }
        pos++;
    }
    
    if (propsEnd == std::string::npos)
    {
        return "";
    }
    
    // Determine if new value should be quoted
    bool needsQuotes = false;
    if (value != "true" && value != "false")
    {
        bool isNumber = !value.empty();
        for (char c : value)
        {
            if (!std::isdigit(c) && c != '.' && c != '-')
            {
                isNumber = false;
                break;
            }
        }
        needsQuotes = !isNumber;
    }
    
    // Build the new property JSON
    // Format: "key": { "value": ... }
    std::string valueJson = needsQuotes ? ("\"" + value + "\"") : value;
    std::string newProperty = ",\n    \"" + key + "\": {\n      \"value\": " + valueJson + "\n    }";
    
    // Check if properties object is empty (only whitespace between braces)
    std::string propsContent = json.substr(propsStart + 1, propsEnd - propsStart - 1);
    bool isEmpty = true;
    for (char c : propsContent)
    {
        if (!std::isspace(c))
        {
            isEmpty = false;
            break;
        }
    }
    
    // Insert the new property before the closing brace of properties
    std::string result;
    if (isEmpty)
    {
        // No leading comma for empty properties
        newProperty = "\n    \"" + key + "\": {\n      \"value\": " + valueJson + "\n    }\n  ";
    }
    
    result = json.substr(0, propsEnd) + newProperty + json.substr(propsEnd);
    return result;
}

bool SettingsLoader::IsHotkeyObject(const std::string& json, size_t objStart)
{
    // A hotkey object has "win", "alt", "ctrl", "shift", and "code" fields
    // Find the end of this object
    int braceCount = 1;
    size_t pos = objStart + 1;
    size_t objEnd = objStart;
    
    while (pos < json.size() && braceCount > 0)
    {
        if (json[pos] == '{') braceCount++;
        else if (json[pos] == '}')
        {
            braceCount--;
            if (braceCount == 0) objEnd = pos;
        }
        pos++;
    }
    
    if (objEnd <= objStart) return false;
    
    std::string objContent = json.substr(objStart, objEnd - objStart + 1);
    
    // Check for hotkey-specific fields
    return (objContent.find("\"win\"") != std::string::npos ||
            objContent.find("\"code\"") != std::string::npos) &&
           objContent.find("\"value\"") == std::string::npos;
}

std::string SettingsLoader::ParseHotkeyObject(const std::string& json, size_t objStart, size_t& objEnd)
{
    // Find the end of this object
    int braceCount = 1;
    size_t pos = objStart + 1;
    objEnd = objStart;
    
    while (pos < json.size() && braceCount > 0)
    {
        if (json[pos] == '{') braceCount++;
        else if (json[pos] == '}')
        {
            braceCount--;
            if (braceCount == 0) objEnd = pos;
        }
        pos++;
    }
    
    if (objEnd <= objStart) return "";
    
    std::string objContent = json.substr(objStart, objEnd - objStart + 1);
    
    // Parse hotkey fields
    bool win = false, ctrl = false, alt = false, shift = false;
    int code = 0;
    
    // Helper to find boolean value
    auto findBool = [&objContent](const std::string& key) -> bool {
        size_t keyPos = objContent.find("\"" + key + "\"");
        if (keyPos == std::string::npos) return false;
        size_t colonPos = objContent.find(':', keyPos);
        if (colonPos == std::string::npos) return false;
        size_t valStart = colonPos + 1;
        while (valStart < objContent.size() && std::isspace(objContent[valStart])) valStart++;
        return objContent.substr(valStart, 4) == "true";
    };
    
    // Helper to find integer value
    auto findInt = [&objContent](const std::string& key) -> int {
        size_t keyPos = objContent.find("\"" + key + "\"");
        if (keyPos == std::string::npos) return 0;
        size_t colonPos = objContent.find(':', keyPos);
        if (colonPos == std::string::npos) return 0;
        size_t valStart = colonPos + 1;
        while (valStart < objContent.size() && std::isspace(objContent[valStart])) valStart++;
        size_t valEnd = valStart;
        while (valEnd < objContent.size() && (std::isdigit(objContent[valEnd]) || objContent[valEnd] == '-'))
            valEnd++;
        if (valEnd > valStart)
            return std::stoi(objContent.substr(valStart, valEnd - valStart));
        return 0;
    };
    
    win = findBool("win");
    ctrl = findBool("ctrl");
    alt = findBool("alt");
    shift = findBool("shift");
    code = findInt("code");
    
    // Build hotkey string
    std::string result;
    if (win) result += "Win+";
    if (ctrl) result += "Ctrl+";
    if (alt) result += "Alt+";
    if (shift) result += "Shift+";
    
    // Convert virtual key code to key name
    if (code > 0)
    {
        if (code >= 'A' && code <= 'Z')
        {
            result += static_cast<char>(code);
        }
        else if (code >= '0' && code <= '9')
        {
            result += static_cast<char>(code);
        }
        else
        {
            // Common VK codes
            switch (code)
            {
            case 0x20: result += "Space"; break;
            case 0x0D: result += "Enter"; break;
            case 0x1B: result += "Escape"; break;
            case 0x09: result += "Tab"; break;
            case 0x08: result += "Backspace"; break;
            case 0x2E: result += "Delete"; break;
            case 0x24: result += "Home"; break;
            case 0x23: result += "End"; break;
            case 0x21: result += "PageUp"; break;
            case 0x22: result += "PageDown"; break;
            case 0x25: result += "Left"; break;
            case 0x26: result += "Up"; break;
            case 0x27: result += "Right"; break;
            case 0x28: result += "Down"; break;
            case 0x70: case 0x71: case 0x72: case 0x73: case 0x74: case 0x75:
            case 0x76: case 0x77: case 0x78: case 0x79: case 0x7A: case 0x7B:
                result += "F" + std::to_string(code - 0x70 + 1);
                break;
            case 0xC0: result += "`"; break;
            case 0xBD: result += "-"; break;
            case 0xBB: result += "="; break;
            case 0xDB: result += "["; break;
            case 0xDD: result += "]"; break;
            case 0xDC: result += "\\"; break;
            case 0xBA: result += ";"; break;
            case 0xDE: result += "'"; break;
            case 0xBC: result += ","; break;
            case 0xBE: result += "."; break;
            case 0xBF: result += "/"; break;
            default:
                result += "VK_0x" + std::to_string(code);
                break;
            }
        }
    }
    
    // Remove trailing + if no key code
    if (!result.empty() && result.back() == '+')
    {
        result.pop_back();
    }
    
    return result.empty() ? "(not set)" : result;
}
