// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.MouseWithoutBorders.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class GuestUiDiagnosticsTests
{
    private static readonly string[] AllowedSnapshotProperties =
    [
        "QueryStatus", "SettingsQueryStatus", "CoordinationQueryStatus", "WorkflowConfigured",
        "OwnerMatchesWorkflow", "ChildOwnsTurn", "ChildWaitingForTurn", "RecordingOwnsTurn",
        "ChildCpuSeconds", "SettingsCpuSeconds", "SettingsResponding", "SettingsProcessId",
        "SettingsStartTimeUtc", "SettingsHwnd", "SettingsThreadCount", "SettingsWorkingSetBytes",
        "SettingsPrivateMemoryBytes", "QueryErrorHResult",
    ];

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public void CoordinationSnapshotDistinguishesOwnershipWithoutExportingWorkflowMaterial(bool ownsTurn, bool matchingWorkflow)
    {
        var root = Path.Combine(Environment.CurrentDirectory, "mwb-ui-diagnostics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var support = Path.Combine(AppContext.BaseDirectory, @"ExperimentPayload\EndpointSupport.ps1");
            var script = """
                $ErrorActionPreference='Stop'
                $tokens=$null; $errors=$null
                $ast=[Management.Automation.Language.Parser]::ParseFile($Support,[ref]$tokens,[ref]$errors)
                if($errors.Count){throw 'Invalid support script'}
                $definitions=$ast.FindAll({
                    param($node)
                    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -in @('Read-RunJson','Get-GuestUiWaitSnapshot')
                },$true)
                foreach($definition in $definitions){. ([scriptblock]::Create($definition.Extent.Text))}
                $env:USERPROFILE=$Root
                $env:WINAPP_UI_WORKFLOW_ID='private-workflow-fixture'
                $script:settingsPid=$PID
                $process=[Diagnostics.Process]::GetCurrentProcess()
                $hash=[Security.Cryptography.SHA256]::Create()
                try{$key=[BitConverter]::ToString($hash.ComputeHash(
                    [Text.Encoding]::UTF8.GetBytes("winapp-ui-workflow-v1`0$env:WINAPP_UI_WORKFLOW_ID"))).Replace('-','').ToLowerInvariant()}
                finally{$hash.Dispose()}
                $stateRoot=Join-Path $Root '.winapp\state\ui'
                $null=New-Item -ItemType Directory $stateRoot -Force
                $owners=@(@{Pid=0;Operation='ui record'})
                $waiters=@()
                if($OwnsTurn){$owners+=@{Pid=$PID;Operation='ui invoke'}}else{$waiters+=@{Pid=$PID}}
                $state=@{Owner=@{Key=$(if($Matches){$key}else{'private-other-owner'})};OwnerCommands=$owners;Waiters=$waiters}
                $statePath=Join-Path $stateRoot "interactive-desktop-$($process.SessionId).state.json"
                $state|ConvertTo-Json -Depth 5|Set-Content $statePath
                $writer=[IO.File]::Open($statePath,'Open','ReadWrite',([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
                try{Get-GuestUiWaitSnapshot $process|ConvertTo-Json -Compress}finally{$writer.Dispose()}
                """;
            script = $"$Root='{root.Replace("'", "''")}';$Support='{support.Replace("'", "''")}';" +
                $"$OwnsTurn=${ownsTurn};$Matches=${matchingWorkflow};" + script;
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe");
            using var command = WinAppSandboxCommand.Start(executable, ["-NoProfile", "-NonInteractive", "-Command", script], root, root);
            var output = command.CompleteAndDispose(TimeSpan.FromSeconds(15)).RequireSuccess();
            using var document = JsonDocument.Parse(output);
            var snapshot = document.RootElement;
            Assert.AreEqual("Succeeded", snapshot.GetProperty("QueryStatus").GetString());
            Assert.AreEqual("Succeeded", snapshot.GetProperty("SettingsQueryStatus").GetString());
            Assert.AreEqual("Succeeded", snapshot.GetProperty("CoordinationQueryStatus").GetString());
            Assert.IsTrue(snapshot.GetProperty("WorkflowConfigured").GetBoolean());
            Assert.AreEqual(matchingWorkflow, snapshot.GetProperty("OwnerMatchesWorkflow").GetBoolean());
            Assert.AreEqual(ownsTurn, snapshot.GetProperty("ChildOwnsTurn").GetBoolean());
            Assert.AreEqual(!ownsTurn, snapshot.GetProperty("ChildWaitingForTurn").GetBoolean());
            Assert.IsTrue(snapshot.GetProperty("RecordingOwnsTurn").GetBoolean());
            Assert.IsFalse(output.Contains("private-", StringComparison.Ordinal));
            Assert.AreEqual(JsonValueKind.Null, snapshot.GetProperty("QueryErrorHResult").ValueKind);
            CollectionAssert.AreEquivalent(
                AllowedSnapshotProperties,
                snapshot.EnumerateObject().Select(property => property.Name).ToArray(),
                "Diagnostics must contain exactly the fixed allowlisted fields.");
            Assert.IsTrue(snapshot.GetProperty("SettingsProcessId").GetInt32() > 0);
            Assert.IsTrue(snapshot.GetProperty("SettingsThreadCount").GetInt32() > 0);
            Assert.IsTrue(snapshot.GetProperty("SettingsWorkingSetBytes").GetInt64() > 0);
            Assert.IsTrue(snapshot.GetProperty("SettingsPrivateMemoryBytes").GetInt64() > 0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
