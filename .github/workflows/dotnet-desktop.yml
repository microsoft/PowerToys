# This workflow uses actions that are not certified by GitHub.
# They are provided by a third-party and are governed by
# separate terms of service, privacy policy, and support
# documentation.

# This workflow will build, test, sign and package a WPF or Windows Forms desktop application
# built on .NET Core.
# To learn how to migrate your existing application to .NET Core,
# refer to https://docs.microsoft.com/en-us/dotnet/desktop-wpf/migration/convert-project-from-net-framework
#
# To configure this workflow:
#
# 1. Configure environment variables
# GitHub sets default environment variables for every workflow run.
# Replace the variables relative to your project in the "env" section below.
#
# 2. Signing
# Generate a signing certificate in the Windows Application
# Packaging Project or add an existing signing certificate to the project.
# Next, use PowerShell to encode the .pfx file using Base64 encoding
# by running the following Powershell script to generate the output string:
#
# $pfx_cert = Get-Content '.\SigningCertificate.pfx' -Encoding Byte
# [System.Convert]::ToBase64String($pfx_cert) | Out-File 'SigningCertificate_Encoded.txt'
#
# Open the output file, SigningCertificate_Encoded.txt, and copy the
# string inside. Then, add the string to the repo as a GitHub secret
# and name it "Base64_Encoded_Pfx."
# For more information on how to configure your signing certificate for
# this workflow, refer to https://github.com/microsoft/github-actions-for-desktop-apps#signing
#
# Finally, add the signing certificate password to the repo as a secret and name it "Pfx_Key".
# See "Build the Windows Application Packaging project" below to see how the secret is used.
#
# For more information on GitHub Actions, refer to https://github.com/features/actions
# For a complete CI/CD sample to get started with GitHub Action workflows for Desktop Applications,
# refer to https://github.com/microsoft/github-actions-for-desktop-apps

name: .NET Core Desktop

on:
  push:
    branches: [ "main" ]
  pull_request:
    branches: [ "main" ]

jobs:

  build:

    strategy:
      matrix:
        configuration: [Debug, Release]

    runs-on: windows-latest  # For a list of available runner types, refer to
                             # https://help.github.com/en/actions/reference/workflow-syntax-for-github-actions#jobsjob_idruns-on

    env:
      Solution_Name: your-solution-name                         # Replace with your solution name, i.e. MyWpfApp.sln.
      Test_Project_Path: your-test-project-path                 # Replace with the path to your test project, i.e. MyWpfApp.Tests\MyWpfApp.Tests.csproj.
      Wap_Project_Directory: your-wap-project-directory-name    # Replace with the Wap project directory relative to the solution, i.e. MyWpfApp.Package.
      Wap_Project_Path: your-wap-project-path                   # Replace with the path to your Wap project, i.e. MyWpf.App.Package\MyWpfApp.Package.wapproj.

    steps:
    - name: Checkout
      uses: actions/checkout@v3
      with:
        fetch-depth: 0

    # Install the .NET Core workload
    - name: Install .NET Core
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 6.0.x

    # Add  MSBuild to the PATH: https://github.com/microsoft/setup-msbuild
    - name: Setup MSBuild.exe
      uses: microsoft/setup-msbuild@v1.0.2

    # Execute all unit tests in the solution
    - name: Execute unit tests
      run: dotnet test

    # Restore the application to populate the obj folder with RuntimeIdentifiers
    - name: Restore the application
      run: msbuild $env:Solution_Name /t:Restore /p:Configuration=$env:Configuration
      env:
        Configuration: ${{ matrix.configuration }}

    # Decode the base 64 encoded pfx and save the Signing_Certificate
    - name: Decode the pfx
      run: |
        $pfx_cert_byte = [System.Convert]::FromBase64String("${{ secrets.Base64_Encoded_Pfx }}")
        $certificatePath = Join-Path -Path $env:Wap_Project_Directory -ChildPath GitHubActionsWorkflow.pfx
        [IO.File]::WriteAllBytes("$certificatePath", $pfx_cert_byte)

    # Create the app package by building and packaging the Windows Application Packaging project
    - name: Create the app package
      run: msbuild $env:Wap_Project_Path /p:Configuration=$env:Configuration /p:UapAppxPackageBuildMode=$env:Appx_Package_Build_Mode /p:AppxBundle=$env:Appx_Bundle /p:PackageCertificateKeyFile=GitHubActionsWorkflow.pfx /p:PackageCertificatePassword=${{ secrets.Pfx_Key }}
      env:
        Appx_Bundle: Always
        Appx_Bundle_Platforms: x86|x64
        Appx_Package_Build_Mode: StoreUpload
        Configuration: ${{ matrix.configuration }}

    # Remove the pfx
    - name: Remove the pfx
      run: Remove-Item -path $env:Wap_Project_Directory\GitHubActionsWorkflow.pfx

    # Upload the MSIX package: https://github.com/marketplace/actions/upload-a-build-artifact
    - name: Upload build artifacts
      uses: actions/upload-artifact@v3
      with:
        name: MSIX Package
        path: ${{ env.Wap_Project_Directory }}\AppPackages
