#include "pch.h"
#include "tasklist_positions.h"

void Tasklist::update()
{
    // Get HWND of the tasklist
    auto tasklist_hwnd = FindWindowA("Shell_TrayWnd", nullptr);
    if (!tasklist_hwnd)
        return;
    tasklist_hwnd = FindWindowExA(tasklist_hwnd, 0, "ReBarWindow32", nullptr);
    if (!tasklist_hwnd)
        return;
    tasklist_hwnd = FindWindowExA(tasklist_hwnd, 0, "MSTaskSwWClass", nullptr);
    if (!tasklist_hwnd)
        return;
    tasklist_hwnd = FindWindowExA(tasklist_hwnd, 0, "MSTaskListWClass", nullptr);
    if (!tasklist_hwnd)
        return;
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

bool Tasklist::update_buttons(std::vector<TasklistButton>& buttons)
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
        TasklistButton button;
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
            button.name = automation_id;
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
            if (button.name == buttons.back().name)
                continue; // skip buttons from the same app
            button.keynum = buttons.back().keynum + 1;
            buttons.push_back(std::move(button));
            if (buttons.back().keynum == 10)
                break; // no more than 10 buttons
        }
    }
    return true;
}

std::vector<TasklistButton> Tasklist::get_buttons()
{
    std::vector<TasklistButton> buttons;
    update_buttons(buttons);
    return buttons;
}