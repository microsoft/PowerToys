#include "pch.h"
#include "bug_report_dialog.h"
#include "Generated files/resource.h"

#include <common/utils/resources.h>
#include <common/version/version.h>

#include <ShlObj.h>
#include <shellapi.h>

#include <atomic>
#include <filesystem>
#include <mutex>
#include <string>

namespace
{
    constexpr UINT WM_BUGREPORT_DONE = WM_APP + 1;
    constexpr UINT WM_BUGREPORT_FAILED = WM_APP + 2;

    constexpr int ID_BTN_OPEN_FOLDER = 1001;
    constexpr int ID_BTN_CREATE_ISSUE = 1002;
    constexpr int ID_BTN_CLOSE = 1003;
    constexpr UINT_PTR ANIM_TIMER_ID = 1;

    const wchar_t* kWindowClass = L"PowerToysBugReportDialog";

    // Only one bug report dialog can be open at a time.
    std::atomic<HWND> g_dialogWnd{ nullptr };

    struct DialogState
    {
        UINT dpi = 96;
        HWND hStatus = nullptr; // header text (generating / done / failed)
        HWND hPath = nullptr; // read-only edit showing the .zip path
        HWND hHint = nullptr; // secondary explanatory text
        HWND hOpenFolder = nullptr;
        HWND hCreateIssue = nullptr;
        HWND hClose = nullptr;
        HFONT hFont = nullptr;
        HFONT hHeaderFont = nullptr;
        std::wstring zipPath;
        int animDots = 0;
        bool done = false;
    };

    int Scale(int value, UINT dpi)
    {
        return MulDiv(value, static_cast<int>(dpi), 96);
    }

    HFONT MakeFont(UINT dpi, bool header)
    {
        NONCLIENTMETRICSW ncm{ sizeof(ncm) };
        SystemParametersInfoW(SPI_GETNONCLIENTMETRICS, sizeof(ncm), &ncm, 0);
        LOGFONTW lf = ncm.lfMessageFont;
        const int pointSize = header ? 14 : 9;
        lf.lfHeight = -MulDiv(pointSize, static_cast<int>(dpi), 72);
        lf.lfWeight = header ? FW_SEMIBOLD : FW_NORMAL;
        return CreateFontIndirectW(&lf);
    }

    void ShowCtl(HWND ctl, bool show)
    {
        ShowWindow(ctl, show ? SW_SHOW : SW_HIDE);
    }

