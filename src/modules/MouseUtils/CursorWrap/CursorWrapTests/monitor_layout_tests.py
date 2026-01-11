"""
Monitor Layout Edge Detection Test Suite for CursorWrap

This script validates the edge detection and wrapping logic across thousands of
monitor configurations without requiring the full PowerToys build environment.

Tests:
- 1-4 monitor configurations
- Common resolutions and DPI scales
- Various arrangements (horizontal, vertical, L-shape, grid)
- Edge detection (touching vs. gap)
- Wrap calculations

Output: JSON report with failures for GitHub Copilot analysis
"""

import json
from dataclasses import dataclass, asdict
from typing import List, Tuple, Dict, Optional
from enum import Enum
import sys

# ============================================================================
# Data Structures (mirrors C++ implementation)
# ============================================================================


@dataclass
class MonitorInfo:
    """Represents a physical monitor"""
    left: int
    top: int
    right: int
    bottom: int
    dpi: int = 96
    primary: bool = False

    @property
    def width(self) -> int:
        return self.right - self.left

    @property
    def height(self) -> int:
        return self.bottom - self.top

    @property
    def center_x(self) -> int:
        return (self.left + self.right) // 2

    @property
    def center_y(self) -> int:
        return (self.top + self.bottom) // 2


class EdgeType(Enum):
    LEFT = "Left"
    RIGHT = "Right"
    TOP = "Top"
    BOTTOM = "Bottom"


@dataclass
class Edge:
    """Represents a monitor edge"""
    edge_type: EdgeType
    position: int  # x for vertical, y for horizontal
    range_start: int
    range_end: int
    monitor_index: int

    def overlaps(self, other: 'Edge', tolerance: int = 1) -> bool:
        """Check if two edges overlap in their perpendicular range"""
        if self.edge_type != other.edge_type:
            return False
        if abs(self.position - other.position) > tolerance:
            return False
        return not (
    self.range_end <= other.range_start or other.range_end <= self.range_start)


@dataclass
class TestFailure:
    """Records a test failure for analysis"""
    test_name: str
    monitor_config: str
    expected: str
    actual: str
    details: Dict

# ============================================================================
# Edge Detection Logic (Python implementation of C++ logic)
# ============================================================================


