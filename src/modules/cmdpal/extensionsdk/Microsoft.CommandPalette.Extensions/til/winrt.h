// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#pragma once

namespace til // Terminal Implementation Library. Also: "Today I Learned"
{
    template<typename T>
    struct property
    {
        explicit constexpr property(auto&&... args) :
            _value{ std::forward<decltype(args)>(args)... } {}

        property& operator=(const property& other) = default;

        T operator()() const noexcept
        {
            return _value;
        }
        void operator()(auto&& arg)
        {
            _value = std::forward<decltype(arg)>(arg);
        }
        explicit operator bool() const noexcept
        {
#ifdef WINRT_Windows_Foundation_H
            if constexpr (std::is_same_v<T, winrt::hstring>)
            {
                return !_value.empty();
            }
            else
#endif
            {
                return _value;
            }
        }
        bool operator==(const property& other) const noexcept
        {
            return _value == other._value;
        }
        bool operator!=(const property& other) const noexcept
        {
            return _value != other._value;
        }
        bool operator==(const T& other) const noexcept
        {
            return _value == other;
        }
        bool operator!=(const T& other) const noexcept
        {
            return _value != other;
        }

    private:
        T _value;
    };

#ifdef WINRT_Windows_Foundation_H

    template<typename ArgsT>
    struct event
    {
        event<ArgsT>() = default;
        winrt::event_token operator()(const ArgsT& handler) { return _handlers.add(handler); }
        void operator()(const winrt::event_token& token) { _handlers.remove(token); }
        operator bool() const noexcept { return bool(_handlers); }
        template<typename... Arg>
        void raise(auto&&... args)
        {
            _handlers(std::forward<decltype(args)>(args)...);
        }
        winrt::event<ArgsT> _handlers;
    };

    template<typename SenderT = winrt::Windows::Foundation::IInspectable, typename ArgsT = winrt::Windows::Foundation::IInspectable>
    struct typed_event
    {
        typed_event<SenderT, ArgsT>() = default;
        winrt::event_token operator()(const winrt::Windows::Foundation::TypedEventHandler<SenderT, ArgsT>& handler) { return _handlers.add(handler); }
        void operator()(const winrt::event_token& token) { _handlers.remove(token); }
        operator bool() const noexcept { return bool(_handlers); }
        template<typename... Arg>
        void raise(Arg const&... args)
        {
            _handlers(std::forward<decltype(args)>(args)...);
        }
        winrt::event<winrt::Windows::Foundation::TypedEventHandler<SenderT, ArgsT>> _handlers;
    };
#endif
#ifdef WINRT_Windows_UI_Xaml_Data_H

    using property_changed_event = til::event<winrt::Windows::UI::Xaml::Data::PropertyChangedEventHandler>;
    // Making a til::observable_property unfortunately doesn't seem feasible.
    // It's gonna just result in more macros, which no one wants.
    //
    // 1. We don't know who the sender is, or would require `this` to always be
    //    the first parameter to one of these observable_property's.
    //
    // 2. We don't know what our own name is. We need to actually raise an event
    //    with the name of the variable as the parameter. Only way to do that is
    //    with something  like
    //
    //        til::observable<int> Foo(this, L"Foo", 42)
    //
    //    which then implies the creation of:
    //
    //        #define OBSERVABLE(type, name, ...) til::observable_property<type> name{ this, L## #name, this.PropertyChanged, __VA_ARGS__ };
    //
    //     Which is just silly

#endif
}
