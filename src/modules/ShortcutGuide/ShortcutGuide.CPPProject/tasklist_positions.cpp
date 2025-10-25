#include "pch.h"
#include "tasklist_positions.h"

// Tried my hardest adapting this to C#, but FindWindowW didn't work properly in C#. ~Noraa Junker

extern "C"
{
    HWND GetTaskbarHwndForCursorMonitor(HMONITOR monitor)
    {
        POINT pt;
        if (!GetCursorPos(&pt))
            return nullptr;

        // Find the primary taskbar
        HWND primaryTaskbar = FindWindowW(L"Shell_TrayWnd", nullptr);
        if (primaryTaskbar)
        {
            MONITORINFO mi = { sizeof(mi) };
            if (GetWindowRect(primaryTaskbar, &mi.rcMonitor))
            {
                HMONITOR primaryMonitor = MonitorFromRect(&mi.rcMonitor, MONITOR_DEFAULTTONEAREST);
                if (primaryMonitor == monitor)
                    return primaryTaskbar;
            }
        }

        // Find the secondary taskbar(s)
        HWND secondaryTaskbar = nullptr;
        while ((secondaryTaskbar = FindWindowExW(nullptr, secondaryTaskbar, L"Shell_SecondaryTrayWnd", nullptr)) != nullptr)
        {
            MONITORINFO mi = { sizeof(mi) };
            RECT rc;
            if (GetWindowRect(secondaryTaskbar, &rc))
            {
                HMONITOR taskbarMonitor = MonitorFromRect(&rc, MONITOR_DEFAULTTONEAREST);
                if (monitor == taskbarMonitor)
                    return secondaryTaskbar;
            }
        }

        return nullptr;
    }

    void update(HMONITOR monitor)
    {
        // Get HWND of the tasklist for the monitor under the cursor
        auto taskbar_hwnd = GetTaskbarHwndForCursorMonitor(monitor);
        if (!taskbar_hwnd)
            return;

        wchar_t class_name[64] = {};
        GetClassNameW(taskbar_hwnd, class_name, 64);

        HWND tasklist_hwnd = nullptr;

        if (wcscmp(class_name, L"Shell_TrayWnd") == 0)
        {
            // Primary taskbar structure
            tasklist_hwnd = FindWindowExW(taskbar_hwnd, 0, L"ReBarWindow32", nullptr);
            if (!tasklist_hwnd)
                return;
            tasklist_hwnd = FindWindowExW(tasklist_hwnd, 0, L"MSTaskSwWClass", nullptr);
            if (!tasklist_hwnd)
                return;
            tasklist_hwnd = FindWindowExW(tasklist_hwnd, 0, L"MSTaskListWClass", nullptr);
            if (!tasklist_hwnd)
                return;
        }
        else if (wcscmp(class_name, L"Shell_SecondaryTrayWnd") == 0)
        {
            // Secondary taskbar structure
            HWND workerw = FindWindowExW(taskbar_hwnd, 0, L"WorkerW", nullptr);
            if (!workerw)
                return;
            tasklist_hwnd = FindWindowExW(workerw, 0, L"MSTaskListWClass", nullptr);
            if (!tasklist_hwnd)
                return;
        }
        else
        {
            // Unknown taskbar type
            return;
        }

        if (!automation)
        {
            winrt::check_hresult(CoCreateInstance(CLSID_CUIAutomation,
                                                  nullptr,
                                                  CLSCTX_INPROC_SERVER,
                                                  IID_IUIAutomation,
                                                  automation.put_void()));
            winrt::check_hresult(automation->CreateTrueCondition(true_condition.put()));
        }
        element = nullptr;
        winrt::check_hresult(automation->ElementFromHandle(tasklist_hwnd, element.put()));
    }

    bool update_buttons(std::vector<TasklistButton>& buttons)
    {
        if (!automation || !element)
        {
            return false;
        }
        winrt::com_ptr<IUIAutomationElementArray> elements;
        if (element->FindAll(TreeScope_Children, true_condition.get(), elements.put()) < 0)
            return false;
        if (!elements)
            return false;
        int count;
        if (elements->get_Length(&count) < 0)
            return false;
        winrt::com_ptr<IUIAutomationElement> child;
        std::vector<TasklistButton> found_buttons;
        found_buttons.reserve(count);
        for (int i = 0; i < count; ++i)
        {
            child = nullptr;
            if (elements->GetElement(i, child.put()) < 0)
                return false;
            TasklistButton button = {};
            if (VARIANT var_rect; child->GetCurrentPropertyValue(UIA_BoundingRectanglePropertyId, &var_rect) >= 0)
            {
                if (var_rect.vt == (VT_R8 | VT_ARRAY))
                {
                    LONG pos;
                    double value;
                    pos = 0;
                    SafeArrayGetElement(var_rect.parray, &pos, &value);
                    button.x = static_cast<long>(value);
                    pos = 1;
                    SafeArrayGetElement(var_rect.parray, &pos, &value);
                    button.y = static_cast<long>(value);
                    pos = 2;
                    SafeArrayGetElement(var_rect.parray, &pos, &value);
                    button.width = static_cast<long>(value);
                    pos = 3;
                    SafeArrayGetElement(var_rect.parray, &pos, &value);
                    button.height = static_cast<long>(value);
                }
                VariantClear(&var_rect);
            }
            else
            {
                return false;
            }
            if (BSTR automation_id; child->get_CurrentAutomationId(&automation_id) >= 0)
            {
                wcsncpy_s(button.name, automation_id, _countof(button.name));
                SysFreeString(automation_id);
            }
            found_buttons.push_back(button);
        }
        // assign keynums
        buttons.clear();
        for (auto& button : found_buttons)
        {
            if (buttons.empty())
            {
                button.keynum = 1;
                buttons.push_back(std::move(button));
            }
            else
            {
                if (button.x < buttons.back().x || button.y < buttons.back().y) // skip 2nd row
                    break;
                if (wcsncmp(button.name, buttons.back().name, _countof(button.name)) == 0)
                    continue; // skip buttons from the same app
                button.keynum = buttons.back().keynum + 1;
                buttons.push_back(std::move(button));
                if (buttons.back().keynum == 10)
                    break; // no more than 10 buttons
            }
        }
        return true;
    }

    __declspec(dllexport) TasklistButton* get_buttons(HMONITOR monitor, int* size)
    {
        update(monitor);
        static std::vector<TasklistButton> buttons;
        update_buttons(buttons);
        *size = static_cast<int>(buttons.size());
        return buttons.data();
    }
}