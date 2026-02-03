// Test file with intentional issues for review/fix loop testing
// DO NOT USE IN PRODUCTION - This is for testing automation only

using System;

namespace PowerToys.TestReviewLoop
{
    /// <summary>
    /// Test utility class - issues fixed per code review.
    /// </summary>
    public class TestUtility
    {
        // Fix 1: Magic numbers extracted as constants
        private const int MillisecondsPerSecond = 1000;
        private const int BaseTimeoutMs = 5000;

        public int CalculateTimeout(int baseValue)
        {
            return baseValue * MillisecondsPerSecond + BaseTimeoutMs;
        }

        // Fix 2: Added null check
        public string ProcessInput(string input)
        {
            return input?.ToUpper().Trim() ?? string.Empty;
        }

        // Fix 3: Improved variable naming and method naming
        public void PrintDoubledValueWithPrefix(int multiplier, string prefix)
        {
            var doubledValue = multiplier * 2;
            var combinedText = prefix + "test";
            Console.WriteLine($"{doubledValue} - {combinedText}");
        }

        // Fix 4: Using TryParse for safe parsing
        public int ParseNumber(string text)
        {
            return int.TryParse(text, out var result) ? result : 0;
        }

        // Fix 5: Removed unused variable
        public void UnusedExample()
        {
            Console.WriteLine("Hello");
        }

        // Fix 6: Logging exception instead of swallowing
        public void BadExceptionHandling()
        {
            try
            {
                throw new InvalidOperationException("Test");
            }
            catch (InvalidOperationException ex)
            {
                // Log the exception - never silently swallow
                // Note: In production code, use PowerToys Logger: Logger.LogError("Operation failed", ex);
                System.Diagnostics.Debug.WriteLine($"Operation failed: {ex.Message}");
            }
        }
    }
}
