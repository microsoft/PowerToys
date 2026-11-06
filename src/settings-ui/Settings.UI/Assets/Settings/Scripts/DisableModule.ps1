$temporaryPath = $null
try {
  $profileBytes = [System.IO.File]::ReadAllBytes($PROFILE)
  $stream = [System.IO.MemoryStream]::new($profileBytes, $false)
  $reader = [System.IO.StreamReader]::new($stream, [System.Text.UTF8Encoding]::new($false, $true), $true)
  try {
    try {
      $profileContent = $reader.ReadToEnd()
      $encoding = $reader.CurrentEncoding
    }
    catch [System.Text.DecoderFallbackException] {
      if ($reader.CurrentEncoding.GetPreamble().Length -ne 0) {
        throw
      }

      # BOM-less legacy profiles can contain non-UTF-8 bytes. Map them losslessly
      # while recognizing the ASCII markers, without converting the user's text.
      $encoding = [System.Text.Encoding]::GetEncoding(28591)
      $profileContent = $encoding.GetString($profileBytes)
    }
  }
  finally {
    $reader.Dispose()
  }

  $roundTripBytes = $encoding.GetPreamble() + $encoding.GetBytes($profileContent)
  if ([Convert]::ToBase64String($profileBytes) -cne [Convert]::ToBase64String($roundTripBytes)) {
    throw [System.IO.InvalidDataException]::new("The profile encoding could not be read losslessly. The profile was not changed.")
  }

  # Only actual comment tokens can delimit a block, not examples inside strings
  # or block comments. Parsing does not execute any profile code.
  $tokens = $null
  $parseErrors = $null
  [void][System.Management.Automation.Language.Parser]::ParseInput($profileContent, [ref]$tokens, [ref]$parseErrors)
  $commentOffsets = [System.Collections.Generic.HashSet[int]]::new()
  foreach ($token in $tokens) {
    if ($token.Kind -eq [System.Management.Automation.Language.TokenKind]::Comment) {
      [void]$commentOffsets.Add($token.Extent.StartOffset)
    }
  }

  $markerPattern = '^[ \t]*#(?<guid>34de4b3d-13a8-4540-b76d-b9e8d3851756|f45873b3-b655-43a6-b217-97c00aa0db58)(?<opening> PowerToys CommandNotFound module)?[ \t]*$'
  $blocks = [System.Collections.Generic.List[object]]::new()
  $opening = $null
  foreach ($line in [regex]::Matches($profileContent, '[^\r\n]+(?:\r\n|\r|\n|$)')) {
    $marker = [regex]::Match($line.Value.TrimEnd("`r", "`n"), $markerPattern)
    if (-not $marker.Success -or -not $commentOffsets.Contains($line.Index + $line.Value.IndexOf('#'))) {
      continue
    }

    if ($marker.Groups['opening'].Success) {
      if ($null -ne $opening) {
        throw [System.IO.InvalidDataException]::new("Command Not Found has nested or duplicate opening markers. The profile was not changed. Repair the markers and try again.")
      }

      $opening = [PSCustomObject]@{ Index = $line.Index; Guid = $marker.Groups['guid'].Value }
    }
    else {
      if ($null -eq $opening -or $opening.Guid -cne $marker.Groups['guid'].Value) {
        throw [System.IO.InvalidDataException]::new("Command Not Found has an unmatched closing marker. The profile was not changed. Repair the markers and try again.")
      }

      $blocks.Add([PSCustomObject]@{ Index = $opening.Index; Length = $line.Index + $line.Length - $opening.Index })
      $opening = $null
    }
  }

  if ($null -ne $opening) {
    throw [System.IO.InvalidDataException]::new("Command Not Found has an unclosed opening marker. The profile was not changed. Repair the markers and try again.")
  }

  if ($blocks.Count -eq 0) {
    Write-Host "No instance of Command Not Found was found in the profile file."
    # This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
    return
  }

  # Validate every boundary before removing any block, retaining all other characters
  # including their original line endings and the absence of a final newline.
  $newContent = $profileContent
  for ($index = $blocks.Count - 1; $index -ge 0; $index--) {
    $newContent = $newContent.Remove($blocks[$index].Index, $blocks[$index].Length)
  }

  $newBytes = $encoding.GetPreamble() + $encoding.GetBytes($newContent)
  $temporaryPath = Join-Path ([System.IO.Path]::GetDirectoryName($PROFILE)) ".$([System.IO.Path]::GetFileName($PROFILE)).$([Guid]::NewGuid().ToString('N')).tmp"
  [System.IO.File]::WriteAllBytes($temporaryPath, $newBytes)
  # Publish only the completed file instead of truncating the original profile.
  [System.IO.File]::Replace($temporaryPath, $PROFILE, [NullString]::Value)
}
catch [System.IO.InvalidDataException], [System.IO.IOException], [System.UnauthorizedAccessException], [System.Text.DecoderFallbackException] {
  # Settings captures stdout, not stderr. Never emit either success message on failure.
  Write-Host "Command Not Found removal failed: $($_.Exception.Message)"
  throw
}
finally {
  if ($null -ne $temporaryPath) {
    [System.IO.File]::Delete($temporaryPath)
  }
}

Write-Host "Removed the Command Not Found reference from the profile file."
# This message will be compared against in Command Not Found Settings page code behind. Take care when changing it.
