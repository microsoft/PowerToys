#pragma once
#include <string>
#include <unordered_map>

namespace ExprtkCalculator::internal
{
    std::wstring EvaluateExpression(
        const std::wstring& expression,
        const std::unordered_map<std::wstring, double>& constants = {});
}