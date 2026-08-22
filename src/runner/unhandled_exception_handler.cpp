#include "pch.h"
#if _DEBUG && _WIN64
#include "unhandled_exception_handler.h"
#include <DbgHelp.h>
#pragma comment(lib, "DbgHelp.lib")
#include <string>
#include <sstream>
#include <csignal>

static IMAGEHLP_SYMBOL64* p_symbol = static_cast<IMAGEHLP_SYMBOL64*>(malloc(sizeof(IMAGEHLP_SYMBOL64) + MAX_PATH * sizeof(WCHAR)));
static IMAGEHLP_LINE64 line;
static bool processing_exception = false;
static WCHAR module_path[MAX_PATH];
static LPTOP_LEVEL_EXCEPTION_FILTER default_top_level_exception_handler = NULL;

static const WCHAR* exception_description(const DWORD& code)
{
    switch (code)
    {
    case EXCEPTION_ACCESS_VIOLATION:
        return L"EXCEPTION_ACCESS_VIOLATION";
    case EXCEPTION_ARRAY_BOUNDS_EXCEEDED:
        return L"EXCEPTION_ARRAY_BOUNDS_EXCEEDED";
    case EXCEPTION_BREAKPOINT:
        return L"EXCEPTION_BREAKPOINT";
    case EXCEPTION_DATATYPE_MISALIGNMENT:
        return L"EXCEPTION_DATATYPE_MISALIGNMENT";
    case EXCEPTION_FLT_DENORMAL_OPERAND:
        return L"EXCEPTION_FLT_DENORMAL_OPERAND";
    case EXCEPTION_FLT_DIVIDE_BY_ZERO:
        return L"EXCEPTION_FLT_DIVIDE_BY_ZERO";
    case EXCEPTION_FLT_INEXACT_RESULT:
        return L"EXCEPTION_FLT_INEXACT_RESULT";
    case EXCEPTION_FLT_INVALID_OPERATION:
        return L"EXCEPTION_FLT_INVALID_OPERATION";
    case EXCEPTION_FLT_OVERFLOW:
        return L"EXCEPTION_FLT_OVERFLOW";
    case EXCEPTION_FLT_STACK_CHECK:
        return L"EXCEPTION_FLT_STACK_CHECK";
    case EXCEPTION_FLT_UNDERFLOW:
        return L"EXCEPTION_FLT_UNDERFLOW";
    case EXCEPTION_ILLEGAL_INSTRUCTION:
        return L"EXCEPTION_ILLEGAL_INSTRUCTION";
    case EXCEPTION_IN_PAGE_ERROR:
        return L"EXCEPTION_IN_PAGE_ERROR";
    case EXCEPTION_INT_DIVIDE_BY_ZERO:
        return L"EXCEPTION_INT_DIVIDE_BY_ZERO";
    case EXCEPTION_INT_OVERFLOW:
        return L"EXCEPTION_INT_OVERFLOW";
    case EXCEPTION_INVALID_DISPOSITION:
        return L"EXCEPTION_INVALID_DISPOSITION";
    case EXCEPTION_NONCONTINUABLE_EXCEPTION:
        return L"EXCEPTION_NONCONTINUABLE_EXCEPTION";
    case EXCEPTION_PRIV_INSTRUCTION:
        return L"EXCEPTION_PRIV_INSTRUCTION";
    case EXCEPTION_SINGLE_STEP:
        return L"EXCEPTION_SINGLE_STEP";
    case EXCEPTION_STACK_OVERFLOW:
        return L"EXCEPTION_STACK_OVERFLOW";
    default:
        return L"UNKNOWN EXCEPTION";
    }
}

void init_symbols()
{
    SymSetOptions(SYMOPT_LOAD_LINES | SYMOPT_UNDNAME);
    line.SizeOfStruct = sizeof(IMAGEHLP_LINE64);
    auto process = GetCurrentProcess();
    SymInitialize(process, NULL, TRUE);
}

void log_stack_trace(std::wstring& generalErrorDescription)
{
    memset(p_symbol, '\0', sizeof(*p_symbol) + MAX_PATH);
    memset(&module_path[0], '\0', sizeof(module_path));
    line.LineNumber = 0;

    CONTEXT context;
    RtlCaptureContext(&context);
    auto process = GetCurrentProcess();
    auto thread = GetCurrentThread();
    STACKFRAME64 stack;
    memset(&stack, 0, sizeof(STACKFRAME64));

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

    std::wstringstream ss;
    ss << generalErrorDescription << std::endl;
    for (ULONG frame = 0;; frame++)
    {
        auto result = StackWalk64(
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

        p_symbol->MaxNameLength = MAX_PATH;
        p_symbol->SizeOfStruct = sizeof(IMAGEHLP_SYMBOL64);

        DWORD64 dw64Displacement;
        SymGetSymFromAddr64(process, stack.AddrPC.Offset, &dw64Displacement, p_symbol);
        DWORD dwDisplacement;
        SymGetLineFromAddr64(process, stack.AddrPC.Offset, &dwDisplacement, &line);

        auto module_base = SymGetModuleBase64(process, stack.AddrPC.Offset);
        if (module_base)
        {
            GetModuleFileName(reinterpret_cast<HINSTANCE>(module_base), module_path, MAX_PATH);
        }
        ss << module_path << "!"
           << p_symbol->Name
           << "(" << line.FileName << ":" << line.LineNumber << ")\n";
        if (!result)
        {
            break;
        }
    }
    auto errorString = ss.str();
    MessageBoxW(NULL, errorString.c_str(), L"Unhandled Error", MB_OK | MB_ICONERROR);
}

LONG WINAPI unhandled_exception_handler(PEXCEPTION_POINTERS info)
{
    if (!processing_exception)
    {
        processing_exception = true;
        try
        {
            init_symbols();
            std::wstring ex_description = L"Exception code not available";
            if (info != NULL && info->ExceptionRecord != NULL && info->ExceptionRecord->ExceptionCode != NULL)
            {
                ex_description = exception_description(info->ExceptionRecord->ExceptionCode);
            }
            log_stack_trace(ex_description);
        }
        catch (...)
        {
        }
        if (default_top_level_exception_handler != NULL && info != NULL)
        {
            default_top_level_exception_handler(info);
        }
        processing_exception = false;
    }
    return EXCEPTION_CONTINUE_SEARCH;
}

extern "C" void AbortHandler(int /*signal_number*/)
{
    init_symbols();
    std::wstring ex_description = L"SIGABRT was raised.";
    log_stack_trace(ex_description);
}

void init_global_error_handlers()
{
    default_top_level_exception_handler = SetUnhandledExceptionFilter(unhandled_exception_handler);
    signal(SIGABRT, &AbortHandler);
}
#endif
