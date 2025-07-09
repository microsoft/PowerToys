#include "pch.h"
#include "Calculator.h"
#include "Calculator.g.cpp"
#include "ExprtkEvaluator.h"

namespace winrt::CalculatorEngineCommon::implementation
{
    Calculator::Calculator(winrt::Windows::Foundation::Collections::IPropertySet const& constants)
    {
        for (auto const& pair : constants)
        {
            auto key = pair.Key();
            auto value = winrt::unbox_value<double>(pair.Value());
            m_constants.emplace(winrt::to_string(key), value);
        }
    }

    hstring Calculator::EvaluateExpression(hstring const& expression)
    {
        auto result = ExprtkCalculator::internal::EvaluateExpression(winrt::to_string(expression), m_constants);

        return hstring(result);
    }
}
