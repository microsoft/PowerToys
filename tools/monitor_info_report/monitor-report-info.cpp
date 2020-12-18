#include "report-monitor-info.h"
#include <Windows.h>
#include <iostream>
#include <ctime>
#include <fstream>
#include <sstream>
#include "../../src/common/utils/winapi_error.h"

int main()
{
    time_t rawtime;
    struct tm* timeinfo;
    char buffer[1024];

    time(&rawtime);
    timeinfo = localtime(&rawtime);

    strftime(buffer, sizeof(buffer), "monitor-info-report-%d-%m-%Y-%H-%M-%S.txt", timeinfo);
    std::string str(buffer);

    std::wofstream of{ str };
    std::wostringstream oss;
    try
    {
        oss << "GetSystemMetrics = " << GetSystemMetrics(SM_CMONITORS) << '\n';
        report(oss);
    }
    catch (std::exception& ex)
    {
        oss << "exception: " << ex.what() << '\n';
    }
    catch (...)
    {
        auto message = get_last_error_message(GetLastError());
        oss << "unknown exception: " << (message.has_value() ? message.value() : L"") << '\n';
    }
    of << oss.str();
    std::wcout << oss.str() << '\n';
    return 0;
}