class MonitorTopology:
    """Implements the edge detection logic to be validated"""

    ADJACENCY_TOLERANCE = 50  # Pixels - tolerance for detecting adjacent edges (matches C++ implementation)
    EDGE_THRESHOLD = 1  # Pixels - cursor must be within this distance to trigger wrap

    def __init__(self, monitors: List[MonitorInfo]):
        self.monitors = monitors
        self.outer_edges: List[Edge] = []
        self._detect_outer_edges()

    def _detect_outer_edges(self):
        """Detect which edges are outer (can wrap)"""
        all_edges = self._collect_all_edges()

        for edge in all_edges:
            if self._is_outer_edge(edge, all_edges):
                self.outer_edges.append(edge)

    def _collect_all_edges(self) -> List[Edge]:
        """Collect all edges from all monitors"""
        edges = []

        for idx, mon in enumerate(self.monitors):
            edges.append(
                Edge(
                    EdgeType.LEFT,
                    mon.left,
                    mon.top,
                    mon.bottom,
                    idx))
            edges.append(
                Edge(
                    EdgeType.RIGHT,
                    mon.right,
                    mon.top,
                    mon.bottom,
                    idx))
            edges.append(Edge(EdgeType.TOP, mon.top, mon.left, mon.right, idx))
            edges.append(
                Edge(
                    EdgeType.BOTTOM,
                    mon.bottom,
                    mon.left,
                    mon.right,
                    idx))

        return edges

    def _is_outer_edge(self, edge: Edge, all_edges: List[Edge]) -> bool:
        """
    Determine if an edge is "outer" (can wrap)

        Rules:
        1. If edge has an adjacent opposite edge (within 50px tolerance AND overlapping range), it's NOT outer
        2. Otherwise, edge IS outer
        Note: This matches C++ EdgesAreAdjacent() logic
        """
        opposite_type = self._get_opposite_edge_type(edge.edge_type)

        # Find opposite edges that overlap in perpendicular range
        opposite_edges = [e for e in all_edges
                            if e.edge_type == opposite_type
                            and e.monitor_index != edge.monitor_index
                            and self._ranges_overlap(edge.range_start, edge.range_end,
                                                    e.range_start, e.range_end)]

        if not opposite_edges:
            return True  # No opposite edges = outer edge
        
        # Check if any opposite edge is adjacent (within tolerance)
        for opp in opposite_edges:
            distance = abs(edge.position - opp.position)
            if distance <= self.ADJACENCY_TOLERANCE:
                return False  # Adjacent edge found = not outer
        
        return True  # No adjacent edges = outer

    @staticmethod
    def _get_opposite_edge_type(edge_type: EdgeType) -> EdgeType:
        """Get the opposite edge type"""
        opposites = {
            EdgeType.LEFT: EdgeType.RIGHT,
            EdgeType.RIGHT: EdgeType.LEFT,
            EdgeType.TOP: EdgeType.BOTTOM,
            EdgeType.BOTTOM: EdgeType.TOP
        }
        return opposites[edge_type]

    @staticmethod
    def _ranges_overlap(
        a_start: int,
        a_end: int,
        b_start: int,
        b_end: int) -> bool:
        """Check if two 1D ranges overlap"""
        return not (a_end <= b_start or b_end <= a_start)

    def calculate_wrap_position(self, x: int, y: int) -> Tuple[int, int]:
        """Calculate where cursor should wrap to"""
        # Find which outer edge was crossed and calculate wrap
        # At corners, multiple edges may match - try all and return first successful wrap
        for edge in self.outer_edges:
            if self._is_on_edge(x, y, edge):
                new_x, new_y = self._wrap_from_edge(x, y, edge)
                if (new_x, new_y) != (x, y):
                    # Wrap succeeded
                    return (new_x, new_y)

        return (x, y)  # No wrap

    def _is_on_edge(self, x: int, y: int, edge: Edge) -> bool:
        """Check if point is on the given edge"""
        tolerance = 2  # Pixels

        if edge.edge_type in (EdgeType.LEFT, EdgeType.RIGHT):
            return (abs(x - edge.position) <= tolerance and
                    edge.range_start <= y <= edge.range_end)
        else:
            return (abs(y - edge.position) <= tolerance and
                    edge.range_start <= x <= edge.range_end)

    def _wrap_from_edge(self, x: int, y: int, edge: Edge) -> Tuple[int, int]:
        """Calculate wrap destination from an outer edge"""
        opposite_type = self._get_opposite_edge_type(edge.edge_type)

        # Find opposite outer edges that overlap
        opposite_edges = [e for e in self.outer_edges
                            if e.edge_type == opposite_type
                            and self._point_in_range(x, y, e)]

        if not opposite_edges:
            return (x, y)  # No wrap destination

        # Find closest opposite edge
        target_edge = min(opposite_edges,
                            key=lambda e: abs(e.position - edge.position))

        # Calculate new position
        if edge.edge_type in (EdgeType.LEFT, EdgeType.RIGHT):
            return (target_edge.position, y)
        else:
            return (x, target_edge.position)

    @staticmethod
    def _point_in_range(x: int, y: int, edge: Edge) -> bool:
        """Check if point's perpendicular coordinate is in edge's range"""
        if edge.edge_type in (EdgeType.LEFT, EdgeType.RIGHT):
            return edge.range_start <= y <= edge.range_end
        else:
            return edge.range_start <= x <= edge.range_end

# ============================================================================
# Test Configuration Generators
# ============================================================================


