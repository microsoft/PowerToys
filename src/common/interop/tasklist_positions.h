#pragma once

struct TasklistButton
{
    wchar_t name[256];
    int x;
    int y;
    int width;
    int height;
    int keynum;
};

extern "C"
{
    winrt::com_ptr<IUIAutomation> automation;
    winrt::com_ptr<IUIAutomationElement> element;
    winrt::com_ptr<IUIAutomationCondition> true_condition;

    // Helper to get the taskbar HWND for the monitor under the cursor
    HWND GetTaskbarHwndForCursorMonitor(HMONITOR monitor);
    bool update_buttons(std::vector<TasklistButton>& buttons);
    __declspec(dllexport) TasklistButton* get_buttons(HMONITOR monitor, int* size);
}
