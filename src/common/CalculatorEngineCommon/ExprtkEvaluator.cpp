#include "ExprtkEvaluator.h"
#include "exprtk.hpp"
#include <iomanip>
#include <iostream>
#include <sstream>
#include <cmath>
#include <limits>
#include <random>

namespace ExprtkCalculator::internal
{
    static double factorial(const double n)
    {
        if (std::isnan(n) || std::isinf(n))
        {
            return std::numeric_limits<double>::quiet_NaN();
        }

        // Only allow non-negative integers
        if (n < 0.0 || std::floor(n) != n)
        {
            return std::numeric_limits<double>::quiet_NaN();
        }
        return std::tgamma(n + 1.0);
    }

    // The calculator's user-facing names for the inverse trigonometric and
    // hyperbolic functions (see CalculateHelper.cs) differ from exprtk's
    // built-in asin/acos/atan/asinh/acosh/atanh, so they are registered
    // as aliases here.
    static double arcsin(const double n)
    {
        return std::asin(n);
    }

    static double arccos(const double n)
    {
        return std::acos(n);
    }

    static double arctan(const double n)
    {
        return std::atan(n);
    }

    static double arsinh(const double n)
    {
        return std::asinh(n);
    }

    static double arcosh(const double n)
    {
        return std::acosh(n);
    }

    static double artanh(const double n)
    {
        return std::atanh(n);
    }

    // Reciprocal hyperbolic functions and the inverses of the reciprocal
    // trigonometric/hyperbolic functions are not provided by exprtk
    // (it only has cot/sec/csc).
    static double coth(const double n)
    {
        return 1.0 / std::tanh(n);
    }

    static double sech(const double n)
    {
        return 1.0 / std::cosh(n);
    }

    static double csch(const double n)
    {
        return 1.0 / std::sinh(n);
    }

    // atan2 keeps arccot continuous with range (0, pi), so arccot(0) = pi/2
    // and negative inputs land in (pi/2, pi).
    static double arccot(const double n)
    {
        return std::atan2(1.0, n);
    }

    static double arcsec(const double n)
    {
        return std::acos(1.0 / n);
    }

    static double arccsc(const double n)
    {
        return std::asin(1.0 / n);
    }

    static double arcoth(const double n)
    {
        return std::atanh(1.0 / n);
    }

    static double arsech(const double n)
    {
        return std::acosh(1.0 / n);
    }

    static double arcsch(const double n)
    {
        return std::asinh(1.0 / n);
    }

    // rand(): returns a uniformly distributed random double in [0, 1)
    struct rand_func : public exprtk::ifunction<double>
    {
        std::mt19937_64 rng;
        std::uniform_real_distribution<double> dist;

        rand_func() :
            exprtk::ifunction<double>(0),
            rng(std::random_device{}()),
            dist(0.0, 1.0)
        {}

        inline double operator()() override
        {
            return dist(rng);
        }
    };

    // randi(n): returns a uniformly distributed random integer in [0, n-1]
    struct randi_func : public exprtk::ifunction<double>
    {
        std::mt19937_64 rng;

        randi_func() :
            exprtk::ifunction<double>(1),
            rng(std::random_device{}())
        {}

        inline double operator()(const double& n) override
        {
            if (std::isnan(n) || std::isinf(n))
            {
                return std::numeric_limits<double>::quiet_NaN();
            }

            constexpr double maxLongLongAsDouble = static_cast<double>(std::numeric_limits<long long>::max());
            if (n < 1.0 || n >= maxLongLongAsDouble)
            {
                return std::numeric_limits<double>::quiet_NaN();
            }

            if (std::floor(n) != n)
            {
                return std::numeric_limits<double>::quiet_NaN();
            }

            std::uniform_int_distribution<long long> dist(0, static_cast<long long>(n) - 1);
            return static_cast<double>(dist(rng));
        }
    };

    std::wstring ToWStringFullPrecision(double value)
    {
        if (std::isnan(value))
        {
            return L"NaN";
        }

        if (std::isinf(value))
        {
            return value > 0 ? L"inf" : L"-inf";
        }

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

        symbol_table.add_function("arcsin", arcsin);
        symbol_table.add_function("arccos", arccos);
        symbol_table.add_function("arctan", arctan);
        symbol_table.add_function("arsinh", arsinh);
        symbol_table.add_function("arcosh", arcosh);
        symbol_table.add_function("artanh", artanh);

        symbol_table.add_function("coth", coth);
        symbol_table.add_function("sech", sech);
        symbol_table.add_function("csch", csch);

        symbol_table.add_function("arccot", arccot);
        symbol_table.add_function("arcsec", arcsec);
        symbol_table.add_function("arccsc", arccsc);

        symbol_table.add_function("arcoth", arcoth);
        symbol_table.add_function("arsech", arsech);
        symbol_table.add_function("arcsch", arcsch);

        // thread_local ensures each thread has its own RNG instance (seeded once,
        // state preserved across calls) without requiring locks.
        static thread_local rand_func rand_fn;
        static thread_local randi_func randi_fn;
        symbol_table.add_function("rand", rand_fn);
        symbol_table.add_function("randi", randi_fn);

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
            return L"ParseError";

        return ToWStringFullPrecision(expression.value());
    }
}