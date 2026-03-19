#include "pch.h"
#include "Tasklist.h"
#include "Tasklist.g.cpp"

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Microsoft::Terminal::UI::implementation
{
    IVector<TasklistButton> Tasklist::GetButtons()
    {
        std::vector<TasklistButton> result;

        try
        {
            // Walk Shell_TrayWnd -> ReBarWindow32 -> MSTaskSwWClass -> MSTaskListWClass
            auto tasklistHwnd = FindWindowW(L"Shell_TrayWnd", nullptr);
            if (!tasklistHwnd)
                return winrt::single_threaded_vector<TasklistButton>(std::move(result));

            tasklistHwnd = FindWindowExW(tasklistHwnd, nullptr, L"ReBarWindow32", nullptr);
            if (!tasklistHwnd)
                return winrt::single_threaded_vector<TasklistButton>(std::move(result));

            tasklistHwnd = FindWindowExW(tasklistHwnd, nullptr, L"MSTaskSwWClass", nullptr);
            if (!tasklistHwnd)
                return winrt::single_threaded_vector<TasklistButton>(std::move(result));

            tasklistHwnd = FindWindowExW(tasklistHwnd, nullptr, L"MSTaskListWClass", nullptr);
            if (!tasklistHwnd)
                return winrt::single_threaded_vector<TasklistButton>(std::move(result));

            winrt::com_ptr<IUIAutomation> automation;
            winrt::check_hresult(CoCreateInstance(
                CLSID_CUIAutomation,
                nullptr,
                CLSCTX_INPROC_SERVER,
                IID_IUIAutomation,
                automation.put_void()));

            winrt::com_ptr<IUIAutomationCondition> trueCondition;
            winrt::check_hresult(automation->CreateTrueCondition(trueCondition.put()));

            winrt::com_ptr<IUIAutomationElement> element;
            winrt::check_hresult(automation->ElementFromHandle(tasklistHwnd, element.put()));

            winrt::com_ptr<IUIAutomationElementArray> elements;
            if (element->FindAll(TreeScope_Children, trueCondition.get(), elements.put()) < 0)
                return winrt::single_threaded_vector<TasklistButton>(std::move(result));
            if (!elements)
                return winrt::single_threaded_vector<TasklistButton>(std::move(result));

            int count = 0;
            if (elements->get_Length(&count) < 0)
                return winrt::single_threaded_vector<TasklistButton>(std::move(result));

            std::vector<TasklistButton> foundButtons;
            foundButtons.reserve(count);

            winrt::com_ptr<IUIAutomationElement> child;
            for (int i = 0; i < count; ++i)
            {
                child = nullptr;
                if (elements->GetElement(i, child.put()) < 0)
                    return winrt::single_threaded_vector<TasklistButton>(std::move(result));

                TasklistButton button{};

                VARIANT varRect{};
                if (child->GetCurrentPropertyValue(UIA_BoundingRectanglePropertyId, &varRect) >= 0)
                {
                    if (varRect.vt == (VT_R8 | VT_ARRAY))
                    {
                        LONG pos;
                        double value;
                        pos = 0; SafeArrayGetElement(varRect.parray, &pos, &value); button.X = static_cast<int32_t>(value);
                        pos = 1; SafeArrayGetElement(varRect.parray, &pos, &value); button.Y = static_cast<int32_t>(value);
                        pos = 2; SafeArrayGetElement(varRect.parray, &pos, &value); button.Width = static_cast<int32_t>(value);
                        pos = 3; SafeArrayGetElement(varRect.parray, &pos, &value); button.Height = static_cast<int32_t>(value);
                    }
                    VariantClear(&varRect);
                }
                else
                {
                    return winrt::single_threaded_vector<TasklistButton>(std::move(result));
                }

                if (BSTR automationId; child->get_CurrentAutomationId(&automationId) >= 0)
                {
                    button.Name = automationId ? winrt::hstring(automationId) : winrt::hstring{};
                    SysFreeString(automationId);
                }

                foundButtons.push_back(button);
            }

            // Assign key numbers
            for (auto& button : foundButtons)
            {
                if (result.empty())
                {
                    button.KeyNum = 1;
                    result.push_back(button);
                }
                else
                {
                    if (button.X < result.back().X || button.Y < result.back().Y) // skip 2nd row
                        break;
                    if (button.Name == result.back().Name)
                        continue; // skip buttons from the same app
                    button.KeyNum = result.back().KeyNum + 1;
                    result.push_back(button);
                    if (result.back().KeyNum == 10)
                        break; // no more than 10 buttons
                }
            }
        }
        catch (...)
        {
            // Return whatever we have on any COM/WinRT failure
        }

        return winrt::single_threaded_vector<TasklistButton>(std::move(result));
    }
}
