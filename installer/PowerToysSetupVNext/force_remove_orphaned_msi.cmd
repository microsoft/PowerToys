@echo off
setlocal ENABLEDELAYEDEXPANSION

REM This script is invoked during uninstall by the Burn bundle before the MSI package.
REM It force-removes orphaned PowerToys MSI products whose cached MSI files are missing.
REM This handles the "msi file for v0.86 not found" error during uninstall.
REM The actual force-removal uses the native MsiConfigureProductExW API with MSIFASTINSTALL=7,
REM which is done by the ForceRemoveOldVersionCA custom action in the MSI (Product.wxs).
REM This script serves as an additional cleanup pass at the bundle level for products
REM that may not be found by the MSI-level custom action (e.g., from a different scope).

REM Find and remove orphaned per-machine products
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  $uc = '{42B84BF7-5FBF-473B-9C8B-049DC16F7708}'; ^
  try { $i = New-Object -ComObject WindowsInstaller.Installer; ^
    $products = @($i.RelatedProducts($uc)); ^
    foreach ($p in $products) { ^
      try { if ($i.ProductState($p) -eq 5) { ^
        $lp = $i.ProductInfo($p, 'LocalPackage'); ^
        if (-not (Test-Path -LiteralPath $lp -ErrorAction SilentlyContinue)) { ^
          Write-Host ('Detected orphaned machine MSI, removing: ' + $p); ^
          $i.ConfigureProduct($p, 0, 2); } } } catch { } } } catch { }

REM Find and remove orphaned per-user products
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  $uc = '{D8B559DB-4C98-487A-A33F-50A8EEE42726}'; ^
  try { $i = New-Object -ComObject WindowsInstaller.Installer; ^
    $products = @($i.RelatedProducts($uc)); ^
    foreach ($p in $products) { ^
      try { if ($i.ProductState($p) -eq 5) { ^
        $lp = $i.ProductInfo($p, 'LocalPackage'); ^
        if (-not (Test-Path -LiteralPath $lp -ErrorAction SilentlyContinue)) { ^
          Write-Host ('Detected orphaned user MSI, removing: ' + $p); ^
          $i.ConfigureProduct($p, 0, 2); } } } catch { } } } catch { }

exit /b 0
