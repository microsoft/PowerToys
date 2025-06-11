#include "ExprtkEvaluator.h"
#include "exprtk.hpp"
#include <iomanip>
#include <iostream>
#include <sstream>

namespace ExprtkCalculator::internal
{

    std::wstring ToWStringFullPrecision(double value)
    {
        std::wostringstream oss;
        oss << std::fixed << std::setprecision(15) << value;
        return oss.str();
    }

    std::string WStringToAscii(const std::wstring& wstr)
    {
        std::string result;
        result.reserve(wstr.size());
        for (wchar_t wc : wstr)
            result.push_back(static_cast<char>(wc));
        return result;
    }

    std::wstring EvaluateExpression(
        const std::wstring& expressionText,
        const std::unordered_map<std::wstring, double>& constants)
    {
        exprtk::symbol_table<double> symbol_table;

        for (auto const& [name, value] : constants)
        {
            symbol_table.add_constant(WStringToAscii(name.c_str()), value);
        }

        exprtk::expression<double> expression;
        expression.register_symbol_table(symbol_table);

        exprtk::parser<double> parser;
        if (!parser.compile(WStringToAscii(expressionText), expression))
            return L"NaN";

        return ToWStringFullPrecision(expression.value());
    }
}