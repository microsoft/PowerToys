# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

$scriptPath = Join-Path $PSScriptRoot '..\verifyWorkspacesMsiAssets.ps1'

Describe 'Workspaces MSI assets' {
    BeforeEach {
        $path = Join-Path $TestDrive 'PowerToys.wxs'
        [xml]$wix = @'
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package>
    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="INSTALLFOLDER">
        <Directory Name="Assets">
          <Directory Id="RootWorkspaces" Name="Workspaces">
            <Component Id="RootIcons"><File Name="DefaultIcon.ico" /><File Name="Workspaces.ico" /></Component>
          </Directory>
        </Directory>
        <Directory Name="WinUI3Apps">
          <Directory Name="Assets">
            <Directory Id="WinUI3Workspaces" Name="Workspaces">
              <Component Id="WinUI3Icons"><File Name="DefaultIcon.ico" /><File Name="Workspaces.ico" /></Component>
            </Directory>
          </Directory>
        </Directory>
        <Component Id="OptionalCleanup">
          <RemoveFolder Directory="RootWorkspaces" On="uninstall" />
        </Component>
        <Component Id="Cleanup">
          <RemoveFolder Directory="RootWorkspaces" On="both" />
          <RemoveFolder Directory="WinUI3Workspaces" On="uninstall" />
        </Component>
      </Directory>
    </StandardDirectory>
    <Feature Id="CoreFeature" Level="1">
      <ComponentRef Id="RootIcons" />
      <ComponentRef Id="WinUI3Icons" />
      <ComponentRef Id="Cleanup" />
    </Feature>
    <Feature Id="DisabledFeature" Level="0"><ComponentRef Id="OptionalCleanup" /></Feature>
  </Package>
</Wix>
'@
        $core, $disabled = $wix.Wix.Package.Feature
    }

    It 'accepts a CoreFeature cleanup row after another feature cleans the same folder' {
        $wix.Save($path)
        { & $scriptPath -DecompiledMsi $path } | Should Not Throw
    }

    It 'rejects <Owner> owned only by a disabled feature' -TestCases @(
        @{ Owner = 'WinUI3Icons'; ErrorText = "CoreFeature does not install 'WinUI3Apps\Assets\Workspaces\DefaultIcon.ico'" }
        @{ Owner = 'Cleanup'; ErrorText = "CoreFeature has no uninstall cleanup for 'Assets\Workspaces'" }
    ) {
        param($Owner, $ErrorText)
        $reference = $core.ComponentRef | Where-Object Id -CEQ $Owner
        $null = $disabled.AppendChild($reference)
        $wix.Save($path)
        { & $scriptPath -DecompiledMsi $path } | Should Throw $ErrorText
    }

    It 'rejects a disabled CoreFeature' {
        $core.SetAttribute('Level', '0')
        $wix.Save($path)
        { & $scriptPath -DecompiledMsi $path } | Should Throw 'CoreFeature does not install'
    }
}