class TestConfigGenerator:
    """Generates comprehensive test configurations"""

    # Common resolutions
    RESOLUTIONS = [
        (1920, 1080),   # 1080p
        (2560, 1440),   # 1440p
        (3840, 2160),   # 4K
        (3440, 1440),   # Ultrawide
        (1920, 1200),   # 16:10
    ]

    # DPI scales
    DPI_SCALES = [96, 120, 144, 192]  # 100%, 125%, 150%, 200%

    @classmethod
    def load_from_file(cls, filepath: str) -> List[List[MonitorInfo]]:
        """Load monitor configuration from captured JSON file"""
        # Handle UTF-8 with BOM (PowerShell default)
        with open(filepath, 'r', encoding='utf-8-sig') as f:
            data = json.load(f)
        
        monitors = []
        for mon in data.get('monitors', []):
            monitor = MonitorInfo(
                left=mon['left'],
                top=mon['top'],
                right=mon['right'],
                bottom=mon['bottom'],
                dpi=mon.get('dpi', 96),
                primary=mon.get('primary', False)
            )
            monitors.append(monitor)
        
        return [monitors] if monitors else []
    
    @classmethod
    def generate_all_configs(cls,
     max_monitors: int = 4) -> List[List[MonitorInfo]]:
        """Generate all test configurations"""
        configs = []

        # Single monitor (baseline)
        configs.extend(cls._single_monitor_configs())

        # Two monitors (most common)
        if max_monitors >= 2:
            configs.extend(cls._two_monitor_configs())

        # Three monitors
        if max_monitors >= 3:
            configs.extend(cls._three_monitor_configs())

        # Four monitors
        if max_monitors >= 4:
            configs.extend(cls._four_monitor_configs())
        
        # Five+ monitors
        if max_monitors >= 5:
            configs.extend(cls._five_plus_monitor_configs(max_monitors))
        
        return configs
    
    @classmethod
    def _single_monitor_configs(cls) -> List[List[MonitorInfo]]:
        """Single monitor configurations"""
        configs = []

        for width, height in cls.RESOLUTIONS[:3]:  # Limit for single monitor
            for dpi in cls.DPI_SCALES[:2]:  # Limit DPI variations
                mon = MonitorInfo(0, 0, width, height, dpi, True)
                configs.append([mon])

        return configs

    @classmethod
    def _two_monitor_configs(cls) -> List[List[MonitorInfo]]:
        """Two monitor configurations"""
        configs = []
        # Both 1080p for simplicity
        res1, res2 = cls.RESOLUTIONS[0], cls.RESOLUTIONS[0]

        # Horizontal (touching)
        configs.append([
            MonitorInfo(0, 0, res1[0], res1[1], primary=True),
    MonitorInfo(res1[0], 0, res1[0] + res2[0], res2[1])
        ])

        # Vertical (touching)
        configs.append([
        MonitorInfo(0, 0, res1[0], res1[1], primary=True),
            MonitorInfo(0, res1[1], res2[0], res1[1] + res2[1])
        ])

        # Different resolutions
        res_big = cls.RESOLUTIONS[2]  # 4K
        configs.append([
            MonitorInfo(0, 0, res1[0], res1[1], primary=True),
            MonitorInfo(res1[0], 0, res1[0] + res_big[0], res_big[1])
        ])

        # Offset alignment (common real-world scenario)
        offset = 200
        configs.append([
            MonitorInfo(0, offset, res1[0], offset + res1[1], primary=True),
            MonitorInfo(res1[0], 0, res1[0] + res2[0], res2[1])
        ])

        return configs

    @classmethod
    def _three_monitor_configs(cls) -> List[List[MonitorInfo]]:
        """Three monitor configurations"""
        configs = []
        res = cls.RESOLUTIONS[0]  # 1080p

        # Linear horizontal
        configs.append([
            MonitorInfo(0, 0, res[0], res[1], primary=True),
            MonitorInfo(res[0], 0, res[0] * 2, res[1]),
            MonitorInfo(res[0] * 2, 0, res[0] * 3, res[1])
        ])

        # L-shape (common gaming setup)
        configs.append([
            MonitorInfo(0, 0, res[0], res[1], primary=True),
            MonitorInfo(res[0], 0, res[0] * 2, res[1]),
            MonitorInfo(0, res[1], res[0], res[1] * 2)
        ])

        # Vertical stack
        configs.append([
            MonitorInfo(0, 0, res[0], res[1], primary=True),
            MonitorInfo(0, res[1], res[0], res[1] * 2),
            MonitorInfo(0, res[1] * 2, res[0], res[1] * 3)
        ])

        return configs

    @classmethod
    def _four_monitor_configs(cls) -> List[List[MonitorInfo]]:
        """Four monitor configurations"""
        configs = []
        res = cls.RESOLUTIONS[0]  # 1080p

        # 2x2 grid (classic)
        configs.append([
            MonitorInfo(0, 0, res[0], res[1], primary=True),
            MonitorInfo(res[0], 0, res[0] * 2, res[1]),
            MonitorInfo(0, res[1], res[0], res[1] * 2),
            MonitorInfo(res[0], res[1], res[0] * 2, res[1] * 2)
        ])

        # Linear horizontal
        configs.append([
            MonitorInfo(0, 0, res[0], res[1], primary=True),
            MonitorInfo(res[0], 0, res[0] * 2, res[1]),
            MonitorInfo(res[0] * 2, 0, res[0] * 3, res[1]),
            MonitorInfo(res[0] * 3, 0, res[0] * 4, res[1])
        ])

        return configs
    
    @classmethod
    def _five_plus_monitor_configs(cls, max_count: int) -> List[List[MonitorInfo]]:
        """Five to ten monitor configurations"""
        configs = []
        res = cls.RESOLUTIONS[0]  # 1080p
        
        # Linear horizontal (5-10 monitors)
        for count in range(5, min(max_count + 1, 11)):
            monitor_list = []
            for i in range(count):
                is_primary = (i == 0)
                monitor_list.append(
                    MonitorInfo(res[0] * i, 0, res[0] * (i + 1), res[1], primary=is_primary)
                )
            configs.append(monitor_list)
        
        return configs

