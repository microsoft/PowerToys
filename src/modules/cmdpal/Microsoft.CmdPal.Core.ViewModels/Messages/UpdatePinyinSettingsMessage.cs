// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.ViewModels.Messages;

/// <summary>
/// Used to update the pinyin input settings in context menu and other components
/// </summary>
public record UpdatePinyinSettingsMessage(bool IsPinyinInput)
{
}
