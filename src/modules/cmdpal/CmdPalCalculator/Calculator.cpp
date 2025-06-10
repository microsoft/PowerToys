#include "pch.h"
#include "Calculator.h"
#include "Calculator.g.cpp"
#include "ExprtkEvaluator.h"

namespace winrt::CmdPalCalculator::implementation
{
    Calculator::Calculator(Windows::Foundation::Collections::IMap<hstring, double> const& constants)
    {
        for (auto const& [k, v] : constants)
        {
            m_constants.emplace(k.c_str(), v);
        }
    }

    hstring Calculator::EvaluateExpression(hstring const& expression)
    {
        auto result = ExprtkCalculator::internal::EvaluateExpression(expression.c_str(), m_constants);

        return hstring(result);
    }
}