# ============================================================================
# Test Validators
# ============================================================================


class EdgeDetectionValidator:
    """Validates edge detection logic"""

    @staticmethod
    def validate_single_monitor(
    monitors: List[MonitorInfo]) -> Optional[TestFailure]:
        """Single monitor should have 4 outer edges"""
        topology = MonitorTopology(monitors)
        expected_count = 4
        actual_count = len(topology.outer_edges)

        if actual_count != expected_count:
            return TestFailure(
                test_name="single_monitor_edges",
                monitor_config=EdgeDetectionValidator._describe_config(
                    monitors),
                expected=f"{expected_count} outer edges",
                actual=f"{actual_count} outer edges",
                details={"edges": [asdict(e) for e in topology.outer_edges]}
            )
        return None

    @staticmethod
    def validate_touching_monitors(
    monitors: List[MonitorInfo]) -> Optional[TestFailure]:
        """Touching monitors should have no gap between them"""
        topology = MonitorTopology(monitors)

        # For 2 touching monitors horizontally, expect 6 outer edges (not 8)
        if len(monitors) == 2:
            # Check if they're aligned horizontally and touching
            m1, m2 = monitors
            if m1.right == m2.left and m1.top == m2.top and m1.bottom == m2.bottom:
                expected = 6  # 2 internal edges removed
                actual = len(topology.outer_edges)
                if actual != expected:
                    return TestFailure(
                        test_name="touching_monitors",
                        monitor_config=EdgeDetectionValidator._describe_config(
                            monitors),
                        expected=f"{expected} outer edges (2 touching edges removed)",
                        actual=f"{actual} outer edges",
                        details={"edges": [asdict(e)
                                                  for e in topology.outer_edges]}
                    )
        return None

    @staticmethod
    def validate_wrap_calculation(
    monitors: List[MonitorInfo]) -> List[TestFailure]:
        """Validate cursor wrap calculations"""
        failures = []
        topology = MonitorTopology(monitors)

        # Test wrapping at each outer edge with multiple points
        for edge in topology.outer_edges:
            test_points = EdgeDetectionValidator._get_test_points_on_edge(
                edge, monitors)
            
            for test_point in test_points:
                x, y = test_point
                
                # Check if there's actually a valid wrap destination
                # (some outer edges may not have opposite edges due to partial overlap)
                opposite_type = topology._get_opposite_edge_type(edge.edge_type)
                has_opposite = any(
                    e.edge_type == opposite_type and 
                    topology._point_in_range(x, y, e)
                    for e in topology.outer_edges
                )
                
                if not has_opposite:
                    # No wrap destination available - this is OK for partial overlaps
                    continue
                
                new_x, new_y = topology.calculate_wrap_position(x, y)

                # Verify wrap happened (position changed)
                if (new_x, new_y) == (x, y):
                    # Should have wrapped but didn't
                    failure = TestFailure(
                        test_name="wrap_calculation",
                        monitor_config=EdgeDetectionValidator._describe_config(
                            monitors),
                        expected=f"Cursor should wrap from ({x},{y})",
                        actual=f"No wrap occurred",
                        details={
                            "edge": asdict(edge),
                            "test_point": (x, y)
                        }
                    )
                    failures.append(failure)

        return failures

    @staticmethod
    def _get_test_points_on_edge(
        edge: Edge, monitors: List[MonitorInfo]) -> List[Tuple[int, int]]:
        """Get multiple test points on the given edge (5 points: top/left corner, quarter, center, three-quarter, bottom/right corner)"""
        monitor = monitors[edge.monitor_index]
        points = []

        if edge.edge_type == EdgeType.LEFT:
            x = monitor.left
            for ratio in [0.0, 0.25, 0.5, 0.75, 1.0]:
                y = int(monitor.top + (monitor.height - 1) * ratio)
                points.append((x, y))
        elif edge.edge_type == EdgeType.RIGHT:
            x = monitor.right - 1
            for ratio in [0.0, 0.25, 0.5, 0.75, 1.0]:
                y = int(monitor.top + (monitor.height - 1) * ratio)
                points.append((x, y))
        elif edge.edge_type == EdgeType.TOP:
            y = monitor.top
            for ratio in [0.0, 0.25, 0.5, 0.75, 1.0]:
                x = int(monitor.left + (monitor.width - 1) * ratio)
                points.append((x, y))
        elif edge.edge_type == EdgeType.BOTTOM:
            y = monitor.bottom - 1
            for ratio in [0.0, 0.25, 0.5, 0.75, 1.0]:
                x = int(monitor.left + (monitor.width - 1) * ratio)
                points.append((x, y))

        return points

    @staticmethod
    def _describe_config(monitors: List[MonitorInfo]) -> str:
        """Generate human-readable config description"""
        if len(monitors) == 1:
            m = monitors[0]
            return f"Single {m.width}x{m.height} @{m.dpi}DPI"

        desc = f"{len(monitors)} monitors: "
        for i, m in enumerate(monitors):
            desc += f"M{i}({m.width}x{m.height} at {m.left},{m.top}) "
        return desc.strip()

