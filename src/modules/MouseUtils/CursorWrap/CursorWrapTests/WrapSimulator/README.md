# CursorWrap Simulator

A Python visualization tool that displays monitor layouts and shows which edges will wrap to other monitors using the exact same logic as the PowerToys CursorWrap implementation.

## Purpose

This tool helps you:
- Visualize your multi-monitor setup
- Identify which screen edges are "outer edges" (edges that don't connect to another monitor)
- See where cursor wrapping will occur when you move the cursor to an outer edge
- **Find problem areas** where edges have NO wrap destination (shown in red)

## Requirements

- Python 3.6+
- Tkinter (included with standard Python on Windows)

## Usage

### Command Line

```bash
python wrap_simulator.py <path_to_monitor_layout.json>
```

### Without Arguments

```bash
python wrap_simulator.py
```

This opens the application with no layout loaded. Use the "Load JSON" button to select a file.

## JSON File Format

The monitor layout JSON file should have this structure:

```json
{
  "captured_at": "2026-02-16T08:50:34+00:00",
  "computer_name": "MY-PC",
  "user_name": "User",
  "monitor_count": 3,
  "monitors": [
    {
      "left": 0,
      "top": 0,
      "right": 2560,
      "bottom": 1440,
      "width": 2560,
      "height": 1440,
      "dpi": 96,
      "scaling_percent": 100.0,
      "primary": true,
      "device_name": "DISPLAY1"
    }
  ]
}
```

## Understanding the Visualization

### Monitor Display
- **Gray rectangles**: Individual monitors
- **Orange border**: Primary monitor
- **Labels**: Show monitor index, device name, and resolution

### Edge Bars (Outside Monitor Boundaries)

Colored bars are drawn outside each **outer edge** (edges not adjacent to another monitor):

| Color | Meaning |
|-------|---------|
| **Yellow** | Edge segment has a wrap destination ✓ |
| **Red with stripes** | NO wrap destination - Problem area! ⚠️ |

The bar outline color indicates the edge type:
- Red = Left edge
- Teal = Right edge  
- Blue = Top edge
- Green = Bottom edge

### Interactive Features

1. **Hover over edge segments**: 
   - See wrap destination info in the status bar
   - Green arrow shows where the cursor would wrap to
   - Green dashed rectangle highlights the destination

2. **Click on edge segments**:
   - Detailed information appears in the info panel
   - Shows full problem analysis with reason codes
   - Explains why wrapping does/doesn't occur
   - Provides suggestions for fixing problems


3. **Wrap Mode Selection**:
   - **Both**: Wrap in all directions (default)
   - **Vertical Only**: Only top/bottom edges wrap
   - **Horizontal Only**: Only left/right edges wrap

4. **Export Analysis**:
   - Click "Export Analysis" to save detailed diagnostic data
   - Exports to JSON format for use in algorithm development
   - Includes all problem segments with reason codes and suggestions

5. **Edge Test Simulation** (NEW):
   - Click "🧪 Test Edges" to start automated edge testing
   - Visually animates cursor movement along ALL outer edges
   - Shows wrap destination for each test point with colored lines:
     - **Red circle**: Source position on outer edge
     - **Green circle**: Wrap destination
     - **Green dashed line**: Connection showing wrap path
     - **Red X**: No wrap destination (problem area)
   - Use "New Algorithm" checkbox to toggle between:
     - **NEW**: Projection-based algorithm (eliminates dead zones)
     - **OLD**: Direct overlap only (may have dead zones)
   - Results summary shows per-edge coverage statistics

## Problem Analysis

When a segment has no wrap destination, the tool provides detailed analysis:

### Problem Reason Codes

| Code | Description |
|------|-------------|
| `WRAP_MODE_DISABLED` | Edge type disabled by current wrap mode setting |
| `NO_OPPOSITE_OUTER_EDGES` | No outer edges of the opposite type exist at all |
| `NO_OVERLAPPING_RANGE` | Opposite edges exist but don't cover this coordinate range |
| `SINGLE_MONITOR` | Only one monitor - nowhere to wrap to |

### Diagnostic Details

For `NO_OVERLAPPING_RANGE` problems, the tool shows:
- Distance to the nearest valid wrap destination
- List of available opposite edges sorted by distance
- Whether the gap is above/below or left/right of the segment
- Suggested fixes (extend monitors or adjust positions)

## Sample Files

Included sample layouts:

- `sample_layout.json` - 3 monitors in a row with one offset
- `sample_staggered.json` - 3 monitors with staggered vertical positions (shows problem areas)
- `sample_with_gap.json` - 2 monitors with a gap between them

## Exported Analysis Format

The "Export Analysis" button generates a JSON file with this structure:

```json
{
  "export_timestamp": "2026-02-16T08:50:34+00:00",
  "wrap_mode": "BOTH",
  "monitor_count": 3,
  "monitors": [...],
  "outer_edges": [...],
  "problem_segments": [
    {
      "source": {
        "monitor_index": 0,
        "monitor_name": "DISPLAY1",
        "edge_type": "TOP",
        "edge_position": 200,
        "segment_range": {"start": 0, "end": 200},
        "segment_length_px": 200
      },
      "analysis": {
        "reason_code": "NO_OVERLAPPING_RANGE",
        "description": "No BOTTOM outer edge overlaps...",
        "suggestion": "To fix: Either extend...",
        "details": {
          "gap_to_nearest": 200,
          "available_opposite_edges": [...]
        }
      }
    }
  ],
  "summary": {
    "total_outer_edges": 8,
    "total_problem_segments": 4,
    "total_problem_pixels": 800,
    "problems_by_reason": {"NO_OVERLAPPING_RANGE": 4},
    "has_problems": true
  }
}
```

## How CursorWrap Logic Works

### Original Algorithm (v1)

1. **Outer Edge Detection**: An edge is "outer" if no other monitor's opposite edge is within 50 pixels AND has sufficient vertical/horizontal overlap

2. **Wrap Destination**: When cursor reaches an outer edge:
   - Find the opposite type outer edge (Left→Right, Top→Bottom, etc.)
   - The destination must overlap with the cursor's perpendicular position
   - Cursor warps to the furthest matching outer edge

3. **Problem Areas**: If no opposite outer edge overlaps with a portion of an outer edge, that segment has no wrap destination - the cursor will simply stop at that edge.

### Enhanced Algorithm (v2) - With Projection

The enhanced algorithm eliminates dead zones by projecting cursor positions to valid destinations:

1. **Direct Overlap**: If an opposite outer edge directly overlaps the cursor's perpendicular coordinate, use it (same as v1)

2. **Nearest Edge Projection**: If no direct overlap exists:
   - Find the nearest opposite outer edge by coordinate distance
   - Calculate a projected position using offset-from-boundary approach
   - The projection preserves relative position similar to how Windows handles monitor transitions

3. **No Dead Zones**: Every point on every outer edge will have a valid wrap destination

### Testing the Algorithm

Use the included test script to validate both algorithms:

```bash
python test_new_algorithm.py [layout_file.json]
```

This compares the old algorithm (with dead zones) against the new algorithm (with projection) and reports coverage.

## Cursor Log Playback

The simulator can play back recorded cursor movement logs to visualize how the cursor moves across monitors.

### Loading a Cursor Log

1. Click "Load Log" to select a cursor movement log file
2. Use the playback controls:
   - **▶ Play / ⏸ Pause**: Start or pause playback
   - **⏹ Stop**: Stop and reset to beginning
   - **⏮ Reset**: Reset to beginning without stopping
   - **Speed slider**: Adjust playback speed (10-500ms between frames)

### Log File Format

The cursor log file is CSV format with the following columns:

```
display_name,x,y,dpi,scaling%
```

Example:
```csv
\\.\DISPLAY1,1234,567,96,100%
\\.\DISPLAY2,2560,720,144,150%
\\.\DISPLAY3,-500,800,96,100%
```

- **display_name**: Windows display name (e.g., `\\.\DISPLAY1`)
- **x, y**: Screen coordinates
- **dpi**: Display DPI
- **scaling%**: Display scaling percentage (with or without % sign)

Lines starting with `#` are treated as comments and ignored.

### Playback Visualization

- **Green cursor**: Normal movement within a monitor
- **Red cursor with burst effect**: Monitor transition detected
- **Blue trail**: Recent cursor movement path (fades over time)
- **Dashed red arrow**: Shows transition path between monitors

The playback automatically slows down when a monitor transition is detected, making it easier to observe wrap behavior.

### Sample Log File

A sample cursor log file `sample_cursor_log.csv` is included that demonstrates cursor movement across a three-monitor setup.

## Architecture

The Python implementation mirrors the C++ code structure:

- `MonitorTopology` class: Manages edge-based monitor layout
- `MonitorEdge` dataclass: Represents a single edge of a monitor
- `EdgeSegment` dataclass: A portion of an edge with wrap info
- `CursorLogEntry` dataclass: A single cursor movement log entry
- `WrapSimulatorApp`: Tkinter GUI application

## Integration with PowerToys

This tool is designed to validate and debug the CursorWrap feature. The JSON files can be generated by the debug build of CursorWrap or created manually for testing specific configurations.
