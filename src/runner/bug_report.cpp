#include "pch.h"
#include "bug_report.h"
#include "Generated files/resource.h"
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include "settings_window.h"

std::atomic_bool isBugReportThreadRunning = false;

void launch_bug_report() noexcept
{
    open_settings_window(std::nullopt, false, std::nullopt, true);
}
