# scripts/pt-a11y-annotate.ps1
# Turns a pt-a11y-probe.ps1 JSON + a window screenshot into "what a screen reader hears" proof:
# every interactive control and heading gets a box and a label with its announced Name.
#   green  = named control          red  = finding (missing / type-name / id-as-name)
#   blue   = heading (label prefixed with H<level>)
#
#   Export-PtA11yAnnotatedScreenshot -ProbeJson editor-after.json -Screenshot editor-after.png -OutFile editor-after-annotated.png

Add-Type -AssemblyName System.Drawing

function Export-PtA11yAnnotatedScreenshot {
    param(
        [Parameter(Mandatory)] [string]$ProbeJson,
        [Parameter(Mandatory)] [string]$Screenshot,
        [Parameter(Mandatory)] [string]$OutFile,
        [string]$Title
    )

    $probe = Get-Content $ProbeJson -Raw | ConvertFrom-Json
    $interactive = @('Button', 'CheckBox', 'ComboBox', 'Edit', 'Hyperlink', 'List', 'ListItem', 'MenuItem',
        'RadioButton', 'Slider', 'Spinner', 'SplitButton', 'TabItem', 'Tree', 'TreeItem', 'DataGrid', 'DataItem')
    $flagged = @{}
    foreach ($f in $probe.Findings) { $flagged["$($f.Element)|$($f.Name)"] = $f.Rule }

    $window = $probe.Elements | Where-Object { $_.Depth -eq 0 -and $_.Bounds } | Select-Object -First 1
    if (-not $window) { throw 'Probe JSON has no window bounds; re-run pt-a11y-probe.ps1.' }
    $wx, $wy, $ww, $wh = $window.Bounds -split ',' | ForEach-Object { [int]$_ }

    $src = [System.Drawing.Image]::FromFile((Resolve-Path $Screenshot))
    $header = if ($Title) { 36 } else { 0 }
    $bmp = [System.Drawing.Bitmap]::new($src.Width, $src.Height + $header)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode = 'AntiAlias'
        $g.TextRenderingHint = 'ClearTypeGridFit'
        $g.Clear([System.Drawing.Color]::Black)
        $g.DrawImage($src, 0, $header, $src.Width, $src.Height)
        # Window captures exclude the invisible DWM resize border; center the horizontal difference.
        $ox = $wx + [int](($ww - $src.Width) / 2)
        $oy = $wy - $header
        $font = [System.Drawing.Font]::new('Segoe UI', 9, [System.Drawing.FontStyle]::Bold)
        if ($Title) { $g.DrawString($Title, [System.Drawing.Font]::new('Segoe UI', 13, [System.Drawing.FontStyle]::Bold), [System.Drawing.Brushes]::White, 8, 6) }

        foreach ($e in $probe.Elements) {
            if (-not $e.Bounds -or $e.Offscreen) { continue }
            $isHeading = $e.HeadingLevel -gt 0
            if (-not $isHeading -and $interactive -notcontains $e.Type) { continue }
            $x, $y, $w, $h = $e.Bounds -split ',' | ForEach-Object { [int]$_ }
            $rect = [System.Drawing.Rectangle]::new($x - $ox, $y - $oy, $w, $h)
            if ($rect.Width -le 0 -or $rect.Height -le 0) { continue }
            if ($e.Type -eq 'List' -and $rect.Height -gt 120) { $rect.Height = 4 } # keep big list boxes from hiding their rows

            $where = if ($e.AutomationId) { "$($e.Type)#$($e.AutomationId)" } else { "$($e.Type) '$($e.ClassName)'" }
            $rule = $flagged["$where|$($e.Name)"]
            $color = if ($rule) { [System.Drawing.Color]::FromArgb(255, 232, 17, 35) } elseif ($isHeading) { [System.Drawing.Color]::FromArgb(255, 0, 120, 212) } else { [System.Drawing.Color]::FromArgb(255, 16, 124, 16) }
            $label = if ($rule -eq 'A11Y-NAME-MISSING') { "$($e.Type): <no name>" } elseif ($isHeading) { "H$($e.HeadingLevel): $($e.Name)" } else { "$($e.Type): $($e.Name)" }

            $pen = [System.Drawing.Pen]::new($color, 2)
            $g.DrawRectangle($pen, $rect)
            $size = $g.MeasureString($label, $font)
            $ly = [Math]::Max($header, $rect.Y - [int]$size.Height)
            $lx = [Math]::Max(0, [Math]::Min($rect.X, $bmp.Width - [int]$size.Width - 2))
            $g.FillRectangle([System.Drawing.SolidBrush]::new($color), $lx, $ly, [int]$size.Width + 2, [int]$size.Height)
            $g.DrawString($label, $font, [System.Drawing.Brushes]::White, $lx + 1, $ly)
            $pen.Dispose()
        }
        $bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $g.Dispose(); $bmp.Dispose(); $src.Dispose()
    }
}
