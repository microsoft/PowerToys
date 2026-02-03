// Test file with intentional issues for review/fix loop testing
// DO NOT USE IN PRODUCTION - This is for testing automation only

using System;

namespace PowerToys.TestReviewLoop
{
    /// <summary>
    /// Test utility class with INTENTIONAL issues for review testing.
    /// </summary>
    public class TestUtility
    {
        // Issue 1: Magic numbers - should be constants
        public int CalculateTimeout(int baseValue)
        {
            return baseValue * 1000 + 5000; // Magic numbers: 1000, 5000
        }

        // Issue 2: Missing null check
        public string ProcessInput(string input)
        {
            return input.ToUpper().Trim(); // No null check!
        }

        // Issue 3: Poor variable naming
        public void DoSomething(int x, string y)
        {
            var a = x * 2;
            var b = y + "test";
            Console.WriteLine($"{a} - {b}");
        }

        // Issue 4: Missing exception handling
        public int ParseNumber(string text)
        {
            return int.Parse(text); // Should use TryParse or try-catch
        }

        // Issue 5: Unused variable
        public void UnusedExample()
        {
            var unusedValue = "This is never used";
            Console.WriteLine("Hello");
        }

        // Issue 6: Empty catch block (swallowing exceptions)
        public void BadExceptionHandling()
        {
            try
            {
                throw new InvalidOperationException("Test");
            }
            catch
            {
                // Silently swallowing exception - bad practice!
            }
        }
    }
}
