// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library;

public sealed class AdvancedPasteAdditionalActions
{
    private AdvancedPasteAdditionalAction _imageToText = new();
    private AdvancedPastePasteAsFileAction _pasteAsFile = new();
    private AdvancedPasteTranscodeAction _transcode = new();

    public static class PropertyNames
    {
        public const string ImageToText = "image-to-text";
        public const string PasteAsFile = "paste-as-file";
        public const string Transcode = "transcode";
    }

    [JsonPropertyName(PropertyNames.ImageToText)]
    public AdvancedPasteAdditionalAction ImageToText
    {
        get => _imageToText;
        init => _imageToText = value ?? new();
    }

    [JsonPropertyName(PropertyNames.PasteAsFile)]
    public AdvancedPastePasteAsFileAction PasteAsFile
    {
        get => _pasteAsFile;
        init => _pasteAsFile = value ?? new();
    }

    [JsonPropertyName(PropertyNames.Transcode)]
    public AdvancedPasteTranscodeAction Transcode
    {
        get => _transcode;
        init => _transcode = value ?? new();
    }

    public IEnumerable<IAdvancedPasteAction> GetAllActions()
    {
        return GetAllActionsRecursive([ImageToText, PasteAsFile, Transcode]);
    }

    /// <summary>
    /// Changed to depth-first traversal to ensure ordered output
    /// </summary>
    /// <param name="actions">The collection of actions to traverse</param>
    /// <returns>All actions returned in depth-first order</returns>
    private static IEnumerable<IAdvancedPasteAction> GetAllActionsRecursive(IEnumerable<IAdvancedPasteAction> actions)
    {
        foreach (var action in actions)
        {
            yield return action;

            foreach (var subAction in GetAllActionsRecursive(action.SubActions))
            {
                yield return subAction;
            }
        }
    }
}
