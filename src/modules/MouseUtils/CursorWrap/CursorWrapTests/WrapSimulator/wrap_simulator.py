#!/usr/bin/env python3
"""
CursorWrap Simulator - Visualizes monitor wrap edges based on PowerToys CursorWrap logic.

This tool loads a monitor layout JSON file and displays:
- Monitor rectangles scaled to fit the window
- Colored bars outside outer edges showing wrap destinations
- Problem areas where edges don't wrap to another location

Usage: python wrap_simulator.py <path_to_monitor_layout.json>
"""

import json
import sys
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
from dataclasses import dataclass, field
from enum import IntEnum
from typing import List, Optional, Tuple, Dict
import argparse
import csv
import re


class EdgeType(IntEnum):
    """Edge type enumeration matching C++ implementation."""
    LEFT = 0
    RIGHT = 1
    TOP = 2
    BOTTOM = 3


class WrapMode(IntEnum):
    """Wrap mode enumeration matching C++ implementation."""
    BOTH = 0
    VERTICAL_ONLY = 1
    HORIZONTAL_ONLY = 2


@dataclass
class MonitorInfo:
    """Monitor information structure matching C++ implementation."""
    left: int
    top: int
    right: int
    bottom: int
    width: int
    height: int
    dpi: int = 96
    scaling_percent: float = 100.0
    primary: bool = False
    device_name: str = ""
    monitor_id: int = 0

    @property
    def rect(self) -> Tuple[int, int, int, int]:
        return (self.left, self.top, self.right, self.bottom)


@dataclass
class MonitorEdge:
    """Represents a single edge of a monitor matching C++ implementation."""
    monitor_index: int
    edge_type: EdgeType
    start: int      # For vertical edges: Y start; horizontal: X start
    end: int        # For vertical edges: Y end; horizontal: X end
    position: int   # For vertical edges: X coord; horizontal: Y coord
    is_outer: bool = True

    def __hash__(self):
        return hash((self.monitor_index, self.edge_type, self.start, self.end, self.position))


class ProblemReason(IntEnum):
    """Reasons why a wrap destination might not exist."""
    NONE = 0                          # No problem - has wrap destination
    WRAP_MODE_DISABLED = 1            # Edge type disabled by current wrap mode
    NO_OPPOSITE_OUTER_EDGES = 2       # No outer edges of the opposite type exist
    NO_OVERLAPPING_RANGE = 3          # Opposite edges exist but don't overlap this range
    SINGLE_MONITOR = 4                # Only one monitor exists


@dataclass
class ProblemAnalysis:
    """Detailed analysis of why a wrap problem exists."""
    reason: ProblemReason
    description: str
    suggestion: str
    details: Dict = field(default_factory=dict)  # Additional diagnostic info


@dataclass
class EdgeSegment:
    """A segment of an edge with wrap destination info."""
    edge: MonitorEdge
    start: int
    end: int
    wraps_to: Optional[MonitorEdge] = None  # None means no wrap destination (problem area)
    problem_analysis: Optional[ProblemAnalysis] = None  # Analysis of why no wrap exists
    
    @property
    def has_wrap_destination(self) -> bool:
        return self.wraps_to is not None


@dataclass
class GapInfo:
    """Information about gaps between monitors."""
    monitor1_index: int
    monitor2_index: int
    horizontal_gap: int
    vertical_overlap: int


@dataclass
class CursorLogEntry:
    """A single cursor movement log entry."""
    display_name: str  # e.g., \\.\DISPLAY1
    x: int
    y: int
    dpi: int
    scaling_percent: float
    line_number: int = 0
    
    @classmethod
    def from_csv_line(cls, line: str, line_number: int = 0) -> Optional['CursorLogEntry']:
        """Parse a CSV line into a CursorLogEntry."""
        try:
            # Handle the format: \\.\DISPLAY1,1234,567,144,150%
            parts = line.strip().split(',')
            if len(parts) < 5:
                return None
            
            display_name = parts[0].strip()
            x = int(parts[1].strip())
            y = int(parts[2].strip())
            dpi = int(parts[3].strip())
            
            # Parse scaling - remove % if present
            scaling_str = parts[4].strip().rstrip('%')
            scaling = float(scaling_str)
            
            return cls(
                display_name=display_name,
                x=x,
                y=y,
                dpi=dpi,
                scaling_percent=scaling,
                line_number=line_number
            )
        except (ValueError, IndexError):
            return None


