# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

$settingsRoot = Join-Path $PSScriptRoot '..\..\src\settings-ui\Settings.UI'

Describe 'MWB Connect-fields button visibility' {
    BeforeAll {
        [xml]$page = Get-Content -LiteralPath (Join-Path $settingsRoot 'SettingsXAML\Views\MouseWithoutBordersPage.xaml') -Raw
        [xml]$app = Get-Content -LiteralPath (Join-Path $settingsRoot 'SettingsXAML\App.xaml') -Raw
        $buttons = @($page.SelectNodes('//*[@AutomationProperties.AutomationId="MouseWithoutBordersShowConnectFieldsButton"]'))
    }

    It 'addresses the actual field-opening command separately from the submitting command' {
        $buttons.Count | Should Be 1
        $buttons[0].GetAttribute('Command') | Should Match '\bShowConnectFieldsCommand\b'
        $submits = @($page.SelectNodes('//*[@AutomationProperties.AutomationId="MouseWithoutBordersConnectButton"]'))
        $submits.Count | Should Be 1
        $submits[0].GetAttribute('Command') | Should Match '\bConnectCommand\b'
    }

    It 'does not invert the already reversed visibility converter a second time' {
        $buttons[0].GetAttribute('Visibility') | Should Match 'ViewModel.ConnectFieldsVisible'
        $buttons[0].GetAttribute('Visibility') | Should Match 'ReverseBoolToVisibilityConverter'
        $buttons[0].GetAttribute('Visibility') | Should Not Match 'ConverterParameter'
        $converter = $app.SelectSingleNode('//*[@*[local-name()="Key"]="ReverseBoolToVisibilityConverter"]')
        $converter.GetAttribute('FalseValue') | Should Be 'Visible'
        $converter.GetAttribute('TrueValue') | Should Be 'Collapsed'
    }
}
