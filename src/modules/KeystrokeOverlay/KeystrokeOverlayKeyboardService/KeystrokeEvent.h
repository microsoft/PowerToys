// KeystrokeEvent.h
// Contains the definition of the KeystrokeEvent struct used to represent keyboard events.
#pragma once // compiled once per build
#include <cstdint>
#include <array>
#include <windows.h>

struct KeystrokeEvent
{
    enum class Type : uint8_t
    {
        Down,
        Up,
        Char
    } type;
    DWORD vk;                 // virtual key code (DWORD type)
    char32_t ch;              // Stores actual character being pressed. Skip for now. 0 if non-printable
    std::array<bool, 4> mods; // Ctrl, Alt, Shift, Win. Read from getkeyboard state and one other method (in hook).
    uint64_t ts_micros;       // Timestamp in microseconds. Monotonic or UTC micros
};

// Note, VKCodes don't distinguish between key cases (A vs a), therefore mod keys
// need to be packaged in KeystrokeEvent in order to aid interpretation.