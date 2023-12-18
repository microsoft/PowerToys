$profileContent = Get-Content $PROFILE

$newContent = ""
$linesToDeleteFound = $False

$profileContent | ForEach-Object {
  if ($_.Contains("34de4b3d-13a8-4540-b76d-b9e8d3851756") -and !$linesToDeleteFound)
  {
    $linesToDeleteFound = $True
    return
  }

  if ($_.Contains("34de4b3d-13a8-4540-b76d-b9e8d3851756") -and $linesToDeleteFound)
  {
    $linesToDeleteFound = $False
    return
  }

  if($linesToDeleteFound)
  {
    return
  }

  $newContent += $_ + "`r`n"
}

Set-Content -Path $PROFILE -Value $newContent