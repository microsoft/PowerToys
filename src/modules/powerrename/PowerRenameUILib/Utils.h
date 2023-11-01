// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#pragma once

// This macro defines a dependency property for a WinRT class.
// Use this in your class' header file after declaring it in the idl.
// Remember to register your dependency property in the respective cpp file.
#define DEPENDENCY_PROPERTY(type, name)                                    \
public:                                                                    \
    static winrt::Microsoft::UI::Xaml::DependencyProperty name##Property() \
    {                                                                      \
        return _##name##Property;                                          \
    }                                                                      \
    type name() const                                                      \
    {                                                                      \
        return winrt::unbox_value<type>(GetValue(_##name##Property));      \
    }                                                                      \
    void name(const type& value)                                           \
    {                                                                      \
        SetValue(_##name##Property, winrt::box_value(value));              \
    }                                                                      \
                                                                           \
private:                                                                   \
    static winrt::Microsoft::UI::Xaml::DependencyProperty _##name##Property;
