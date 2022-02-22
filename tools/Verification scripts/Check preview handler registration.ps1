#Requires -Version 7.2

using namespace System.Management.Automation

[CmdletBinding()]
param()

function PublicStaticVoidMain {
  [CmdletBinding()]
  param ()

  class TypeHandlerData {
    [String] $Name
    [String] $Handler
  }

  [String[]]$TypesToCheck = @(".svg", ".svgz", ".pdf", ".gcode", ".stl", ".md", ".markdown", ".mdown", ".mkdn", ".mkd", ".mdwn", ".mdtxt", ".mdtext", ".txt", ".ini")
   $IPREVIEW_HANDLER_CLSID = '{8895b1c6-b41f-4c1c-a562-0d564250836f}'
  $PowerToysHandlers = @{
    '{07665729-6243-4746-95b7-79579308d1b2}' = "PowerToys PDF handler"
    '{ddee2b8a-6807-48a6-bb20-2338174ff779}' = "PowerToys SVG handler"
    '{ec52dea8-7c9f-4130-a77b-1737d0418507}' = "PowerToys GCode handler"
    '{45769bcc-e8fd-42d0-947e-02beef77a1f5}' = "PowerToys Markdown handler"
    '{afbd5a44-2520-4ae0-9224-6cfce8fe4400}' = "PowerToys Monaco fallback handler"
  }

  function ResolveHandlerGUIDtoName {
    param (
        [Parameter(Mandatory,Position=0)]
        [String]
        $GUID
    )
    return $PowerToysHandlers[$GUID] ?? "Something else"
  }

  $TypesToCheck | ForEach-Object {
    $HandlerGUID = $null
    $HandlerGUID = Get-ItemPropertyValue -Path "HKCU://Software/Classes/$_/shellex/$IPREVIEW_HANDLER_CLSID" -Name '(default)' -ErrorAction SilentlyContinue

    New-Object -TypeName TypeHandlerData -Property @{
      Name = $_
      Handler = ($null -eq $HandlerGUID) ? "Nothing for current user" : (ResolveHandlerGUIDtoName ($HandlerGUID))
    }
  } | Format-Table -Autosize
}

PublicStaticVoidMain
