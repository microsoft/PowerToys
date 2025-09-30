#pragma once

#include <vector>
#include <string>

// Test case structure for comprehensive monitor layout testing
struct MonitorTestCase
{
    std::string name;
    std::string description;
    int grid[3][3]; // 3x3 grid representing monitor layout (0 = no monitor, 1-9 = monitor ID)
    
    // Test scenarios to validate
    struct TestScenario
    {
        int sourceMonitor;      // Which monitor to start cursor on (1-based)
        int edgeDirection;      // 0=top, 1=right, 2=bottom, 3=left
        int expectedTargetMonitor; // Expected destination monitor (1-based, -1 = wrap within same monitor)
        std::string description;
    };
    
    std::vector<TestScenario> scenarios;
};

// Comprehensive test cases for all possible 3x3 monitor grid configurations
class CursorWrapTestSuite
{
public:
    static std::vector<MonitorTestCase> GetAllTestCases()
    {
        std::vector<MonitorTestCase> testCases;
        
        // Test Case 1: Single monitor (center)
        testCases.push_back({
            "Single_Center",
            "Single monitor in center position",
            {
                {0, 0, 0},
                {0, 1, 0},
                {0, 0, 0}
            },
            {
                {1, 0, -1, "Top edge wraps to bottom of same monitor"},
                {1, 1, -1, "Right edge wraps to left of same monitor"},
                {1, 2, -1, "Bottom edge wraps to top of same monitor"},
                {1, 3, -1, "Left edge wraps to right of same monitor"}
            }
        });
        
        // Test Case 2: Two monitors horizontal (left + right)
        testCases.push_back({
            "Dual_Horizontal_Left_Right",
            "Two monitors: left + right",
            {
                {0, 0, 0},
                {1, 0, 2},
                {0, 0, 0}
            },
            {
                {1, 0, -1, "Monitor 1 top wraps to bottom of monitor 1"},
                {1, 1, 2, "Monitor 1 right edge moves to monitor 2 left"},
                {1, 2, -1, "Monitor 1 bottom wraps to top of monitor 1"},
                {1, 3, -1, "Monitor 1 left edge wraps to right of monitor 1"},
                {2, 0, -1, "Monitor 2 top wraps to bottom of monitor 2"},
                {2, 1, -1, "Monitor 2 right edge wraps to left of monitor 2"},
                {2, 2, -1, "Monitor 2 bottom wraps to top of monitor 2"},
                {2, 3, 1, "Monitor 2 left edge moves to monitor 1 right"}
            }
        });
        
        // Test Case 3: Two monitors vertical (Monitor 2 above Monitor 1) - CORRECTED FOR USER'S SETUP
        testCases.push_back({
            "Dual_Vertical_2_Above_1", 
            "Two monitors: Monitor 2 (top) above Monitor 1 (bottom/main)",
            {
                {0, 2, 0},  // Row 0: Monitor 2 (physically top monitor)
                {0, 0, 0},  // Row 1: Empty
                {0, 1, 0}   // Row 2: Monitor 1 (physically bottom/main monitor)
            },
            {
                // Monitor 1 (bottom/main monitor) tests
                {1, 0, 2, "Monitor 1 (bottom) top edge should move to Monitor 2 (top) bottom"},
                {1, 1, -1, "Monitor 1 right wraps to left of monitor 1"},
                {1, 2, -1, "Monitor 1 bottom wraps to top of monitor 1"},
                {1, 3, -1, "Monitor 1 left wraps to right of monitor 1"},
                
                // Monitor 2 (top monitor) tests  
                {2, 0, -1, "Monitor 2 (top) top wraps to bottom of monitor 2"},
                {2, 1, -1, "Monitor 2 right wraps to left of monitor 2"},
                {2, 2, 1, "Monitor 2 (top) bottom edge should move to Monitor 1 (bottom) top"},
                {2, 3, -1, "Monitor 2 left wraps to right of monitor 2"}
            }
        });
        
        // Test Case 4: Three monitors L-shape (center + left + top)
        testCases.push_back({
            "Triple_L_Shape",
            "Three monitors in L-shape: center + left + top",
            {
                {0, 3, 0},
                {2, 1, 0},
                {0, 0, 0}
            },
            {
                {1, 0, 3, "Monitor 1 top moves to monitor 3 bottom"},
                {1, 1, -1, "Monitor 1 right wraps to left of monitor 1"},
                {1, 2, -1, "Monitor 1 bottom wraps to top of monitor 1"},
                {1, 3, 2, "Monitor 1 left moves to monitor 2 right"},
                {2, 0, -1, "Monitor 2 top wraps to bottom of monitor 2"},
                {2, 1, 1, "Monitor 2 right moves to monitor 1 left"},
                {2, 2, -1, "Monitor 2 bottom wraps to top of monitor 2"},
                {2, 3, -1, "Monitor 2 left wraps to right of monitor 2"},
                {3, 0, -1, "Monitor 3 top wraps to bottom of monitor 3"},
                {3, 1, -1, "Monitor 3 right wraps to left of monitor 3"},
                {3, 2, 1, "Monitor 3 bottom moves to monitor 1 top"},
                {3, 3, -1, "Monitor 3 left wraps to right of monitor 3"}
            }
        });
        
        // Test Case 5: Three monitors horizontal (left + center + right)
        testCases.push_back({
            "Triple_Horizontal",
            "Three monitors horizontal: left + center + right",
            {
                {0, 0, 0},
                {1, 2, 3},
                {0, 0, 0}
            },
            {
                {1, 0, -1, "Monitor 1 top wraps to bottom"},
                {1, 1, 2, "Monitor 1 right moves to monitor 2"},
                {1, 2, -1, "Monitor 1 bottom wraps to top"},
                {1, 3, -1, "Monitor 1 left wraps to right"},
                {2, 0, -1, "Monitor 2 top wraps to bottom"},
                {2, 1, 3, "Monitor 2 right moves to monitor 3"},
                {2, 2, -1, "Monitor 2 bottom wraps to top"},
                {2, 3, 1, "Monitor 2 left moves to monitor 1"},
                {3, 0, -1, "Monitor 3 top wraps to bottom"},
                {3, 1, -1, "Monitor 3 right wraps to left"},
                {3, 2, -1, "Monitor 3 bottom wraps to top"},
                {3, 3, 2, "Monitor 3 left moves to monitor 2"}
            }
        });
        
        // Test Case 6: Three monitors vertical (top + center + bottom)
        testCases.push_back({
            "Triple_Vertical",
            "Three monitors vertical: top + center + bottom",
            {
                {0, 1, 0},
                {0, 2, 0},
                {0, 3, 0}
            },
            {
                {1, 0, -1, "Monitor 1 top wraps to bottom"},
                {1, 1, -1, "Monitor 1 right wraps to left"},
                {1, 2, 2, "Monitor 1 bottom moves to monitor 2"},
                {1, 3, -1, "Monitor 1 left wraps to right"},
                {2, 0, 1, "Monitor 2 top moves to monitor 1"},
                {2, 1, -1, "Monitor 2 right wraps to left"},
                {2, 2, 3, "Monitor 2 bottom moves to monitor 3"},
                {2, 3, -1, "Monitor 2 left wraps to right"},
                {3, 0, 2, "Monitor 3 top moves to monitor 2"},
                {3, 1, -1, "Monitor 3 right wraps to left"},
                {3, 2, -1, "Monitor 3 bottom wraps to top"},
                {3, 3, -1, "Monitor 3 left wraps to right"}
            }
        });
        
        return testCases;
    }
    
    // Helper function to print test case in a readable format
    static std::string FormatTestCase(const MonitorTestCase& testCase)
    {
        std::string result = "Test Case: " + testCase.name + "\n";
        result += "Description: " + testCase.description + "\n";
        result += "Layout:\n";
        
        for (int row = 0; row < 3; row++)
        {
            result += "  ";
            for (int col = 0; col < 3; col++)
            {
                if (testCase.grid[row][col] == 0)
                {
                    result += ". ";
                }
                else
                {
                    result += std::to_string(testCase.grid[row][col]) + " ";
                }
            }
            result += "\n";
        }
        
        result += "Test Scenarios:\n";
        for (const auto& scenario : testCase.scenarios)
        {
            result += "  - " + scenario.description + "\n";
        }
        
        return result;
    }
    
    // Helper function to validate a specific test case against actual behavior
    static bool ValidateTestCase(const MonitorTestCase& testCase)
    {
        // This would be called with actual CursorWrap instance to validate behavior
        // For now, just return true - this would need actual implementation
        return true;
    }
};