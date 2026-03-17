#!/usr/bin/env python3
"""
Test script to validate the new projection-based wrapping algorithm.
"""

import json
import sys
from wrap_simulator import MonitorTopology, MonitorInfo, WrapMode

def test_layout(layout_file: str):
    """Test a monitor layout with both old and new algorithms."""
    
    # Load the layout
    with open(layout_file, 'r') as f:
        layout = json.load(f)
    
    # Create monitor info objects
    monitors = []
    for i, m in enumerate(layout['monitors']):
        monitors.append(MonitorInfo(
            left=m['left'], top=m['top'], right=m['right'], bottom=m['bottom'],
            width=m['width'], height=m['height'], dpi=m.get('dpi', 96),
            scaling_percent=m.get('scaling_percent', 100), primary=m.get('primary', False),
            device_name=m.get('device_name', f'DISPLAY{i+1}'), monitor_id=i
        ))
    
    # Initialize topology
    topology = MonitorTopology()
    topology.initialize(monitors)
    
    print(f"Layout: {layout_file}")
    print(f"Monitors: {len(monitors)}")
    print(f"Outer edges: {len(topology.outer_edges)}")
    
    # Validate with OLD algorithm
    print("\n--- OLD Algorithm (may have dead zones) ---")
    old_problems = 0
    old_problem_details = []
    for edge in topology.outer_edges:
        segments = topology.get_edge_segments_with_wrap_info(edge, WrapMode.BOTH)
        for seg in segments:
            if not seg.has_wrap_destination:
                length = seg.end - seg.start
                old_problems += length
                detail = f"Mon {edge.monitor_index} {edge.edge_type.name} [{seg.start}-{seg.end}] ({length}px)"
                old_problem_details.append(detail)
                print(f"  PROBLEM: {detail}")
    print(f"Total problematic pixels: {old_problems}")
    
    # Validate with NEW algorithm
    print("\n--- NEW Algorithm (with projection) ---")
    result = topology.validate_all_edges_have_destinations(WrapMode.BOTH)
    print(f"Total edge length: {result['total_edge_length']}px")
    print(f"Covered: {result['covered_length']}px ({result['coverage_percent']:.1f}%)")
    print(f"Uncovered: {result['uncovered_length']}px")
    print(f"Fully covered: {result['is_fully_covered']}")
    
    if result['problem_areas']:
        for prob in result['problem_areas']:
            print(f"  PROBLEM: {prob}")
    
    # Summary
    print("\n--- COMPARISON ---")
    print(f"Old algorithm dead zones: {old_problems}px")
    print(f"New algorithm dead zones: {result['uncovered_length']}px")
    if old_problems > 0 and result['uncovered_length'] == 0:
        print("SUCCESS: New algorithm eliminates all dead zones!")
    elif result['uncovered_length'] > 0:
        print("WARNING: New algorithm still has dead zones")
    else:
        print("Both algorithms have no dead zones for this layout")
    
    return result['is_fully_covered']


def main():
    layout_files = [
        'mikehall_monitor_layout.json',
        'sample_layout.json',
        'sample_staggered.json',
    ]
    
    # Allow specifying layout on command line
    if len(sys.argv) > 1:
        layout_files = sys.argv[1:]
    
    all_passed = True
    for layout_file in layout_files:
        try:
            print(f"\n{'='*60}")
            passed = test_layout(layout_file)
            if not passed:
                all_passed = False
        except FileNotFoundError:
            print(f"File not found: {layout_file}")
        except Exception as e:
            print(f"Error testing {layout_file}: {e}")
            all_passed = False
    
    print(f"\n{'='*60}")
    if all_passed:
        print("ALL TESTS PASSED")
    else:
        print("SOME TESTS FAILED")
    
    return 0 if all_passed else 1


if __name__ == "__main__":
    sys.exit(main())
