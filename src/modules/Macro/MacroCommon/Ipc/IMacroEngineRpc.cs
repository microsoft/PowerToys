// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.MacroCommon.Ipc;

public interface IMacroEngineRpc
{
    Task ExecuteMacroAsync(string macroId, CancellationToken ct);

    Task SuspendHotkeysAsync(CancellationToken ct);

    Task ResumeHotkeysAsync(CancellationToken ct);

    Task<IReadOnlyList<string>> GetMacroIdsAsync(CancellationToken ct);
}

public static class MacroIpcConstants
{
    public const string PipeName = "PowerToys.MacroEngine";
}
