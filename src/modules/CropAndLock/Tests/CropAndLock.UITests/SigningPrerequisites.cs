// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CropAndLock.UITests
{
    internal static class SigningPrerequisites
    {
        private const string SetupHelp =
            "See CropAndLock.UITests\\README.md (Local Test Explorer and Sign before local VM deployment). " +
            "Builds/restaging can replace signed files; prepare signing again before rerunning these scenarios.";

        internal sealed record Signature(string Status, string Message, string Signer, bool MachineTrusted);

        internal static void RequirePackage(string path)
        {
            RequireSignature(
                ReadSignature(path),
                requireMicrosoftPublisher: false,
                EnvironmentConfig.IsInPipeline,
                $"The packaged scenario requires a signed, trusted fixture: '{path}'. {SetupHelp}");
        }

        internal static void RequireSettingsClient()
        {
            var path = SessionHelper.GetExecutablePath(PowerToysModule.PowerToysSettings);
            RequireSignature(
                ReadSignature(path),
                requireMicrosoftPublisher: true,
                EnvironmentConfig.IsInPipeline,
                $"The authenticated Settings lifecycle scenario requires a trusted Microsoft-signed Settings client: '{path}'. {SetupHelp}");
        }

        internal static void RequireSignature(Signature signature, bool requireMicrosoftPublisher, bool isInPipeline, string requirement)
        {
            if (signature.Status == "Valid" &&
                (!requireMicrosoftPublisher || (signature.Signer == "Microsoft Corporation" && signature.MachineTrusted)))
            {
                return;
            }

            var message = $"{requirement} Signature status: {signature.Status}; signer: '{signature.Signer}'; " +
                $"machine trust: {signature.MachineTrusted}. {signature.Message}";
            var missingSetup = signature.Status is "NotSigned" or "NotTrusted" or "UnknownError" or "Valid";
            if (isInPipeline || !missingSetup)
            {
                Assert.Fail(message);
            }

            // Missing local setup is not a product failure. CI and corrupt signatures still fail.
            Assert.Inconclusive(message);
        }

        private static Signature ReadSignature(string path)
        {
            Assert.IsTrue(File.Exists(path), $"Required test/runtime file was not staged: '{path}'.");
            const string command = """
                $ErrorActionPreference = 'Stop'
                $signature = Get-AuthenticodeSignature -LiteralPath $env:POWERTOYS_UITEST_SIGNATURE_PATH
                $signer = ''
                $machineTrusted = $false
                if ($null -ne $signature.SignerCertificate) {
                    $signer = $signature.SignerCertificate.GetNameInfo(
                        [Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false)
                    $chain = New-Object Security.Cryptography.X509Certificates.X509Chain($true)
                    try {
                        $chain.ChainPolicy.RevocationMode = [Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
                        $chain.ChainPolicy.UrlRetrievalTimeout = [TimeSpan]::FromSeconds(5)
                        $machineTrusted = $chain.Build($signature.SignerCertificate)
                    }
                    finally {
                        $chain.Dispose()
                    }
                }
                @{
                    Status = $signature.Status.ToString()
                    Message = $signature.StatusMessage
                    Signer = $signer
                    MachineTrusted = $machineTrusted
                } | ConvertTo-Json -Compress
                """;

            // Use Windows' existing Authenticode/SIP implementation for both PE and MSIX files.
            // The hidden read-only probe never signs files or adds certificate trust.
            var start = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add("-NoLogo");
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-OutputFormat");
            start.ArgumentList.Add("Text");
            start.ArgumentList.Add("-EncodedCommand");
            start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(command)));
            start.Environment["POWERTOYS_UITEST_SIGNATURE_PATH"] = path;

            // A pwsh-launched test host can otherwise make Windows PowerShell load incompatible PS7 modules.
            start.Environment.Remove("PSModulePath");

            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the signature prerequisite probe.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                throw new TimeoutException($"Signature prerequisite probe timed out for '{path}'.");
            }

            var output = stdout.GetAwaiter().GetResult();
            var error = stderr.GetAwaiter().GetResult();
            Assert.AreEqual(0, process.ExitCode, $"Signature prerequisite probe failed for '{path}': {error}");
            var signature = JsonSerializer.Deserialize<Signature>(output);
            Assert.IsNotNull(signature, $"Signature prerequisite probe returned no result for '{path}'.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(signature.Status), $"Signature prerequisite probe returned no status for '{path}'.");
            return signature;
        }
    }
}
