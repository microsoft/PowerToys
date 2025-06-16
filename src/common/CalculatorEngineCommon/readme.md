# C++/WinRT CalculatorEngine Project Overview

This project wraps the exprtk expression parsing library with a C++/WinRT component,  
making advanced mathematical evaluation capabilities available to Windows applications.  
It is designed specifically to provide calculation support for the CmdPal calculator extension.

## Using exprtk

This project uses [exprtk](https://github.com/ArashPartow/exprtk) as the 
expression parsing and evaluation engine.

How to use exprtk in this project:
- The exprtk header file (`exprtk.hpp`) is included in the project source.
- You can use exprtk to parse and evaluate mathematical expressions in your 
  C++ code. For example:

    ```cpp
    #include "exprtk.hpp"
    exprtk::expression<double> expression;
    exprtk::parser<double> parser;
    std::string formula = "3 + 4 * 2";
    parser.compile(formula, expression);
    double result = expression.value();
    ```

How to update exprtk:
1. Download the latest `exprtk.hpp` from the [official repository](https://github.com/ArashPartow/exprtk).
2. Replace the existing `exprtk.hpp` file in the project with the new version.
3. Rebuild the project to ensure compatibility and take advantage of any updates.