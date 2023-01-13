#include "SerializedSharedMemory.h"
#ifdef _M_ARM64
#define _mm_pause() __yield();
#endif
inline char* SerializedSharedMemory::lock_flag_addr() noexcept
{
    return reinterpret_cast<char*>(_memory._data + _memory._size);
}

inline void SerializedSharedMemory::lock() noexcept
{
    if (_read_only)
    {
        return;
    }
    while (LOCKED == _InterlockedCompareExchange8(lock_flag_addr(), LOCKED, !LOCKED))
    {
        while (*lock_flag_addr() == LOCKED)
        {
            _mm_pause();
        }
    }
}

inline void SerializedSharedMemory::unlock() noexcept
{
    if (_read_only)
    {
        return;
    }
    _InterlockedExchange8(lock_flag_addr(), !LOCKED);
}

SerializedSharedMemory::SerializedSharedMemory(std::array<wil::unique_handle, 2> handles,
                                               memory_t memory,
                                               const bool readonly) noexcept
    :
    _handles{ std::move(handles) }, _memory{ std::move(memory) }, _read_only(readonly)
{
}

SerializedSharedMemory::~SerializedSharedMemory() noexcept
{
    if (_memory._data)
    {
        UnmapViewOfFile(_memory._data);
    }
}

SerializedSharedMemory::SerializedSharedMemory(SerializedSharedMemory&& rhs) noexcept
{
    *this = std::move(rhs);
}

SerializedSharedMemory& SerializedSharedMemory::operator=(SerializedSharedMemory&& rhs) noexcept
{
    _handles = {};
    _handles.swap(rhs._handles);
    _memory = std::move(rhs._memory);
    rhs._memory = {};
    _read_only = rhs._read_only;
    rhs._read_only = true;

    return *this;
}

std::optional<SerializedSharedMemory> SerializedSharedMemory::create(const std::wstring_view object_name,
                                                                     const size_t size,
                                                                     const bool read_only,
                                                                     SECURITY_ATTRIBUTES* maybe_attributes) noexcept
{
    SECURITY_DESCRIPTOR sd;
    SECURITY_ATTRIBUTES sa = { sizeof SECURITY_ATTRIBUTES };
    if (!maybe_attributes)
    {
        sa.lpSecurityDescriptor = &sd;
        sa.bInheritHandle = false;
        if (!InitializeSecurityDescriptor(&sd, SECURITY_DESCRIPTOR_REVISION) ||
            !SetSecurityDescriptorDacl(&sd, true, nullptr, false))
        {
            return std::nullopt;
        }
    }

    // We need an extra byte for locking if it's not readonly
    const ULARGE_INTEGER UISize{ .QuadPart = static_cast<uint64_t>(size) + !read_only };

    wil::unique_handle hMapFile{ CreateFileMappingW(INVALID_HANDLE_VALUE,
                                                    maybe_attributes ? maybe_attributes : &sa,
                                                    read_only ? PAGE_READONLY : PAGE_READWRITE,
                                                    UISize.HighPart,
                                                    UISize.LowPart,
                                                    object_name.data()) };
    if (!hMapFile)
    {
        return std::nullopt;
    }
    auto shmem = static_cast<uint8_t*>(
        MapViewOfFile(hMapFile.get(), read_only ? FILE_MAP_READ : FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, static_cast<SIZE_T>(UISize.QuadPart)));
    if (!shmem)
    {
        return std::nullopt;
    }
    std::array<wil::unique_handle, 2> handles = { std::move(hMapFile), {} };
    return SerializedSharedMemory{ std::move(handles), memory_t{ shmem, size }, read_only };
}

std::optional<SerializedSharedMemory> SerializedSharedMemory::open(const std::wstring_view object_name,
                                                                   const size_t size,
                                                                   const bool read_only) noexcept
{
    wil::unique_handle hMapFile{ OpenFileMappingW(FILE_MAP_READ | FILE_MAP_WRITE, FALSE, object_name.data()) };
    if (!hMapFile)
    {
        return std::nullopt;
    }

    auto shmem = static_cast<uint8_t*>(
        MapViewOfFile(hMapFile.get(), read_only ? FILE_MAP_READ : FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, size + !read_only));

    if (!shmem)
    {
        return std::nullopt;
    }
    std::array<wil::unique_handle, 2> handles = { std::move(hMapFile), {} };
    return SerializedSharedMemory{ std::move(handles), memory_t{ shmem, size }, read_only };
}

std::optional<SerializedSharedMemory> SerializedSharedMemory::create_readonly(
    const std::wstring_view object_name,
    const std::wstring_view file_path,
    SECURITY_ATTRIBUTES* maybe_attributes) noexcept
{
    SECURITY_DESCRIPTOR sd;
    SECURITY_ATTRIBUTES sa = { sizeof SECURITY_ATTRIBUTES };
    if (!maybe_attributes)
    {
        sa.lpSecurityDescriptor = &sd;
        sa.bInheritHandle = false;
        if (!InitializeSecurityDescriptor(&sd, SECURITY_DESCRIPTOR_REVISION) ||
            !SetSecurityDescriptorDacl(&sd, true, nullptr, false))
        {
            return std::nullopt;
        }
    }
    wil::unique_handle hFile{ CreateFileW(file_path.data(),
                                          GENERIC_READ,
                                          FILE_SHARE_READ | FILE_SHARE_WRITE,
                                          maybe_attributes ? maybe_attributes : &sa,
                                          OPEN_EXISTING,
                                          FILE_ATTRIBUTE_NORMAL,
                                          nullptr) };

    if (!hFile)
    {
        return std::nullopt;
    }

    LARGE_INTEGER fileSize;
    if (!GetFileSizeEx(hFile.get(), &fileSize))
    {
        return std::nullopt;
    }
    wil::unique_handle hMapFile{ CreateFileMappingW(hFile.get(),
                                                    maybe_attributes ? maybe_attributes : &sa,
                                                    PAGE_READONLY,
                                                    fileSize.HighPart,
                                                    fileSize.LowPart,
                                                    object_name.data()) };
    if (!hMapFile)
    {
        return std::nullopt;
    }

    auto shmem = static_cast<uint8_t*>(MapViewOfFile(nullptr, FILE_MAP_READ, 0, 0, static_cast<size_t>(fileSize.QuadPart)));
    if (shmem)
    {
        return std::nullopt;
    }
    std::array<wil::unique_handle, 2> handles = { std::move(hMapFile), std::move(hFile) };

    return SerializedSharedMemory{ std::move(handles), memory_t{ shmem, static_cast<size_t>(fileSize.QuadPart) }, true };
}

void SerializedSharedMemory::access(std::function<void(memory_t)> access_routine) noexcept
{
    lock();
    access_routine(_memory);
    unlock();
}