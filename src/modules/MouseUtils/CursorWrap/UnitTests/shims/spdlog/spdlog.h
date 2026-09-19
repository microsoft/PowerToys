// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Minimal spdlog shim for CursorWrap unit tests.
//
// MonitorTopology.cpp pulls in the shared logger.h, which unconditionally includes
// <spdlog/spdlog.h>. Linking the real logger.lib (and therefore the full spdlog library)
// drags tens of megabytes of spdlog/fmt template debug information into the test binary's
// PDB, which the native CppUnitTest (VSTest) discoverer fails to parse - so no tests are
// discovered. This shim satisfies every spdlog symbol referenced by logger.h with trivial,
// header-only no-ops so the unit-test binary is completely spdlog-free and stays lean.
#pragma once

#include <memory>
#include <string>
#include <vector>

namespace spdlog
{
	namespace sinks
	{
		class sink
		{
		};
	}

	using sink_ptr = std::shared_ptr<sinks::sink>;

	class logger
	{
	public:
		template<typename FormatString, typename... Args>
		void trace(const FormatString&, const Args&...) {}
		template<typename FormatString, typename... Args>
		void debug(const FormatString&, const Args&...) {}
		template<typename FormatString, typename... Args>
		void info(const FormatString&, const Args&...) {}
		template<typename FormatString, typename... Args>
		void warn(const FormatString&, const Args&...) {}
		template<typename FormatString, typename... Args>
		void error(const FormatString&, const Args&...) {}
		template<typename FormatString, typename... Args>
		void critical(const FormatString&, const Args&...) {}
		void flush() {}
	};

	inline std::shared_ptr<logger> null_logger_mt(const std::string&)
	{
		return std::make_shared<logger>();
	}
}
