"""
Test Results Analyzer for CursorWrap Monitor Layout Tests

Analyzes test_report.json and provides detailed explanations of failures,
patterns, and recommendations.
"""

import json
import sys
from collections import defaultdict
from typing import Dict, List, Any


class TestResultAnalyzer:
    """Analyzes test results and provides insights"""
    
    def __init__(self, report_path: str = "test_report.json"):
        with open(report_path, 'r') as f:
            self.report = json.load(f)
        
        self.failures = self.report.get('failures', [])
        self.summary = self.report.get('summary', {})
        self.recommendations = self.report.get('recommendations', [])
    
    def print_overview(self):
        """Print test overview"""
        print("=" * 80)
        print("CURSORWRAP TEST RESULTS ANALYSIS")
        print("=" * 80)
        print(f"\nTotal Configurations Tested: {self.summary.get('total_configs', 0)}")
        print(f"Passed: {self.summary.get('passed', 0)} ({self.summary.get('pass_rate', 'N/A')})")
        print(f"Failed: {self.summary.get('failed', 0)}")
        print(f"Total Issues: {self.summary.get('total_issues', 0)}")
        
        if self.summary.get('passed', 0) == self.summary.get('total_configs', 0):
            print("\n✓ ALL TESTS PASSED! Edge detection logic is working correctly.")
            return
        
        print(f"\n⚠ {self.summary.get('total_issues', 0)} issues detected\n")
    
    def analyze_failure_patterns(self):
        """Analyze and categorize failure patterns"""
        print("=" * 80)
        print("FAILURE PATTERN ANALYSIS")
        print("=" * 80)
        
        # Group by test type
        by_test_type = defaultdict(list)
        for failure in self.failures:
            by_test_type[failure['test_name']].append(failure)
        
        # Group by configuration
        by_config = defaultdict(list)
        for failure in self.failures:
            by_config[failure['monitor_config']].append(failure)
        
        print(f"\n1. Failures by Test Type:")
        for test_type, failures in sorted(by_test_type.items(), key=lambda x: len(x[1]), reverse=True):
            print(f"   • {test_type}: {len(failures)} failures")
        
        print(f"\n2. Configurations with Failures:")
        for config, failures in sorted(by_config.items(), key=lambda x: len(x[1]), reverse=True):
            print(f"   • {config}")
            print(f"     {len(failures)} issues")
        
        return by_test_type, by_config
    
    def analyze_wrap_calculation_failures(self, failures: List[Dict[str, Any]]):
        """Detailed analysis of wrap calculation failures"""
        print("\n" + "=" * 80)
        print("WRAP CALCULATION FAILURE ANALYSIS")
        print("=" * 80)
        
        # Analyze cursor positions
        positions = []
        configs = set()
        
        for failure in failures:
            configs.add(failure['monitor_config'])
            # Extract position from expected message
            if 'test_point' in failure.get('details', {}):
                pos = failure['details']['test_point']
                positions.append(pos)
        
        print(f"\nAffected Configurations: {len(configs)}")
        for config in sorted(configs):
            print(f"  • {config}")
        
        if positions:
            print(f"\nFailed Test Points: {len(positions)}")
            # Analyze if failures are at edges
            edge_positions = defaultdict(int)
            for x, y in positions:
                # Simplified edge detection
                if x <= 10:
                    edge_positions['left edge'] += 1
                elif y <= 10:
                    edge_positions['top edge'] += 1
                else:
                    edge_positions['other'] += 1
            
            if edge_positions:
                print("\nPosition Distribution:")
                for pos_type, count in edge_positions.items():
                    print(f"  • {pos_type}: {count}")
    
    def explain_common_issues(self):
        """Explain common issues found in results"""
        print("\n" + "=" * 80)
        print("COMMON ISSUE EXPLANATIONS")
        print("=" * 80)
        
        has_wrap_failures = any(f['test_name'] == 'wrap_calculation' for f in self.failures)
        has_edge_failures = any(f['test_name'] == 'single_monitor_edges' for f in self.failures)
        has_touching_failures = any(f['test_name'] == 'touching_monitors' for f in self.failures)
        
        if has_wrap_failures:
            print("\n⚠ WRAP CALCULATION FAILURES")
            print("-" * 80)
            print("Issue: Cursor is on an outer edge but wrapping is not occurring.")
            print("\nLikely Causes:")
            print("  1. Partial Overlap Problem:")
            print("     • When monitors have different sizes (e.g., 4K + 1080p)")
            print("     • Only part of an edge is actually adjacent to another monitor")
            print("     • Current code marks the ENTIRE edge as non-outer if ANY part is adjacent")
            print("     • This prevents wrapping even in regions where it should occur")
            print("\n  2. Edge Detection Logic:")
            print("     • Check IdentifyOuterEdges() in MonitorTopology.cpp")
            print("     • Consider segmenting edges based on actual overlap regions")
            print("\n  3. Test Point Selection:")
            print("     • Failures may be at corners or quarter points")
            print("     • Indicates edge behavior varies along its length")
        
        if has_edge_failures:
            print("\n⚠ SINGLE MONITOR EDGE FAILURES")
            print("-" * 80)
            print("Issue: Single monitor should have exactly 4 outer edges.")
            print("\nThis indicates a fundamental problem in edge detection baseline.")
        
        if has_touching_failures:
            print("\n⚠ TOUCHING MONITORS FAILURES")
            print("-" * 80)
            print("Issue: Adjacent monitors not detected correctly.")
            print("\nCheck EdgesAreAdjacent() logic and 50px tolerance settings.")
    
    def print_recommendations(self):
        """Print recommendations from the report"""
        if not self.recommendations:
            return
        
        print("\n" + "=" * 80)
        print("RECOMMENDATIONS")
        print("=" * 80)
        
        for i, rec in enumerate(self.recommendations, 1):
            print(f"\n{i}. {rec}")
    
    def detailed_failure_dump(self):
        """Print all failure details"""
        print("\n" + "=" * 80)
        print("DETAILED FAILURE LISTING")
        print("=" * 80)
        
        for i, failure in enumerate(self.failures, 1):
            print(f"\n[{i}] {failure['test_name']}")
            print(f"Configuration: {failure['monitor_config']}")
            print(f"Expected: {failure['expected']}")
            print(f"Actual: {failure['actual']}")
            
            if 'details' in failure:
                details = failure['details']
                if 'edge' in details:
                    edge = details['edge']
                    print(f"Edge: {edge.get('edge_type', 'N/A')} at position {edge.get('position', 'N/A')}, "
                          f"range [{edge.get('range_start', 'N/A')}, {edge.get('range_end', 'N/A')}]")
                if 'test_point' in details:
                    print(f"Test Point: {details['test_point']}")
            print("-" * 80)
    
    def generate_github_copilot_prompt(self):
        """Generate a prompt suitable for GitHub Copilot to fix the issues"""
        print("\n" + "=" * 80)
        print("GITHUB COPILOT FIX PROMPT")
        print("=" * 80)
        print("\n```markdown")
        print("# CursorWrap Edge Detection Bug Report")
        print()
        print("## Test Results Summary")
        print(f"- Total Configurations Tested: {self.summary.get('total_configs', 0)}")
        print(f"- Pass Rate: {self.summary.get('pass_rate', 'N/A')}")
        print(f"- Failed Tests: {self.summary.get('failed', 0)}")
        print(f"- Total Issues: {self.summary.get('total_issues', 0)}")
        print()
        
        # Group failures
        by_test_type = defaultdict(list)
        for failure in self.failures:
            by_test_type[failure['test_name']].append(failure)
        
        print("## Critical Issues Found")
        print()
        
        # Analyze wrap calculation failures
        if 'wrap_calculation' in by_test_type:
            failures = by_test_type['wrap_calculation']
            configs = set(f['monitor_config'] for f in failures)
            
            print("### 1. Wrap Calculation Failures (PARTIAL OVERLAP BUG)")
            print()
            print(f"**Count**: {len(failures)} failures across {len(configs)} configuration(s)")
            print()
            print("**Affected Configurations**:")
            for config in sorted(configs):
                print(f"- {config}")
            print()
            
            print("**Root Cause Analysis**:")
            print()
            print("The current implementation in `MonitorTopology::IdentifyOuterEdges()` marks an")
            print("ENTIRE edge as non-outer if ANY portion of that edge is adjacent to another monitor.")
            print()
            print("**Problem Scenario**: 1080p monitor + 4K monitor at bottom-right")
            print("```")
            print("4K Monitor (3840x2160 at 0,0)")
            print("┌────────────────────────────────────────┐")
            print("│                                        │ <- Y: 0-1080 NO adjacent monitor")
            print("│                                        │    RIGHT EDGE SHOULD BE OUTER")
            print("│                                        │")
            print("│                                        │┌──────────┐")
            print("│                                        ││ 1080p    │ <- Y: 1080-2160 HAS adjacent")
            print("└────────────────────────────────────────┘│ at       │    RIGHT EDGE NOT OUTER")
            print("                                          │ (3840,   │")
            print("                                          │  1080)   │")
            print("                                          └──────────┘")
            print("```")
            print()
            print("**Current Behavior**: Right edge of 4K monitor is marked as NON-OUTER for entire")
            print("range (Y: 0-2160) because it detects adjacency in the bottom portion (Y: 1080-2160).")
            print()
            print("**Expected Behavior**: Right edge should be:")
            print("- OUTER from Y: 0 to Y: 1080 (no adjacent monitor)")
            print("- NON-OUTER from Y: 1080 to Y: 2160 (adjacent to 1080p monitor)")
            print()
            
            print("**Failed Test Examples**:")
            print()
            for i, failure in enumerate(failures[:3], 1):  # Show first 3
                details = failure.get('details', {})
                test_point = details.get('test_point', 'N/A')
                edge = details.get('edge', {})
                edge_type = edge.get('edge_type', 'N/A')
                position = edge.get('position', 'N/A')
                range_start = edge.get('range_start', 'N/A')
                range_end = edge.get('range_end', 'N/A')
                
                print(f"{i}. **Configuration**: {failure['monitor_config']}")
                print(f"   - Test Point: {test_point}")
                print(f"   - Edge: {edge_type} at X={position}, Y range=[{range_start}, {range_end}]")
                print(f"   - Expected: Cursor wraps to opposite edge")
                print(f"   - Actual: No wrap occurred (edge incorrectly marked as non-outer)")
                print()
            
            if len(failures) > 3:
                print(f"   ... and {len(failures) - 3} more similar failures")
                print()
        
        # Other failure types
        if 'single_monitor_edges' in by_test_type:
            print("### 2. Single Monitor Edge Detection Failures")
            print()
            print(f"**Count**: {len(by_test_type['single_monitor_edges'])} failures")
            print()
            print("Single monitor configurations should have exactly 4 outer edges.")
            print("This indicates a fundamental problem in baseline edge detection.")
            print()
        
        if 'touching_monitors' in by_test_type:
            print("### 3. Adjacent Monitor Detection Failures")
            print()
            print(f"**Count**: {len(by_test_type['touching_monitors'])} failures")
            print()
            print("Adjacent monitors not being detected correctly by EdgesAreAdjacent().")
            print()
        
        print("## Required Code Changes")
        print()
        print("### File: `MonitorTopology.cpp`")
        print()
        print("**Change 1**: Modify `IdentifyOuterEdges()` to support partial edge adjacency")
        print()
        print("Instead of marking entire edges as outer/non-outer, the code needs to:")
        print()
        print("1. **Segment edges** based on actual overlap regions with adjacent monitors")
        print("2. Create **sub-edges** for portions of an edge that have different outer status")
        print("3. Update `IsOnOuterEdge()` to check if the **cursor's specific position** is on an outer portion")
        print()
        print("**Proposed Approach**:")
        print()
        print("```cpp")
        print("// Instead of: edge.isOuter = true/false for entire edge")
        print("// Use: Store list of outer ranges for each edge")
        print()
        print("struct MonitorEdge {")
        print("    // ... existing fields ...")
        print("    std::vector<std::pair<int, int>> outerRanges; // Ranges where edge is outer")
        print("};")
        print()
        print("// In IdentifyOuterEdges():")
        print("// For each edge, find ALL adjacent opposite edges")
        print("// Calculate which portions of the edge have NO adjacent opposite")
        print("// Store these as outer ranges")
        print()
        print("// In IsOnOuterEdge():")
        print("// Check if cursor position falls within any outer range")
        print("if (edge.type == EdgeType::Left || edge.type == EdgeType::Right) {")
        print("    // Check if cursorPos.y is in any outer range")
        print("} else {")
        print("    // Check if cursorPos.x is in any outer range")
        print("}")
        print("```")
        print()
        print("**Change 2**: Update `EdgesAreAdjacent()` validation")
        print()
        print("The 50px tolerance logic is correct but needs to return overlap range info:")
        print()
        print("```cpp")
        print("struct AdjacencyResult {")
        print("    bool isAdjacent;")
        print("    int overlapStart;  // Where the adjacency begins")
        print("    int overlapEnd;    // Where the adjacency ends")
        print("};")
        print()
        print("AdjacencyResult CheckEdgeAdjacency(const MonitorEdge& edge1, ")
        print("                                    const MonitorEdge& edge2, ")
        print("                                    int tolerance);")
        print("```")
        print()
        print("## Test Validation")
        print()
        print("After implementing changes, run:")
        print("```bash")
        print("python monitor_layout_tests.py --max-monitors 10")
        print("```")
        print()
        print("Expected results:")
        print("- All 21+ configurations should pass")
        print("- Specifically, the 4K+1080p configuration should pass all 5 test points per edge")
        print("- Wrap calculation should work correctly at partial overlap boundaries")
        print()
        print("## Files to Modify")
        print()
        print("1. `MonitorTopology.h` - Update MonitorEdge structure")
        print("2. `MonitorTopology.cpp` - Implement segmented edge detection")
        print("   - `IdentifyOuterEdges()` - Main logic change")
        print("   - `IsOnOuterEdge()` - Check position against ranges")
        print("   - `EdgesAreAdjacent()` - Optionally return range info")
        print()
        print("```")
    
    def run_analysis(self, detailed: bool = False, copilot_mode: bool = False):
        """Run complete analysis"""
        if copilot_mode:
            self.generate_github_copilot_prompt()
            return
        
        self.print_overview()
        
        if not self.failures:
            print("\n✓ No failures to analyze!")
            return
        
        by_test_type, by_config = self.analyze_failure_patterns()
        
        # Specific analysis for wrap calculation failures
        if 'wrap_calculation' in by_test_type:
            self.analyze_wrap_calculation_failures(by_test_type['wrap_calculation'])
        
        self.explain_common_issues()
        self.print_recommendations()
        
        if detailed:
            self.detailed_failure_dump()


def main():
    """Main entry point"""
    import argparse
    
    parser = argparse.ArgumentParser(
        description="Analyze CursorWrap test results"
    )
    parser.add_argument(
        "--report",
        default="test_report.json",
        help="Path to test report JSON file"
    )
    parser.add_argument(
        "--detailed",
        action="store_true",
        help="Show detailed failure listing"
    )
    parser.add_argument(
        "--copilot",
        action="store_true",
        help="Generate GitHub Copilot-friendly fix prompt"
    )
    
    args = parser.parse_args()
    
    try:
        analyzer = TestResultAnalyzer(args.report)
        analyzer.run_analysis(detailed=args.detailed, copilot_mode=args.copilot)
        
        # Exit with error code if there were failures
        sys.exit(0 if not analyzer.failures else 1)
        
    except FileNotFoundError:
        print(f"Error: Could not find report file: {args.report}")
        print("\nRun monitor_layout_tests.py first to generate the report.")
        sys.exit(1)
    except json.JSONDecodeError:
        print(f"Error: Invalid JSON in report file: {args.report}")
        sys.exit(1)
    except Exception as e:
        print(f"Error analyzing report: {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()
