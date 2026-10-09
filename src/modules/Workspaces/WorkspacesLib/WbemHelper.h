#pragma once

struct IWbemLocator;
struct IWbemServices;

class WbemHelper
{
public:
    static std::unique_ptr<WbemHelper> Create();
    ~WbemHelper();

    std::wstring GetCommandLineArgs(DWORD processID) const;
    std::wstring GetExecutablePath(DWORD processID) const;

private:
    WbemHelper() = default;

    bool Initialize();

    std::wstring Query(const std::wstring& query, const std::wstring& propertyName) const;

    IWbemLocator* m_locator = NULL;
    IWbemServices* m_services = NULL;
};
