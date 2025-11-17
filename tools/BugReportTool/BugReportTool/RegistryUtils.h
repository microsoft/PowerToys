#pragma once

#include <vector>
#include <string>
#include <fstream>
#include <filesystem>
#include <unordered_map>
#include <Windows.h>

void ReportRegistry(const std::filesystem::path& tmpDir);
void ReportCompatibilityTab(const std::filesystem::path& tmpDir);
