# Validating/Testing Cursor Wrap.

If a user determines that CursorWrap isn't working on their PC there are some steps you can take to determine why CursorWrap functionality might not be working as expected.

Note that for a single monitor cursor wrap should always work since all monitor edges are not touching/overlapping with other monitors - the cursor should always wrap to the opposite edge of the same monitor.

Multi-monitor is supported through building a polygon shape for the outer edges of all monitors, inner monitor edges are ignored, movement of the cursor from one monitor to an adjacent monitor is handled by Windows - CursorWrap doesn't get involved in monitor-to-monitor movement, only outer-edges.

We have seen a couple of computer setups that have multi-monitors where CursorWrap doesn't work as expected, this appears to be due to a monitor not being 'snapped' to the edge of an adjacent monitor - If you use Display Settings in Windows you can move monitors around, these appear to 'snap' to an edge of an existing monitor.

What to do if Cursor Wrapping isn't working as expected ?

1. in the CursorWrapTests folder there's a PowerShell script called `Capture-MonitorLayout.ps1` - this will generate a .json file in the form `"$($env:USERNAME)_monitor_layout.json` - the .json file contains an array of monitors, their position, size, dpi, and scaling.
2. Use `CursorWrapTests/monitor_layout_tests.py` to validate the monitor layout/wrapping behavior (uses the json file from point 1 above).
3. Use `analyze_test_results.py` to analyze the monitor layout test output and provide information about why wrapping might not be working

To run `monitor_layout_tests.py` you will need Python installed on your PC.

Run `python monitor_layout_tests.py --layout-file <path to json file>` you can also add an optional `--verbose` to view verbose output.

monitor_layout_tests.py will produce an output file called `test_report.json` - the contents of the file will look like this (this is from a single monitor test).

```json
{
  "summary": {
    "total_configs": 1,
    "passed": 1,
    "failed": 0,
    "total_issues": 0,
    "pass_rate": "100.00%"
  },
  "failures": [],
  "recommendations": [
    "All tests passed - edge detection logic is working correctly!"
  ]
}
```

If there are failures (the failures array is not empty) you can run the second python application called `analyze_test_results.py`

Supported options include:
```text
  -h, --help            show this help message and exit
  --report REPORT       Path to test report JSON file
  --detailed            Show detailed failure listing
  --copilot             Generate GitHub Copilot-friendly fix prompt
  ```

Running the analyze_test_results.py script against our single monitor test results produces the following:

```text
python .\analyze_test_results.py --detailed
================================================================================
CURSORWRAP TEST RESULTS ANALYSIS
================================================================================

Total Configurations Tested: 1
Passed: 1 (100.00%)
Failed: 0
Total Issues: 0

✓ ALL TESTS PASSED! Edge detection logic is working correctly.

✓ No failures to analyze!
```

