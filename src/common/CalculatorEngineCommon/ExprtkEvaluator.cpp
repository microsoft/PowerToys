#include "ExprtkEvaluator.h"
#include "exprtk.hpp"
#include <iomanip>
#include <iostream>
#include <sstream>
#include <cmath>
#include <limits>

namespace ExprtkCalculator::internal
{
    static double factorial(const double n)
    {
        // Only allow non-negative integers
        if (n < 0.0 || std::floor(n) != n)
        {
            return std::numeric_limits<double>::quiet_NaN();
        }
        return std::tgamma(n + 1.0);
    }

    static double sign(const double n)
    {
        if (n > 0.0) return 1.0;
        if (n < 0.0) return -1.0;
        return 0.0;
    }

    std::wstring ToWStringFullPrecision(double value)
    {
        std::wostringstream oss;
        oss.imbue(std::locale::classic());
        oss << std::fixed << std::setprecision(15) << value;
        return oss.str();
    }

    std::wstring EvaluateExpression(
        const std::string& expressionText,
        const std::unordered_map<std::string, double>& constants)
    {
        exprtk::symbol_table<double> symbol_table;

        for (auto const& [name, value] : constants)
        {
            symbol_table.add_constant(name, value);
        }

        symbol_table.add_function("factorial", factorial);
        symbol_table.add_function("sign", sign);

        exprtk::expression<double> expression;
        expression.register_symbol_table(symbol_table);

        exprtk::parser<double> parser;

        // Enable all base functions and arithmetic operators
        parser.settings().enable_all_base_functions(); // Enable all base functions like sin, cos, log, etc.
        parser.settings().enable_all_arithmetic_ops(); // Enable all arithmetic operators like +, -, *, /, etc.

        // Disable all control structures and assignment operators to ensure only expressions are evaluated
        parser.settings().disable_all_control_structures(); // Disable control structures like if, for, while, etc.
        parser.settings().disable_all_assignment_ops(); // Disable assignment operators like =, +=, -=, etc.

        // Disabled for now, but can be enabled later for enhanced functionality
        parser.settings().disable_all_logic_ops(); // Disable logical operators like &&, ||, !, etc.
        parser.settings().disable_all_inequality_ops(); // Disable inequality operators like <, >, <=, >=, !=, etc.

        if (!parser.compile(expressionText, expression))
            return L"NaN";

        return ToWStringFullPrecision(expression.value());
    }
}