// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "WindowTabs.h"
#include "GroupModel.h"
#include "Session.h"
#include <commctrl.h>
#include <dwmapi.h>
#include <windowsx.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/logger/logger.h>
#include <filesystem>
#include <fstream>
#include <future>
#include <map>
#include <memory>
#include <optional>
#include <set>

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace WindowTabs
{
    namespace
    {
        constexpr UINT EventMessage = WM_APP + 1;
        constexpr UINT CycleMessage = WM_APP + 2;
        constexpr UINT TransferMessage = WM_APP + 3;
        constexpr wchar_t WindowProperty[] = L"PowerToys.WindowTabs.Member";
        constexpr wchar_t DispatcherClass[] = L"PowerToys.WindowTabs.Dispatcher";
        constexpr wchar_t StripClass[] = L"PowerToys.WindowTabs.Strip";
        thread_local HWND dispatcher = nullptr;

        HWND Handle(WindowId id)
        {
            return reinterpret_cast<HWND>(id);
        }
        WindowId Id(HWND window)
        {
            return reinterpret_cast<WindowId>(window);
        }

        struct Member
        {
            HWND window = nullptr;
            DWORD process = 0;
            WINDOWPLACEMENT placement{ sizeof(WINDOWPLACEMENT) };
            SavedWindow identity;

            bool Alive() const
            {
                DWORD current = 0;
                GetWindowThreadProcessId(window, &current);
                return IsWindow(window) && current == process && GetPropW(window, WindowProperty) == this;
            }

            void Release() const
            {
                if (Alive())
                {
                    RemovePropW(window, WindowProperty);
                    auto original = placement;
                    original.flags |= WPF_ASYNCWINDOWPLACEMENT;
                    SetWindowPlacement(window, &original);
                }
            }
        };

        std::optional<SavedWindow> Describe(HWND window)
        {
            DWORD process = 0;
            GetWindowThreadProcessId(window, &process);
            const auto style = GetWindowLongPtrW(window, GWL_STYLE);
            const auto extended = GetWindowLongPtrW(window, GWL_EXSTYLE);
            DWORD cloaked = 0;
            if (!IsWindow(window) || !IsWindowVisible(window) || !IsWindowEnabled(window) || process == GetCurrentProcessId() ||
                GetAncestor(window, GA_ROOT) != window || GetWindow(window, GW_OWNER) ||
                (style & WS_CHILD) || !(style & WS_THICKFRAME) || !(style & WS_CAPTION) ||
                (extended & (WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST)) || IsHungAppWindow(window) ||
                FAILED(DwmGetWindowAttribute(window, DWMWA_CLOAKED, &cloaked, sizeof(cloaked))) || cloaked)
            {
                return std::nullopt;
            }
            HANDLE handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, process);
            if (!handle)
            {
                return std::nullopt;
            }
            // Fail closed for elevated/protected processes, even when Runner is elevated.
            HANDLE token = nullptr;
            TOKEN_ELEVATION elevation{};
            DWORD returned = 0;
            bool allowed = false;
            if (OpenProcessToken(handle, TOKEN_QUERY, &token))
            {
                allowed = GetTokenInformation(token, TokenElevation, &elevation, sizeof(elevation), &returned) && !elevation.TokenIsElevated;
                CloseHandle(token);
            }
            wchar_t executable[32768]{};
            DWORD length = ARRAYSIZE(executable);
            allowed = QueryFullProcessImageNameW(handle, 0, executable, &length) && allowed;
            CloseHandle(handle);
            wchar_t title[4097]{};
            wchar_t windowClass[257]{};
            if (!allowed || !GetWindowTextW(window, title, ARRAYSIZE(title)) || !GetClassNameW(window, windowClass, ARRAYSIZE(windowClass)))
            {
                return std::nullopt;
            }
            RECT rect{};
            MONITORINFO monitor{ sizeof(MONITORINFO) };
            GetWindowRect(window, &rect);
            GetMonitorInfoW(MonitorFromWindow(window, MONITOR_DEFAULTTONEAREST), &monitor);
            // Borderless/fullscreen windows cannot accommodate a separate tab strip.
            if (!IsIconic(window) && rect.left <= monitor.rcMonitor.left && rect.top <= monitor.rcMonitor.top && rect.right >= monitor.rcMonitor.right && rect.bottom >= monitor.rcMonitor.bottom)
            {
                return std::nullopt;
            }
            return SavedWindow{ executable, windowClass, title };
        }

        struct Runtime;
        struct TabGroup
        {
            Runtime* runtime;
            Group model;
            HWND strip = nullptr;
            HWND tabs = nullptr;
            RECT bounds{};
            WindowId pressed = 0;
            POINT pressPoint{};
            bool dragging = false;
            std::vector<std::wstring> labels;
            HFONT font = nullptr;
            UINT dpi = 0;
            ~TabGroup()
            {
                if (strip)
                    DestroyWindow(strip);
                if (font)
                    DeleteObject(font);
            }
        };

        struct Runtime
        {
            HINSTANCE instance;
            std::atomic<bool>& restore;
            std::map<WindowId, std::unique_ptr<Member>> members;
            std::vector<std::unique_ptr<TabGroup>> groups;
            std::vector<SavedGroup> pending;
            std::filesystem::path sessionPath;
            HWND moving = nullptr;
            bool shiftDrag = false;
            bool dirty = false;
            bool previousRestore = false;
            bool endingSession = false;

            TabGroup* Find(WindowId id)
            {
                for (const auto& group : groups)
                {
                    if (group->model.Contains(id))
                        return group.get();
                }
                return nullptr;
            }

            bool Capture(HWND window)
            {
                if (members.contains(Id(window)))
                    return members.at(Id(window))->Alive();
                const auto description = Describe(window);
                if (!description || GetPropW(window, WindowProperty))
                    return false;
                auto member = std::make_unique<Member>();
                member->window = window;
                member->identity = *description;
                GetWindowThreadProcessId(window, &member->process);
                if (!GetWindowPlacement(window, &member->placement) || !SetPropW(window, WindowProperty, member.get()))
                    return false;
                members.emplace(Id(window), std::move(member));
                return true;
            }

            void Refresh(TabGroup& group)
            {
                std::vector<std::wstring> labels;
                for (const auto id : group.model.tabs)
                {
                    wchar_t text[4097]{};
                    GetWindowTextW(Handle(id), text, ARRAYSIZE(text));
                    labels.emplace_back(text);
                }
                if (labels != group.labels)
                {
                    dirty = true;
                    TabCtrl_DeleteAllItems(group.tabs);
                    for (size_t index = 0; index < labels.size(); ++index)
                    {
                        TCITEMW item{};
                        item.mask = TCIF_TEXT;
                        item.pszText = labels[index].data();
                        TabCtrl_InsertItem(group.tabs, static_cast<int>(index), &item);
                    }
                    group.labels = std::move(labels);
                }
                auto it = std::find(group.model.tabs.begin(), group.model.tabs.end(), group.model.active);
                TabCtrl_SetCurSel(group.tabs, static_cast<int>(it - group.model.tabs.begin()));
            }

            void Position(TabGroup& group)
            {
                HWND active = Handle(group.model.active);
                DWORD cloaked = 0;
                DwmGetWindowAttribute(active, DWMWA_CLOAKED, &cloaked, sizeof(cloaked));
                if (!IsWindow(active) || IsIconic(active) || !IsWindowVisible(active) || cloaked)
                {
                    ShowWindow(group.strip, SW_HIDE);
                    return;
                }
                RECT rect{};
                if (!GetWindowRect(active, &rect))
                    return;
                group.bounds = rect;
                const auto dpi = GetDpiForWindow(active);
                const int height = MulDiv(36, static_cast<int>(dpi), 96);
                if (group.dpi != dpi)
                {
                    NONCLIENTMETRICSW metrics{ sizeof(NONCLIENTMETRICSW) };
                    if (SystemParametersInfoForDpi(SPI_GETNONCLIENTMETRICS, sizeof(metrics), &metrics, 0, dpi))
                    {
                        if (auto font = CreateFontIndirectW(&metrics.lfMessageFont))
                        {
                            SendMessageW(group.tabs, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
                            if (group.font)
                                DeleteObject(group.font);
                            group.font = font;
                        }
                    }
                    group.dpi = dpi;
                }
                MONITORINFO info{ sizeof(MONITORINFO) };
                GetMonitorInfoW(MonitorFromWindow(active, MONITOR_DEFAULTTONEAREST), &info);
                // When maximized, use the titlebar area instead of going off-screen.
                const int top = std::max(info.rcWork.top, rect.top - height);
                // An unowned strip survives an application closing its selected
                // window. Keep it immediately above the active window in z-order.
                HWND previous = GetWindow(active, GW_HWNDPREV);
                if (previous == group.strip)
                    previous = GetWindow(group.strip, GW_HWNDPREV);
                SetWindowPos(group.strip, previous ? previous : HWND_TOP, rect.left, top, rect.right - rect.left, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
                SetWindowPos(group.tabs, nullptr, 0, 0, rect.right - rect.left, height, SWP_NOACTIVATE | SWP_NOZORDER);
                Refresh(group);
            }

            void Select(TabGroup& group, WindowId id, bool focus = true)
            {
                if (!members.contains(id) || !members.at(id)->Alive() || !IsWindowEnabled(Handle(group.model.active)) || !group.model.Select(id))
                    return;
                const HWND active = Handle(id);
                ShowWindowAsync(active, SW_RESTORE);
                SetWindowPos(active, nullptr, group.bounds.left, group.bounds.top, group.bounds.right - group.bounds.left, group.bounds.bottom - group.bounds.top, SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);
                for (const auto other : group.model.tabs)
                {
                    if (other != id && members.at(other)->Alive())
                        ShowWindowAsync(Handle(other), SW_MINIMIZE);
                }
                if (focus)
                    SetForegroundWindow(active);
                Position(group);
                dirty = true;
            }

            void Detach(WindowId id, bool restorePlacement = true)
            {
                if (auto group = Find(id))
                {
                    group->model.Remove(id);
                    if (restorePlacement && members.contains(id))
                        members.at(id)->Release();
                    else if (members.contains(id) && members.at(id)->Alive())
                        RemovePropW(Handle(id), WindowProperty);
                    members.erase(id);
                    if (!group->model.tabs.empty())
                        Select(*group, group->model.active, false);
                    dirty = true;
                }
            }

            void Cleanup()
            {
                for (auto it = groups.begin(); it != groups.end();)
                {
                    auto& group = **it;
                    if (group.model.tabs.size() < 2)
                    {
                        for (auto id : group.model.tabs)
                        {
                            members.at(id)->Release();
                            members.erase(id);
                        }
                        it = groups.erase(it);
                    }
                    else
                        ++it;
                }
            }

            bool Join(HWND source, HWND target);
            void Tick();
            void Restore();
            void Save();
            static LRESULT CALLBACK StripProc(HWND, UINT, WPARAM, LPARAM);
            static LRESULT CALLBACK TabsProc(HWND, UINT, WPARAM, LPARAM, UINT_PTR, DWORD_PTR);
            static LRESULT StripProcImpl(HWND, UINT, WPARAM, LPARAM);
            static LRESULT TabsProcImpl(HWND, UINT, WPARAM, LPARAM, DWORD_PTR);
        };

        bool Runtime::Join(HWND source, HWND target)
        {
            if (source == target || !Describe(source) || !Describe(target))
                return false;
            auto destination = Find(Id(target));
            if (!destination && groups.size() >= 64)
                return false;
            if (destination && (destination->model.Contains(Id(source)) || destination->model.tabs.size() >= 32))
                return false;
            const bool capturedTarget = members.contains(Id(target));
            if (!Capture(target))
                return false;
            if (!Capture(source))
            {
                if (!capturedTarget)
                {
                    members.at(Id(target))->Release();
                    members.erase(Id(target));
                }
                return false;
            }
            if (!destination)
            {
                auto group = std::make_unique<TabGroup>();
                group->runtime = this;
                group->model.Add(Id(target));
                GetWindowRect(target, &group->bounds);
                group->strip = CreateWindowExW(WS_EX_TOOLWINDOW, StripClass, L"Window Tabs", WS_POPUP | WS_CLIPCHILDREN, 0, 0, 0, 0, nullptr, nullptr, instance, group.get());
                if (!group->strip)
                {
                    if (!capturedTarget)
                    {
                        members.at(Id(target))->Release();
                        members.erase(Id(target));
                    }
                    if (!Find(Id(source)))
                    {
                        members.at(Id(source))->Release();
                        members.erase(Id(source));
                    }
                    return false;
                }
                group->tabs = CreateWindowExW(0, WC_TABCONTROLW, L"Window Tabs", WS_CHILD | WS_VISIBLE | WS_TABSTOP | TCS_FOCUSONBUTTONDOWN | TCS_TOOLTIPS, 0, 0, 0, 0, group->strip, nullptr, instance, nullptr);
                if (!group->tabs)
                {
                    if (!capturedTarget)
                    {
                        members.at(Id(target))->Release();
                        members.erase(Id(target));
                    }
                    if (!Find(Id(source)))
                    {
                        members.at(Id(source))->Release();
                        members.erase(Id(source));
                    }
                    return false;
                }
                SetPropW(group->strip, WindowProperty, group.get());
                SendMessageW(group->tabs, WM_SETFONT, reinterpret_cast<WPARAM>(GetStockObject(DEFAULT_GUI_FONT)), TRUE);
                SetWindowSubclass(group->tabs, TabsProc, 1, reinterpret_cast<DWORD_PTR>(group.get()));
                destination = group.get();
                groups.push_back(std::move(group));
            }
            if (auto old = Find(Id(source)))
            {
                old->model.Remove(Id(source));
                if (old->model.tabs.size() >= 2)
                    Select(*old, old->model.active, false);
            }
            destination->model.Add(Id(source));
            Select(*destination, Id(source));
            Cleanup();
            return true;
        }

        LRESULT CALLBACK Runtime::StripProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam)
        {
            try
            {
                if (runtime && message == WM_QUERYENDSESSION)
                {
                    runtime->Save();
                    runtime->endingSession = true;
                    KillTimer(window, 1);
                    return TRUE;
                }
                if (runtime && message == WM_ENDSESSION)
                {
                    if (wparam)
                        PostMessageW(window, WM_CLOSE, 0, 0);
                    else
                    {
                        runtime->endingSession = false;
                        SetTimer(window, 1, 500, nullptr);
                    }
                    return 0;
                }
                return StripProcImpl(window, message, wparam, lparam);
            }
            catch (...)
            {
                PostMessageW(dispatcher, WM_CLOSE, 0, 0);
                return 0;
            }
        }

        LRESULT Runtime::StripProcImpl(HWND window, UINT message, WPARAM wparam, LPARAM lparam)
        {
            auto group = reinterpret_cast<TabGroup*>(GetWindowLongPtrW(window, GWLP_USERDATA));
            if (message == WM_NCCREATE)
            {
                group = static_cast<TabGroup*>(reinterpret_cast<CREATESTRUCTW*>(lparam)->lpCreateParams);
                SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(group));
            }
            if (group && message == WM_NOTIFY && reinterpret_cast<NMHDR*>(lparam)->code == TCN_SELCHANGE)
            {
                const int index = TabCtrl_GetCurSel(group->tabs);
                if (index >= 0 && static_cast<size_t>(index) < group->model.tabs.size())
                    group->runtime->Select(*group, group->model.tabs[index]);
                return 0;
            }
            if (message == WM_CLOSE)
                return 0; // Closing a strip must never close its applications.
            return DefWindowProcW(window, message, wparam, lparam);
        }

        LRESULT CALLBACK Runtime::TabsProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam, UINT_PTR, DWORD_PTR data)
        {
            try
            {
                return TabsProcImpl(window, message, wparam, lparam, data);
            }
            catch (...)
            {
                PostMessageW(dispatcher, WM_CLOSE, 0, 0);
                return 0;
            }
        }

        LRESULT Runtime::TabsProcImpl(HWND window, UINT message, WPARAM wparam, LPARAM lparam, DWORD_PTR data)
        {
            auto& group = *reinterpret_cast<TabGroup*>(data);
            auto& runtime = *group.runtime;
            TCHITTESTINFO hit{};
            hit.pt = { GET_X_LPARAM(lparam), GET_Y_LPARAM(lparam) };
            const int index = TabCtrl_HitTest(window, &hit);
            if (message == WM_LBUTTONDOWN && index >= 0)
            {
                group.pressed = group.model.tabs[index];
                group.pressPoint = hit.pt;
                SetCapture(window);
            }
            if (message == WM_MOUSEMOVE && group.pressed)
            {
                group.dragging = group.dragging || abs(hit.pt.x - group.pressPoint.x) > GetSystemMetrics(SM_CXDRAG) || abs(hit.pt.y - group.pressPoint.y) > GetSystemMetrics(SM_CYDRAG);
            }
            if (message == WM_CAPTURECHANGED)
            {
                group.pressed = 0;
                group.dragging = false;
            }
            if (message == WM_LBUTTONUP && group.pressed)
            {
                const auto pressed = group.pressed;
                const bool dragging = group.dragging;
                group.pressed = 0;
                group.dragging = false;
                ReleaseCapture();
                if (dragging)
                {
                    RECT rect{};
                    GetClientRect(window, &rect);
                    if (PtInRect(&rect, hit.pt) && index >= 0)
                        group.model.Reorder(pressed, static_cast<size_t>(index));
                    else
                    {
                        auto point = hit.pt;
                        ClientToScreen(window, &point);
                        for (const auto& other : runtime.groups)
                        {
                            RECT destination{};
                            GetWindowRect(other->strip, &destination);
                            if (other.get() != &group && PtInRect(&destination, point))
                            {
                                // Join can destroy the source strip. Defer until
                                // this common-control callback has returned.
                                PostMessageW(dispatcher, TransferMessage, pressed, static_cast<LPARAM>(other->model.active));
                                return 0;
                            }
                        }
                        runtime.Detach(pressed);
                    }
                    runtime.Refresh(group);
                    runtime.dirty = true;
                    return 0;
                }
            }
            if (message == WM_KEYDOWN)
            {
                if (wparam == VK_TAB && GetKeyState(VK_CONTROL) < 0)
                {
                    runtime.Select(group, group.model.Cycle(GetKeyState(VK_SHIFT) >= 0), false);
                    return 0;
                }
                if (wparam == VK_DELETE)
                {
                    if (GetKeyState(VK_SHIFT) < 0)
                        runtime.Detach(group.model.active);
                    else
                        PostMessageW(Handle(group.model.active), WM_CLOSE, 0, 0);
                    return 0;
                }
            }
            if (message == WM_MBUTTONUP && index >= 0)
            {
                PostMessageW(Handle(group.model.tabs[index]), WM_CLOSE, 0, 0);
                return 0;
            }
            if (message == WM_CONTEXTMENU)
            {
                POINT point{ GET_X_LPARAM(lparam), GET_Y_LPARAM(lparam) };
                auto id = group.model.active;
                if (point.x != -1 || point.y != -1)
                {
                    TCHITTESTINFO contextHit{};
                    contextHit.pt = point;
                    ScreenToClient(window, &contextHit.pt);
                    const int contextIndex = TabCtrl_HitTest(window, &contextHit);
                    if (contextIndex >= 0)
                        id = group.model.tabs[contextIndex];
                }
                if (point.x == -1 && point.y == -1)
                    GetCursorPos(&point);
                HMENU menu = CreatePopupMenu();
                wchar_t label[256]{};
                const auto instance = reinterpret_cast<HINSTANCE>(&__ImageBase);
                for (UINT command = 1; command <= 3; ++command)
                {
                    LoadStringW(instance, 100 + command, label, ARRAYSIZE(label));
                    AppendMenuW(menu, MF_STRING, command, label);
                }
                const auto command = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, point.x, point.y, 0, group.strip, nullptr);
                DestroyMenu(menu);
                if (command == 1)
                    runtime.Detach(id);
                if (command == 2)
                    PostMessageW(Handle(id), WM_CLOSE, 0, 0);
                if (command == 3)
                {
                    const auto ids = group.model.tabs;
                    for (auto member : ids)
                        runtime.Detach(member);
                }
                return 0;
            }
            return DefSubclassProc(window, message, wparam, lparam);
        }

        std::vector<HWND> Candidates()
        {
            std::vector<HWND> windows;
            EnumWindows([](HWND window, LPARAM value) -> BOOL {
                if (Describe(window))
                    reinterpret_cast<std::vector<HWND>*>(value)->push_back(window);
                return TRUE;
            },
                        reinterpret_cast<LPARAM>(&windows));
            return windows;
        }

        void Runtime::Restore()
        {
            if (pending.empty())
                return;
            std::vector<std::pair<WindowId, SavedWindow>> candidates;
            for (auto window : Candidates())
            {
                if (!Find(Id(window)))
                {
                    if (auto description = Describe(window))
                        candidates.emplace_back(Id(window), *description);
                }
            }
            for (auto it = pending.begin(); it != pending.end();)
            {
                std::erase_if(candidates, [this](const auto& candidate) { return Find(candidate.first) != nullptr; });
                const auto ids = MatchGroup(*it, candidates);
                std::vector<HWND> matched;
                for (auto id : ids)
                    matched.push_back(Handle(id));
                if (matched.size() != it->tabs.size())
                {
                    ++it;
                    continue;
                }
                bool success = Join(matched[1], matched[0]);
                for (size_t index = 2; success && index < matched.size(); ++index)
                    success = Join(matched[index], matched[0]);
                if (success)
                {
                    auto group = Find(Id(matched[0]));
                    Select(*group, Id(matched[it->selected]), false);
                    it = pending.erase(it);
                }
                else
                {
                    for (auto window : matched)
                        Detach(Id(window));
                    Cleanup();
                    ++it;
                }
            }
        }

        void Runtime::Save()
        {
            if (!restore)
                return;
            std::vector<SavedGroup> saved = pending;
            for (const auto& group : groups)
            {
                if (group->model.tabs.size() < 2)
                    continue;
                SavedGroup item;
                for (auto id : group->model.tabs)
                {
                    if (id == group->model.active)
                        item.selected = item.tabs.size();
                    auto identity = members.at(id)->identity;
                    wchar_t title[4097]{};
                    if (GetWindowTextW(Handle(id), title, ARRAYSIZE(title)))
                        identity.title = title;
                    item.tabs.push_back(std::move(identity));
                }
                // Identical application/class/title tuples cannot be restored
                // unambiguously. Do not let one such group invalidate the file.
                auto candidate = saved;
                candidate.push_back(item);
                if (candidate.size() <= 64 && !ParseSession(SerializeSession(candidate)).empty())
                    saved.push_back(std::move(item));
            }
            try
            {
                std::filesystem::create_directories(sessionPath.parent_path());
                auto temporary = sessionPath;
                temporary += L".tmp";
                const auto text = winrt::to_string(SerializeSession(saved));
                if (text.size() > 1024 * 1024)
                    return;
                std::ofstream stream(temporary, std::ios::binary | std::ios::trunc);
                stream << text;
                stream.close();
                if (!stream || !MoveFileExW(temporary.c_str(), sessionPath.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
                    Logger::warn("Window Tabs could not save the session.");
                else
                    dirty = false;
            }
            catch (const std::exception&)
            {
                Logger::warn("Window Tabs could not save the session.");
            }
        }

        void Runtime::Tick()
        {
            if (previousRestore != restore)
            {
                previousRestore = restore;
                if (!previousRestore)
                {
                    pending.clear();
                    DeleteFileW(sessionPath.c_str());
                }
                else
                    dirty = true;
            }
            std::vector<WindowId> removed;
            for (const auto& [id, member] : members)
            {
                // A modal dialog temporarily disables its owner. Release the entire
                // window rather than minimize it underneath an unsaved-work prompt.
                if (!member->Alive() || !IsWindowEnabled(member->window) || IsHungAppWindow(member->window))
                    removed.push_back(id);
            }
            for (auto id : removed)
                Detach(id);
            Cleanup();
            for (const auto& group : groups)
                Position(*group);
            if (restore)
                Restore();
            if (dirty)
                Save();
        }

        void CALLBACK EventHook(HWINEVENTHOOK, DWORD event, HWND window, LONG object, LONG child, DWORD, DWORD)
        {
            if (dispatcher && window && object == OBJID_WINDOW && child == 0)
                PostMessageW(dispatcher, EventMessage, event, reinterpret_cast<LPARAM>(window));
        }

        LRESULT CALLBACK DispatcherProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam)
        {
            auto runtime = reinterpret_cast<Runtime*>(GetWindowLongPtrW(window, GWLP_USERDATA));
            if (message == WM_NCCREATE)
            {
                runtime = static_cast<Runtime*>(reinterpret_cast<CREATESTRUCTW*>(lparam)->lpCreateParams);
                SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(runtime));
            }
            try
            {
                if (runtime && message == WM_TIMER)
                {
                    runtime->Tick();
                    return 0;
                }
                if (runtime && message == TransferMessage)
                {
                    runtime->Join(Handle(static_cast<WindowId>(wparam)), Handle(static_cast<WindowId>(lparam)));
                    return 0;
                }
                if (runtime && message == CycleMessage)
                {
                    auto group = runtime->Find(Id(reinterpret_cast<HWND>(lparam)));
                    if (!group)
                    {
                        for (const auto& item : runtime->groups)
                            if (item->strip == reinterpret_cast<HWND>(lparam))
                                group = item.get();
                    }
                    if (group)
                        runtime->Select(*group, group->model.Cycle(wparam != 0));
                    return 0;
                }
                if (runtime && message == EventMessage)
                {
                    if (runtime->endingSession)
                        return 0;
                    const auto source = reinterpret_cast<HWND>(lparam);
                    if (wparam == EVENT_SYSTEM_MOVESIZESTART)
                    {
                        runtime->moving = source;
                        runtime->shiftDrag = GetAsyncKeyState(VK_SHIFT) < 0;
                    }
                    if (wparam == EVENT_SYSTEM_MOVESIZEEND && source == runtime->moving)
                    {
                        runtime->moving = nullptr;
                        if (runtime->shiftDrag)
                        {
                            POINT point{};
                            GetCursorPos(&point);
                            for (auto target : Candidates())
                            {
                                if (target == source || IsIconic(target))
                                    continue;
                                RECT rect{};
                                GetWindowRect(target, &rect);
                                rect.bottom = rect.top + MulDiv(48, static_cast<int>(GetDpiForWindow(target)), 96);
                                if (auto group = runtime->Find(Id(target)))
                                {
                                    RECT strip{};
                                    GetWindowRect(group->strip, &strip);
                                    UnionRect(&rect, &rect, &strip);
                                }
                                if (PtInRect(&rect, point))
                                {
                                    runtime->Join(source, target);
                                    break;
                                }
                            }
                        }
                    }
                    if (wparam == EVENT_SYSTEM_FOREGROUND)
                    {
                        if (auto group = runtime->Find(Id(source)); group && group->model.active != Id(source))
                            runtime->Select(*group, Id(source), false);
                    }
                    return 0;
                }
                if (message == WM_CLOSE)
                {
                    DestroyWindow(window);
                    return 0;
                }
                if (message == WM_DESTROY)
                {
                    PostQuitMessage(0);
                    return 0;
                }
            }
            catch (...)
            {
                // Never unwind a C++ exception across a USER32 callback boundary.
                Logger::error("Window Tabs operation failed; releasing grouped windows.");
                PostMessageW(window, WM_CLOSE, 0, 0);
            }
            return DefWindowProcW(window, message, wparam, lparam);
        }
    }

    Service::~Service()
    {
        Stop();
    }

    bool Service::Start(HINSTANCE instance)
    {
        Stop();
        std::promise<HWND> ready;
        auto started = ready.get_future();
        m_thread = std::thread([this, instance, ready = std::move(ready)]() mutable {
            Runtime runtime{ instance, m_restore };
            HWINEVENTHOOK moveHook = nullptr;
            HWINEVENTHOOK foregroundHook = nullptr;
            bool signaled = false;
            try
            {
                winrt::init_apartment(winrt::apartment_type::single_threaded);
                runtime.previousRestore = m_restore;
                runtime.sessionPath = std::filesystem::path(PTSettingsHelper::get_module_save_folder_location(L"WindowTabs")) / L"session.json";
                try
                {
                    if (m_restore && std::filesystem::exists(runtime.sessionPath) && std::filesystem::file_size(runtime.sessionPath) <= 1024 * 1024)
                    {
                        std::ifstream stream(runtime.sessionPath, std::ios::binary);
                        std::string text((std::istreambuf_iterator<char>(stream)), {});
                        runtime.pending = ParseSession(winrt::to_hstring(text));
                    }
                    if (!m_restore)
                        DeleteFileW(runtime.sessionPath.c_str());
                }
                catch (...)
                {
                    Logger::warn("Window Tabs ignored an unreadable session file.");
                }
                INITCOMMONCONTROLSEX controls{ sizeof(controls), ICC_TAB_CLASSES };
                InitCommonControlsEx(&controls);
                WNDCLASSEXW dispatcherType{ sizeof(WNDCLASSEXW) };
                dispatcherType.hInstance = instance;
                dispatcherType.lpfnWndProc = DispatcherProc;
                dispatcherType.lpszClassName = DispatcherClass;
                RegisterClassExW(&dispatcherType);
                auto stripType = dispatcherType;
                stripType.lpfnWndProc = Runtime::StripProc;
                stripType.lpszClassName = StripClass;
                stripType.hCursor = LoadCursorW(nullptr, IDC_ARROW);
                RegisterClassExW(&stripType);
                // An invisible top-level window receives the session-ending
                // broadcast before applications disappear during logoff.
                dispatcher = CreateWindowExW(WS_EX_TOOLWINDOW, DispatcherClass, L"", WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, instance, &runtime);
                moveHook = SetWinEventHook(EVENT_SYSTEM_MOVESIZESTART, EVENT_SYSTEM_MOVESIZEEND, nullptr, EventHook, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
                foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, nullptr, EventHook, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
                if (!dispatcher || !moveHook || !foregroundHook || !SetTimer(dispatcher, 1, 500, nullptr))
                    throw std::runtime_error("Window Tabs initialization failed");
                m_dispatcher = dispatcher;
                ready.set_value(dispatcher);
                signaled = true;
                MSG message{};
                while (GetMessageW(&message, nullptr, 0, 0) > 0)
                {
                    TranslateMessage(&message);
                    DispatchMessageW(&message);
                }
                runtime.Save();
            }
            catch (...)
            {
                Logger::error("Window Tabs service failed.");
            }
            m_dispatcher = nullptr;
            if (!signaled)
                ready.set_value(nullptr);
            if (moveHook)
                UnhookWinEvent(moveHook);
            if (foregroundHook)
                UnhookWinEvent(foregroundHook);
            for (const auto& [id, member] : runtime.members)
                member->Release();
            runtime.groups.clear();
            if (IsWindow(dispatcher))
                DestroyWindow(dispatcher);
            dispatcher = nullptr;
            UnregisterClassW(StripClass, instance);
            UnregisterClassW(DispatcherClass, instance);
            winrt::uninit_apartment();
        });
        return started.get() != nullptr;
    }

    void Service::Stop()
    {
        if (auto window = m_dispatcher.load())
            PostMessageW(window, WM_CLOSE, 0, 0);
        if (m_thread.joinable())
            m_thread.join();
    }

    bool Service::Cycle(bool forward)
    {
        const auto window = m_dispatcher.load();
        const auto foreground = GetForegroundWindow();
        return window && GetPropW(foreground, WindowProperty) && PostMessageW(window, CycleMessage, forward, reinterpret_cast<LPARAM>(foreground));
    }
}
