#pragma once
#include <atomic>
#include <future>
#include <string>
#include <filesystem>
#include <Windows.h>

void InitializeReportBugLinkAsync();
bool LaunchBugReport();
std::string FindNewestBugReportFile();
std::string GetDotNetVersion();
std::string GetOSVersion();
std::string GetModuleFolderPath();
std::string WideStringToString(const std::wstring& wstr);
std::wstring stringToWideString(const std::string& str);
std::string ReportWindowsSettings();

extern std::atomic_bool isBugReportThreadRunning;
