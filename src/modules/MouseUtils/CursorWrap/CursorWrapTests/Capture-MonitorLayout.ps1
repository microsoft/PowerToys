#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Captures the current monitor layout configuration for CursorWrap testing.

.DESCRIPTION
    Queries Windows for all connected monitors and saves their configuration
    (position, size, DPI, primary status) to a JSON file that can be used
    for testing the CursorWrap module.

.PARAMETER OutputPath
    Path where the JSON file will be saved. Default: monitor_layout.json

.EXAMPLE
    .\Capture-MonitorLayout.ps1
    
.EXAMPLE
    .\Capture-MonitorLayout.ps1 -OutputPath "my_setup.json"
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "$($env:USERNAME)_monitor_layout.json"
)

# Add Windows Forms for screen enumeration
Add-Type -AssemblyName System.Windows.Forms

function Get-MonitorDPI {
    param([System.Windows.Forms.Screen]$Screen)
    
    # Try to get DPI using P/Invoke with multiple methods
    Add-Type @"
using System;
using System.Runtime.InteropServices;
public class DisplayConfig {
    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
    
    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
    
    [DllImport("user32.dll")]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);
    
    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    
    [DllImport("gdi32.dll")]
    public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
    
    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT {
        public int X;
        public int Y;
    }
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFOEX {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
    
    public const uint MONITOR_DEFAULTTOPRIMARY = 1;
    public const int MDT_EFFECTIVE_DPI = 0;
    public const int MDT_ANGULAR_DPI = 1;
    public const int MDT_RAW_DPI = 2;
    public const int LOGPIXELSX = 88;
    public const int LOGPIXELSY = 90;
}
"@ -ErrorAction SilentlyContinue
    
    try {
        $point = New-Object DisplayConfig+POINT
        $point.X = $Screen.Bounds.Left + ($Screen.Bounds.Width / 2)
        $point.Y = $Screen.Bounds.Top + ($Screen.Bounds.Height / 2)
        
        $hMonitor = [DisplayConfig]::MonitorFromPoint($point, 1)
        
        # Method 1: Try GetDpiForMonitor (Windows 8.1+)
        [uint]$dpiX = 0
        [uint]$dpiY = 0
        $result = [DisplayConfig]::GetDpiForMonitor($hMonitor, 0, [ref]$dpiX, [ref]$dpiY)
        
        if ($result -eq 0 -and $dpiX -gt 0) {
            Write-Verbose "DPI detected via GetDpiForMonitor: $dpiX"
            return $dpiX
        }
        
        # Method 2: Try RAW DPI
        $result = [DisplayConfig]::GetDpiForMonitor($hMonitor, 2, [ref]$dpiX, [ref]$dpiY)
        if ($result -eq 0 -and $dpiX -gt 0) {
            Write-Verbose "DPI detected via RAW DPI: $dpiX"
            return $dpiX
        }
        
        # Method 3: Try getting device context DPI (legacy method)
        $hdc = [DisplayConfig]::GetDC([IntPtr]::Zero)
        if ($hdc -ne [IntPtr]::Zero) {
            $dpiValue = [DisplayConfig]::GetDeviceCaps($hdc, 88) # LOGPIXELSX
            [DisplayConfig]::ReleaseDC([IntPtr]::Zero, $hdc)
            if ($dpiValue -gt 0) {
                Write-Verbose "DPI detected via GetDeviceCaps: $dpiValue"
                return $dpiValue
            }
        }
    }
    catch {
        Write-Verbose "DPI detection error: $($_.Exception.Message)"
    }
    
    Write-Warning "Could not detect DPI for $($Screen.DeviceName), using default 96 DPI"
    return 96  # Standard 96 DPI (100% scaling)
}

