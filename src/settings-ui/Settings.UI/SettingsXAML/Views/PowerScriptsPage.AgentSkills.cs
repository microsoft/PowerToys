// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerScripts.Core.AgentSkills;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class PowerScriptsPage
{
    private void CopyAgentSkillInstructions_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var resources = Helpers.ResourceLoaderInstance.ResourceLoader;
            var instructions = resources.GetString("PowerScripts_AgentSetupInstructions")
                .Replace("{0}", AgentSkillBundle.GetDirectory(AppContext.BaseDirectory), StringComparison.Ordinal);
            var data = new DataPackage();
            data.SetText(instructions);
            Clipboard.SetContent(data);
            Clipboard.Flush();
            AgentSkillFeedback.Severity = InfoBarSeverity.Success;
            AgentSkillFeedback.Message = resources.GetString("PowerScripts_AgentInstructionsCopied");
        }
        catch (Exception exception)
        {
            Logger.LogError("Unable to copy the PowerScripts agent skill setup instructions.", exception);
            AgentSkillFeedback.Severity = InfoBarSeverity.Error;
            AgentSkillFeedback.Message = exception.Message;
        }

        AgentSkillFeedback.IsOpen = true;
    }
}
