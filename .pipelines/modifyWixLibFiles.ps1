# This is a horrible hack, but in order to use WiX files to build an installer, we need to pretend WiX 3.1.14 wasn't build with the 4996 warning disabled.

#All the places in the .lib files where we want to replace -wd4996 with spaces
[int[]]$dutil_x64_replace_spots = (0x0002026A,0x000302AA,0x0003629A,0x0004823C,0x0004CF42,0x00053456,0x00061B12,0x0008847E,0x0008EA52,0x000ADDF6,0x000BE0A6,0x000D5DAC,0x000EECBC,0x001163BE,0x0011EA34,0x001258F0,0x0012D56C,0x00134994,0x0013AF80,0x001492FC,0x001514E4,0x00153BF0,0x00155D6E,0x00158430,0x0015B960,0x001668F6,0x0016906C,0x0016EBC0,0x001753BA,0x0017C330,0x0018CD08,0x001964FC,0x0019DFE8,0x001A4F86,0x001A98E6,0x001B351A,0x001BA802,0x001D1646,0x001E2090,0x001EBD96,0x001F4E86,0x001FB9BA,0x00203AAC,0x0020B552,0x00213DB6,0x0021B0AA,0x00226D38,0x002380A6,0x0023E6AE,0x002495CC,0x00258ACE,0x0025FE92,0x0026410C,0x0026EC66,0x0058AAFD)
[int[]]$dutil_arm64_replace_spots = (0x0001FFD0,0x000313DC,0x00036ECC,0x00049890,0x0004E4C0,0x00055108,0x00063598,0x0008E5CA,0x0009444A,0x000B41A6,0x000C44C0,0x000DCF48,0x000F5DA0,0x0011E926,0x00127378,0x0012E66C,0x0013617E,0x0013D562,0x00143D4E,0x00152E94,0x0015C176,0x0015EBBC,0x00160F48,0x0016370E,0x00166A22,0x0017269E,0x00174F74,0x0017ACA8,0x0018190E,0x00188F54,0x0019954E,0x001A30FA,0x001AAD4A,0x001B2454,0x001B6E78,0x001C13CA,0x001C89D8,0x001E13C2,0x001F3002,0x001FD1B0,0x002074D0,0x0020E4B8,0x002172BA,0x0021F520,0x00228286,0x0022FB82,0x0023B4F6,0x0024DB58,0x002543A4,0x0025EF92,0x0026F334,0x00277768,0x0027B9DA,0x002883FC,0x005A5117)
[int[]]$wcautil_x64_replace_spots = (0x0000927E,0x00013024,0x00029486,0x0002DC32,0x00037E96,0x0003C70A,0x00046116,0x001CC843)
[int[]]$wcautil_arm64_replace_spots = (0x000092CC,0x000135FA,0x0002AB72,0x0002F2C6,0x0003A462,0x0003EBBC,0x00049010,0x001D0943)

function ReplaceStringInBinaryFile {
  param (
    [Parameter(Mandatory, Position=0)] [String] $filePath,
    [Parameter(Mandatory, Position=1)] [int[]] $offsetList
  )

  $bytes  = [System.IO.File]::ReadAllBytes($filePath)

  #Verify the string we are replacing matches what we expect.
  foreach ($offset in $offsetList) {
    if ($bytes[$offset] -ne 0x2D) { # '-'
      Write-Host -ForegroundColor Red "Binary file " $filePath " didn't match as expected in offset " $offset "`r`n"
      exit 1
    }
    if ($bytes[$offset+1] -ne 0x77) { # 'w'
      Write-Host -ForegroundColor Red "Binary file " $filePath " didn't match as expected in offset " $offset "`r`n"
      exit 1
    }
    if ($bytes[$offset+2] -ne 0x64) { # 'd'
      Write-Host -ForegroundColor Red "Binary file " $filePath " didn't match as expected in offset " $offset "`r`n"
      exit 1
    }
    if ($bytes[$offset+3] -ne 0x34) { # '4'
      Write-Host -ForegroundColor Red "Binary file " $filePath " didn't match as expected in offset " $offset "`r`n"
      exit 1 
    }
    if($bytes[$offset+4] -ne 0x39) { # '9'
      Write-Host -ForegroundColor Red "Binary file " $filePath " didn't match as expected in offset " $offset "`r`n"
      exit 1
    }
    if($bytes[$offset+5] -ne 0x39) { # '9'
      Write-Host -ForegroundColor Red "Binary file " $filePath " didn't match as expected in offset " $offset "`r`n"
      exit 1
    }
    if($bytes[$offset+6] -ne 0x36) { # '6'
      Write-Host -ForegroundColor Red "Binary file " $filePath " didn't match as expected in offset " $offset "`r`n"
      exit 1
    }
    $bytes[$offset] = 0x20
    $bytes[$offset+1] = 0x20
    $bytes[$offset+2] = 0x20
    $bytes[$offset+3] = 0x20
    $bytes[$offset+4] = 0x20
    $bytes[$offset+5] = 0x20
    $bytes[$offset+6] = 0x20
  }

  [System.IO.File]::WriteAllBytes($filePath, $bytes)

}

ReplaceStringInBinaryFile "C:\Program Files (x86)\WiX Toolset v3.14\SDK\VS2017\lib\x64\dutil.lib" $dutil_x64_replace_spots
ReplaceStringInBinaryFile "C:\Program Files (x86)\WiX Toolset v3.14\SDK\VS2017\lib\ARM64\dutil.lib" $dutil_arm64_replace_spots
ReplaceStringInBinaryFile "C:\Program Files (x86)\WiX Toolset v3.14\SDK\VS2017\lib\x64\wcautil.lib" $wcautil_x64_replace_spots
ReplaceStringInBinaryFile "C:\Program Files (x86)\WiX Toolset v3.14\SDK\VS2017\lib\ARM64\wcautil.lib" $wcautil_arm64_replace_spots
