#include "ntdll.hpp"

#include <vector>
#include <iostream>
#include <string>
#include <string_view>

std::wstring_view unicode_to_view(UNICODE_STRING str) {
    return { str.Buffer, str.Length / sizeof(WCHAR) };
}

int cin_test() {
    NtdllExtensions ntdll;

    std::wstring file_name;
    std::getline(std::wcin, file_name);
    
    DWORD target_pid;
    std::wcin >> target_pid;
    
    while (file_name.size() && iswspace(file_name.back())) {
        file_name.pop_back();
    }

    HANDLE file_handle = CreateFileW(file_name.c_str(), 0, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, NULL, OPEN_EXISTING, 0, NULL);
    if (file_handle == INVALID_HANDLE_VALUE) {
        std::wcout << L"Error opening the target file: " << file_name << '\n';
        return 1;
    }

    auto kernel_name = ntdll.file_handle_to_kernel_name(file_handle);
    if (kernel_name.empty()) {
        std::wcout << L"Error reading name from kernel\n";
        return 1;
    }
    CloseHandle(file_handle);

    for (auto [pid, h, tn, fn] : ntdll.handles()) {
        if (fn == kernel_name || fn == file_name || pid == target_pid) {
            std::wcout << pid << ' ' << h << ' ' << tn << ' ' << fn << '\n';
        }
    }

    return 0;
}

int dump_test() {
    NtdllExtensions ntdll;

    for (auto [name, pid] : ntdll.processes()) {
        std::wcout << name << ' ' << pid << '\n';
    }

    for (auto [pid, h, tn, fn] : ntdll.handles()) {
        if (!fn.empty()) {
            std::wcout << pid << ' ' << h << ' ' << tn << ' ' << fn << '\n';
        }
    }

    std::wstring file_name;
    std::getline(std::wcin, file_name);
    while (file_name.size() && iswspace(file_name.back())) {
        file_name.pop_back();
    }

    HANDLE file_handle = CreateFileW(file_name.c_str(), 0, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, NULL, OPEN_EXISTING, 0, NULL);
    if (!file_handle) {
        std::wcout << L"Error opening the target file: " << file_name << '\n';
        return 1;
    }

    auto kernel_name = ntdll.file_handle_to_kernel_name(file_handle);
    if (kernel_name.empty()) {
        std::wcout << L"Error reading name from kernel\n";
        return 1;
    }

    CloseHandle(file_handle);
    return 0;
}

int file_hog() {
    HANDLE file_handle = CreateFileW(L"C:\\hog", GENERIC_READ | GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, 0, NULL);
    std::wcout << "Press enter to close: ";
    std::wstring ignored;
    std::getline(std::wcin, ignored);
    CloseHandle(file_handle);
    return 0;
}

int main() {
    int test;
    std::wcin >> test;
    std::wstring ignored;
    std::getline(std::wcin, ignored);

    if (test == 0) {
        return cin_test();
    }
    else if (test == 1) {
        return dump_test();
    }
    else if (test == 2) {
        return file_hog();
    }
}
