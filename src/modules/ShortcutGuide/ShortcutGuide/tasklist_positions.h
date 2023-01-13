#pragma once
#include <vector>
#include <unordered_set>
#include <string>
#include <Windows.h>
#include <UIAutomationClient.h>

struct TasklistButton
{
    std::wstring name;
    long x{};
    long y{};
    long width{};
    long height{};
    long keynum{};
};

class Tasklist
{
public:
    void update();
    std::vector<TasklistButton> get_buttons();
    bool update_buttons(std::vector<TasklistButton>& buttons);

private:
    winrt::com_ptr<IUIAutomation> automation;
    winrt::com_ptr<IUIAutomationElement> element;
    winrt::com_ptr<IUIAutomationCondition> true_condition;
};
