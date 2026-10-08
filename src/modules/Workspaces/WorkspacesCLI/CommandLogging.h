// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#pragma once

#include <spdlog/sinks/base_sink.h>
#include <spdlog/sinks/basic_file_sink.h>
#include <common/logger/logger.h>
#include <WorkspacesLib/CliCommands.h>

namespace CliLogging
{
    class CommandSink final : public spdlog::sinks::base_sink<std::mutex>
    {
    public:
        explicit CommandSink(const std::filesystem::path& path) :
            m_file(std::make_shared<spdlog::sinks::basic_file_sink_mt>(path.wstring(), false))
        {
        }

    private:
        void sink_it_(const spdlog::details::log_msg& message) override
        {
            const std::string_view text(message.payload.data(), message.payload.size());
            if (text.starts_with("Workspaces CLI:"))
            {
                m_file->log(message);
            }
            else if (message.level >= spdlog::level::warn)
            {
                // Shared launcher diagnostics can contain paths, titles and arguments.
                auto sanitized = message;
                sanitized.payload = "Workspaces module reported a warning/error; consult the CLI result.";
                m_file->log(sanitized);
            }
        }

        void flush_() override
        {
            m_file->flush();
        }

        std::shared_ptr<spdlog::sinks::basic_file_sink_mt> m_file;
    };

    inline void Initialize()
    {
        const auto folder = WorkspacesCli::SettingsRoot() / L"Workspaces" / L"Logs";
        std::filesystem::create_directories(folder);
        Logger::init(std::vector<spdlog::sink_ptr>{ std::make_shared<CommandSink>(folder / L"cli.log") });
    }
}
