#include "pch.h"
#include "Styles.h"

Style AccentButtonStyle()
{
    return Application::Current().Resources().Lookup(box_value(L"AccentButtonStyle")).as<Style>();
}