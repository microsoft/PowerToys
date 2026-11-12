#pragma once

#include <atomic>
#include <common/Telemetry/EtwTrace/EtwTrace.h>

extern HMODULE module_instance_handle;
extern Shared::Trace::ETWTrace trace;
extern std::atomic_uint32_t active_rename_workers;