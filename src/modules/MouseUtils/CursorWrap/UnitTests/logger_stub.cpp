#include "../../../common/logger/logger.h"

// Satisfies the only external logger symbol MonitorTopology.cpp references. Uses the spdlog
// shim (see shims/spdlog/spdlog.h) so no real spdlog is linked into the test binary.
std::shared_ptr<spdlog::logger> Logger::logger = spdlog::null_logger_mt("cursorwrap_unittests_null");
