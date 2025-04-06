// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class FormContent : BaseObservable, IFormContent
{
    public virtual string DataJson
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(DataJson));
        }
    }

= string.Empty;

    public virtual string StateJson
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(StateJson));
        }
    }

= string.Empty;

    public virtual string TemplateJson
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(TemplateJson));
        }
    }

= string.Empty;

    public virtual ICommandResult SubmitForm(string inputs, string data) => SubmitForm(inputs);

    public virtual ICommandResult SubmitForm(string inputs) => CommandResult.KeepOpen();
}