class MonitorTopology:
    """
    Monitor topology helper - manages edge-based monitor layout.
    This is a Python port of the C++ MonitorTopology class.
    """
    
    ADJACENCY_TOLERANCE = 50  # Matching C++ tolerance exactly
    
    def __init__(self):
        self.monitors: List[MonitorInfo] = []
        self.outer_edges: List[MonitorEdge] = []
        self.edge_map: Dict[Tuple[int, EdgeType], MonitorEdge] = {}
    
    def initialize(self, monitors: List[MonitorInfo]) -> None:
        """Initialize topology from monitor list."""
        self.monitors = monitors
        self.outer_edges.clear()
        self.edge_map.clear()
        
        if not monitors:
            return
        
        self._build_edge_map()
        self._identify_outer_edges()
    
    def _build_edge_map(self) -> None:
        """Create edges for each monitor using monitor index."""
        for idx, monitor in enumerate(self.monitors):
            # Left edge
            left_edge = MonitorEdge(
                monitor_index=idx,
                edge_type=EdgeType.LEFT,
                position=monitor.left,
                start=monitor.top,
                end=monitor.bottom,
                is_outer=True
            )
            self.edge_map[(idx, EdgeType.LEFT)] = left_edge
            
            # Right edge (position is right - 1 to match C++)
            right_edge = MonitorEdge(
                monitor_index=idx,
                edge_type=EdgeType.RIGHT,
                position=monitor.right - 1,
                start=monitor.top,
                end=monitor.bottom,
                is_outer=True
            )
            self.edge_map[(idx, EdgeType.RIGHT)] = right_edge
            
            # Top edge
            top_edge = MonitorEdge(
                monitor_index=idx,
                edge_type=EdgeType.TOP,
                position=monitor.top,
                start=monitor.left,
                end=monitor.right,
                is_outer=True
            )
            self.edge_map[(idx, EdgeType.TOP)] = top_edge
            
            # Bottom edge (position is bottom - 1 to match C++)
            bottom_edge = MonitorEdge(
                monitor_index=idx,
                edge_type=EdgeType.BOTTOM,
                position=monitor.bottom - 1,
                start=monitor.left,
                end=monitor.right,
                is_outer=True
            )
            self.edge_map[(idx, EdgeType.BOTTOM)] = bottom_edge
    
    def _identify_outer_edges(self) -> None:
        """Check each edge against all other edges to find adjacent ones."""
        # Make a copy of keys to iterate since we modify the dict values
        for key1 in list(self.edge_map.keys()):
            edge1 = self.edge_map[key1]
            
            for key2, edge2 in self.edge_map.items():
                if edge1.monitor_index == edge2.monitor_index:
                    continue  # Same monitor
                
                if self._edges_are_adjacent(edge1, edge2):
                    edge1.is_outer = False
                    break
            
            if edge1.is_outer:
                self.outer_edges.append(edge1)
    
    def _edges_are_adjacent(self, edge1: MonitorEdge, edge2: MonitorEdge) -> bool:
        """
        Check if two edges are adjacent (within tolerance).
        
        This matches the C++ EdgesAreAdjacent implementation exactly:
        - Edges must be opposite types (LEFT-RIGHT, RIGHT-LEFT, TOP-BOTTOM, BOTTOM-TOP)
        - Positions must be within ADJACENCY_TOLERANCE pixels
        - Perpendicular ranges must overlap by more than ADJACENCY_TOLERANCE
        """
        # Edges must be opposite types to be adjacent
        opposite_types = (
            (edge1.edge_type == EdgeType.LEFT and edge2.edge_type == EdgeType.RIGHT) or
            (edge1.edge_type == EdgeType.RIGHT and edge2.edge_type == EdgeType.LEFT) or
            (edge1.edge_type == EdgeType.TOP and edge2.edge_type == EdgeType.BOTTOM) or
            (edge1.edge_type == EdgeType.BOTTOM and edge2.edge_type == EdgeType.TOP)
        )
        
        if not opposite_types:
            return False
        
        # Check if positions are within tolerance
        # For adjacent edges, positions should be close (e.g., right edge of mon1 at x=2559
        # should be adjacent to left edge of mon2 at x=2560)
        if abs(edge1.position - edge2.position) > self.ADJACENCY_TOLERANCE:
            return False
        
        # Check if perpendicular ranges overlap significantly
        # (not just touching, but overlapping by more than tolerance)
        overlap_start = max(edge1.start, edge2.start)
        overlap_end = min(edge1.end, edge2.end)
        
        return overlap_end > overlap_start + self.ADJACENCY_TOLERANCE
    
    def find_opposite_outer_edge(self, from_edge: EdgeType, relative_position: int) -> Optional[MonitorEdge]:
        """Find the opposite outer edge for wrapping (original method - for overlapping regions)."""
        if from_edge == EdgeType.LEFT:
            target_type = EdgeType.RIGHT
            find_max = True
        elif from_edge == EdgeType.RIGHT:
            target_type = EdgeType.LEFT
            find_max = False
        elif from_edge == EdgeType.TOP:
            target_type = EdgeType.BOTTOM
            find_max = True
        elif from_edge == EdgeType.BOTTOM:
            target_type = EdgeType.TOP
            find_max = False
        else:
            return None
        
        result = None
        extreme_position = float('-inf') if find_max else float('inf')
        
        for edge in self.outer_edges:
            if edge.edge_type != target_type:
                continue
            
            # Check if this edge overlaps with the relative position
            if relative_position >= edge.start and relative_position <= edge.end:
                if (find_max and edge.position > extreme_position) or \
                   (not find_max and edge.position < extreme_position):
                    extreme_position = edge.position
                    result = edge
        
        return result
    
    def find_nearest_opposite_edge(self, from_edge_type: EdgeType, cursor_coordinate: int, 
                                    source_edge: MonitorEdge) -> Tuple[Optional[MonitorEdge], bool, int]:
        """
        Find the nearest opposite outer edge, including projection for non-overlapping regions.
        This implements Windows-like behavior for cursor transitions.
        
        Returns:
            Tuple of (target_edge, requires_projection, projected_coordinate)
            - target_edge: The destination edge, or None if not found
            - requires_projection: True if cursor position needs offset projection
            - projected_coordinate: The calculated coordinate on the target edge
        """
        # First, try to find an edge that directly overlaps
        direct_match = self.find_opposite_outer_edge(from_edge_type, cursor_coordinate)
        if direct_match is not None:
            return (direct_match, False, cursor_coordinate)
        
        # No direct overlap - find the nearest opposite edge by coordinate distance
        if from_edge_type == EdgeType.LEFT:
            target_type = EdgeType.RIGHT
            find_max = True
        elif from_edge_type == EdgeType.RIGHT:
            target_type = EdgeType.LEFT
            find_max = False
        elif from_edge_type == EdgeType.TOP:
            target_type = EdgeType.BOTTOM
            find_max = True
        elif from_edge_type == EdgeType.BOTTOM:
            target_type = EdgeType.TOP
            find_max = False
        else:
            return (None, False, 0)
        
        best_distance = float('inf')
        best_edge: Optional[MonitorEdge] = None
        best_projected_coord = 0
        
        for edge in self.outer_edges:
            if edge.edge_type != target_type:
                continue
            
            # Calculate distance from cursor coordinate to this edge's range
            if cursor_coordinate < edge.start:
                distance = edge.start - cursor_coordinate
                projected_coord = edge.start  # Clamp to edge start
            elif cursor_coordinate > edge.end:
                distance = cursor_coordinate - edge.end
                projected_coord = edge.end  # Clamp to edge end
            else:
                distance = 0
                projected_coord = cursor_coordinate
            
            # Choose the best edge: prefer closer edges, and among equals prefer extreme position
            is_better = False
            if distance < best_distance:
                is_better = True
            elif distance == best_distance and best_edge is not None:
                if (find_max and edge.position > best_edge.position) or \
                   (not find_max and edge.position < best_edge.position):
                    is_better = True
            
            if is_better:
                best_distance = distance
                best_edge = edge
                best_projected_coord = projected_coord
        
        if best_edge is not None:
            # Calculate projected position using offset-from-boundary approach
            projected = self._calculate_projected_position(cursor_coordinate, source_edge, best_edge)
            return (best_edge, True, projected)
        
        return (None, False, 0)
    
    def _calculate_projected_position(self, cursor_coordinate: int, source_edge: MonitorEdge,
                                       target_edge: MonitorEdge) -> int:
        """
        Calculate projected position for cursor in non-overlapping region.
        Uses offset-from-boundary approach similar to Windows cursor transitions.
        """
        # Find the shared boundary region between source and target edges
        shared_start = max(source_edge.start, target_edge.start)
        shared_end = min(source_edge.end, target_edge.end)
        
        if cursor_coordinate >= shared_start and cursor_coordinate <= shared_end:
            # Cursor is in shared region - return as-is
            return cursor_coordinate
        
        if cursor_coordinate < shared_start:
            # Cursor is BEFORE the shared region (e.g., above shared area)
            # Use offset from top of source non-shared region
            offset_from_source_top = cursor_coordinate - source_edge.start
            projected_coord = target_edge.start + offset_from_source_top
        else:
            # Cursor is AFTER the shared region (e.g., below shared area)
            # Use offset from bottom of source non-shared region
            offset_from_source_bottom = source_edge.end - cursor_coordinate
            projected_coord = target_edge.end - offset_from_source_bottom
        
        # Clamp to target edge bounds
        projected_coord = max(target_edge.start, min(projected_coord, target_edge.end))
        
        return projected_coord
    
    def get_wrap_destination_with_projection(self, from_edge_type: EdgeType, cursor_coordinate: int,
                                              source_edge: MonitorEdge) -> Tuple[Optional[MonitorEdge], int]:
        """
        Get the wrap destination for a cursor position, including non-overlapping regions.
        
        Returns:
            Tuple of (target_edge, target_coordinate)
        """
        target_edge, requires_projection, projected_coord = self.find_nearest_opposite_edge(
            from_edge_type, cursor_coordinate, source_edge)
        
        if target_edge is None:
            return (None, cursor_coordinate)
        
        return (target_edge, projected_coord)
    
    def get_edge_segments_with_wrap_info(self, edge: MonitorEdge, wrap_mode: WrapMode) -> List[EdgeSegment]:
        """
        Break an outer edge into segments based on wrap destinations.
        Each segment either wraps to a specific destination or has no wrap (problem area).
        
        NOTE: This uses the ORIGINAL algorithm that requires direct overlap.
        For the NEW algorithm with projection, use get_edge_segments_with_projection().
        """
        # Check if this edge type is allowed by wrap mode
        is_horizontal_edge = edge.edge_type in (EdgeType.LEFT, EdgeType.RIGHT)
        is_vertical_edge = edge.edge_type in (EdgeType.TOP, EdgeType.BOTTOM)
        
        if wrap_mode == WrapMode.VERTICAL_ONLY and is_horizontal_edge:
            # Horizontal edges (left/right) are disabled in vertical-only mode
            analysis = ProblemAnalysis(
                reason=ProblemReason.WRAP_MODE_DISABLED,
                description=f"Left/Right edges are disabled in Vertical Only wrap mode",
                suggestion="Change wrap mode to 'Both' or 'Horizontal Only' to enable this edge",
                details={"wrap_mode": wrap_mode.name, "edge_type": edge.edge_type.name}
            )
            return [EdgeSegment(edge=edge, start=edge.start, end=edge.end, wraps_to=None, problem_analysis=analysis)]
        
        if wrap_mode == WrapMode.HORIZONTAL_ONLY and is_vertical_edge:
            # Vertical edges (top/bottom) are disabled in horizontal-only mode
            analysis = ProblemAnalysis(
                reason=ProblemReason.WRAP_MODE_DISABLED,
                description=f"Top/Bottom edges are disabled in Horizontal Only wrap mode",
                suggestion="Change wrap mode to 'Both' or 'Vertical Only' to enable this edge",
                details={"wrap_mode": wrap_mode.name, "edge_type": edge.edge_type.name}
            )
            return [EdgeSegment(edge=edge, start=edge.start, end=edge.end, wraps_to=None, problem_analysis=analysis)]
        
        # Find all possible wrap destinations along this edge
        # Sample points along the edge to find where wrap destinations change
        segments: List[EdgeSegment] = []
        
        current_start = edge.start
        current_wrap = self.find_opposite_outer_edge(edge.edge_type, current_start)
        
        # Sample at 1-pixel intervals to find exact boundaries
        for pos in range(edge.start + 1, edge.end + 1):
            wrap_dest = self.find_opposite_outer_edge(edge.edge_type, pos)
            
            # Check if wrap destination changed
            if self._wrap_dest_differs(current_wrap, wrap_dest):
                # Close current segment
                problem_analysis = None if current_wrap else self._analyze_wrap_problem(edge, current_start, pos)
                segments.append(EdgeSegment(
                    edge=edge,
                    start=current_start,
                    end=pos,
                    wraps_to=current_wrap,
                    problem_analysis=problem_analysis
                ))
                current_start = pos
                current_wrap = wrap_dest
        
        # Close final segment
        problem_analysis = None if current_wrap else self._analyze_wrap_problem(edge, current_start, edge.end)
        segments.append(EdgeSegment(
            edge=edge,
            start=current_start,
            end=edge.end,
            wraps_to=current_wrap,
            problem_analysis=problem_analysis
        ))
        
        return segments
    
    def get_edge_segments_with_projection(self, edge: MonitorEdge, wrap_mode: WrapMode) -> List[EdgeSegment]:
        """
        Break an outer edge into segments based on wrap destinations using the NEW projection algorithm.
        This algorithm eliminates dead zones by projecting cursor positions to the nearest valid destination.
        
        Every point on an outer edge will have a valid wrap destination (no problem areas).
        """
        # Check if this edge type is allowed by wrap mode
        is_horizontal_edge = edge.edge_type in (EdgeType.LEFT, EdgeType.RIGHT)
        is_vertical_edge = edge.edge_type in (EdgeType.TOP, EdgeType.BOTTOM)
        
        if wrap_mode == WrapMode.VERTICAL_ONLY and is_horizontal_edge:
            analysis = ProblemAnalysis(
                reason=ProblemReason.WRAP_MODE_DISABLED,
                description=f"Left/Right edges are disabled in Vertical Only wrap mode",
                suggestion="Change wrap mode to 'Both' or 'Horizontal Only' to enable this edge",
                details={"wrap_mode": wrap_mode.name, "edge_type": edge.edge_type.name}
            )
            return [EdgeSegment(edge=edge, start=edge.start, end=edge.end, wraps_to=None, problem_analysis=analysis)]
        
        if wrap_mode == WrapMode.HORIZONTAL_ONLY and is_vertical_edge:
            analysis = ProblemAnalysis(
                reason=ProblemReason.WRAP_MODE_DISABLED,
                description=f"Top/Bottom edges are disabled in Horizontal Only wrap mode",
                suggestion="Change wrap mode to 'Both' or 'Vertical Only' to enable this edge",
                details={"wrap_mode": wrap_mode.name, "edge_type": edge.edge_type.name}
            )
            return [EdgeSegment(edge=edge, start=edge.start, end=edge.end, wraps_to=None, problem_analysis=analysis)]
        
        # With the new projection algorithm, find wrap destinations using find_nearest_opposite_edge
        segments: List[EdgeSegment] = []
        
        current_start = edge.start
        current_wrap, _, _ = self.find_nearest_opposite_edge(edge.edge_type, current_start, edge)
        
        # Sample at 1-pixel intervals to find exact boundaries
        for pos in range(edge.start + 1, edge.end + 1):
            wrap_dest, _, _ = self.find_nearest_opposite_edge(edge.edge_type, pos, edge)
            
            # Check if wrap destination changed
            if self._wrap_dest_differs(current_wrap, wrap_dest):
                # Close current segment - with projection, there should always be a destination
                segments.append(EdgeSegment(
                    edge=edge,
                    start=current_start,
                    end=pos,
                    wraps_to=current_wrap,
                    problem_analysis=None  # No problems with new algorithm
                ))
                current_start = pos
                current_wrap = wrap_dest
        
        # Close final segment
        segments.append(EdgeSegment(
            edge=edge,
            start=current_start,
            end=edge.end,
            wraps_to=current_wrap,
            problem_analysis=None
        ))
        
        return segments
    
    def validate_all_edges_have_destinations(self, wrap_mode: WrapMode = WrapMode.BOTH) -> Dict:
        """
        Validate that every point on every outer edge has a valid wrap destination.
        This is used to verify the new projection-based algorithm eliminates dead zones.
        
        Returns:
            Dict with validation results including any remaining problem areas
        """
        results = {
            "total_outer_edges": len(self.outer_edges),
            "total_edge_length": 0,
            "covered_length": 0,
            "uncovered_length": 0,
            "problem_areas": [],
            "is_fully_covered": True
        }
        
        for edge in self.outer_edges:
            edge_length = edge.end - edge.start
            results["total_edge_length"] += edge_length
            
            # Use the new projection algorithm
            segments = self.get_edge_segments_with_projection(edge, wrap_mode)
            
            for segment in segments:
                segment_length = segment.end - segment.start
                if segment.has_wrap_destination:
                    results["covered_length"] += segment_length
                else:
                    results["uncovered_length"] += segment_length
                    results["is_fully_covered"] = False
                    results["problem_areas"].append({
                        "monitor_index": edge.monitor_index,
                        "edge_type": edge.edge_type.name,
                        "range": (segment.start, segment.end),
                        "length": segment_length,
                        "reason": segment.problem_analysis.reason.name if segment.problem_analysis else "UNKNOWN"
                    })
        
        results["coverage_percent"] = (results["covered_length"] / results["total_edge_length"] * 100 
                                       if results["total_edge_length"] > 0 else 0)
        
        return results
    
    def _analyze_wrap_problem(self, edge: MonitorEdge, range_start: int, range_end: int) -> ProblemAnalysis:
        """
        Analyze why a wrap destination doesn't exist for a given edge range.
        This provides detailed diagnostic information for debugging.
        """
        # Determine the target edge type for wrapping
        target_type = {
            EdgeType.LEFT: EdgeType.RIGHT,
            EdgeType.RIGHT: EdgeType.LEFT,
            EdgeType.TOP: EdgeType.BOTTOM,
            EdgeType.BOTTOM: EdgeType.TOP
        }[edge.edge_type]
        
        # Check if only one monitor exists
        if len(self.monitors) <= 1:
            return ProblemAnalysis(
                reason=ProblemReason.SINGLE_MONITOR,
                description="Only one monitor exists - no wrap destinations possible",
                suggestion="Connect additional monitors for cursor wrapping to work",
                details={"monitor_count": len(self.monitors)}
            )
        
        # Find all opposite type outer edges
        opposite_edges = [e for e in self.outer_edges if e.edge_type == target_type]
        
        if not opposite_edges:
            # No opposite outer edges exist at all
            all_edges_of_type = [(idx, et) for (idx, et), e in self.edge_map.items() if et == target_type]
            inner_edges_info = []
            for idx, et in all_edges_of_type:
                e = self.edge_map[(idx, et)]
                if not e.is_outer:
                    # Find which monitor makes this edge inner
                    adjacent_monitor = self._find_adjacent_monitor(e)
                    inner_edges_info.append({
                        "monitor_index": idx,
                        "edge_position": e.position,
                        "range": (e.start, e.end),
                        "adjacent_to_monitor": adjacent_monitor
                    })
            
            return ProblemAnalysis(
                reason=ProblemReason.NO_OPPOSITE_OUTER_EDGES,
                description=f"No outer {target_type.name} edges exist in the monitor configuration",
                suggestion=f"All {target_type.name} edges are adjacent to other monitors. "
                          f"The cursor wraps from {edge.edge_type.name} to the furthest {target_type.name} outer edge, "
                          f"but none exist.",
                details={
                    "source_edge_type": edge.edge_type.name,
                    "target_edge_type": target_type.name,
                    "inner_edges_count": len(inner_edges_info),
                    "inner_edges": inner_edges_info
                }
            )
        
        # Opposite edges exist but don't overlap with this range
        # Analyze why there's no overlap
        range_mid = (range_start + range_end) // 2
        
        # Find the closest opposite edges and their ranges
        closest_edges = []
        for opp_edge in opposite_edges:
            # Calculate how far this edge's range is from our range
            if opp_edge.end < range_start:
                # Edge is entirely below/left of our range
                distance = range_start - opp_edge.end
                position = "below" if edge.edge_type in (EdgeType.LEFT, EdgeType.RIGHT) else "left of"
            elif opp_edge.start > range_end:
                # Edge is entirely above/right of our range
                distance = opp_edge.start - range_end
                position = "above" if edge.edge_type in (EdgeType.LEFT, EdgeType.RIGHT) else "right of"
            else:
                # Overlaps - shouldn't happen if we're in this function
                distance = 0
                position = "overlapping"
            
            closest_edges.append({
                "monitor_index": opp_edge.monitor_index,
                "monitor_name": self.monitors[opp_edge.monitor_index].device_name if opp_edge.monitor_index < len(self.monitors) else "Unknown",
                "edge_position": opp_edge.position,
                "edge_range": (opp_edge.start, opp_edge.end),
                "distance_to_segment": distance,
                "relative_position": position
            })
        
        closest_edges.sort(key=lambda x: x["distance_to_segment"])
        
        # Build a detailed description
        if closest_edges:
            nearest = closest_edges[0]
            description = (
                f"No {target_type.name} outer edge overlaps with the coordinate range [{range_start}, {range_end}]. "
                f"Nearest {target_type.name} edge is on Monitor {nearest['monitor_index']} ({nearest['monitor_name']}) "
                f"with range [{nearest['edge_range'][0]}, {nearest['edge_range'][1]}], "
                f"which is {nearest['distance_to_segment']}px {nearest['relative_position']} this segment."
            )
            
            # Calculate what range adjustment would fix this
            if nearest["relative_position"] in ("below", "left of"):
                suggestion = (
                    f"To fix: Either extend the segment's monitor downward/leftward by {nearest['distance_to_segment']}px, "
                    f"or move Monitor {nearest['monitor_index']} upward/rightward. "
                    f"Alternatively, add another monitor that covers this range."
                )
            elif nearest["relative_position"] in ("above", "right of"):
                suggestion = (
                    f"To fix: Either extend the segment's monitor upward/rightward by {nearest['distance_to_segment']}px, "
                    f"or move Monitor {nearest['monitor_index']} downward/leftward. "
                    f"Alternatively, add another monitor that covers this range."
                )
            else:
                suggestion = "Unexpected state - edge should overlap."
        else:
            description = f"No {target_type.name} outer edges found at all."
            suggestion = "Check monitor configuration for missing outer edges."
        
        return ProblemAnalysis(
            reason=ProblemReason.NO_OVERLAPPING_RANGE,
            description=description,
            suggestion=suggestion,
            details={
                "source_edge_type": edge.edge_type.name,
                "source_range": (range_start, range_end),
                "target_edge_type": target_type.name,
                "available_opposite_edges": closest_edges,
                "gap_to_nearest": closest_edges[0]["distance_to_segment"] if closest_edges else None
            }
        )
    
    def _find_adjacent_monitor(self, edge: MonitorEdge) -> Optional[int]:
        """Find which monitor makes this edge an inner edge (not outer)."""
        for (idx, et), other_edge in self.edge_map.items():
            if idx == edge.monitor_index:
                continue
            if self._edges_are_adjacent(edge, other_edge):
                return idx
        return None
    
    def _wrap_dest_differs(self, wrap1: Optional[MonitorEdge], wrap2: Optional[MonitorEdge]) -> bool:
        """Check if two wrap destinations are different."""
        if wrap1 is None and wrap2 is None:
            return False
        if wrap1 is None or wrap2 is None:
            return True
        return (wrap1.monitor_index != wrap2.monitor_index or 
                wrap1.edge_type != wrap2.edge_type)
    
    def detect_monitor_gaps(self) -> List[GapInfo]:
        """Detect gaps between monitors that should be snapped together."""
        gaps = []
        
        for i in range(len(self.monitors)):
            for j in range(i + 1, len(self.monitors)):
                m1 = self.monitors[i]
                m2 = self.monitors[j]
                
                # Check vertical overlap
                v_overlap_start = max(m1.top, m2.top)
                v_overlap_end = min(m1.bottom, m2.bottom)
                v_overlap = v_overlap_end - v_overlap_start
                
                if v_overlap <= 0:
                    continue
                
                # Check horizontal gap
                h_gap = min(abs(m1.right - m2.left), abs(m2.right - m1.left))
                
                if h_gap > self.ADJACENCY_TOLERANCE:
                    gaps.append(GapInfo(
                        monitor1_index=i,
                        monitor2_index=j,
                        horizontal_gap=h_gap,
                        vertical_overlap=v_overlap
                    ))
        
        return gaps


