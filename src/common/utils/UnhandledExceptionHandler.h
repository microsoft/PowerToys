#pragma once

#include <Windows.h>
#include <DbgHelp.h>
#include <signal.h>
#include <sstream>
#include <stdio.h>

#include "winapi_error.h"
#include "../logger/logger.h"

static BOOLEAN processingException = FALSE;

static inline const char* exceptionDescription(const DWORD& code)
{
    switch (code)
    {
    case EXCEPTION_ACCESS_VIOLATION:
        return "EXCEPTION_ACCESS_VIOLATION";
    case EXCEPTION_ARRAY_BOUNDS_EXCEEDED:
        return "EXCEPTION_ARRAY_BOUNDS_EXCEEDED";
    case EXCEPTION_BREAKPOINT:
        return "EXCEPTION_BREAKPOINT";
    case EXCEPTION_DATATYPE_MISALIGNMENT:
        return "EXCEPTION_DATATYPE_MISALIGNMENT";
    case EXCEPTION_FLT_DENORMAL_OPERAND:
        return "EXCEPTION_FLT_DENORMAL_OPERAND";
    case EXCEPTION_FLT_DIVIDE_BY_ZERO:
        return "EXCEPTION_FLT_DIVIDE_BY_ZERO";
    case EXCEPTION_FLT_INEXACT_RESULT:
        return "EXCEPTION_FLT_INEXACT_RESULT";
    case EXCEPTION_FLT_INVALID_OPERATION:
        return "EXCEPTION_FLT_INVALID_OPERATION";
    case EXCEPTION_FLT_OVERFLOW:
        return "EXCEPTION_FLT_OVERFLOW";
    case EXCEPTION_FLT_STACK_CHECK:
        return "EXCEPTION_FLT_STACK_CHECK";
    case EXCEPTION_FLT_UNDERFLOW:
        return "EXCEPTION_FLT_UNDERFLOW";
    case EXCEPTION_ILLEGAL_INSTRUCTION:
        return "EXCEPTION_ILLEGAL_INSTRUCTION";
    case EXCEPTION_IN_PAGE_ERROR:
        return "EXCEPTION_IN_PAGE_ERROR";
    case EXCEPTION_INT_DIVIDE_BY_ZERO:
        return "EXCEPTION_INT_DIVIDE_BY_ZERO";
    case EXCEPTION_INT_OVERFLOW:
        return "EXCEPTION_INT_OVERFLOW";
    case EXCEPTION_INVALID_DISPOSITION:
        return "EXCEPTION_INVALID_DISPOSITION";
    case EXCEPTION_NONCONTINUABLE_EXCEPTION:
        return "EXCEPTION_NONCONTINUABLE_EXCEPTION";
    case EXCEPTION_PRIV_INSTRUCTION:
        return "EXCEPTION_PRIV_INSTRUCTION";
    case EXCEPTION_SINGLE_STEP:
        return "EXCEPTION_SINGLE_STEP";
    case EXCEPTION_STACK_OVERFLOW:
        return "EXCEPTION_STACK_OVERFLOW";
    default:
        return "UNKNOWN EXCEPTION";
    }
}

/* Returns the index of the last backslash in the file path */
inline int GetFilenameStart(wchar_t* path)
{
    int pos = 0;
    int found = 0;
    if (path != NULL)
    {
        while (path[pos] != L'\0' && pos < MAX_PATH)
        {
            if (path[pos] == L'\\')
            {
                found = pos + 1;
            }
            ++pos;
        }
    }

    return found;
}

inline std::wstring GetModuleName(HANDLE process, const STACKFRAME64& stack)
{
    static wchar_t modulePath[MAX_PATH]{};
    const size_t size = sizeof(modulePath);
    memset(&modulePath[0], '\0', size);

    DWORD64 moduleBase = SymGetModuleBase64(process, stack.AddrPC.Offset);
    if (!moduleBase)
    {
        Logger::error(L"Failed to get a module. {}", get_last_error_or_default(GetLastError()));
        return std::wstring();
    }

    if (!GetModuleFileNameW(reinterpret_cast<HINSTANCE>(moduleBase), modulePath, MAX_PATH))
    {
        Logger::error(L"Failed to get a module path. {}", get_last_error_or_default(GetLastError()));
        return std::wstring();
    }

    const int start = GetFilenameStart(modulePath);
    return std::wstring(modulePath, start);
}

