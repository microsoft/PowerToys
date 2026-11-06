// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.AgentSkills;

public enum AgentSkillTarget
{
    Copilot,
    ClaudeCode,
    Codex,
    Custom,
}

public enum AgentSkillInstallState
{
    NotInstalled,
    Installed,
    UpdateAvailable,
    Modified,
    Conflict,
}

public sealed record AgentSkillStatus(
    AgentSkillTarget Target,
    string DestinationDirectory,
    string AvailableVersion,
    string? InstalledVersion,
    AgentSkillInstallState State,
    bool KeepUpdated);
