# Fuzzing .NET Code with OneFuzz

This document explains the purpose of the project, the rationale for using specific technologies, and key instructions for fuzz testing .NET code using OneFuzz.

## Overview

This project demonstrates fuzz testing for .NET applications. It uses a `.NET 8 (Windows)` project where a code file is linked to the project. The linked file contains the functions required for fuzz testing.

## Why Use .NET 8 (Windows)?

1. **Current Support**: At the time of writing, OneFuzz supports only .NET 8 projects. The Fuzz team is actively working on .NET 9 support.
2. **Interim Solution**: Until .NET 9 support is available, .NET 8 serves as a robust and temporary solution for fuzz testing, enabling direct code linking for efficient development.

## Requesting Access

To log into the production instance of OneFuzz with the CLI, you **must request access**. Visit the internal [OneFuzz Access Request Page](https://myaccess.microsoft.com/@microsoft.onmicrosoft.com#/access-packages/6df691eb-e3d1-444b-b4b2-9e944dc794be) for details.

## How to Fuzz .NET Code

To set up and run fuzz testing on .NET code, follow the detailed guide available [Fuzz .NET Code](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/howto/fuzzing-dotnet-code).

## Running a .NET Fuzz Target Locally

Testing a .NET fuzz target locally requires specific configurations. For a step-by-step guide, see the section on [Running a .NET Fuzz Target Locally](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/howto/fuzzing-dotnet-code#extra-running-a-net-fuzz-target-locally).

## Writing a Good OneFuzzConfig.json

The `OneFuzzConfig.json` file provides critical information for deploying fuzzing jobs using the OneFuzz Ingestion Preparation Tool and Ingestion Service.

### Structure

The primary structure is an array of configuration entries. Outside the array, the `configVersion` field is used to track changes to the configuration schema.

For more details on how to write and structure this file, see the [OneFuzzConfig V3 Documentation](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/onefuzzconfig/onefuzzconfigv3).

## Tools

### OneFuzz Ingestion Preparation (OIP) Tool

The OIP tool helps prepare data for ingestion and fuzz testing. Learn more about [OneFuzz Ingestion Preparation (OIP) Tool](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/oip/onefuzzingestionpreparationtool).

### OneFuzz CLI

The CLI provides commands to manage and execute fuzzing jobs. Download and set up the CLI by following this [guide](https://eng.ms/docs/cloud-ai-platform/azure-edge-platform-aep/aep-security/epsf-edge-and-platform-security-fundamentals/the-onefuzz-service/onefuzz/howto/downloading-cli).