inline std::wstring GetName(HANDLE process, const STACKFRAME64& stack)
{
    static IMAGEHLP_SYMBOL64* pSymbol = static_cast<IMAGEHLP_SYMBOL64*>(malloc(sizeof(IMAGEHLP_SYMBOL64) + MAX_PATH * sizeof(TCHAR)));
    if (!pSymbol)
    {
        return std::wstring();
    }

    memset(pSymbol, '\0', sizeof(*pSymbol) + MAX_PATH);
    pSymbol->MaxNameLength = MAX_PATH;
    pSymbol->SizeOfStruct = sizeof(IMAGEHLP_SYMBOL64);

    DWORD64 dw64Displacement = 0;
    if (!SymGetSymFromAddr64(process, stack.AddrPC.Offset, &dw64Displacement, pSymbol))
    {
        Logger::error(L"Failed to get a symbol. {}", get_last_error_or_default(GetLastError()));
        return std::wstring();
    }

    std::string str = pSymbol->Name;
    return std::wstring(str.begin(), str.end());
}

inline std::wstring GetLine(HANDLE process, const STACKFRAME64& stack)
{
    static IMAGEHLP_LINE64 line{};

    memset(&line, '\0', sizeof(IMAGEHLP_LINE64));
    line.SizeOfStruct = sizeof(IMAGEHLP_LINE64);
    line.LineNumber = 0;

    DWORD dwDisplacement = 0;
    if (!SymGetLineFromAddr64(process, stack.AddrPC.Offset, &dwDisplacement, &line))
    {
        return std::wstring();
    }

    std::string fileName(line.FileName);
    return L"(" + std::wstring(fileName.begin(), fileName.end()) + L":" + std::to_wstring(line.LineNumber) + L")";
}

inline void LogStackTrace()
{
    CONTEXT context;
    try
    {
        RtlCaptureContext(&context);
    }
    catch (...)
    {
        Logger::error(L"Failed to capture context. {}", get_last_error_or_default(GetLastError()));
        return;
    }

    STACKFRAME64 stack;
    memset(&stack, 0, sizeof(STACKFRAME64));

    HANDLE process = GetCurrentProcess();
    HANDLE thread = GetCurrentThread();

#ifdef _M_ARM64
    stack.AddrPC.Offset = context.Pc;
    stack.AddrStack.Offset = context.Sp;
    stack.AddrFrame.Offset = context.Fp;
#else
    stack.AddrPC.Offset = context.Rip;
    stack.AddrStack.Offset = context.Rsp;
    stack.AddrFrame.Offset = context.Rbp;
#endif
    stack.AddrPC.Mode = AddrModeFlat;
    stack.AddrStack.Mode = AddrModeFlat;
    stack.AddrFrame.Mode = AddrModeFlat;

    BOOL result = false;
    std::wstringstream ss;
    for (;;)
    {
        result = StackWalk64(
#ifdef _M_ARM64
            IMAGE_FILE_MACHINE_ARM64,
#else
            IMAGE_FILE_MACHINE_AMD64,
#endif
            process,
            thread,
            &stack,
            &context,
            NULL,
            SymFunctionTableAccess64,
            SymGetModuleBase64,
            NULL);

        if (!result)
        {
            break;
        }

        ss << GetModuleName(process, stack) << "!" << GetName(process, stack) << GetLine(process, stack) << std::endl;
    }

    Logger::error(L"STACK TRACE\r\n{}", ss.str());
    Logger::flush();
}

inline LONG WINAPI UnhandledExceptionHandler(PEXCEPTION_POINTERS info)
{
    if (!processingException)
    {
        bool headerLogged = false;
        try
        {
            const char* exDescription = "Exception code not available";
            processingException = true;
            if (info != NULL && info->ExceptionRecord != NULL && info->ExceptionRecord->ExceptionCode != NULL)
            {
                exDescription = exceptionDescription(info->ExceptionRecord->ExceptionCode);
            }

            headerLogged = true;
            Logger::error(exDescription);
            LogStackTrace();
        }
        catch (...)
        {
            Logger::error("Failed to log stack trace");
            Logger::flush();
        }

        processingException = false;
    }

    return EXCEPTION_CONTINUE_SEARCH;
}

/* Handler to trap abort() calls */
inline void AbortHandler(int /*signal_number*/)
{
    Logger::error("--- ABORT");
    try
    {
        LogStackTrace();
    }
    catch (...)
    {
        Logger::error("Failed to log stack trace on abort");
        Logger::flush();
    }
}

inline void InitSymbols()
{
    // Preload symbols so they will be available in case of out-of-memory exception
    SymSetOptions(SYMOPT_LOAD_LINES | SYMOPT_UNDNAME);
    HANDLE process = GetCurrentProcess();
    if (!SymInitialize(process, NULL, TRUE))
    {
        Logger::error(L"Failed to initialize symbol handler. {}", get_last_error_or_default(GetLastError()));
    }
}

inline void InitUnhandledExceptionHandler(void)
{
    try
    {
        InitSymbols();
        // Global handler for unhandled exceptions
        SetUnhandledExceptionFilter(UnhandledExceptionHandler);
        // Handler for abort()
        signal(SIGABRT, &AbortHandler);
    }
    catch (...)
    {
        Logger::error("Failed to init global unhandled exception handler");
    }
}
