#pragma once
#include <string>
#include <unordered_map>

namespace ExprtkCalculator::internal
{
    std::wstring EvaluateExpression(
        const std::string& expression,
        const std::unordered_map<std::string, double>& constants);
}