class WrapSimulatorApp:
    """Main application class for the CursorWrap Simulator."""
    
    # Colors for different edge states
    EDGE_COLORS = {
        EdgeType.LEFT: "#FF6B6B",    # Red
        EdgeType.RIGHT: "#4ECDC4",   # Teal
        EdgeType.TOP: "#45B7D1",     # Blue
        EdgeType.BOTTOM: "#96CEB4",  # Green
    }
    
    WRAP_DESTINATION_COLOR = "#FFD93D"  # Yellow for segments that wrap
    NO_WRAP_COLOR = "#FF0000"           # Bright red for problem areas (no wrap)
    MONITOR_FILL = "#2C3E50"            # Dark blue-gray
    MONITOR_OUTLINE = "#ECF0F1"         # Light gray
    PRIMARY_HIGHLIGHT = "#F39C12"       # Orange for primary monitor
    
    EDGE_BAR_WIDTH = 8  # Width of edge indicator bars
    
    def __init__(self, root: tk.Tk, json_path: Optional[str] = None):
        self.root = root
        self.root.title("CursorWrap Simulator")
        self.root.minsize(1024, 768)
        
        self.topology = MonitorTopology()
        self.monitors: List[MonitorInfo] = []
        self.wrap_mode = tk.IntVar(value=WrapMode.BOTH)
        self.loaded_data: Optional[dict] = None  # Store loaded JSON data
        
        # Scaling factors for display
        self.scale = 1.0
        self.offset_x = 0
        self.offset_y = 0
        
        # Track canvas item IDs for hover detection
        self.monitor_items: Dict[int, int] = {}  # canvas_id -> monitor_index
        self.edge_segment_items: List[Tuple[int, EdgeSegment]] = []  # (canvas_id, segment)
        self.highlight_items: List[int] = []  # IDs of temporary highlight elements
        
        # Cursor log playback state
        self.cursor_log: List[CursorLogEntry] = []
        self.playback_index = 0
        self.playback_running = False
        self.playback_speed = tk.IntVar(value=50)  # milliseconds between frames
        self.cursor_canvas_items: List[int] = []  # Canvas items for cursor visualization
        self.cursor_trail: List[Tuple[int, int]] = []  # Trail of recent positions
        self.max_trail_length = 50
        self.last_monitor_name: Optional[str] = None  # Track monitor transitions
        
        # Edge test simulation state
        self.edge_test_running = False
        self.edge_test_points: List[Tuple[MonitorEdge, int, int, int]] = []  # (edge, coord, dest_x, dest_y)
        self.edge_test_index = 0
        self.edge_test_canvas_items: List[int] = []
        self.edge_test_results: Dict = {}  # Store test results
        
        self._setup_ui()
        
        if json_path:
            self.load_json(json_path)
    
    def _setup_ui(self):
        """Set up the user interface."""
        # Main container
        main_frame = ttk.Frame(self.root)
        main_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        # Top toolbar
        toolbar = ttk.Frame(main_frame)
        toolbar.pack(fill=tk.X, pady=(0, 5))
        
        ttk.Button(toolbar, text="Load JSON", command=self._on_load_json).pack(side=tk.LEFT, padx=2)
        ttk.Button(toolbar, text="Show Summary", command=self._show_summary).pack(side=tk.LEFT, padx=2)
        ttk.Button(toolbar, text="Export Analysis", command=self._export_analysis).pack(side=tk.LEFT, padx=2)
        
        ttk.Separator(toolbar, orient=tk.VERTICAL).pack(side=tk.LEFT, fill=tk.Y, padx=10)
        
        # Cursor log playback controls
        ttk.Button(toolbar, text="Load Log", command=self._on_load_cursor_log).pack(side=tk.LEFT, padx=2)
        self.play_button = ttk.Button(toolbar, text="▶ Play", command=self._toggle_playback, state=tk.DISABLED)
        self.play_button.pack(side=tk.LEFT, padx=2)
        ttk.Button(toolbar, text="⏹ Stop", command=self._stop_playback).pack(side=tk.LEFT, padx=2)
        ttk.Button(toolbar, text="⏮ Reset", command=self._reset_playback).pack(side=tk.LEFT, padx=2)
        
        ttk.Label(toolbar, text="Speed:").pack(side=tk.LEFT, padx=(5, 2))
        speed_scale = ttk.Scale(toolbar, from_=10, to=500, variable=self.playback_speed, 
                                orient=tk.HORIZONTAL, length=80)
        speed_scale.pack(side=tk.LEFT, padx=2)
        ttk.Label(toolbar, text="(ms)").pack(side=tk.LEFT, padx=(0, 5))
        
        ttk.Separator(toolbar, orient=tk.VERTICAL).pack(side=tk.LEFT, fill=tk.Y, padx=10)
        
        # Edge Test controls (NEW)
        ttk.Button(toolbar, text="🧪 Test Edges", command=self._start_edge_test).pack(side=tk.LEFT, padx=2)
        self.edge_test_stop_btn = ttk.Button(toolbar, text="⏹ Stop Test", command=self._stop_edge_test, state=tk.DISABLED)
        self.edge_test_stop_btn.pack(side=tk.LEFT, padx=2)
        
        # Algorithm selection - triggers redraw when changed
        self.use_new_algorithm = tk.BooleanVar(value=True)
        ttk.Checkbutton(toolbar, text="New Algorithm", variable=self.use_new_algorithm, 
                        command=self._on_algorithm_change).pack(side=tk.LEFT, padx=5)
        
        ttk.Separator(toolbar, orient=tk.VERTICAL).pack(side=tk.LEFT, fill=tk.Y, padx=10)
        
        ttk.Label(toolbar, text="Wrap Mode:").pack(side=tk.LEFT, padx=(0, 5))
        
        modes = [("Both", WrapMode.BOTH), ("Vertical Only", WrapMode.VERTICAL_ONLY), 
                 ("Horizontal Only", WrapMode.HORIZONTAL_ONLY)]
        for text, mode in modes:
            ttk.Radiobutton(toolbar, text=text, variable=self.wrap_mode, 
                          value=mode, command=self._on_mode_change).pack(side=tk.LEFT, padx=2)
        
        # Content area with canvas and info panel
        content_frame = ttk.PanedWindow(main_frame, orient=tk.HORIZONTAL)
        content_frame.pack(fill=tk.BOTH, expand=True)
        
        # Canvas frame
        canvas_frame = ttk.Frame(content_frame)
        content_frame.add(canvas_frame, weight=3)
        
        self.canvas = tk.Canvas(canvas_frame, bg="#1a1a2e", highlightthickness=0)
        self.canvas.pack(fill=tk.BOTH, expand=True)
        
        # Info panel
        info_frame = ttk.Frame(content_frame, width=300)
        content_frame.add(info_frame, weight=1)
        
        # Info panel content
        ttk.Label(info_frame, text="Monitor Information", font=('TkDefaultFont', 10, 'bold')).pack(pady=5)
        
        self.info_text = tk.Text(info_frame, wrap=tk.WORD, font=('Consolas', 9), 
                                  bg='#2d2d2d', fg='#ffffff', height=20)
        self.info_text.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        # Legend
        legend_frame = ttk.LabelFrame(info_frame, text="Legend")
        legend_frame.pack(fill=tk.X, padx=5, pady=5)
        
        self._create_legend_item(legend_frame, self.EDGE_COLORS[EdgeType.LEFT], "Left Edge (outer)")
        self._create_legend_item(legend_frame, self.EDGE_COLORS[EdgeType.RIGHT], "Right Edge (outer)")
        self._create_legend_item(legend_frame, self.EDGE_COLORS[EdgeType.TOP], "Top Edge (outer)")
        self._create_legend_item(legend_frame, self.EDGE_COLORS[EdgeType.BOTTOM], "Bottom Edge (outer)")
        self._create_legend_item(legend_frame, self.WRAP_DESTINATION_COLOR, "Has Wrap Destination")
        self._create_legend_item(legend_frame, self.NO_WRAP_COLOR, "NO WRAP (Problem!)")
        
        # Playback status frame
        playback_frame = ttk.LabelFrame(info_frame, text="Cursor Log Playback")
        playback_frame.pack(fill=tk.X, padx=5, pady=5)
        
        self.playback_status_var = tk.StringVar(value="No log loaded")
        ttk.Label(playback_frame, textvariable=self.playback_status_var, 
                 font=('Consolas', 8)).pack(fill=tk.X, padx=5, pady=2)
        
        self.playback_position_var = tk.StringVar(value="Position: -")
        ttk.Label(playback_frame, textvariable=self.playback_position_var,
                 font=('Consolas', 8)).pack(fill=tk.X, padx=5, pady=2)
        
        self.playback_monitor_var = tk.StringVar(value="Monitor: -")
        ttk.Label(playback_frame, textvariable=self.playback_monitor_var,
                 font=('Consolas', 8)).pack(fill=tk.X, padx=5, pady=2)
        
        # Hover info label at bottom
        self.hover_info_var = tk.StringVar(value="Hover over edges to see wrap information")
        hover_label = ttk.Label(main_frame, textvariable=self.hover_info_var, 
                                font=('Consolas', 9), anchor='w')
        hover_label.pack(fill=tk.X, pady=(5, 0))
        
        # Bind events
        self.canvas.bind("<Configure>", self._on_canvas_resize)
        self.canvas.bind("<Motion>", self._on_mouse_move)
        self.canvas.bind("<Button-1>", self._on_click)
    
    def _create_legend_item(self, parent: ttk.Frame, color: str, text: str):
        """Create a legend item with color swatch and label."""
        frame = ttk.Frame(parent)
        frame.pack(fill=tk.X, padx=5, pady=2)
        
        swatch = tk.Canvas(frame, width=20, height=12, highlightthickness=1, 
                          highlightbackground='gray')
        swatch.create_rectangle(0, 0, 20, 12, fill=color, outline='')
        swatch.pack(side=tk.LEFT, padx=(0, 5))
        
        ttk.Label(frame, text=text, font=('TkDefaultFont', 8)).pack(side=tk.LEFT)
    
    def _on_load_json(self):
        """Handle load JSON button click."""
        path = filedialog.askopenfilename(
            title="Select Monitor Layout JSON",
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")]
        )
        if path:
            self.load_json(path)
    
    def _on_mode_change(self):
        """Handle wrap mode change."""
        self._redraw()
    
    def _on_algorithm_change(self):
        """Handle algorithm selection change - redraw to show updated wrap destinations."""
        self._redraw()
        algo_name = "NEW (projection)" if self.use_new_algorithm.get() else "OLD (overlap only)"
        self.hover_info_var.set(f"Algorithm changed to: {algo_name}")
    
    def _show_summary(self):
        """Show the monitor summary info panel."""
        if self.loaded_data:
            self._update_info_panel(self.loaded_data)
    
    def _export_analysis(self):
        """Export detailed problem analysis to a JSON file."""
        if not self.monitors:
            messagebox.showwarning("No Data", "Load a monitor layout first.")
            return
        
        wrap_mode = WrapMode(self.wrap_mode.get())
        
        # Build comprehensive analysis
        analysis = {
            "export_timestamp": str(self.loaded_data.get("captured_at", "unknown")),
            "wrap_mode": wrap_mode.name,
            "monitor_count": len(self.monitors),
            "monitors": [],
            "outer_edges": [],
            "problem_segments": [],
            "summary": {}
        }
        
        # Monitor details
        for i, mon in enumerate(self.monitors):
            analysis["monitors"].append({
                "index": i,
                "device_name": mon.device_name,
                "bounds": {"left": mon.left, "top": mon.top, "right": mon.right, "bottom": mon.bottom},
                "size": {"width": mon.width, "height": mon.height},
                "primary": mon.primary
            })
        
        # Outer edges
        for edge in self.topology.outer_edges:
            analysis["outer_edges"].append({
                "monitor_index": edge.monitor_index,
                "edge_type": edge.edge_type.name,
                "position": edge.position,
                "range": {"start": edge.start, "end": edge.end}
            })
        
        # Problem analysis
        total_problem_pixels = 0
        problem_count_by_reason = {}
        
        for edge in self.topology.outer_edges:
            segments = self.topology.get_edge_segments_with_wrap_info(edge, wrap_mode)
            for seg in segments:
                if not seg.has_wrap_destination:
                    total_problem_pixels += (seg.end - seg.start)
                    
                    problem_data = {
                        "source": {
                            "monitor_index": edge.monitor_index,
                            "monitor_name": self.monitors[edge.monitor_index].device_name,
                            "edge_type": edge.edge_type.name,
                            "edge_position": edge.position,
                            "segment_range": {"start": seg.start, "end": seg.end},
                            "segment_length_px": seg.end - seg.start
                        }
                    }
                    
                    if seg.problem_analysis:
                        pa = seg.problem_analysis
                        reason_name = pa.reason.name
                        problem_count_by_reason[reason_name] = problem_count_by_reason.get(reason_name, 0) + 1
                        
                        problem_data["analysis"] = {
                            "reason_code": reason_name,
                            "description": pa.description,
                            "suggestion": pa.suggestion,
                            "details": pa.details
                        }
                    
                    analysis["problem_segments"].append(problem_data)
        
        # Summary
        analysis["summary"] = {
            "total_outer_edges": len(self.topology.outer_edges),
            "total_problem_segments": len(analysis["problem_segments"]),
            "total_problem_pixels": total_problem_pixels,
            "problems_by_reason": problem_count_by_reason,
            "has_problems": len(analysis["problem_segments"]) > 0
        }
        
        # Save to file
        path = filedialog.asksaveasfilename(
            title="Export Problem Analysis",
            defaultextension=".json",
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")],
            initialfile="wrap_analysis.json"
        )
        
        if path:
            try:
                with open(path, 'w') as f:
                    json.dump(analysis, f, indent=2, default=str)
                messagebox.showinfo("Export Complete", f"Analysis exported to:\n{path}")
            except Exception as e:
                messagebox.showerror("Export Error", f"Failed to save: {e}")

    # =========================================================================
    # Cursor Log Playback Methods
    # =========================================================================
    
    def _on_load_cursor_log(self):
        """Load a cursor movement log file."""
        path = filedialog.askopenfilename(
            title="Select Cursor Movement Log",
            filetypes=[("CSV files", "*.csv"), ("Log files", "*.log"), ("Text files", "*.txt"), ("All files", "*.*")]
        )
        if path:
            self._load_cursor_log(path)
    
    def _load_cursor_log(self, path: str):
        """Parse and load a cursor log file."""
        try:
            self.cursor_log.clear()
            self.playback_index = 0
            
            with open(path, 'r') as f:
                for line_num, line in enumerate(f, 1):
                    line = line.strip()
                    if not line or line.startswith('#'):
                        continue  # Skip empty lines and comments
                    
                    entry = CursorLogEntry.from_csv_line(line, line_num)
                    if entry:
                        self.cursor_log.append(entry)
            
            if self.cursor_log:
                self.play_button.config(state=tk.NORMAL)
                self.playback_status_var.set(f"Loaded: {len(self.cursor_log)} positions")
                self.playback_position_var.set(f"Position: 0 / {len(self.cursor_log)}")
                self._clear_cursor_display()
                
                # Validate all positions if monitors are loaded
                validation_summary = self._validate_cursor_log()
                
                if validation_summary:
                    messagebox.showwarning("Log Validation Issues", 
                        f"Loaded {len(self.cursor_log)} positions from:\n{path}\n\n"
                        f"{validation_summary}")
                else:
                    messagebox.showinfo("Log Loaded", 
                        f"Loaded {len(self.cursor_log)} cursor positions from:\n{path}\n\n"
                        f"✓ All positions validated successfully.")
            else:
                messagebox.showwarning("No Data", "No valid cursor positions found in the file.")
                
        except Exception as e:
            messagebox.showerror("Load Error", f"Failed to load log: {e}")
    
    def _validate_cursor_log(self) -> str:
        """Validate all cursor log entries against monitor layout. Returns summary of issues."""
        if not self.monitors or not self.cursor_log:
            return ""
        
        issues = {
            'outside_all': [],      # Coordinates outside all monitors
            'wrong_monitor': [],    # Claimed monitor doesn't match actual
        }
        
        for i, entry in enumerate(self.cursor_log):
            is_valid, actual_monitor, claimed_monitor, msg = self._validate_cursor_position(entry)
            
            if not is_valid:
                if actual_monitor is None:
                    issues['outside_all'].append((i, entry))
                else:
                    issues['wrong_monitor'].append((i, entry, actual_monitor))
        
        # Build summary
        summary_parts = []
        
        if issues['outside_all']:
            summary_parts.append(f"⚠️ {len(issues['outside_all'])} positions OUTSIDE all monitors!")
            # Show first few examples
            for idx, entry in issues['outside_all'][:3]:
                summary_parts.append(f"   Line {entry.line_number}: ({entry.x}, {entry.y}) - {entry.display_name}")
            if len(issues['outside_all']) > 3:
                summary_parts.append(f"   ... and {len(issues['outside_all']) - 3} more")
        
        if issues['wrong_monitor']:
            summary_parts.append(f"⚠️ {len(issues['wrong_monitor'])} positions claim WRONG monitor!")
            for idx, entry, actual in issues['wrong_monitor'][:3]:
                summary_parts.append(f"   Line {entry.line_number}: claims {entry.display_name}, "
                                   f"actually in {actual.device_name}")
            if len(issues['wrong_monitor']) > 3:
                summary_parts.append(f"   ... and {len(issues['wrong_monitor']) - 3} more")
        
        return "\n".join(summary_parts)
    
    def _toggle_playback(self):
        """Toggle playback on/off."""
        if self.playback_running:
            self._pause_playback()
        else:
            self._start_playback()
    
    def _start_playback(self):
        """Start cursor log playback."""
        if not self.cursor_log:
            return
        
        if not self.monitors:
            messagebox.showwarning("No Monitors", "Load a monitor layout JSON first.")
            return
        
        self.playback_running = True
        self.play_button.config(text="⏸ Pause")
        self._playback_step()
    
    def _pause_playback(self):
        """Pause playback."""
        self.playback_running = False
        self.play_button.config(text="▶ Play")
    
    def _stop_playback(self):
        """Stop playback and reset."""
        self._pause_playback()
        self._reset_playback()
    
    def _reset_playback(self):
        """Reset playback to beginning."""
        self.playback_index = 0
        self.cursor_trail.clear()
        self.last_monitor_name = None
        self._clear_cursor_display()
        if self.cursor_log:
            self.playback_position_var.set(f"Position: 0 / {len(self.cursor_log)}")
        self.playback_monitor_var.set("Monitor: -")
    
    def _playback_step(self):
        """Execute one step of playback."""
        if not self.playback_running or self.playback_index >= len(self.cursor_log):
            self._pause_playback()
            if self.playback_index >= len(self.cursor_log):
                self.playback_status_var.set("Playback complete")
            return
        
        entry = self.cursor_log[self.playback_index]
        
        # Check for monitor transition
        monitor_changed = (self.last_monitor_name is not None and 
                          entry.display_name != self.last_monitor_name)
        
        # Update display
        self._draw_cursor_position(entry, monitor_changed)
        
        # Update status
        self.playback_position_var.set(f"Position: {self.playback_index + 1} / {len(self.cursor_log)}")
        self.playback_monitor_var.set(f"Monitor: {entry.display_name} ({entry.x}, {entry.y})")
        
        if monitor_changed:
            self.playback_status_var.set(f"⚡ TRANSITION: {self.last_monitor_name} → {entry.display_name}")
        else:
            self.playback_status_var.set(f"Playing... Line {entry.line_number}")
        
        self.last_monitor_name = entry.display_name
        self.playback_index += 1
        
        # Schedule next step
        # Slow down on monitor transitions for visibility
        delay = self.playback_speed.get()
        if monitor_changed:
            delay = max(delay * 3, 300)  # At least 300ms pause on transitions
        
        self.root.after(delay, self._playback_step)
    
    def _clear_cursor_display(self):
        """Clear cursor visualization from canvas."""
        for item_id in self.cursor_canvas_items:
            self.canvas.delete(item_id)
        self.cursor_canvas_items.clear()
    
    def _find_monitor_at_point(self, x: int, y: int) -> Optional[MonitorInfo]:
        """Find which monitor contains the given global coordinates."""
        for monitor in self.monitors:
            if (monitor.left <= x < monitor.right and 
                monitor.top <= y < monitor.bottom):
                return monitor
        return None
    
    def _validate_cursor_position(self, entry: CursorLogEntry) -> Tuple[bool, Optional[MonitorInfo], Optional[MonitorInfo], str]:
        """
        Validate cursor position against monitor layout.
        
        Returns:
            - is_valid: True if position is within a monitor
            - actual_monitor: The monitor the coordinates are actually in (or None)
            - claimed_monitor: The monitor the log claims (based on display_name)
            - message: Explanation of any issues
        """
        # Find the monitor the log claims
        claimed_monitor = None
        for monitor in self.monitors:
            if monitor.device_name == entry.display_name.replace('\\\\.\\', ''):
                claimed_monitor = monitor
                break
        
        # Find the monitor the coordinates are actually in
        actual_monitor = self._find_monitor_at_point(entry.x, entry.y)
        
        # Build validation message
        if actual_monitor is None:
            # Coordinates outside all monitors!
            closest_monitor, distance, direction = self._find_closest_monitor(entry.x, entry.y)
            message = (f"⚠️ INVALID: ({entry.x}, {entry.y}) is OUTSIDE all monitors!\n"
                      f"   Claimed: {entry.display_name}\n"
                      f"   Nearest: {closest_monitor.device_name if closest_monitor else 'None'} "
                      f"({distance}px {direction})")
            return False, None, claimed_monitor, message
        
        elif claimed_monitor and actual_monitor.device_name != claimed_monitor.device_name:
            # Coordinates are in a different monitor than claimed
            message = (f"⚠️ MISMATCH: Log claims {entry.display_name} but\n"
                      f"   ({entry.x}, {entry.y}) is actually in {actual_monitor.device_name}")
            return False, actual_monitor, claimed_monitor, message
        
        else:
            # Valid
            return True, actual_monitor, claimed_monitor, ""
    
    def _find_closest_monitor(self, x: int, y: int) -> Tuple[Optional[MonitorInfo], int, str]:
        """Find the closest monitor to the given point and the distance/direction."""
        if not self.monitors:
            return None, 0, ""
        
        closest = None
        min_distance = float('inf')
        direction = ""
        
        for monitor in self.monitors:
            # Calculate distance to monitor edges
            dx = 0
            dy = 0
            dir_parts = []
            
            if x < monitor.left:
                dx = monitor.left - x
                dir_parts.append("left")
            elif x >= monitor.right:
                dx = x - monitor.right + 1
                dir_parts.append("right")
            
            if y < monitor.top:
                dy = monitor.top - y
                dir_parts.append("above")
            elif y >= monitor.bottom:
                dy = y - monitor.bottom + 1
                dir_parts.append("below")
            
            distance = (dx * dx + dy * dy) ** 0.5
            
            if distance < min_distance:
                min_distance = distance
                closest = monitor
                direction = " and ".join(dir_parts) if dir_parts else "inside"
        
        return closest, int(min_distance), direction

    def _draw_cursor_position(self, entry: CursorLogEntry, is_transition: bool = False):
        """Draw cursor position on canvas."""
        # Clear old cursor items (keep trail)
        self._clear_cursor_display()
        
        # Validate cursor position
        is_valid, actual_monitor, claimed_monitor, validation_msg = self._validate_cursor_position(entry)
        
        # Convert screen coordinates to canvas coordinates
        canvas_x, canvas_y = self._screen_to_canvas(entry.x, entry.y)
        
        # Add to trail
        self.cursor_trail.append((canvas_x, canvas_y))
        if len(self.cursor_trail) > self.max_trail_length:
            self.cursor_trail.pop(0)
        
        # Draw trail (fading effect)
        if len(self.cursor_trail) > 1:
            for i in range(len(self.cursor_trail) - 1):
                # Calculate opacity based on position in trail
                alpha = int(255 * (i + 1) / len(self.cursor_trail))
                # Approximate with discrete colors
                intensity = int(100 + 155 * (i + 1) / len(self.cursor_trail))
                
                # Use orange trail for invalid positions
                if not is_valid:
                    color = f"#ff{intensity:02x}00"  # Orange gradient
                else:
                    color = f"#{intensity:02x}{intensity:02x}ff"  # Blue gradient
                
                x1, y1 = self.cursor_trail[i]
                x2, y2 = self.cursor_trail[i + 1]
                
                line_id = self.canvas.create_line(
                    x1, y1, x2, y2,
                    fill=color,
                    width=2,
                    smooth=True
                )
                self.cursor_canvas_items.append(line_id)
        
        # Determine cursor color based on validity and transition
        if not is_valid:
            cursor_color = '#FF8C00'  # Orange for invalid/outside positions
            # Draw warning indicator
            for r in [25, 20, 15]:
                warn_id = self.canvas.create_oval(
                    canvas_x - r, canvas_y - r,
                    canvas_x + r, canvas_y + r,
                    outline='#FF8C00',
                    width=2,
                    dash=(3, 3)
                )
                self.cursor_canvas_items.append(warn_id)
            
            # Draw X mark for invalid
            x_size = 12
            x1_id = self.canvas.create_line(
                canvas_x - x_size, canvas_y - x_size,
                canvas_x + x_size, canvas_y + x_size,
                fill='#FF0000', width=3
            )
            x2_id = self.canvas.create_line(
                canvas_x - x_size, canvas_y + x_size,
                canvas_x + x_size, canvas_y - x_size,
                fill='#FF0000', width=3
            )
            self.cursor_canvas_items.append(x1_id)
            self.cursor_canvas_items.append(x2_id)
            
            # Update status with validation message
            self.playback_status_var.set(validation_msg.split('\n')[0])
            
        elif is_transition:
            cursor_color = '#FF6B6B'  # Red for transitions
            # Highlight transitions with a larger, different colored marker
            # Draw "burst" effect
            for r in [20, 15, 10]:
                burst_id = self.canvas.create_oval(
                    canvas_x - r, canvas_y - r,
                    canvas_x + r, canvas_y + r,
                    outline='#FF6B6B',
                    width=2
                )
                self.cursor_canvas_items.append(burst_id)
            
            # Draw transition line from previous position
            if len(self.cursor_trail) >= 2:
                prev_x, prev_y = self.cursor_trail[-2]
                line_id = self.canvas.create_line(
                    prev_x, prev_y, canvas_x, canvas_y,
                    fill='#FF6B6B',
                    width=3,
                    dash=(5, 3),
                    arrow=tk.LAST,
                    arrowshape=(12, 15, 5)
                )
                self.cursor_canvas_items.append(line_id)
        else:
            cursor_color = '#00FF00'  # Green for normal movement
        
        # Draw current cursor position
        cursor_size = 8
        
        # Draw cursor dot
        cursor_id = self.canvas.create_oval(
            canvas_x - cursor_size, canvas_y - cursor_size,
            canvas_x + cursor_size, canvas_y + cursor_size,
            fill=cursor_color,
            outline='white',
            width=2
        )
        self.cursor_canvas_items.append(cursor_id)
        
        # Draw crosshair
        crosshair_size = 15
        h_line = self.canvas.create_line(
            canvas_x - crosshair_size, canvas_y,
            canvas_x + crosshair_size, canvas_y,
            fill='white',
            width=1
        )
        v_line = self.canvas.create_line(
            canvas_x, canvas_y - crosshair_size,
            canvas_x, canvas_y + crosshair_size,
            fill='white',
            width=1
        )
        self.cursor_canvas_items.append(h_line)
        self.cursor_canvas_items.append(v_line)
        
        # Draw coordinate label
        label_text = f"({entry.x}, {entry.y})"
        label_id = self.canvas.create_text(
            canvas_x + 15, canvas_y - 15,
            text=label_text,
            fill='white',
            font=('Consolas', 8),
            anchor='sw'
        )
        self.cursor_canvas_items.append(label_id)
        
        # If transition, also show the monitor names
        if is_transition and self.last_monitor_name:
            transition_label = f"{self.last_monitor_name} → {entry.display_name}"
            trans_label_id = self.canvas.create_text(
                canvas_x + 15, canvas_y + 15,
                text=transition_label,
                fill='#FF6B6B',
                font=('Consolas', 9, 'bold'),
                anchor='nw'
            )
            self.cursor_canvas_items.append(trans_label_id)

    def _on_canvas_resize(self, event):
        """Handle canvas resize."""
        self._redraw()
    
    def _on_mouse_move(self, event):
        """Handle mouse movement for hover information."""
        # Clear previous highlights
        for item_id in self.highlight_items:
            self.canvas.delete(item_id)
        self.highlight_items.clear()
        
        # Find what's under the cursor
        items = self.canvas.find_overlapping(event.x - 2, event.y - 2, 
                                              event.x + 2, event.y + 2)
        
        info_lines = []
        
        # Check edge segments
        for item_id, segment in self.edge_segment_items:
            if item_id in items:
                edge = segment.edge
                monitor = self.monitors[edge.monitor_index]
                
                info_lines.append(f"Edge Segment:")
                info_lines.append(f"  Monitor: {edge.monitor_index} ({monitor.device_name})")
                info_lines.append(f"  Edge: {edge.edge_type.name}")
                info_lines.append(f"  Range: {segment.start} to {segment.end}")
                info_lines.append(f"  Position: {edge.position}")
                
                if segment.wraps_to:
                    dest = segment.wraps_to
                    dest_monitor = self.monitors[dest.monitor_index]
                    info_lines.append(f"  Wraps To:")
                    info_lines.append(f"    Monitor: {dest.monitor_index} ({dest_monitor.device_name})")
                    info_lines.append(f"    Edge: {dest.edge_type.name}")
                    info_lines.append(f"    Position: {dest.position}")
                    
                    # Highlight the destination edge
                    self._highlight_destination(segment, dest)
                else:
                    info_lines.append(f"  ⚠️ NO WRAP DESTINATION!")
                
                info_lines.append("")
        
        # Check monitors
        for item_id, mon_idx in self.monitor_items.items():
            if item_id in items:
                monitor = self.monitors[mon_idx]
                info_lines.append(f"Monitor {mon_idx}:")
                info_lines.append(f"  Device: {monitor.device_name}")
                info_lines.append(f"  Size: {monitor.width} x {monitor.height}")
                info_lines.append(f"  Position: ({monitor.left}, {monitor.top})")
                info_lines.append(f"  Bounds: ({monitor.left}, {monitor.top}) to ({monitor.right}, {monitor.bottom})")
                info_lines.append(f"  DPI: {monitor.dpi} ({monitor.scaling_percent}%)")
                info_lines.append(f"  Primary: {'Yes' if monitor.primary else 'No'}")
                info_lines.append("")
        
        if info_lines:
            self.hover_info_var.set(" | ".join(info_lines[:3]))  # Show first 3 lines in status bar
        else:
            self.hover_info_var.set("Hover over edges or monitors for information")
    
    def _on_click(self, event):
        """Handle mouse click to show detailed information."""
        items = self.canvas.find_overlapping(event.x - 2, event.y - 2, 
                                              event.x + 2, event.y + 2)
        
        # Check edge segments
        for item_id, segment in self.edge_segment_items:
            if item_id in items:
                self._show_segment_detail(segment)
                return
    
    def _show_segment_detail(self, segment: EdgeSegment):
        """Show detailed information about a segment in a popup."""
        edge = segment.edge
        monitor = self.monitors[edge.monitor_index]
        
        info = []
        info.append("=" * 45)
        info.append("EDGE SEGMENT DETAIL")
        info.append("=" * 45)
        info.append("")
        info.append(f"Source Monitor: {edge.monitor_index} ({monitor.device_name})")
        info.append(f"Edge Type: {edge.edge_type.name}")
        info.append(f"Edge Position: {edge.position}")
        info.append(f"Segment Range: {segment.start} to {segment.end}")
        info.append(f"Segment Length: {segment.end - segment.start} pixels")
        info.append("")
        
        if segment.wraps_to:
            dest = segment.wraps_to
            dest_monitor = self.monitors[dest.monitor_index]
            info.append("✓ WRAP DESTINATION EXISTS")
            info.append("")
            info.append(f"Destination Monitor: {dest.monitor_index} ({dest_monitor.device_name})")
            info.append(f"Destination Edge: {dest.edge_type.name}")
            info.append(f"Destination Position: {dest.position}")
            info.append(f"Destination Range: [{dest.start}, {dest.end}]")
        else:
            info.append("⚠️ NO WRAP DESTINATION - PROBLEM AREA!")
            info.append("")
            wrap_mode = WrapMode(self.wrap_mode.get())
            info.append(f"Current Wrap Mode: {wrap_mode.name}")
            info.append("")
            
            # Show detailed problem analysis if available
            if segment.problem_analysis:
                pa = segment.problem_analysis
                info.append("-" * 45)
                info.append("PROBLEM ANALYSIS")
                info.append("-" * 45)
                info.append("")
                info.append(f"Reason Code: {pa.reason.name}")
                info.append("")
                info.append("Description:")
                # Word wrap the description
                desc_words = pa.description.split()
                line = "  "
                for word in desc_words:
                    if len(line) + len(word) > 43:
                        info.append(line)
                        line = "  "
                    line += word + " "
                if line.strip():
                    info.append(line)
                
                info.append("")
                info.append("Suggested Fix:")
                # Word wrap the suggestion
                sug_words = pa.suggestion.split()
                line = "  "
                for word in sug_words:
                    if len(line) + len(word) > 43:
                        info.append(line)
                        line = "  "
                    line += word + " "
                if line.strip():
                    info.append(line)
                
                # Show additional diagnostic details
                if pa.details:
                    info.append("")
                    info.append("-" * 45)
                    info.append("DIAGNOSTIC DETAILS")
                    info.append("-" * 45)
                    
                    if "gap_to_nearest" in pa.details and pa.details["gap_to_nearest"]:
                        info.append(f"Gap to nearest edge: {pa.details['gap_to_nearest']}px")
                    
                    if "source_range" in pa.details:
                        sr = pa.details["source_range"]
                        info.append(f"Source range: [{sr[0]}, {sr[1]}]")
                    
                    if "available_opposite_edges" in pa.details:
                        opp_edges = pa.details["available_opposite_edges"]
                        if opp_edges:
                            info.append("")
                            info.append("Opposite edges (sorted by distance):")
                            for i, oe in enumerate(opp_edges[:5]):  # Show top 5
                                info.append(f"  {i+1}. Mon {oe['monitor_index']} ({oe['monitor_name']})")
                                info.append(f"     Range: [{oe['edge_range'][0]}, {oe['edge_range'][1]}]")
                                info.append(f"     Distance: {oe['distance_to_segment']}px {oe['relative_position']}")
                        else:
                            info.append("No opposite outer edges available!")
                    
                    if "inner_edges" in pa.details and pa.details["inner_edges"]:
                        info.append("")
                        info.append("Inner edges (not available for wrap):")
                        for ie in pa.details["inner_edges"]:
                            info.append(f"  Mon {ie['monitor_index']}: "
                                      f"adjacent to Mon {ie['adjacent_to_monitor']}")
            else:
                # Fallback to old logic if no analysis
                is_horizontal_edge = edge.edge_type in (EdgeType.LEFT, EdgeType.RIGHT)
                if wrap_mode == WrapMode.VERTICAL_ONLY and is_horizontal_edge:
                    info.append("Reason: Left/Right edges disabled in Vertical Only mode")
                elif wrap_mode == WrapMode.HORIZONTAL_ONLY and not is_horizontal_edge:
                    info.append("Reason: Top/Bottom edges disabled in Horizontal Only mode")
                else:
                    info.append("Reason: No opposite outer edge found for this range")
                    info.append("")
                    info.append("This means cursor movement at this edge won't wrap")
                    info.append("to another monitor - it will stop at the edge.")
        
        info.append("")
        info.append("=" * 45)
        
        # Update info text widget
        self.info_text.delete(1.0, tk.END)
        self.info_text.insert(tk.END, "\n".join(info))
    
    def _highlight_destination(self, source_segment: EdgeSegment, dest_edge: MonitorEdge):
        """Draw highlight showing the wrap destination for a hovered segment."""
        bar_width = self.EDGE_BAR_WIDTH * 1.5
        
        # Calculate the destination range that corresponds to this source segment
        # Use relative positioning like the C++ code
        source_edge = source_segment.edge
        
        # For the highlight, we show the full destination edge range that overlaps
        # with the source segment range
        dest_start = max(dest_edge.start, source_segment.start)
        dest_end = min(dest_edge.end, source_segment.end)
        
        # Calculate canvas position for destination edge
        if dest_edge.edge_type == EdgeType.LEFT:
            x1, y1 = self._screen_to_canvas(dest_edge.position - bar_width / self.scale, dest_start)
            x2, y2 = self._screen_to_canvas(dest_edge.position, dest_end)
            x1 -= bar_width / 2
        elif dest_edge.edge_type == EdgeType.RIGHT:
            x1, y1 = self._screen_to_canvas(dest_edge.position + 1, dest_start)
            x2, y2 = self._screen_to_canvas(dest_edge.position + 1 + bar_width / self.scale, dest_end)
            x2 += bar_width / 2
        elif dest_edge.edge_type == EdgeType.TOP:
            x1, y1 = self._screen_to_canvas(dest_start, dest_edge.position - bar_width / self.scale)
            x2, y2 = self._screen_to_canvas(dest_end, dest_edge.position)
            y1 -= bar_width / 2
        elif dest_edge.edge_type == EdgeType.BOTTOM:
            x1, y1 = self._screen_to_canvas(dest_start, dest_edge.position + 1)
            x2, y2 = self._screen_to_canvas(dest_end, dest_edge.position + 1 + bar_width / self.scale)
            y2 += bar_width / 2
        
        # Draw highlight rectangle
        highlight_id = self.canvas.create_rectangle(
            x1 - 2, y1 - 2, x2 + 2, y2 + 2,
            outline='#00FF00',  # Bright green
            width=3,
            dash=(5, 3)
        )
        self.highlight_items.append(highlight_id)
        
        # Draw arrow from source to destination
        src_center = self._get_segment_center(source_segment)
        dst_center = self._get_edge_center(dest_edge, dest_start, dest_end)
        
        arrow_id = self.canvas.create_line(
            src_center[0], src_center[1],
            dst_center[0], dst_center[1],
            fill='#00FF00',
            width=2,
            arrow=tk.LAST,
            arrowshape=(12, 15, 5),
            dash=(8, 4)
        )
        self.highlight_items.append(arrow_id)
    
    def _get_segment_center(self, segment: EdgeSegment) -> Tuple[float, float]:
        """Get canvas center coordinates of an edge segment."""
        edge = segment.edge
        mid = (segment.start + segment.end) / 2
        
        if edge.edge_type == EdgeType.LEFT:
            return self._screen_to_canvas(edge.position - self.EDGE_BAR_WIDTH / self.scale / 2, mid)
        elif edge.edge_type == EdgeType.RIGHT:
            return self._screen_to_canvas(edge.position + 1 + self.EDGE_BAR_WIDTH / self.scale / 2, mid)
        elif edge.edge_type == EdgeType.TOP:
            return self._screen_to_canvas(mid, edge.position - self.EDGE_BAR_WIDTH / self.scale / 2)
        else:  # BOTTOM
            return self._screen_to_canvas(mid, edge.position + 1 + self.EDGE_BAR_WIDTH / self.scale / 2)
    
    def _get_edge_center(self, edge: MonitorEdge, start: int, end: int) -> Tuple[float, float]:
        """Get canvas center coordinates of an edge range."""
        mid = (start + end) / 2
        
        if edge.edge_type == EdgeType.LEFT:
            return self._screen_to_canvas(edge.position - self.EDGE_BAR_WIDTH / self.scale / 2, mid)
        elif edge.edge_type == EdgeType.RIGHT:
            return self._screen_to_canvas(edge.position + 1 + self.EDGE_BAR_WIDTH / self.scale / 2, mid)
        elif edge.edge_type == EdgeType.TOP:
            return self._screen_to_canvas(mid, edge.position - self.EDGE_BAR_WIDTH / self.scale / 2)
        else:  # BOTTOM
            return self._screen_to_canvas(mid, edge.position + 1 + self.EDGE_BAR_WIDTH / self.scale / 2)
    
    def load_json(self, path: str):
        """Load monitor layout from JSON file."""
        try:
            with open(path, 'r') as f:
                data = json.load(f)
            
            self.monitors = []
            for i, mon_data in enumerate(data.get('monitors', [])):
                monitor = MonitorInfo(
                    left=mon_data.get('left', 0),
                    top=mon_data.get('top', 0),
                    right=mon_data.get('right', 0),
                    bottom=mon_data.get('bottom', 0),
                    width=mon_data.get('width', 0),
                    height=mon_data.get('height', 0),
                    dpi=mon_data.get('dpi', 96),
                    scaling_percent=mon_data.get('scaling_percent', 100.0),
                    primary=mon_data.get('primary', False),
                    device_name=mon_data.get('device_name', f'DISPLAY{i+1}'),
                    monitor_id=i
                )
                self.monitors.append(monitor)
            
            self.topology.initialize(self.monitors)
            self.loaded_data = data
            self._update_info_panel(data)
            self._redraw()
            
            self.root.title(f"CursorWrap Simulator - {path}")
            
        except Exception as e:
            messagebox.showerror("Error", f"Failed to load JSON: {e}")
    
    def _update_info_panel(self, data: dict):
        """Update the info panel with loaded data."""
        self.info_text.delete(1.0, tk.END)
        
        lines = []
        lines.append(f"Captured: {data.get('captured_at', 'Unknown')}")
        lines.append(f"Computer: {data.get('computer_name', 'Unknown')}")
        lines.append(f"Monitor Count: {data.get('monitor_count', len(self.monitors))}")
        lines.append("")
        lines.append("=" * 40)
        lines.append("")
        
        for i, monitor in enumerate(self.monitors):
            lines.append(f"Monitor {i}: {monitor.device_name}")
            lines.append(f"  Size: {monitor.width} x {monitor.height}")
            lines.append(f"  Position: ({monitor.left}, {monitor.top})")
            lines.append(f"  DPI: {monitor.dpi} ({monitor.scaling_percent}%)")
            lines.append(f"  Primary: {'Yes' if monitor.primary else 'No'}")
            lines.append("")
        
        lines.append("=" * 40)
        lines.append("")
        lines.append(f"Outer Edges: {len(self.topology.outer_edges)}")
        
        for edge in self.topology.outer_edges:
            lines.append(f"  Mon {edge.monitor_index} {edge.edge_type.name}: "
                        f"pos={edge.position}, range=[{edge.start}, {edge.end}]")
        
        # Check for problem areas (segments with no wrap destination)
        lines.append("")
        lines.append("=" * 40)
        lines.append("WRAP ANALYSIS")
        lines.append("=" * 40)
        
        wrap_mode = WrapMode(self.wrap_mode.get())
        problem_segments = []
        total_problem_pixels = 0
        
        for edge in self.topology.outer_edges:
            segments = self.topology.get_edge_segments_with_wrap_info(edge, wrap_mode)
            for seg in segments:
                if not seg.has_wrap_destination:
                    problem_segments.append(seg)
                    total_problem_pixels += (seg.end - seg.start)
        
        if problem_segments:
            lines.append("")
            lines.append(f"⚠️ PROBLEM AREAS: {len(problem_segments)} segments")
            lines.append(f"   Total: {total_problem_pixels} pixels without wrap")
            
            # Group by problem reason
            by_reason: Dict[ProblemReason, List[EdgeSegment]] = {}
            for seg in problem_segments:
                reason = seg.problem_analysis.reason if seg.problem_analysis else ProblemReason.NONE
                if reason not in by_reason:
                    by_reason[reason] = []
                by_reason[reason].append(seg)
            
            lines.append("")
            lines.append("-" * 40)
            
            for reason, segs in by_reason.items():
                lines.append("")
                lines.append(f"▸ {reason.name} ({len(segs)} segments)")
                lines.append("")
                
                for seg in segs:
                    edge = seg.edge
                    mon = self.monitors[edge.monitor_index]
                    lines.append(f"  • Mon {edge.monitor_index} ({mon.device_name}) {edge.edge_type.name}")
                    lines.append(f"    Range: {seg.start} to {seg.end} ({seg.end - seg.start}px)")
                    
                    if seg.problem_analysis:
                        pa = seg.problem_analysis
                        lines.append(f"")
                        lines.append(f"    CAUSE: {pa.description}")
                        lines.append(f"")
                        lines.append(f"    FIX: {pa.suggestion}")
                        
                        # Show relevant details
                        if pa.details:
                            if "gap_to_nearest" in pa.details and pa.details["gap_to_nearest"]:
                                lines.append(f"    Gap to nearest valid edge: {pa.details['gap_to_nearest']}px")
                            
                            if "available_opposite_edges" in pa.details:
                                opp_edges = pa.details["available_opposite_edges"]
                                if opp_edges:
                                    lines.append(f"")
                                    lines.append(f"    Available opposite edges:")
                                    for oe in opp_edges[:3]:  # Show top 3 closest
                                        lines.append(f"      - Mon {oe['monitor_index']} ({oe['monitor_name']}): "
                                                   f"range [{oe['edge_range'][0]}, {oe['edge_range'][1]}], "
                                                   f"{oe['distance_to_segment']}px {oe['relative_position']}")
                    
                    lines.append("")
                    lines.append("    " + "-" * 36)
        else:
            lines.append("")
            lines.append("✓ All outer edges have wrap destinations!")
        
        # Check for gaps
        gaps = self.topology.detect_monitor_gaps()
        if gaps:
            lines.append("")
            lines.append("⚠️ Monitor Gaps Detected:")
            for gap in gaps:
                lines.append(f"  Between Mon {gap.monitor1_index} and Mon {gap.monitor2_index}:")
                lines.append(f"    Horizontal gap: {gap.horizontal_gap}px")
                lines.append(f"    Vertical overlap: {gap.vertical_overlap}px")
        
        self.info_text.insert(tk.END, "\n".join(lines))
    
    def _calculate_scale(self):
        """Calculate scale factor to fit monitors in canvas."""
        if not self.monitors:
            return
        
        canvas_width = self.canvas.winfo_width()
        canvas_height = self.canvas.winfo_height()
        
        if canvas_width < 10 or canvas_height < 10:
            return
        
        # Find bounding box of all monitors
        min_x = min(m.left for m in self.monitors)
        max_x = max(m.right for m in self.monitors)
        min_y = min(m.top for m in self.monitors)
        max_y = max(m.bottom for m in self.monitors)
        
        layout_width = max_x - min_x
        layout_height = max_y - min_y
        
        # Add padding for edge bars
        padding = 50
        available_width = canvas_width - 2 * padding
        available_height = canvas_height - 2 * padding
        
        # Calculate scale to fit
        scale_x = available_width / layout_width if layout_width > 0 else 1
        scale_y = available_height / layout_height if layout_height > 0 else 1
        self.scale = min(scale_x, scale_y)
        
        # Calculate offset to center
        scaled_width = layout_width * self.scale
        scaled_height = layout_height * self.scale
        self.offset_x = (canvas_width - scaled_width) / 2 - min_x * self.scale
        self.offset_y = (canvas_height - scaled_height) / 2 - min_y * self.scale
    
    def _screen_to_canvas(self, x: int, y: int) -> Tuple[float, float]:
        """Convert screen coordinates to canvas coordinates."""
        return (x * self.scale + self.offset_x, y * self.scale + self.offset_y)
    
    def _redraw(self):
        """Redraw the entire canvas."""
        self.canvas.delete("all")
        self.monitor_items.clear()
        self.edge_segment_items.clear()
        
        if not self.monitors:
            self.canvas.create_text(
                self.canvas.winfo_width() / 2,
                self.canvas.winfo_height() / 2,
                text="Load a monitor layout JSON file to begin",
                fill="white",
                font=('TkDefaultFont', 14)
            )
            return
        
        self._calculate_scale()
        
        # Draw monitors
        self._draw_monitors()
        
        # Draw edge bars
        self._draw_edge_bars()
    
    def _draw_monitors(self):
        """Draw monitor rectangles."""
        for i, monitor in enumerate(self.monitors):
            x1, y1 = self._screen_to_canvas(monitor.left, monitor.top)
            x2, y2 = self._screen_to_canvas(monitor.right, monitor.bottom)
            
            # Draw monitor rectangle
            outline_color = self.PRIMARY_HIGHLIGHT if monitor.primary else self.MONITOR_OUTLINE
            outline_width = 3 if monitor.primary else 1
            
            item_id = self.canvas.create_rectangle(
                x1, y1, x2, y2,
                fill=self.MONITOR_FILL,
                outline=outline_color,
                width=outline_width
            )
            self.monitor_items[item_id] = i
            
            # Draw monitor label
            center_x = (x1 + x2) / 2
            center_y = (y1 + y2) / 2
            
            label_text = f"Monitor {i}\n{monitor.device_name}\n{monitor.width}x{monitor.height}"
            if monitor.primary:
                label_text += "\n[PRIMARY]"
            
            self.canvas.create_text(
                center_x, center_y,
                text=label_text,
                fill="white",
                font=('TkDefaultFont', 9),
                justify=tk.CENTER
            )
    
    def _draw_edge_bars(self):
        """Draw edge bars outside outer edges showing wrap destinations."""
        wrap_mode = WrapMode(self.wrap_mode.get())
        bar_width = self.EDGE_BAR_WIDTH
        
        # Use new algorithm if checkbox is checked
        use_new = self.use_new_algorithm.get()
        
        for edge in self.topology.outer_edges:
            if use_new:
                segments = self.topology.get_edge_segments_with_projection(edge, wrap_mode)
            else:
                segments = self.topology.get_edge_segments_with_wrap_info(edge, wrap_mode)
            
            for segment in segments:
                self._draw_edge_segment(segment, bar_width)
    
    def _draw_edge_segment(self, segment: EdgeSegment, bar_width: float):
        """Draw a single edge segment with appropriate color."""
        edge = segment.edge
        
        # Determine color based on wrap destination
        if segment.wraps_to is not None:
            # Has wrap destination - use destination edge color mixed with yellow
            color = self.WRAP_DESTINATION_COLOR
        else:
            # No wrap destination - problem area!
            color = self.NO_WRAP_COLOR
        
        # Calculate segment position
        if edge.edge_type == EdgeType.LEFT:
            # Bar to the left of the monitor
            x1, y1 = self._screen_to_canvas(edge.position - bar_width / self.scale, segment.start)
            x2, y2 = self._screen_to_canvas(edge.position, segment.end)
            x1 -= bar_width / 2
        elif edge.edge_type == EdgeType.RIGHT:
            # Bar to the right of the monitor
            x1, y1 = self._screen_to_canvas(edge.position + 1, segment.start)
            x2, y2 = self._screen_to_canvas(edge.position + 1 + bar_width / self.scale, segment.end)
            x2 += bar_width / 2
        elif edge.edge_type == EdgeType.TOP:
            # Bar above the monitor
            x1, y1 = self._screen_to_canvas(segment.start, edge.position - bar_width / self.scale)
            x2, y2 = self._screen_to_canvas(segment.end, edge.position)
            y1 -= bar_width / 2
        elif edge.edge_type == EdgeType.BOTTOM:
            # Bar below the monitor
            x1, y1 = self._screen_to_canvas(segment.start, edge.position + 1)
            x2, y2 = self._screen_to_canvas(segment.end, edge.position + 1 + bar_width / self.scale)
            y2 += bar_width / 2
        
        # Draw the segment bar
        # First draw the base edge color
        base_color = self.EDGE_COLORS[edge.edge_type]
        
        item_id = self.canvas.create_rectangle(
            x1, y1, x2, y2,
            fill=color,
            outline=base_color,
            width=2
        )
        self.edge_segment_items.append((item_id, segment))
        
        # If it's a problem area (no wrap), add a pattern or marker
        if not segment.has_wrap_destination:
            # Add diagonal lines pattern to make it more visible
            if edge.edge_type in (EdgeType.LEFT, EdgeType.RIGHT):
                # Vertical bar - horizontal stripes
                for stripe_y in range(int(y1), int(y2), 6):
                    self.canvas.create_line(x1, stripe_y, x2, stripe_y, fill='white', width=1)
            else:
                # Horizontal bar - vertical stripes
                for stripe_x in range(int(x1), int(x2), 6):
                    self.canvas.create_line(stripe_x, y1, stripe_x, y2, fill='white', width=1)

    # =========================================================================
    # Edge Test Simulation Methods
    # =========================================================================
    
    def _start_edge_test(self):
        """Start the edge test simulation - tests wrap behavior at all outer edge points."""
        if not self.monitors:
            messagebox.showwarning("No Data", "Load a monitor layout first.")
            return
        
        if self.edge_test_running:
            return
        
        # Stop any cursor log playback
        self._stop_playback()
        
        # Generate test points for all outer edges
        self.edge_test_points = self._generate_edge_test_points()
        self.edge_test_index = 0
        self.edge_test_running = True
        self.edge_test_results = {
            "total_points": len(self.edge_test_points),
            "tested": 0,
            "with_destination": 0,
            "without_destination": 0,
            "by_edge": {}
        }
        
        self.edge_test_stop_btn.config(state=tk.NORMAL)
        self.play_button.config(state=tk.DISABLED)
        
        # Update info panel
        self.info_text.delete(1.0, tk.END)
        self.info_text.insert(tk.END, "EDGE TEST SIMULATION\n")
        self.info_text.insert(tk.END, "=" * 40 + "\n\n")
        self.info_text.insert(tk.END, f"Testing {len(self.edge_test_points)} edge points...\n")
        self.info_text.insert(tk.END, f"Algorithm: {'NEW (with projection)' if self.use_new_algorithm.get() else 'OLD (direct overlap only)'}\n\n")
        
        # Start the test animation
        self._edge_test_step()
    
    def _stop_edge_test(self):
        """Stop the edge test simulation."""
        self.edge_test_running = False
        self.edge_test_stop_btn.config(state=tk.DISABLED)
        if self.cursor_log:
            self.play_button.config(state=tk.NORMAL)
        
        # Clear test visualization
        for item_id in self.edge_test_canvas_items:
            self.canvas.delete(item_id)
        self.edge_test_canvas_items.clear()
        
        # Show results summary
        self._show_edge_test_results()
    
    def _generate_edge_test_points(self) -> List[Tuple[MonitorEdge, int, int, int]]:
        """
        Generate test points along all outer edges.
        Returns list of (edge, perpendicular_coord, source_x, source_y).
        """
        points = []
        wrap_mode = WrapMode(self.wrap_mode.get())
        use_new = self.use_new_algorithm.get()
        
        # Sample every N pixels along each edge
        sample_interval = 20  # pixels
        
        for edge in self.topology.outer_edges:
            # Check if this edge type is enabled by wrap mode
            is_horizontal_edge = edge.edge_type in (EdgeType.LEFT, EdgeType.RIGHT)
            is_vertical_edge = edge.edge_type in (EdgeType.TOP, EdgeType.BOTTOM)
            
            if wrap_mode == WrapMode.VERTICAL_ONLY and is_horizontal_edge:
                continue
            if wrap_mode == WrapMode.HORIZONTAL_ONLY and is_vertical_edge:
                continue
            
            # Generate sample points along the edge
            for coord in range(edge.start, edge.end + 1, sample_interval):
                # Calculate source position (on the edge)
                if edge.edge_type == EdgeType.LEFT:
                    src_x, src_y = edge.position, coord
                elif edge.edge_type == EdgeType.RIGHT:
                    src_x, src_y = edge.position, coord
                elif edge.edge_type == EdgeType.TOP:
                    src_x, src_y = coord, edge.position
                elif edge.edge_type == EdgeType.BOTTOM:
                    src_x, src_y = coord, edge.position
                
                # Calculate wrap destination
                if use_new:
                    target_edge, dest_coord = self.topology.get_wrap_destination_with_projection(
                        edge.edge_type, coord, edge)
                else:
                    target_edge = self.topology.find_opposite_outer_edge(edge.edge_type, coord)
                    dest_coord = coord if target_edge else None
                
                if target_edge:
                    # Calculate destination position
                    if target_edge.edge_type == EdgeType.LEFT:
                        dest_x, dest_y = target_edge.position, dest_coord
                    elif target_edge.edge_type == EdgeType.RIGHT:
                        dest_x, dest_y = target_edge.position, dest_coord
                    elif target_edge.edge_type == EdgeType.TOP:
                        dest_x, dest_y = dest_coord, target_edge.position
                    elif target_edge.edge_type == EdgeType.BOTTOM:
                        dest_x, dest_y = dest_coord, target_edge.position
                    
                    points.append((edge, coord, src_x, src_y, dest_x, dest_y, True))
                else:
                    # No destination
                    points.append((edge, coord, src_x, src_y, src_x, src_y, False))
        
        return points
    
    def _edge_test_step(self):
        """Perform one step of the edge test animation."""
        if not self.edge_test_running:
            return
        
        if self.edge_test_index >= len(self.edge_test_points):
            # Test complete
            self._stop_edge_test()
            return
        
        # Clear previous visualization
        for item_id in self.edge_test_canvas_items:
            self.canvas.delete(item_id)
        self.edge_test_canvas_items.clear()
        
        # Get current test point
        point = self.edge_test_points[self.edge_test_index]
        edge, coord, src_x, src_y, dest_x, dest_y, has_dest = point
        
        # Update results
        self.edge_test_results["tested"] += 1
        edge_key = f"Mon{edge.monitor_index}_{edge.edge_type.name}"
        if edge_key not in self.edge_test_results["by_edge"]:
            self.edge_test_results["by_edge"][edge_key] = {"total": 0, "success": 0, "fail": 0}
        self.edge_test_results["by_edge"][edge_key]["total"] += 1
        
        if has_dest:
            self.edge_test_results["with_destination"] += 1
            self.edge_test_results["by_edge"][edge_key]["success"] += 1
        else:
            self.edge_test_results["without_destination"] += 1
            self.edge_test_results["by_edge"][edge_key]["fail"] += 1
        
        # Draw source cursor
        cx, cy = self._screen_to_canvas(src_x, src_y)
        cursor_size = 10
        
        # Source cursor (red circle)
        item = self.canvas.create_oval(
            cx - cursor_size, cy - cursor_size,
            cx + cursor_size, cy + cursor_size,
            fill="#FF4444", outline="white", width=2
        )
        self.edge_test_canvas_items.append(item)
        
        # Draw destination and connection line if there's a valid destination
        if has_dest and (dest_x != src_x or dest_y != src_y):
            dx, dy = self._screen_to_canvas(dest_x, dest_y)
            
            # Connection line (dashed)
            item = self.canvas.create_line(
                cx, cy, dx, dy,
                fill="#00FF00", width=2, dash=(5, 3)
            )
            self.edge_test_canvas_items.append(item)
            
            # Destination cursor (green circle)
            item = self.canvas.create_oval(
                dx - cursor_size, dy - cursor_size,
                dx + cursor_size, dy + cursor_size,
                fill="#44FF44", outline="white", width=2
            )
            self.edge_test_canvas_items.append(item)
        elif not has_dest:
            # Draw X for no destination
            item = self.canvas.create_line(
                cx - cursor_size, cy - cursor_size,
                cx + cursor_size, cy + cursor_size,
                fill="#FF0000", width=3
            )
            self.edge_test_canvas_items.append(item)
            item = self.canvas.create_line(
                cx - cursor_size, cy + cursor_size,
                cx + cursor_size, cy - cursor_size,
                fill="#FF0000", width=3
            )
            self.edge_test_canvas_items.append(item)
        
        # Update hover info
        status = "✓ WRAPS" if has_dest else "✗ NO WRAP"
        self.hover_info_var.set(
            f"Testing: Mon{edge.monitor_index} {edge.edge_type.name} @ {coord} → {status} "
            f"[{self.edge_test_index + 1}/{len(self.edge_test_points)}]"
        )
        
        # Update status panel
        progress = (self.edge_test_index + 1) / len(self.edge_test_points) * 100
        self.playback_status_var.set(f"Progress: {progress:.1f}%")
        self.playback_position_var.set(f"Point: {self.edge_test_index + 1} / {len(self.edge_test_points)}")
        
        # Move to next point
        self.edge_test_index += 1
        
        # Schedule next step
        speed = self.playback_speed.get()
        self.root.after(speed, self._edge_test_step)
    
    def _show_edge_test_results(self):
        """Show the edge test results summary."""
        results = self.edge_test_results
        
        self.info_text.delete(1.0, tk.END)
        self.info_text.insert(tk.END, "EDGE TEST RESULTS\n")
        self.info_text.insert(tk.END, "=" * 40 + "\n\n")
        
        total = results.get("total_points", 0)
        tested = results.get("tested", 0)
        success = results.get("with_destination", 0)
        fail = results.get("without_destination", 0)
        
        self.info_text.insert(tk.END, f"Algorithm: {'NEW' if self.use_new_algorithm.get() else 'OLD'}\n\n")
        self.info_text.insert(tk.END, f"Total Points: {total}\n")
        self.info_text.insert(tk.END, f"Tested: {tested}\n")
        self.info_text.insert(tk.END, f"With Destination: {success} ({success/total*100:.1f}%)\n" if total > 0 else "")
        self.info_text.insert(tk.END, f"Without Destination: {fail} ({fail/total*100:.1f}%)\n\n" if total > 0 else "")
        
        if fail == 0 and total > 0:
            self.info_text.insert(tk.END, "✅ ALL POINTS HAVE WRAP DESTINATIONS!\n\n")
        elif fail > 0:
            self.info_text.insert(tk.END, f"⚠️ {fail} POINTS WITHOUT DESTINATION\n\n")
        
        # Per-edge breakdown
        self.info_text.insert(tk.END, "By Edge:\n")
        self.info_text.insert(tk.END, "-" * 30 + "\n")
        for edge_key, stats in results.get("by_edge", {}).items():
            status = "✓" if stats["fail"] == 0 else "✗"
            self.info_text.insert(tk.END, 
                f"{status} {edge_key}: {stats['success']}/{stats['total']}\n")
        
        self.hover_info_var.set(f"Test complete: {success}/{total} points with destinations")


def main():
    parser = argparse.ArgumentParser(
        description='CursorWrap Simulator - Visualize monitor wrap edges',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Example:
    python wrap_simulator.py monitor_layout.json

The JSON file should contain monitor layout information with the following structure:
{
    "monitors": [
        {
            "left": 0, "top": 0, "right": 2560, "bottom": 1440,
            "width": 2560, "height": 1440,
            "primary": true, "device_name": "DISPLAY1"
        },
        ...
    ]
}
        """
    )
    parser.add_argument('json_file', nargs='?', help='Path to monitor layout JSON file')
    
    args = parser.parse_args()
    
    root = tk.Tk()
    app = WrapSimulatorApp(root, args.json_file)
    root.mainloop()


if __name__ == '__main__':
    main()
