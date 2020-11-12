#pragma once
#include <string>
#include <memory>
#include <filesystem>

class Logger
{
private:
    class impl;
    std::unique_ptr<impl, std::default_delete<impl>> _impl;
    //static std::filesystem::path logFilePath;

    // static Sink sink;
    // std::shared_ptr<spdlog::sinks::daily_file_sink_mt> sink;
    //std::shared_ptr<spdlog::logger> logger;

public:
    Logger();
    Logger(std::filesystem::path dir, std::string loggerName, std::string severity);
    void LogInfo(std::string str);

    ~Logger();
};