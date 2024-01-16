@{
    ModuleVersion = '0.1.0'
    GUID = '28c9afa2-92e5-413e-8e53-44b2d7a83ac6'
    Author = 'Carlos Zamora'
    CompanyName = "Microsoft Corporation"
    Copyright = "Copyright (c) Microsoft Corporation."
    Description = 'Enable suggestions on how to install missing commands via winget'
    PowerShellVersion = '7.4'
    NestedModules = @('PowerToys.CmdNotFound.dll')
    RequiredModules   = @(@{ModuleName = 'Microsoft.WinGet.Client';  ModuleVersion = "0.2.1"; })
}
