<#
    .SYNOPSIS
    Modify XAML files to adhere to XAML Styler settings.

    .DESCRIPTION
    The Apply XAML Stying Script can be used to check or modify XAML files with the repo's XAML Styler settings.
    Learn more about XAML Styler at https://github.com/Xavalon/XamlStyler

    By default, uses git status to check all new or modified files.

    Use "PS> Help .\applyXamlStyling.ps1 -Full" for more details on parameters.

    .PARAMETER LastCommit
    Runs against last commit vs. current changes

    .PARAMETER Unstaged
    Runs against unstaged changed files

    .PARAMETER Staged
    Runs against staged files vs. current changes

    .PARAMETER Main
    Runs against main vs. current branch

    .PARAMETER Passive
    Runs a passive check against all files in the repo for the CI

    .EXAMPLE
    PS> .\applyXamlStyling.ps1 -Main
#>
param(
    [switch]$LastCommit = $false,
    [switch]$Unstaged = $false,
    [switch]$Staged = $false,
    [switch]$Main = $false,
    [switch]$Passive = $false
)

Write-Output "Use 'Help .\applyXamlStyling.ps1' for more info or '-Main' to run against all files."
Write-Output ""
Write-Output "Restoring dotnet tools..."
dotnet tool restore --disable-parallel --no-cache

if (-not $Passive)
{
    # Look for unstaged changed files by default
    $gitDiffCommand = "git status -s --porcelain"

    if ($Main)
    {
        Write-Output 'Checking Current Branch against `main` Files Only'
        $branch = git status | Select-String -Pattern "On branch (?<branch>.*)$"
        if ($null -eq $branch.Matches)
        {
            $branch = git status | Select-String -Pattern "HEAD detached at (?<branch>.*)$"
            if ($null -eq $branch.Matches)
            {
                Write-Error 'Don''t know how to fetch branch from `git status`:'
                git status | Write-Error
                exit 1
            }
        }
        $branch = $branch.Matches.groups[1].Value
        $gitDiffCommand = "git diff origin/main $branch --name-only --diff-filter=ACM"
    }
    elseif ($Unstaged)
    {
        # Look for unstaged files
        Write-Output "Checking Unstaged Files"
        $gitDiffCommand = "git diff --name-only --diff-filter=ACM"
    }
    elseif ($Staged)
    {
        # Look for staged files
        Write-Output "Checking Staged Files Only"
        $gitDiffCommand = "git diff --cached --name-only --diff-filter=ACM"
    }
    elseif ($LastCommit)
    {
        # Look at last commit files
        Write-Output "Checking the Last Commit's Files Only"
        $gitDiffCommand = "git diff HEAD^ HEAD --name-only --diff-filter=ACM"
    }
    else 
    {
        Write-Output "Checking Git Status Files Only"    
    }

    Write-Output "Running Git Diff: $gitDiffCommand"
    $files = Invoke-Expression $gitDiffCommand | Select-String -Pattern "\.xaml$"

    if (-not $Passive -and -not $Main -and -not $Unstaged -and -not $Staged -and -not $LastCommit)
    {
        # Remove 'status' column of 3 characters at beginning of lines
        $files = $files | ForEach-Object { $_.ToString().Substring(3) }
    }

    if ($files.count -gt 0)
    {
        dotnet tool run xstyler -c "$PSScriptRoot\..\Settings.XamlStyler" -f $files
    }
    else
    {
        Write-Output "No XAML Files found to style..."
    }
}
else 
{
    Write-Output "Checking all files (passively)"
    $files = Get-ChildItem -Path "$PSScriptRoot\..\src\*.xaml" -Recurse | Select-Object -ExpandProperty FullName | Where-Object { $_ -notmatch "(\\obj\\)|(\\bin\\)|(\\x64\\)|(\\Generated Files\\PowerRenameXAML\\)" }

    if ($files.count -gt 0)
    {
        dotnet tool run xstyler -p -c "$PSScriptRoot\..\Settings.XamlStyler" -f $files

        if ($lastExitCode -eq 1)
        {
            Write-Error 'XAML Styling is incorrect, please run `.\.pipelines\applyXamlStyling.ps1 -Main` locally.'
        }

        # Return XAML Styler Status
        exit $lastExitCode
    }
    else
    {
        exit 0
    }
}