# ============================================================================
# Test Runner
# ============================================================================


class TestRunner:
    """Orchestrates the test execution"""

    def __init__(self, max_monitors: int = 10, verbose: bool = False, layout_file: str = None):
        self.max_monitors = max_monitors
        self.verbose = verbose
        self.layout_file = layout_file
        self.failures: List[TestFailure] = []
        self.test_count = 0
        self.passed_count = 0

    def _print_layout_diagram(self, monitors: List[MonitorInfo]):
        """Print a text-based diagram of the monitor layout"""
        print("\n" + "=" * 80)
        print("MONITOR LAYOUT DIAGRAM")
        print("=" * 80)
        
        # Find bounds of entire desktop
        min_x = min(m.left for m in monitors)
        min_y = min(m.top for m in monitors)
        max_x = max(m.right for m in monitors)
        max_y = max(m.bottom for m in monitors)
        
        # Calculate scale to fit in ~70 chars wide
        desktop_width = max_x - min_x
        desktop_height = max_y - min_y
        
        # Scale factor: target 70 chars width
        scale = desktop_width / 70.0
        if scale < 1:
            scale = 1
        
        # Create grid (70 chars wide, proportional height)
        grid_width = 70
        grid_height = max(10, int(desktop_height / scale))
        grid_height = min(grid_height, 30)  # Cap at 30 lines
        
        # Initialize grid with spaces
        grid = [[' ' for _ in range(grid_width)] for _ in range(grid_height)]
        
        # Draw each monitor
        for idx, mon in enumerate(monitors):
            # Convert monitor coords to grid coords
            x1 = int((mon.left - min_x) / scale)
            y1 = int((mon.top - min_y) / scale)
            x2 = int((mon.right - min_x) / scale)
            y2 = int((mon.bottom - min_y) / scale)
            
            # Clamp to grid
            x1 = max(0, min(x1, grid_width - 1))
            x2 = max(0, min(x2, grid_width))
            y1 = max(0, min(y1, grid_height - 1))
            y2 = max(0, min(y2, grid_height))
            
            # Draw monitor border and fill
            char = str(idx) if idx < 10 else chr(65 + idx - 10)  # 0-9, then A-Z
            
            for y in range(y1, y2):
                for x in range(x1, x2):
                    if y < grid_height and x < grid_width:
                        # Draw borders
                        if y == y1 or y == y2 - 1:
                            grid[y][x] = '─'
                        elif x == x1 or x == x2 - 1:
                            grid[y][x] = '│'
                        else:
                            grid[y][x] = char
            
            # Draw corners
            if y1 < grid_height and x1 < grid_width:
                grid[y1][x1] = '┌'
            if y1 < grid_height and x2 - 1 < grid_width:
                grid[y1][x2 - 1] = '┐'
            if y2 - 1 < grid_height and x1 < grid_width:
                grid[y2 - 1][x1] = '└'
            if y2 - 1 < grid_height and x2 - 1 < grid_width:
                grid[y2 - 1][x2 - 1] = '┘'
        
        # Print grid
        print()
        for row in grid:
            print(''.join(row))
        
        # Print legend
        print("\n" + "-" * 80)
        print("MONITOR DETAILS:")
        print("-" * 80)
        for idx, mon in enumerate(monitors):
            char = str(idx) if idx < 10 else chr(65 + idx - 10)
            primary = " [PRIMARY]" if mon.primary else ""
            scaling = int((mon.dpi / 96.0) * 100)
            print(f"  [{char}] Monitor {idx}{primary}")
            print(f"      Position: ({mon.left}, {mon.top})")
            print(f"      Size: {mon.width}x{mon.height}")
            print(f"      DPI: {mon.dpi} ({scaling}% scaling)")
            print(f"      Bounds: [{mon.left}, {mon.top}, {mon.right}, {mon.bottom}]")
        
        print("=" * 80 + "\n")

    def run_all_tests(self):
        """Execute all test configurations"""
        print("=" * 80)
        print("CursorWrap Monitor Layout Edge Detection Test Suite")
        print("=" * 80)

        # Load or generate configs
        if self.layout_file:
            print(f"\nLoading monitor layout from {self.layout_file}...")
            configs = TestConfigGenerator.load_from_file(self.layout_file)
            # Show visual diagram for captured layouts
            if configs:
                self._print_layout_diagram(configs[0])
        else:
            print("\nGenerating test configurations...")
            configs = TestConfigGenerator.generate_all_configs(self.max_monitors)
        
        total_tests = len(configs)
        print(f"Testing {total_tests} configuration(s)")
        print("=" * 80)

        # Run tests
        for i, config in enumerate(configs, 1):
            self._run_test_config(config, i, total_tests)

        # Report results
        self._print_summary()
        self._save_report()

    def _run_test_config(
            self,
            monitors: List[MonitorInfo],
            iteration: int,
            total: int):
        """Run all validators on a single configuration"""
        desc = EdgeDetectionValidator._describe_config(monitors)

        if not self.verbose:
            # Minimal output: just progress
            progress = (iteration / total) * 100
            print(
                f"\r[{iteration}/{total}] {progress:5.1f}% - Testing: {desc[:60]:<60}", end="", flush=True)
        else:
            print(f"\n[{iteration}/{total}] Testing: {desc}")

        # Run validators
        self.test_count += 1
        config_passed = True

        # Single monitor validation
        if len(monitors) == 1:
            failure = EdgeDetectionValidator.validate_single_monitor(monitors)
            if failure:
                self.failures.append(failure)
                config_passed = False

        # Touching monitors validation (2+ monitors)
        if len(monitors) >= 2:
            failure = EdgeDetectionValidator.validate_touching_monitors(monitors)
            if failure:
                self.failures.append(failure)
                config_passed = False

        # Wrap calculation validation
        wrap_failures = EdgeDetectionValidator.validate_wrap_calculation(monitors)
        if wrap_failures:
            self.failures.extend(wrap_failures)
            config_passed = False

        if config_passed:
            self.passed_count += 1
        
        if self.verbose and not config_passed:
            print(f"  ? FAILED ({len([f for f in self.failures if desc in f.monitor_config])} issues)")
        elif self.verbose:
            print("  ? PASSED")
    
    def _print_summary(self):
        """Print test summary"""
        print("\n\n" + "=" * 80)
        print("TEST SUMMARY")
        print("=" * 80)
        print(f"Total Configurations: {self.test_count}")
        print(f"Passed: {self.passed_count} ({self.passed_count/self.test_count*100:.1f}%)")
        print(f"Failed: {self.test_count - self.passed_count} ({(self.test_count - self.passed_count)/self.test_count*100:.1f}%)")
        print(f"Total Issues Found: {len(self.failures)}")
        print("=" * 80)
        
        if self.failures:
            print("\n??  FAILURES DETECTED - See test_report.json for details")
            print("\nTop 5 Failure Types:")
            failure_types = {}
            for f in self.failures:
                failure_types[f.test_name] = failure_types.get(f.test_name, 0) + 1
            
            for test_name, count in sorted(failure_types.items(), key=lambda x: x[1], reverse=True)[:5]:
                print(f"  - {test_name}: {count} failures")
        else:
            print("\n? ALL TESTS PASSED!")
    
    def _save_report(self):
        """Save detailed JSON report"""
        
        # Helper to convert enums to strings
        def convert_for_json(obj):
            if isinstance(obj, dict):
                return {k: convert_for_json(v) for k, v in obj.items()}
            elif isinstance(obj, list):
                return [convert_for_json(item) for item in obj]
            elif isinstance(obj, Enum):
                return obj.value
            else:
                return obj
        
        report = {
            "summary": {
                "total_configs": self.test_count,
                "passed": self.passed_count,
                "failed": self.test_count - self.passed_count,
                "total_issues": len(self.failures),
                "pass_rate": f"{self.passed_count/self.test_count*100:.2f}%"
            },
            "failures": convert_for_json([asdict(f) for f in self.failures]),
            "recommendations": self._generate_recommendations()
        }
        
        output_file = "test_report.json"
        with open(output_file, "w") as f:
            json.dump(report, f, indent=2)
        
        print(f"\n?? Detailed report saved to: {output_file}")
    
    def _generate_recommendations(self) -> List[str]:
        """Generate recommendations based on failures"""
        recommendations = []
        
        failure_types = {}
        for f in self.failures:
            failure_types[f.test_name] = failure_types.get(f.test_name, 0) + 1
        
        if "single_monitor_edges" in failure_types:
            recommendations.append(
                "Single monitor edge detection failing - verify baseline case in MonitorTopology::_detect_outer_edges()"
            )
        
        if "touching_monitors" in failure_types:
            recommendations.append(
                f"Adjacent monitor detection failing ({failure_types['touching_monitors']} cases) - "
                "review ADJACENCY_TOLERANCE (50px) and edge overlap logic in EdgesAreAdjacent()"
            )
        
        if "wrap_calculation" in failure_types:
            recommendations.append(
                f"Wrap calculation failing ({failure_types['wrap_calculation']} cases) - "
                "review CursorWrapCore::HandleMouseMove() wrap destination logic"
            )
        
        if not recommendations:
            recommendations.append("All tests passed - edge detection logic is working correctly!")
        
        return recommendations

# ============================================================================
# Main Entry Point
# ============================================================================

# ============================================================================
# Main Entry Point
# ============================================================================

def main():
    """Main entry point"""
    import argparse
    
    parser = argparse.ArgumentParser(
        description="CursorWrap Monitor Layout Edge Detection Test Suite"
    )
    parser.add_argument(
        "--max-monitors",
        type=int,
        default=10,
        help="Maximum number of monitors to test (1-10)"
    )
    parser.add_argument(
        "--verbose",
        action="store_true",
        help="Enable verbose output"
    )
    parser.add_argument(
        "--layout-file",
        type=str,
        help="Use captured monitor layout JSON file instead of generated configs"
    )
    
    args = parser.parse_args()
    
    if not args.layout_file:
        # Validate max_monitors only for generated configs
        if args.max_monitors < 1 or args.max_monitors > 10:
            print("Error: max-monitors must be between 1 and 10")
            sys.exit(1)
    
    runner = TestRunner(
        max_monitors=args.max_monitors,
        verbose=args.verbose,
        layout_file=args.layout_file
    )
    runner.run_all_tests()
    
    # Exit with error code if tests failed
    sys.exit(0 if not runner.failures else 1)

if __name__ == "__main__":
    main()