    std::wstring GetDesktopPath()
    {
        PWSTR raw = nullptr;
        std::wstring result;
        if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_Desktop, KF_FLAG_DEFAULT, nullptr, &raw)))
        {
            result = raw;
        }
        if (raw)
        {
            CoTaskMemFree(raw);
        }
        return result;
    }

    // The tool names its output PowerToysReport_<timestamp>.zip, so pick the most
    // recently written matching file in the destination folder.
    std::wstring FindNewestReport(const std::wstring& folder)
    {
        namespace fs = std::filesystem;
        if (folder.empty())
        {
            return {};
        }

        std::wstring best;
        fs::file_time_type bestTime{};
        std::error_code ec;
        for (const auto& entry : fs::directory_iterator(folder, ec))
        {
            if (ec)
            {
                break;
            }
            std::error_code itemEc;
            if (!entry.is_regular_file(itemEc))
            {
                continue;
            }
            const auto name = entry.path().filename().wstring();
            if (name.rfind(L"PowerToysReport_", 0) != 0 || entry.path().extension() != L".zip")
            {
                continue;
            }
            const auto writeTime = fs::last_write_time(entry, itemEc);
            if (itemEc)
            {
                continue;
            }
            if (best.empty() || writeTime > bestTime)
            {
                best = entry.path().wstring();
                bestTime = writeTime;
            }
        }
        return best;
    }

    // The dialog is created from the runner (a background process). When the
    // request comes from Settings, Settings owns the foreground, so a plain
    // SetForegroundWindow would leave the dialog hidden behind it. Use the
    // canonical AttachThreadInput recipe to reliably bring it to the front.
    void ForceForeground(HWND hwnd)
    {
        const HWND foreground = GetForegroundWindow();
        const DWORD targetThread = GetWindowThreadProcessId(hwnd, nullptr);
        const DWORD foregroundThread = foreground ? GetWindowThreadProcessId(foreground, nullptr) : 0;
        const bool attach = foregroundThread != 0 && foregroundThread != targetThread;
        if (attach)
        {
            AttachThreadInput(foregroundThread, targetThread, TRUE);
        }
        BringWindowToTop(hwnd);
        SetForegroundWindow(hwnd);
        SetActiveWindow(hwnd);
        if (attach)
        {
            AttachThreadInput(foregroundThread, targetThread, FALSE);
        }
    }

    void RevealZip(const std::wstring& path)    {
        if (path.empty())
        {
            return;
        }
        const std::wstring args = L"/select,\"" + path + L"\"";
        ShellExecuteW(nullptr, L"open", L"explorer.exe", args.c_str(), nullptr, SW_SHOWNORMAL);
    }

    void OpenIssuePage(const std::wstring& zipPath)
    {
        const std::wstring url =
            L"https://github.com/microsoft/PowerToys/issues/new?template=bug_report.yml&labels=Issue-Bug%2CTriage-Needed&version=" +
            get_product_version();
        ShellExecuteW(nullptr, L"open", url.c_str(), nullptr, nullptr, SW_SHOWNORMAL);

        // Reveal the .zip so the user can drag it into the freshly opened issue page.
        RevealZip(zipPath);
    }

    HWND CreateButton(HWND parent, int id, const std::wstring& text, int x, int y, int w, int h, HFONT font, HINSTANCE inst, bool visible)
    {
        HWND btn = CreateWindowExW(0,
                                   L"BUTTON",
                                   text.c_str(),
                                   WS_CHILD | WS_TABSTOP | BS_PUSHBUTTON | (visible ? WS_VISIBLE : 0),
                                   x,
                                   y,
                                   w,
                                   h,
                                   parent,
                                   reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)),
                                   inst,
                                   nullptr);
        SendMessageW(btn, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        return btn;
    }

    void CreateControls(HWND hwnd, DialogState* st)
    {
        const HINSTANCE inst = reinterpret_cast<HINSTANCE>(&__ImageBase);
        const UINT dpi = st->dpi;

        RECT cr{};
        GetClientRect(hwnd, &cr);
        const int margin = Scale(20, dpi);
        const int contentW = cr.right - 2 * margin;
        int y = margin;

        st->hStatus = CreateWindowExW(0, L"STATIC", L"", WS_CHILD | WS_VISIBLE | SS_LEFT | SS_NOPREFIX, margin, y, contentW, Scale(58, dpi), hwnd, nullptr, inst, nullptr);
        SendMessageW(st->hStatus, WM_SETFONT, reinterpret_cast<WPARAM>(st->hHeaderFont), TRUE);
        y += Scale(64, dpi);

        st->hPath = CreateWindowExW(0, L"EDIT", L"", WS_CHILD | ES_READONLY | ES_AUTOHSCROLL | WS_TABSTOP, margin, y, contentW, Scale(24, dpi), hwnd, nullptr, inst, nullptr);
        SendMessageW(st->hPath, WM_SETFONT, reinterpret_cast<WPARAM>(st->hFont), TRUE);
        ShowCtl(st->hPath, false);
        y += Scale(34, dpi);

        st->hHint = CreateWindowExW(0, L"STATIC", GET_RESOURCE_STRING(IDS_BUGREPORT_GENERATING_HINT).c_str(), WS_CHILD | WS_VISIBLE | SS_LEFT | SS_NOPREFIX, margin, y, contentW, Scale(48, dpi), hwnd, nullptr, inst, nullptr);
        SendMessageW(st->hHint, WM_SETFONT, reinterpret_cast<WPARAM>(st->hFont), TRUE);

        const int btnH = Scale(30, dpi);
        const int btnY = cr.bottom - margin - btnH;
        const int gap = Scale(10, dpi);
        const int wOpen = Scale(110, dpi);
        const int wIssue = Scale(150, dpi);
        const int wClose = Scale(90, dpi);

        st->hOpenFolder = CreateButton(hwnd, ID_BTN_OPEN_FOLDER, GET_RESOURCE_STRING(IDS_BUGREPORT_OPEN_FOLDER), margin, btnY, wOpen, btnH, st->hFont, inst, false);
        st->hCreateIssue = CreateButton(hwnd, ID_BTN_CREATE_ISSUE, GET_RESOURCE_STRING(IDS_BUGREPORT_CREATE_ISSUE), margin + wOpen + gap, btnY, wIssue, btnH, st->hFont, inst, false);
        st->hClose = CreateButton(hwnd, ID_BTN_CLOSE, GET_RESOURCE_STRING(IDS_BUGREPORT_CLOSE), cr.right - margin - wClose, btnY, wClose, btnH, st->hFont, inst, true);
    }

    void SetDoneState(DialogState* st)
    {
        st->done = true;
        SetWindowTextW(st->hStatus, GET_RESOURCE_STRING(IDS_BUGREPORT_DONE_HEADER).c_str());
        SetWindowTextW(st->hPath, st->zipPath.c_str());
        SetWindowTextW(st->hHint, GET_RESOURCE_STRING(IDS_BUGREPORT_DONE_HINT).c_str());
        ShowCtl(st->hPath, true);
        ShowCtl(st->hOpenFolder, true);
        ShowCtl(st->hCreateIssue, true);
        SetFocus(st->hCreateIssue);
    }

    void SetFailedState(DialogState* st)
    {
        st->done = true;
        SetWindowTextW(st->hStatus, GET_RESOURCE_STRING(IDS_BUGREPORT_FAILED).c_str());
        ShowCtl(st->hPath, false);
        ShowCtl(st->hHint, false);
        ShowCtl(st->hOpenFolder, false);
        ShowCtl(st->hCreateIssue, false);
        SetFocus(st->hClose);
    }

    LRESULT CALLBACK DlgProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
    {
        DialogState* st = reinterpret_cast<DialogState*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
        switch (msg)
        {
        case WM_CREATE:
        {
            auto* cs = reinterpret_cast<CREATESTRUCTW*>(lParam);
            st = static_cast<DialogState*>(cs->lpCreateParams);
            SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(st));
            CreateControls(hwnd, st);
            SetWindowTextW(st->hStatus, GET_RESOURCE_STRING(IDS_BUGREPORT_GENERATING).c_str());
            SetTimer(hwnd, ANIM_TIMER_ID, 400, nullptr);
            return 0;
        }
        case WM_TIMER:
            if (st && wParam == ANIM_TIMER_ID && !st->done)
            {
                st->animDots = (st->animDots + 1) % 4;
                std::wstring text = GET_RESOURCE_STRING(IDS_BUGREPORT_GENERATING);
                text.append(static_cast<size_t>(st->animDots), L'.');
                SetWindowTextW(st->hStatus, text.c_str());
            }
            return 0;
        case WM_BUGREPORT_DONE:
            if (st)
            {
                KillTimer(hwnd, ANIM_TIMER_ID);
                SetDoneState(st);
            }
            return 0;
        case WM_BUGREPORT_FAILED:
            if (st)
            {
                KillTimer(hwnd, ANIM_TIMER_ID);
                SetFailedState(st);
            }
            return 0;
        case WM_CTLCOLORSTATIC:
            SetBkColor(reinterpret_cast<HDC>(wParam), GetSysColor(COLOR_WINDOW));
            SetTextColor(reinterpret_cast<HDC>(wParam), GetSysColor(COLOR_WINDOWTEXT));
            return reinterpret_cast<LRESULT>(GetSysColorBrush(COLOR_WINDOW));
        case WM_COMMAND:
            if (st)
            {
                switch (LOWORD(wParam))
                {
                case ID_BTN_OPEN_FOLDER:
                    RevealZip(st->zipPath);
                    return 0;
                case ID_BTN_CREATE_ISSUE:
                    OpenIssuePage(st->zipPath);
                    return 0;
                case ID_BTN_CLOSE:
                    DestroyWindow(hwnd);
                    return 0;
                }
            }
            return 0;
        case WM_CLOSE:
            DestroyWindow(hwnd);
            return 0;
        case WM_DESTROY:
            PostQuitMessage(0);
            return 0;
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    void RegisterDialogClass()
    {
        static std::once_flag flag;
        std::call_once(flag, []() {
            WNDCLASSEXW wc{ sizeof(wc) };
            wc.lpfnWndProc = DlgProc;
            wc.hInstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
            wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
            wc.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
            wc.lpszClassName = kWindowClass;
            wc.hIcon = LoadIconW(reinterpret_cast<HINSTANCE>(&__ImageBase), MAKEINTRESOURCEW(APPICON));
            RegisterClassExW(&wc);
        });
    }
}

