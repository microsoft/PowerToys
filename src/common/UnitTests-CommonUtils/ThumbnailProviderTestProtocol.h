#pragma once

#include <Windows.h>

struct nested_job_probe_result
{
    DWORD version = 1;
    DWORD process_in_outer_job = FALSE;
    DWORD launch_status = 0;
    DWORD error = ERROR_SUCCESS;
    DWORD exit_code = 0;
    DWORD process_id = 0;
};
