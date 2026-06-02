// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace KeyboardManagerEditorUI.ViewModels
{
    public sealed class TemplateChoiceViewModel
    {
        public TemplateChoiceViewModel(string value, string displayText)
        {
            Value = value;
            DisplayText = displayText;
        }

        public string Value { get; }

        public string DisplayText { get; }
    }
}
