#Requires -Version 7.2

using namespace System.Management.Automation

[CmdletBinding()]
param()

function PublicStaticVoidMain {
  [CmdletBinding()]
  param ()

  class TypeHandlerData {
    [String] $Name
    [String] $CurrentUserHandler
    [String] $MachineWideHandler
  }

  [String[]]$TypesToCheck = @(".markdown", ".mdtext", ".mdtxt", ".mdown", ".mkdn", ".mdwn", ".mkd", ".md", ".svg", ".svgz", ".pdf", ".gcode", ".stl", ".txt", ".ini", ".qoi")
  $IPREVIEW_HANDLER_CLSID = '{8895b1c6-b41f-4c1c-a562-0d564250836f}'
  $PowerToysHandlers = @{
    '{07665729-6243-4746-95b7-79579308d1b2}' = "PowerToys PDF handler"
    '{ddee2b8a-6807-48a6-bb20-2338174ff779}' = "PowerToys SVG handler"
    '{ec52dea8-7c9f-4130-a77b-1737d0418507}' = "PowerToys GCode handler"
    '{8AA07897-C30B-4543-865B-00A0E5A1B32D}' = "PowerToys QOI handler"
    '{45769bcc-e8fd-42d0-947e-02beef77a1f5}' = "PowerToys Markdown handler"
    '{afbd5a44-2520-4ae0-9224-6cfce8fe4400}' = "PowerToys Monaco fallback handler"
    '{DC6EFB56-9CFA-464D-8880-44885D7DC193}' = "Adobe Acrobat DC"
  }

  function ResolveHandlerGUIDtoName {
    param (
      [Parameter(Mandatory, Position = 0)]
      [String] $GUID
    )
    return $PowerToysHandlers[$GUID] ?? $GUID
  }

  function WriteMyProgress {
    param (
      [Parameter(Mandatory, Position=0)] [Int32] $ItemsPending,
      [Parameter(Mandatory, Position=1)] [Int32] $ItemsTotal,
      [switch] $Completed
    )
    [Int32] $PercentComplete = ($ItemsPending / $ItemsTotal) * 100
    if ($PercentComplete -lt 1) { $PercentComplete = 1}
    Write-Progress -Activity 'Querying Windows Registry' -Status "$ItemsPending of $ItemsTotal" -PercentComplete $PercentComplete -Completed:$Completed
  }

  $ItemsTotal = $TypesToCheck.Count * 2
  $ItemsPending = 0
  WriteMyProgress 0 $ItemsTotal

  $CheckResults = New-Object -TypeName 'System.Collections.Generic.List[TypeHandlerData]'
  foreach ($item in $TypesToCheck) {
    $CurrentUserGUID = Get-ItemPropertyValue -Path "HKCU://Software/Classes/$item/shellex/$IPREVIEW_HANDLER_CLSID" -Name '(default)' -ErrorAction SilentlyContinue
    $ItemsPending += 1
    WriteMyProgress $ItemsPending $ItemsTotal

    $MachineWideGUID = "Didn't check"
    # $MachineWideGUID = Get-ItemPropertyValue -Path "HKLM://Software/Classes/$item/shellex/$IPREVIEW_HANDLER_CLSID" -Name '(default)' -ErrorAction SilentlyContinue
    $ItemsPending += 1
    WriteMyProgress $ItemsPending $ItemsTotal

    $temp = New-Object -TypeName TypeHandlerData -Property @{
      Name               = $item
      CurrentUserHandler = ($null -eq $CurrentUserGUID) ? "Nothing" : (ResolveHandlerGUIDtoName ($CurrentUserGUID))
      MachineWideHandler = ($null -eq $MachineWideGUID) ? "Nothing" : (ResolveHandlerGUIDtoName ($MachineWideGUID))
    }
    $CheckResults.Add($temp)

    Clear-Variable 'CurrentUserGUID', 'MachineWideGUID'
  }
  WriteMyProgress $ItemsPending $ItemsTotal -Completed
  $CheckResults | Format-Table -AutoSize
}

PublicStaticVoidMain
