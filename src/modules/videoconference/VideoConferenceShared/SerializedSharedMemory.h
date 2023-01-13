#pragma once
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include <Windows.h>
#include <string>
#include <optional>
#include <wil/resource.h>
#include <functional>
#include <array>

// Wrapper class allowing sharing readonly/writable memory with a serialized access via atomic locking.
// Note that it doesn't protect against a 3rd party concurrently modifying physical file contents.
class SerializedSharedMemory
{
public:
    struct memory_t
    {
      uint8_t * _data = nullptr;
      size_t _size = 0;
    };

    static std::optional<SerializedSharedMemory> create(const std::wstring_view object_name,
                                                        const size_t size,
                                                        const bool read_only,
                                                        SECURITY_ATTRIBUTES* maybe_attributes = nullptr) noexcept;
    static std::optional<SerializedSharedMemory> create_readonly(
        const std::wstring_view object_name,
        const std::wstring_view file_path,
        SECURITY_ATTRIBUTES* maybe_attributes = nullptr) noexcept;
    static std::optional<SerializedSharedMemory> open(const std::wstring_view object_name,
                                                      const size_t size,
                                                      const bool read_only) noexcept;

    void access(std::function<void(memory_t)> access_routine) noexcept;
    inline size_t size() const noexcept { return _memory._size; }

    ~SerializedSharedMemory() noexcept;
    SerializedSharedMemory(SerializedSharedMemory&&) noexcept;
    SerializedSharedMemory& operator=(SerializedSharedMemory&&) noexcept;

private:
    std::array<wil::unique_handle, 2> _handles;
    memory_t _memory;
    bool _read_only = true;
    constexpr static inline int64_t LOCKED = 1;

    char* lock_flag_addr() noexcept;
    void lock() noexcept;
    void unlock() noexcept;

    SerializedSharedMemory(std::array<wil::unique_handle, 2> handles, memory_t memory, const bool readonly) noexcept;
};