// spdlog-msvc-fix.h
//
// Workaround for MSVC 14.51 (compiler version 19.51, _MSC_VER >= 1951) removing
// stdext::checked_array_iterator. Force-included for all spdlog consumers via
// deps/spdlog.props, because spdlog v1.8.5's bundled fmt format.h(357) still
// references this type inside #if defined(_SECURE_SCL) && _SECURE_SCL -- a
// branch entered in Debug builds where _ITERATOR_DEBUG_LEVEL > 0.
//
// On MSVC 14.50 and earlier, the type still exists in <iterator>, so this shim
// is a no-op via the _MSC_VER guard. On MSVC 14.51+, it provides a minimal
// pointer-backed substitute that satisfies the bundled fmt's usage:
//
//   template <typename T> using checked_ptr = stdext::checked_array_iterator<T*>;
//   template <typename T> checked_ptr<T> make_checked(T* p, size_t size) {
//     return {p, size};
//   }
//   ... return make_checked(get_data(c) + size, n);
//
// When deps/spdlog is bumped past v1.14 (which ships fmt 10.2 and drops this
// dependency), this shim and its <ForcedIncludeFiles> entry in deps/spdlog.props
// can be deleted.

#pragma once

#if defined(__cplusplus) && defined(_MSC_VER) && _MSC_VER >= 1951

#include <cstddef>
#include <iterator>
#include <type_traits>

namespace stdext
{
    template <typename _Ptr>
    class checked_array_iterator
    {
        _Ptr _Myarray = nullptr;
        std::size_t _Mysize = 0;
        std::size_t _Myindex = 0;

    public:
        using iterator_category = std::random_access_iterator_tag;
        using value_type = std::remove_cv_t<std::remove_pointer_t<_Ptr>>;
        using difference_type = std::ptrdiff_t;
        using pointer = _Ptr;
        using reference = std::remove_pointer_t<_Ptr>&;

        constexpr checked_array_iterator() = default;

        constexpr checked_array_iterator(_Ptr arr, std::size_t size, std::size_t idx = 0) noexcept
            : _Myarray(arr), _Mysize(size), _Myindex(idx)
        {
        }

        constexpr reference operator*() const noexcept { return _Myarray[_Myindex]; }
        constexpr pointer operator->() const noexcept { return _Myarray + _Myindex; }
        constexpr reference operator[](difference_type n) const noexcept
        {
            return _Myarray[_Myindex + static_cast<std::size_t>(n)];
        }

        constexpr checked_array_iterator& operator++() noexcept { ++_Myindex; return *this; }
        constexpr checked_array_iterator operator++(int) noexcept { auto t = *this; ++_Myindex; return t; }
        constexpr checked_array_iterator& operator--() noexcept { --_Myindex; return *this; }
        constexpr checked_array_iterator operator--(int) noexcept { auto t = *this; --_Myindex; return t; }

        constexpr checked_array_iterator& operator+=(difference_type n) noexcept
        {
            _Myindex = static_cast<std::size_t>(static_cast<difference_type>(_Myindex) + n);
            return *this;
        }
        constexpr checked_array_iterator& operator-=(difference_type n) noexcept
        {
            _Myindex = static_cast<std::size_t>(static_cast<difference_type>(_Myindex) - n);
            return *this;
        }

        friend constexpr checked_array_iterator operator+(checked_array_iterator it, difference_type n) noexcept { it += n; return it; }
        friend constexpr checked_array_iterator operator+(difference_type n, checked_array_iterator it) noexcept { return it + n; }
        friend constexpr checked_array_iterator operator-(checked_array_iterator it, difference_type n) noexcept { it -= n; return it; }
        friend constexpr difference_type operator-(checked_array_iterator a, checked_array_iterator b) noexcept
        {
            return static_cast<difference_type>(a._Myindex) - static_cast<difference_type>(b._Myindex);
        }

        friend constexpr bool operator==(checked_array_iterator a, checked_array_iterator b) noexcept { return a._Myindex == b._Myindex; }
        friend constexpr bool operator!=(checked_array_iterator a, checked_array_iterator b) noexcept { return !(a == b); }
        friend constexpr bool operator<(checked_array_iterator a, checked_array_iterator b) noexcept { return a._Myindex < b._Myindex; }
        friend constexpr bool operator>(checked_array_iterator a, checked_array_iterator b) noexcept { return b < a; }
        friend constexpr bool operator<=(checked_array_iterator a, checked_array_iterator b) noexcept { return !(b < a); }
        friend constexpr bool operator>=(checked_array_iterator a, checked_array_iterator b) noexcept { return !(a < b); }
    };
} // namespace stdext

#endif // __cplusplus && _MSC_VER >= 1951
