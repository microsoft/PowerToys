#pragma once
#include "NativeMethods.g.h"

namespace winrt::PowerToys::FileLocksmithLib::Interop::implementation
{
    struct NativeMethods : NativeMethodsT<NativeMethods>
    {
        NativeMethods() = default;

        static com_array<winrt::PowerToys::FileLocksmithLib::Interop::ProcessResult> FindProcessesRecursive(array_view<hstring const> paths);
        static hstring PidToFullPath(uint32_t pid);
        static com_array<hstring> ReadPathsFromFile();
        static bool StartAsElevated(array_view<hstring const> paths);
        static bool SetDebugPrivilege();
        static bool IsProcessElevated();
    };
}
namespace winrt::PowerToys::FileLocksmithLib::Interop::factory_implementation
{
    struct NativeMethods : NativeMethodsT<NativeMethods, implementation::NativeMethods>
    {
    };
}
