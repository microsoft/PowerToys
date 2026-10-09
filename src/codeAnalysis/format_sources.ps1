param (
  [switch]$all = $false
)

if(!(Get-Command "git" -ErrorAction SilentlyContinue)) {
  throw "You need to have a git in path to be able to format only the dirty files!"
}

$clangFormat = "clang-format.exe"
if(!(Get-Command $clangFormat -ErrorAction SilentlyContinue)) {
    Write-Information "Can't find clang-format.exe in %PATH%, trying to use %VCINSTALLDIR%..."
    $clangFormat="$env:VCINSTALLDIR\Tools\Llvm\bin\clang-format.exe"
    if(!(Test-Path -Path $clangFormat -PathType leaf)) {
      throw "Can't find clang-format.exe executable. Make sure you either have it in %PATH% or run this script from vcvars.bat!"
    }
}

$sourceExtensions = New-Object System.Collections.Generic.HashSet[string]
$sourceExtensions.Add(".cpp") | Out-Null
$sourceExtensions.Add(".h")   | Out-Null

function Get-Dirty-Files-From-Git() {
  $repo_root = & git rev-parse --show-toplevel

  $staged    = & git diff --name-only --diff-filter=d --cached | % { $repo_root + "/" + $_ }
  $unstaged  = & git ls-files -m
  $untracked = & git ls-files --others --exclude-standard
  $result = New-Object System.Collections.Generic.List[string]
  $staged, $unstaged, $untracked | % {
    $_.Split(" ") | 
      where {Test-Path $_ -PathType Leaf} |
      where {$sourceExtensions.Contains((Get-Item $_).Extension)} | 
      foreach {$result.Add($_)}
  } 
  return $result
}

if($all) { 
  $filesToFormat = 
    Get-ChildItem -Recurse -File ..\src | 
    Resolve-Path -Relative |
    where { (Get-Item $_).Directory -notmatch "(Generated Files)|node_modules" -And 
      $sourceExtensions.Contains((Get-Item $_).Extension)}
}
else {
  $filesToFormat = Get-Dirty-Files-From-Git
}

$filesToFormat | % {
  Write-Host "Formatting $_"
  & $clangFormat -i -style=file -fallback-style=none $_ 2>&1
}

Write-Host "Done!"