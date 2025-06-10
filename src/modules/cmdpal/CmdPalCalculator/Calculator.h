#pragma once

#include "Calculator.g.h"

namespace winrt::CmdPalCalculator::implementation
{
    struct Calculator : CalculatorT<Calculator>
    {
        Calculator() = default;

        Calculator(winrt::Windows::Foundation::Collections::IPropertySet const& constants);

        winrt::hstring EvaluateExpression(winrt::hstring const& expression);

    private:
        std::unordered_map<std::wstring, double> m_constants;
    };
}

namespace winrt::CmdPalCalculator::factory_implementation
{
    struct Calculator : CalculatorT<Calculator, implementation::Calculator>
    {
    };
}
