#pragma once

#include <guiddef.h>
#include <map>
#include <memory>
#include <optional>

#include <FancyZonesLib/ModuleConstants.h>

#include <common/SettingsAPI/FileWatcher.h>
#include <common/SettingsAPI/settings_helpers.h>

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

    inline static std::wstring LayoutHotkeysFileName()
    {
        std::wstring saveFolderPath = PTSettingsHelper::get_module_save_folder_location(NonLocalizable::ModuleKey);
#if defined(UNIT_TESTS)
        return saveFolderPath + L"\\test-layout-hotkeys.json";
#else
        return saveFolderPath + L"\\layout-hotkeys.json";
#endif
    }

    void LoadData();

    std::optional<GUID> GetLayoutId(int key) const noexcept;
    size_t GetHotkeysCount() const noexcept;

private:
    LayoutHotkeys();
    ~LayoutHotkeys() = default;

    THotkeyMap m_hotkeyMap;
    std::unique_ptr<FileWatcher> m_fileWatcher;
};
