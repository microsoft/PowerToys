// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#pragma once

#include "KeyChord.g.h"

namespace winrt::Microsoft::CmdPal::Extensions::implementation
{
    struct KeyChord : KeyChordT<KeyChord>
    {
        KeyChord() noexcept = default;
        KeyChord(const winrt::Windows::System::VirtualKeyModifiers modifiers, int32_t vkey, int32_t scanCode) noexcept;
        KeyChord(bool ctrl, bool alt, bool shift, bool win, int32_t vkey, int32_t scanCode) noexcept;

        uint64_t Hash() const noexcept;
        bool Equals(const Extensions::KeyChord& other) const noexcept;

        winrt::Windows::System::VirtualKeyModifiers Modifiers() const noexcept;
        void Modifiers(const winrt::Windows::System::VirtualKeyModifiers value) noexcept;
        int32_t Vkey() const noexcept;
        void Vkey(int32_t value) noexcept;
        int32_t ScanCode() const noexcept;
        void ScanCode(int32_t value) noexcept;

    private:
        winrt::Windows::System::VirtualKeyModifiers _modifiers{};
        int32_t _vkey{};
        int32_t _scanCode{};
    };
}

namespace winrt::Microsoft::CmdPal::Extensions::factory_implementation
{
    struct KeyChord : KeyChordT<KeyChord, implementation::KeyChord>
    {
    };
}
