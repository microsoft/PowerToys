#pragma once

#include "Class.g.h"

namespace winrt::Peek_Win32::implementation
{
    struct Class : ClassT<Class>
    {
        Class() = default;

        int32_t MaPropriete();
        void MaPropriete(int32_t value);
    };
}

namespace winrt::Peek_Win32::factory_implementation
{
    struct Class : ClassT<Class, implementation::Class>
    {
    };
}
