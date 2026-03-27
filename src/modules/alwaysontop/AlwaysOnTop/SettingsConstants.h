#pragma once

#include <string_view>

enum class SettingId
{
    Hotkey = 0,
    IncreaseOpacityHotkey,
    DecreaseOpacityHotkey,
    SoundEnabled,
    ShowInSystemMenu,
    FrameEnabled,
    FrameThickness,
    FrameColor,
    FrameOpacity,
    BlockInGameMode,
    ExcludeApps,
    FrameAccentColor,
    RoundCornersEnabled
};

inline constexpr std::wstring_view SettingIdToString(SettingId id) noexcept
{
    switch (id)
    {
    case SettingId::Hotkey:
        return L"Hotkey";
    case SettingId::SoundEnabled:
        return L"SoundEnabled";
    case SettingId::ShowInSystemMenu:
        return L"ShowInSystemMenu";
    case SettingId::FrameEnabled:
        return L"FrameEnabled";
    case SettingId::FrameThickness:
        return L"FrameThickness";
    case SettingId::FrameColor:
        return L"FrameColor";
    case SettingId::FrameOpacity:
        return L"FrameOpacity";
    case SettingId::BlockInGameMode:
        return L"BlockInGameMode";
    case SettingId::ExcludeApps:
        return L"ExcludeApps";
    case SettingId::FrameAccentColor:
        return L"FrameAccentColor";
    case SettingId::RoundCornersEnabled:
        return L"RoundCornersEnabled";
    default:
        return L"Unknown";
    }
}
