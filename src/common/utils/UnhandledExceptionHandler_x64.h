#pragma once

#include <Windows.h>
#include <DbgHelp.h>
#include <signal.h>
#include <sstream>
#include <stdio.h>

#include "winapi_error.h"
#include "../logger/logger.h"

static IMAGEHLP_SYMBOL64* pSymbol = (IMAGEHLP_SYMBOL64*)malloc(sizeof(IMAGEHLP_SYMBOL64) + MAX_PATH * sizeof(TCHAR));
static IMAGEHLP_LINE64 line;
static BOOLEAN processingException = FALSE;
static CHAR modulePath[MAX_PATH];

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
inline int GetFilenameStart(CHAR* path)
{
    int pos = 0;
    int found = 0;
    if (path != NULL)
    {
        while (path[pos] != '\0' && pos < MAX_PATH)
        {
            if (path[pos] == '\\')
            {
                found = pos + 1;
            }
            ++pos;
        }
    }

    return found;
}

inline void LogStackTrace()
{
    BOOL result;
    HANDLE thread;
    HANDLE process;
    CONTEXT context;
    STACKFRAME64 stack;
    ULONG frame;
    DWORD64 dw64Displacement;
    DWORD dwDisplacement;

    memset(&stack, 0, sizeof(STACKFRAME64));
    memset(pSymbol, '\0', sizeof(*pSymbol) + MAX_PATH);
    memset(&modulePath[0], '\0', sizeof(modulePath));
    line.LineNumber = 0;

    try
    {
        RtlCaptureContext(&context);
    }
    catch (...)
    {
        Logger::error(L"Failed to capture context. {}", get_last_error_or_default(GetLastError()));
        return;
    }
    
    process = GetCurrentProcess();
    thread = GetCurrentThread();
    dw64Displacement = 0;
    stack.AddrPC.Offset = context.Rip;
    stack.AddrPC.Mode = AddrModeFlat;
    stack.AddrStack.Offset = context.Rsp;
    stack.AddrStack.Mode = AddrModeFlat;
    stack.AddrFrame.Offset = context.Rbp;
    stack.AddrFrame.Mode = AddrModeFlat;

    std::stringstream ss;
    for (frame = 0;; frame++)
    {
        result = StackWalk64(
            IMAGE_FILE_MACHINE_AMD64,
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

        pSymbol->MaxNameLength = MAX_PATH;
        pSymbol->SizeOfStruct = sizeof(IMAGEHLP_SYMBOL64);

        if (!SymGetSymFromAddr64(process, stack.AddrPC.Offset, &dw64Displacement, pSymbol))
        {
            Logger::error(L"Failed to get a symbol. {}", get_last_error_or_default(GetLastError()));
        }

        line.LineNumber = 0;
        SymGetLineFromAddr64(process, stack.AddrPC.Offset, &dwDisplacement, &line);

        DWORD64 moduleBase = SymGetModuleBase64(process, stack.AddrPC.Offset);
        if (moduleBase)
        {
            if (!GetModuleFileNameA((HINSTANCE)moduleBase, modulePath, MAX_PATH))
            {
                Logger::error(L"Failed to get a module path. {}", get_last_error_or_default(GetLastError()));
            }
        }
        else
        {
            Logger::error(L"Failed to get a module. {}", get_last_error_or_default(GetLastError()));
        }
        
        ss << std::string(modulePath).substr(GetFilenameStart(modulePath)) << "!" << pSymbol->Name << "(" << line.FileName << ":" << line.LineNumber << std::endl;
    }

    Logger::error("STACK TRACE\r\n{}", ss.str());
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
inline void AbortHandler(int signal_number)
{
    Logger::error("--- ABORT");
    try
    {
        LogStackTrace();
    }
    catch(...)
    {
        Logger::error("Failed to log stack trace on abort");
        Logger::flush();
    }
}

inline void InitSymbols()
{
    // Preload symbols so they will be available in case of out-of-memory exception
    SymSetOptions(SYMOPT_LOAD_LINES | SYMOPT_UNDNAME);
    line.SizeOfStruct = sizeof(IMAGEHLP_LINE64);
    HANDLE process = GetCurrentProcess();
    if (!SymInitialize(process, NULL, TRUE))
    {
        Logger::error(L"Failed to initialize symbol handler. {}", get_last_error_or_default(GetLastError()));
    }
}

inline void InitUnhandledExceptionHandler_x64(void)
{
    try
    {
        InitSymbols();
        // Global handler for unhandled exceptions
        SetUnhandledExceptionFilter(UnhandledExceptionHandler);
        // Handler for abort()
        signal(SIGABRT, &AbortHandler);    
    }
    catch(...)
    {
        Logger::error("Failed to init global unhandled exception handler");
    }
}
