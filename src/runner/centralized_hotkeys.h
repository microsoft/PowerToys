#pragma once
#include <Windows.h>
#include <functional>

namespace CentralizedHotkeys
{
    struct Action
    {
        std::wstring moduleName;
        std::function<void(WORD, WORD)> action;

        Action(std::wstring moduleName = L"", std::function<void(WORD, WORD)> action = ([](WORD /*modifiersMask*/, WORD /*vkCode*/) {}))
        {
            this->moduleName = moduleName;
            this->action = action;
        }
    };

    struct Shortcut
    {
        WORD modifiersMask;
        WORD vkCode;
        const wchar_t* hotkeyName;

        Shortcut(WORD modifiersMask = 0, WORD vkCode = 0, const wchar_t* hotkeyName = nullptr)
        {
            this->modifiersMask = modifiersMask;
            this->vkCode = vkCode;
            this->hotkeyName = hotkeyName;
        }

        bool operator<(const Shortcut& key) const
        {
            return std::pair<WORD, WORD>{ this->modifiersMask, this->vkCode } < std::pair<WORD, WORD>{ key.modifiersMask, key.vkCode };
        }
    };

    std::wstring ToWstring(const Shortcut& shortcut);

    bool AddHotkeyAction(Shortcut shortcut, Action action, std::wstring moduleName, bool isEnabled);

    void UnregisterHotkeysForModule(std::wstring moduleName);

    void PopulateHotkey(Shortcut shortcut);

    void RegisterWindow(HWND hwnd);
}
