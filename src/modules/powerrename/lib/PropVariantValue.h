#pragma once

#include <propvarutil.h>
#include <propidl.h>

namespace PowerRenameLib
{
    /// <summary>
    /// RAII wrapper around PROPVARIANT to ensure proper initialization and cleanup.
    /// Move-only semantics keep ownership simple while still allowing use in optionals.
    /// </summary>
    struct PropVariantValue
    {
        PropVariantValue() noexcept
        {
            PropVariantInit(&value);
        }

        ~PropVariantValue()
        {
            PropVariantClear(&value);
        }

        PropVariantValue(const PropVariantValue&) = delete;
        PropVariantValue& operator=(const PropVariantValue&) = delete;

        PropVariantValue(PropVariantValue&& other) noexcept
        {
            value = other.value;
            PropVariantInit(&other.value);  // Properly clear the moved-from object
        }

        PropVariantValue& operator=(PropVariantValue&& other) noexcept
        {
            if (this != &other)
            {
                PropVariantClear(&value);
                value = other.value;
                PropVariantInit(&other.value);  // Properly clear the moved-from object
            }
            return *this;
        }

        PROPVARIANT* GetAddressOf() noexcept
        {
            return &value;
        }

        PROPVARIANT& Get() noexcept
        {
            return value;
        }

        const PROPVARIANT& Get() const noexcept
        {
            return value;
        }

    private:
        PROPVARIANT value;
    };
}
