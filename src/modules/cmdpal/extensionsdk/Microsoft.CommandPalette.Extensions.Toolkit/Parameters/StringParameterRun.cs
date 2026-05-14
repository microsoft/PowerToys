// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class StringParameterRun : ParameterValueRun, IStringParameterRun
{
    private string _text = string.Empty;

    public virtual string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value))
            {
                OnPropertyChanged(nameof(NeedsValue));
            }
        }
    }

    public override bool NeedsValue => string.IsNullOrEmpty(Text);

    public StringParameterRun()
    {
    }

    public StringParameterRun(string placeholderText)
    {
        PlaceholderText = placeholderText;
    }

    public override void ClearValue()
    {
        Text = string.Empty;
    }

    public override object? Value
    {
        get => Text;
        set
        {
            if (value is string s)
            {
                Text = s;
            }
            else
            {
                var message = $"{nameof(StringParameterRun)}.{nameof(Value)} expected a string but received '{value?.GetType().FullName ?? "null"}'.";
                ExtensionHost.LogMessage(new LogMessage(message) { State = MessageState.Error });
                throw new ArgumentException(message, nameof(value));
            }
        }
    }
}
