// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.AgentSkills;

public static class AgentSkillBundle
{
    public static string GetDirectory(string applicationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        var developmentDirectory = Path.GetFullPath(Path.Combine(applicationDirectory, "PowerScriptsAgentSkill"));
        var installedDirectory = Path.GetFullPath(Path.Combine(applicationDirectory, "..", "PowerScripts", "AgentSkill"));
        foreach (var directory in new[] { developmentDirectory, installedDirectory })
        {
            if (File.Exists(Path.Combine(directory, "SKILL.md")))
            {
                return directory;
            }
        }

        throw new FileNotFoundException(
            "The bundled PowerScripts skill could not be found. Repair or reinstall PowerToys.",
            Path.Combine(installedDirectory, "SKILL.md"));
    }
}
