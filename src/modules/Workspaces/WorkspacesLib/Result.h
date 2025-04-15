#pragma once

#include <variant>

template<typename T>
class Ok
{
public:
    explicit constexpr Ok(T value) :
        value(std::move(value)) {}

    constexpr T&& get() { return std::move(value); }

    T value;
};

template<typename T>
class Error
{
public:
    explicit constexpr Error(T value) :
        value(std::move(value)) {}

    constexpr T&& get() { return std::move(value); }

    T value;
};

template<typename OkT, typename ErrT>
class Result
{
public:
    using VariantT = std::variant<Ok<OkT>, Error<ErrT>>;

    constexpr Result(Ok<OkT> value) :
        variant(std::move(value)) 
    {}
    
    constexpr Result(Error<ErrT> value) :
        variant(std::move(value)) 
    {}

    constexpr bool isOk() const { return std::holds_alternative<Ok<OkT>>(variant); }
    constexpr bool isError() const { return std::holds_alternative<Error<ErrT>>(variant); }

    constexpr OkT value() const { return std::get<Ok<OkT>>(variant).value; }
    constexpr ErrT error() const { return std::get<Error<ErrT>>(variant).value; }

    constexpr OkT&& getValue() { return std::get<Ok<OkT>>(variant).get(); }
    constexpr ErrT&& getError() { return std::get<Error<ErrT>>(variant).get(); }

    VariantT variant;
};
