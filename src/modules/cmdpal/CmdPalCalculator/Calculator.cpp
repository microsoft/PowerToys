#include "pch.h"
#include "Calculator.h"
#include "Calculator.g.cpp"
#include "ExprtkEvaluator.h"

namespace winrt::CmdPalCalculator::implementation
{
    Calculator::Calculator(winrt::Windows::Foundation::Collections::IPropertySet const& constants)
    {
        for (auto const& pair : constants)
        {
            auto key = pair.Key();
            auto value = winrt::unbox_value<double>(pair.Value());
            m_constants.emplace(key.c_str(), value);
        }
    }

    hstring Calculator::EvaluateExpression(hstring const& expression)
    {
        auto result = ExprtkCalculator::internal::EvaluateExpression(expression.c_str(), m_constants);

        return hstring(result);
    }
}
