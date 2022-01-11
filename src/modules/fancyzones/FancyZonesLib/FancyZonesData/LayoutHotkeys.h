#pragma once

#include <guiddef.h>
#include <map>
#include <memory>
#include <optional>

#include <common/SettingsAPI/FileWatcher.h>

namespace NonLocalizable
{
    namespace LayoutHotkeysIds
    {
        const static wchar_t* LayoutHotkeysArrayID = L"layout-hotkeys";
        const static wchar_t* LayoutUuidID = L"layout-id";
        const static wchar_t* KeyID = L"key";
    }
}

class LayoutHotkeys
{
public:
    using THotkeyMap = std::map<int, GUID>;

    static LayoutHotkeys& instance();

    static std::wstring GetDataFileName();
    void LoadData(); 
    
    std::optional<GUID> GetLayoutId(int key) const noexcept;
    size_t GetHotkeysCount() const noexcept;

private:
    LayoutHotkeys();
    ~LayoutHotkeys() = default;

    THotkeyMap m_hotkeyMap;
    std::unique_ptr<FileWatcher> m_fileWatcher;
};
