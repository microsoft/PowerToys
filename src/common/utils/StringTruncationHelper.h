// StringTruncationHelper.h - Unit test for string truncation
// Implements fix for issue #45363
#pragma once
#include <string>
#include <algorithm>

namespace PowerToys::Utils
{
    // Truncates a string to maxLength characters, appending ellipsis if truncated
    inline std::wstring TruncateString(const std::wstring& input, size_t maxLength)
    {
        if (input.length() <= maxLength)
        {
            return input;
        }
        
        if (maxLength < 3)
        {
            return input.substr(0, maxLength);
        }
        
        return input.substr(0, maxLength - 3) + L"...";
    }
    
    // Test cases for TruncateString
    namespace Tests
    {
        inline bool TestTruncateString()
        {
            // Test 1: Short string (no truncation)
            auto result1 = TruncateString(L"Hello", 10);
            if (result1 != L"Hello") return false;
            
            // Test 2: Exact length
            auto result2 = TruncateString(L"Hello", 5);
            if (result2 != L"Hello") return false;
            
            // Test 3: Truncation with ellipsis
            auto result3 = TruncateString(L"Hello World", 8);
            if (result3 != L"Hello...") return false;
            
            // Test 4: Very short max length
            auto result4 = TruncateString(L"Hello", 2);
            if (result4 != L"He") return false;
            
            // Test 5: Empty string
            auto result5 = TruncateString(L"", 10);
            if (result5 != L"") return false;
            
            return true;
        }
    }
}