function Capture-MonitorLayout {
    Write-Host "Capturing monitor layout..." -ForegroundColor Cyan
    Write-Host "=" * 80
    
    $screens = [System.Windows.Forms.Screen]::AllScreens
    $monitors = @()
    
    foreach ($screen in $screens) {
        $isPrimary = $screen.Primary
        $bounds = $screen.Bounds
        $dpi = Get-MonitorDPI -Screen $screen
        
        $monitor = [ordered]@{
            left = $bounds.Left
            top = $bounds.Top
            right = $bounds.Right
            bottom = $bounds.Bottom
            width = $bounds.Width
            height = $bounds.Height
            dpi = $dpi
            scaling_percent = [math]::Round(($dpi / 96.0) * 100, 0)
            primary = $isPrimary
            device_name = $screen.DeviceName
        }
        
        $monitors += $monitor
        
        # Display info
        $primaryTag = if ($isPrimary) { " [PRIMARY]" } else { "" }
        $scaling = [math]::Round(($dpi / 96.0) * 100, 0)
        
        Write-Host "`nMonitor $($monitors.Count)$primaryTag" -ForegroundColor Green
        Write-Host "  Device: $($screen.DeviceName)"
        Write-Host "  Position: ($($bounds.Left), $($bounds.Top))"
        Write-Host "  Size: $($bounds.Width)x$($bounds.Height)"
        Write-Host "  DPI: $dpi ($scaling% scaling)"
        Write-Host "  Bounds: [$($bounds.Left), $($bounds.Top), $($bounds.Right), $($bounds.Bottom)]"
    }
    
    # Create output object
    $output = [ordered]@{
        captured_at = (Get-Date -Format "yyyy-MM-ddTHH:mm:sszzz")
        computer_name = $env:COMPUTERNAME
        user_name = $env:USERNAME
        monitor_count = $monitors.Count
        monitors = $monitors
    }
    
    # Save to JSON
    $json = $output | ConvertTo-Json -Depth 10
    Set-Content -Path $OutputPath -Value $json -Encoding UTF8
    
    Write-Host "`n" + ("=" * 80)
    Write-Host "Monitor layout saved to: $OutputPath" -ForegroundColor Green
    Write-Host "Total monitors captured: $($monitors.Count)" -ForegroundColor Cyan
    Write-Host "`nYou can now use this file with the test script:" -ForegroundColor Yellow
    Write-Host "  python monitor_layout_tests.py --layout-file $OutputPath" -ForegroundColor White
    
    return $output
}

# Main execution
try {
    $layout = Capture-MonitorLayout
    
    # Display summary
    Write-Host "`n" + ("=" * 80)
    Write-Host "SUMMARY" -ForegroundColor Cyan
    Write-Host ("=" * 80)
    Write-Host "Configuration Name: $($layout.computer_name)"
    Write-Host "Captured: $($layout.captured_at)"
    Write-Host "Monitors: $($layout.monitor_count)"
    
    # Calculate desktop dimensions
    $widths = @($layout.monitors | ForEach-Object { $_.width })
    $heights = @($layout.monitors | ForEach-Object { $_.height })
    
    $totalWidth = ($widths | Measure-Object -Sum).Sum
    $maxHeight = ($heights | Measure-Object -Maximum).Maximum
    
    Write-Host "Total Desktop Width: $totalWidth pixels"
    Write-Host "Max Desktop Height: $maxHeight pixels"
    
    # Analyze potential coordinate issues
    Write-Host "`n" + ("=" * 80)
    Write-Host "COORDINATE ANALYSIS" -ForegroundColor Cyan
    Write-Host ("=" * 80)
    
    # Check for gaps between monitors
    if ($layout.monitor_count -gt 1) {
        $hasGaps = $false
        for ($i = 0; $i -lt $layout.monitor_count - 1; $i++) {
            $m1 = $layout.monitors[$i]
            for ($j = $i + 1; $j -lt $layout.monitor_count; $j++) {
                $m2 = $layout.monitors[$j]
                
                # Check horizontal gap
                $hGap = [Math]::Min([Math]::Abs($m1.right - $m2.left), [Math]::Abs($m2.right - $m1.left))
                # Check vertical overlap
                $vOverlapStart = [Math]::Max($m1.top, $m2.top)
                $vOverlapEnd = [Math]::Min($m1.bottom, $m2.bottom)
                $vOverlap = $vOverlapEnd - $vOverlapStart
                
                if ($hGap -gt 50 -and $vOverlap -gt 0) {
                    Write-Host "⚠ Gap detected between Monitor $($i+1) and Monitor $($j+1): ${hGap}px horizontal gap" -ForegroundColor Yellow
                    Write-Host "  Vertical overlap: ${vOverlap}px" -ForegroundColor Yellow
                    Write-Host "  This may indicate a Windows coordinate bug if monitors appear snapped in Display Settings" -ForegroundColor Yellow
                    $hasGaps = $true
                }
            }
        }
        if (-not $hasGaps) {
            Write-Host "✓ No unexpected gaps detected" -ForegroundColor Green
        }
    }
    
    # DPI/Scaling notes
    Write-Host "`nDPI/Scaling Impact on Coordinates:" -ForegroundColor Cyan
    Write-Host "• Coordinate values (left, top, right, bottom) are in LOGICAL PIXELS"
    Write-Host "• These are DPI-independent virtual coordinates"
    Write-Host "• Physical pixels = Logical pixels × (DPI / 96)"
    Write-Host "• Example: 1920 logical pixels at 150% scaling = 1920 × 1.5 = 2880 physical pixels"
    Write-Host "• Windows snaps monitors using logical pixel coordinates"
    Write-Host "• If monitors appear snapped but coordinates show gaps, this is a Windows bug"
    
    exit 0
}
catch {
    Write-Host "`nError capturing monitor layout:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    exit 1
}
