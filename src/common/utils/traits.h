#pragma once

#include <memory>

template <typename T>
inline void default_delete(T * p) noexcept
{
  delete p;
}

template <typename T>
using pimpl_t = std::unique_ptr<T, decltype(&default_delete<T>)>;
