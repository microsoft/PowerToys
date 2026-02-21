#pragma once
#include "Clipboard.g.h"

namespace winrt::PowerToys::Interop::implementation
{
    struct Clipboard : ClipboardT<Clipboard>
    {
        static void PasteAsPlainText();
    };
}
namespace winrt::PowerToys::Interop::factory_implementation
{
    struct Clipboard : ClipboardT<Clipboard, implementation::Clipboard>
    {
    };
}
