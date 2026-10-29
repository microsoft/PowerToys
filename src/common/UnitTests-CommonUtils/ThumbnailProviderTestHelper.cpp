#include "ThumbnailProviderTestProtocol.h"

#include <filesystem>
#include <fstream>
#include <string>

#include <thumbnail_provider.h>

int wmain(int argc, wchar_t* argv[])
{
    if (argc != 4)
    {
        return ERROR_BAD_ARGUMENTS;
    }

    BOOL processInOuterJob = FALSE;
    if (!IsProcessInJob(GetCurrentProcess(), nullptr, &processInOuterJob))
    {
        return static_cast<int>(GetLastError());
    }

    const std::filesystem::path resultPath{ argv[1] };
    const std::filesystem::path application{ argv[2] };
    const std::filesystem::path pingPath{ argv[3] };
    const auto arguments =
        L"/d /s /c \"\"" + pingPath.wstring() + L"\" -n 120 127.0.0.1 >nul\"";
    const auto launch = thumbnail_provider::launch_in_job(application, arguments, 5'000);

    const nested_job_probe_result result{
        .process_in_outer_job = static_cast<DWORD>(processInOuterJob),
        .launch_status = static_cast<DWORD>(launch.status),
        .error = launch.error,
        .exit_code = launch.exit_code,
        .process_id = launch.process_id,
    };

    std::ofstream output(resultPath, std::ios::binary | std::ios::trunc);
    output.write(reinterpret_cast<const char*>(&result), sizeof(result));
    if (!output.good())
    {
        return ERROR_WRITE_FAULT;
    }

    return processInOuterJob &&
                   launch.status == thumbnail_provider::launch_status::timed_out &&
                   launch.error == ERROR_TIMEOUT ?
               ERROR_SUCCESS :
               ERROR_PROCESS_ABORTED;
}
