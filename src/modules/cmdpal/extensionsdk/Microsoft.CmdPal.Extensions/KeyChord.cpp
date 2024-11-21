// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#include "pch.h"
#include "KeyChord.h"

#include "KeyChord.g.cpp"

using VirtualKeyModifiers = winrt::Windows::System::VirtualKeyModifiers;

// these are more-or-less the WIL helpers, but without pulling in all of WIL

//! Set zero or more bitflags specified by `flags` in the variable `var`.
#define WI_SetAllFlags(var, flags) ((var) |= (flags))
//! Set a single compile-time constant `flag` in the variable `var`.
#define WI_SetFlag(var, flag) WI_SetAllFlags(var, flag)
//! Conditionally sets a single compile-time constant `flag` in the variable `var` only if `condition` is true.
#define WI_SetFlagIf(var, flag, condition) \
    do                                     \
    {                                      \
        if (condition)   \
        {                                  \
            WI_SetFlag(var, flag);         \
        }                                  \
    } while ((void)0, 0)

namespace winrt::Microsoft::CmdPal::Extensions::implementation
{
    static constexpr VirtualKeyModifiers modifiersFromBooleans(bool ctrl, bool alt, bool shift, bool win)
    {
        auto modifiers = VirtualKeyModifiers::None;
        WI_SetFlagIf(modifiers, VirtualKeyModifiers::Control, ctrl);
        WI_SetFlagIf(modifiers, VirtualKeyModifiers::Menu, alt);
        WI_SetFlagIf(modifiers, VirtualKeyModifiers::Shift, shift);
        WI_SetFlagIf(modifiers, VirtualKeyModifiers::Windows, win);
        return modifiers;
    }

    KeyChord::KeyChord(bool ctrl, bool alt, bool shift, bool win, int32_t vkey, int32_t scanCode) noexcept :
        KeyChord(modifiersFromBooleans(ctrl, alt, shift, win), vkey, scanCode)
    {
    }

    KeyChord::KeyChord(const VirtualKeyModifiers modifiers, int32_t vkey, int32_t scanCode) noexcept :
        _modifiers{ modifiers },
        _vkey{ vkey },
        _scanCode{ scanCode }
    {
        // ActionMap needs to identify KeyChords which should "layer" (overwrite) each other.
        // For instance win+sc(41) and win+` both specify the same KeyChord on an US keyboard layout
        // from the perspective of a user. Either of the two should correctly overwrite the other.
        // We can help ActionMap with this by ensuring that Vkey() is always valid.
        if (!_vkey)
        {
            _vkey = MapVirtualKeyW(scanCode, MAPVK_VSC_TO_VK_EX);
        }

        assert(_vkey || _scanCode);
    }

    uint64_t KeyChord::Hash() const noexcept
    {
        // Two KeyChords are equal if they have the same modifiers and either identical
        // Vkey or ScanCode, with Vkey being preferred. See KeyChord::Equals().
        // This forces us to _either_ hash _vkey or _scanCode.
        //
        // Additionally the hash value with _vkey==123 and _scanCode==123 must be different.
        // --> Taint hashes of KeyChord without _vkey.
        auto h = static_cast<uint64_t>(_modifiers) << 32;
        h |= _vkey ? _vkey : (_scanCode | 0xBABE0000);

        // I didn't like how std::hash uses the byte-wise FNV1a for integers.
        // So I built my own std::hash with murmurhash3.
        h ^= h >> 33;
        h *= UINT64_C(0xff51afd7ed558ccd);
        h ^= h >> 33;
        h *= UINT64_C(0xc4ceb9fe1a85ec53);
        h ^= h >> 33;

        return h;
    }

    bool KeyChord::Equals(const Extensions::KeyChord& other) const noexcept
    {
        // Two KeyChords are equal if they have the same modifiers and either identical
        // Vkey or ScanCode, with Vkey being preferred. Vkey is preferred because:
        //   ActionMap needs to identify KeyChords which should "layer" (overwrite) each other.
        //   For instance win+sc(41) and win+` both specify the same KeyChord on an US keyboard layout
        //   from the perspective of a user. Either of the two should correctly overwrite the other.
        //
        // Two problems exist here:
        // * Since a value of 0 indicates that the Vkey/ScanCode isn't set, we cannot use == to compare them.
        //   Otherwise we return true, even if the Vkey/ScanCode isn't set on both sides.
        // * Whenever Equals() returns true, the Hash() value _must_ be identical.
        //   For instance the code below ensures the preference of Vkey over ScanCode by:
        //     this->_vkey || other->_vkey ? ...vkey... : ...scanCode...
        //   We cannot use "&&", even if it would be technically more correct, as this would mean the
        //   return value of this function would be dependent on the existence of a Vkey in "other".
        //   But Hash() has no "other" argument to consider when deciding if its Vkey or ScanCode should be hashed.
        //
        // Bitwise operators are used because MSVC doesn't support compiling
        // boolean operators into bitwise ones at the time of writing.
        const auto otherSelf = winrt::get_self<KeyChord>(other);
        return _modifiers == otherSelf->_modifiers && ((_vkey | otherSelf->_vkey) ? _vkey == otherSelf->_vkey : _scanCode == otherSelf->_scanCode);
    }

    VirtualKeyModifiers KeyChord::Modifiers() const noexcept
    {
        return _modifiers;
    }

    void KeyChord::Modifiers(const VirtualKeyModifiers value) noexcept
    {
        _modifiers = value;
    }

    int32_t KeyChord::Vkey() const noexcept
    {
        return _vkey;
    }

    void KeyChord::Vkey(int32_t value) noexcept
    {
        _vkey = value;
    }

    int32_t KeyChord::ScanCode() const noexcept
    {
        return _scanCode;
    }

    void KeyChord::ScanCode(int32_t value) noexcept
    {
        _scanCode = value;
    }
}