void run_bug_report_dialog(const std::wstring& toolPath, const std::function<void()>& onProcessFinished)
{
    // If a result window from a previous run is still open, just focus it
    // instead of starting a second report.
    if (HWND existing = g_dialogWnd.load())
    {
        if (IsWindow(existing))
        {
            ForceForeground(existing);
            if (onProcessFinished)
            {
                onProcessFinished();
            }
            return;
        }

        g_dialogWnd.store(nullptr);
    }

    RegisterDialogClass();

    DialogState st;
    st.dpi = GetDpiForSystem();
    st.hFont = MakeFont(st.dpi, false);
    st.hHeaderFont = MakeFont(st.dpi, true);

    const DWORD style = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU;
    const DWORD exStyle = WS_EX_DLGMODALFRAME | WS_EX_APPWINDOW;

    RECT rc{ 0, 0, Scale(480, st.dpi), Scale(270, st.dpi) };
    AdjustWindowRectExForDpi(&rc, style, FALSE, exStyle, st.dpi);
    const int w = rc.right - rc.left;
    const int h = rc.bottom - rc.top;

    RECT work{};
    SystemParametersInfoW(SPI_GETWORKAREA, 0, &work, 0);
    const int x = work.left + ((work.right - work.left) - w) / 2;
    const int y = work.top + ((work.bottom - work.top) - h) / 2;

    HWND hwnd = CreateWindowExW(exStyle,
                                kWindowClass,
                                GET_RESOURCE_STRING(IDS_BUGREPORT_DIALOG_TITLE).c_str(),
                                style,
                                x,
                                y,
                                w,
                                h,
                                nullptr,
                                nullptr,
                                reinterpret_cast<HINSTANCE>(&__ImageBase),
                                &st);

    bool finishedNotified = false;
    auto notifyFinished = [&]() {
        if (!finishedNotified)
        {
            finishedNotified = true;
            if (onProcessFinished)
            {
                onProcessFinished();
            }
        }
    };

    if (!hwnd)
    {
        // Fall back to running the tool without UI so a report is still produced.
        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NO_CONSOLE;
        sei.lpFile = toolPath.c_str();
        sei.nShow = SW_HIDE;
        if (ShellExecuteExW(&sei) && sei.hProcess)
        {
            WaitForSingleObject(sei.hProcess, INFINITE);
            CloseHandle(sei.hProcess);
        }
        notifyFinished();
        if (st.hFont)
        {
            DeleteObject(st.hFont);
        }
        if (st.hHeaderFont)
        {
            DeleteObject(st.hHeaderFont);
        }
        return;
    }

    g_dialogWnd.store(hwnd);
    ShowWindow(hwnd, SW_SHOW);
    ForceForeground(hwnd);

    const std::wstring desktop = GetDesktopPath();

    STARTUPINFOW si{ sizeof(si) };
    PROCESS_INFORMATION pi{};
    std::wstring cmd = L"\"" + toolPath + L"\"";
    const BOOL started = CreateProcessW(nullptr, cmd.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi);

    bool processHandled = !started;
    if (!started)
    {
        PostMessageW(hwnd, WM_BUGREPORT_FAILED, 0, 0);
        notifyFinished();
    }

    MSG msg;
    bool running = true;
    while (running)
    {
        const DWORD count = processHandled ? 0 : 1;
        HANDLE handles[1] = { pi.hProcess };
        const DWORD result = MsgWaitForMultipleObjects(count, count ? handles : nullptr, FALSE, INFINITE, QS_ALLINPUT);

        if (count == 1 && result == WAIT_OBJECT_0)
        {
            DWORD exitCode = 1;
            GetExitCodeProcess(pi.hProcess, &exitCode);
            processHandled = true;
            CloseHandle(pi.hProcess);
            CloseHandle(pi.hThread);
            pi.hProcess = nullptr;
            pi.hThread = nullptr;

            const std::wstring zip = (exitCode == 0) ? FindNewestReport(desktop) : std::wstring{};
            if (exitCode == 0 && !zip.empty())
            {
                st.zipPath = zip;
                PostMessageW(hwnd, WM_BUGREPORT_DONE, 0, 0);
            }
            else
            {
                PostMessageW(hwnd, WM_BUGREPORT_FAILED, 0, 0);
            }
            notifyFinished();
            continue;
        }

        while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE))
        {
            if (msg.message == WM_QUIT)
            {
                running = false;
                break;
            }
            if (!IsDialogMessageW(hwnd, &msg))
            {
                TranslateMessage(&msg);
                DispatchMessageW(&msg);
            }
        }
    }

    g_dialogWnd.store(nullptr);

    // If the window was closed before the tool finished, keep waiting for the tool
    // so we only clear the "running" state once the process actually exits.
    if (pi.hProcess)
    {
        WaitForSingleObject(pi.hProcess, INFINITE);
        CloseHandle(pi.hProcess);
        pi.hProcess = nullptr;
    }
    if (pi.hThread)
    {
        CloseHandle(pi.hThread);
        pi.hThread = nullptr;
    }
    notifyFinished();
    if (st.hFont)
    {
        DeleteObject(st.hFont);
    }
    if (st.hHeaderFont)
    {
        DeleteObject(st.hHeaderFont);
    }
}
