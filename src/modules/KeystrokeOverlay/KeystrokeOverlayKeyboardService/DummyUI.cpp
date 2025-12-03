// DummyUI.cpp
// Minimal console "UI": prints one key per line from \\.\pipe\KeystrokeOverlayPipe
// Build (MinGW-w64):  x86_64-w64-mingw32-g++ DummyUI.cpp -o DummyUI.exe -static -lkernel32 -luser32

#include <windows.h>
#include <string>
#include <iostream>
#include <vector>
#include <cstdint>
#include <cstring>

static const wchar_t* kPipe = LR"(\\.\pipe\KeystrokeOverlayPipe)";

static bool ReadExact(HANDLE h, void* buf, DWORD len) {
    auto* p = static_cast<uint8_t*>(buf);
    DWORD got = 0;
    while (got < len) {
        DWORD n = 0;
        if (!ReadFile(h, p + got, len - got, &n, nullptr) || n == 0) return false;
        got += n;
    }
    return true;
}

// Very small extractor: for each {"t":...,"vk":...,"text":"..."} inside "events":[...]
// prints either text (if non-empty) or vk.
static void PrintOneLinePerEvent(const std::string& frame) {
    const char* s = frame.c_str();

    // find events array
    const char* events = strstr(s, "\"events\"");
    if (!events) return;
    const char* p = strchr(events, '[');
    if (!p) return;
    ++p;

    int depth = 0;
    std::string obj;
    for (const char* it = p; *it; ++it) {
        if (*it == '{') {
            if (depth++ == 0) obj.clear();
            obj.push_back(*it);
        } else if (*it == '}') {
            obj.push_back(*it);
            if (--depth == 0) {
                // extract "text" (string) and "vk" (number)
                auto pickString = [&](const char* key)->std::string {
                    std::string needle = std::string("\"")+key+"\"";
                    size_t pos = obj.find(needle);
                    if (pos == std::string::npos) return {};
                    pos = obj.find(':', pos);
                    if (pos == std::string::npos) return {};
                    ++pos;
                    while (pos < obj.size() && obj[pos] == ' ') ++pos;
                    if (pos >= obj.size() || obj[pos] != '"') return {};
                    size_t i = pos + 1; bool esc = false;
                    for (; i < obj.size(); ++i) {
                        char c = obj[i];
                        if (c == '\\' && !esc) { esc = true; continue; }
                        if (c == '"'  && !esc) break;
                        esc = false;
                    }
                    return obj.substr(pos + 1, i - (pos + 1));
                };
                auto pickNumber = [&](const char* key)->std::string {
                    std::string needle = std::string("\"")+key+"\"";
                    size_t pos = obj.find(needle);
                    if (pos == std::string::npos) return {};
                    pos = obj.find(':', pos);
                    if (pos == std::string::npos) return {};
                    ++pos;
                    while (pos < obj.size() && obj[pos] == ' ') ++pos;
                    size_t i = pos;
                    while (i < obj.size() && (obj[i] == '-' || obj[i] == '+' || (obj[i] >= '0' && obj[i] <= '9'))) ++i;
                    return obj.substr(pos, i - pos);
                };

                std::string text = pickString("text");
                if (!text.empty())
                    std::cout << text << "\n";
                else {
                    std::string vk = pickNumber("vk");
                    if (!vk.empty()) std::cout << "VK:" << vk << "\n";
                }
            }
        } else {
            if (depth > 0) obj.push_back(*it);
        }

        if (*it == ']' && depth == 0) break; // end of events array
    }
}

int main() {
    SetConsoleOutputCP(CP_UTF8); // show Unicode chars properly

    for (;;) {
        // wait/connect loop
        if (!WaitNamedPipeW(kPipe, 2000)) {
            Sleep(300);
            continue;
        }
        HANDLE h = CreateFileW(kPipe, GENERIC_READ, 0, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
        if (h == INVALID_HANDLE_VALUE) {
            Sleep(300);
            continue;
        }

        // read frames until disconnect
        for (;;) {
            DWORD len = 0;
            if (!ReadExact(h, &len, sizeof(len))) break;
            if (len == 0 || len > 8 * 1024 * 1024) break;

            std::string buf(len, '\0');
            if (!ReadExact(h, buf.data(), len)) break;

            // minimal per-event printing
            PrintOneLinePerEvent(buf);
        }

        CloseHandle(h);
        // reconnect
        Sleep(200);
    }
    return 0;
}
