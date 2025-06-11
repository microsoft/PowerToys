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

    std::wstring EvaluateExpression(
        const std::string& expressionText,
        const std::unordered_map<std::string, double>& constants)
    {
        exprtk::symbol_table<double> symbol_table;

        for (auto const& [name, value] : constants)
        {
            symbol_table.add_constant(name, value);
        }

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