// Test string utilities - mock for orchestration test
// Related to issue #45363

#pragma once

#include <string>

namespace PowerToys {
namespace Utils {

inline std::wstring TruncateString(const std::wstring& input, size_t maxLength) {
    if (input.length() <= maxLength) return input;
    if (maxLength <= 3) return input.substr(0, maxLength);
    return input.substr(0, maxLength - 3) + L"...";
}

} // namespace Utils
} // namespace PowerToys
