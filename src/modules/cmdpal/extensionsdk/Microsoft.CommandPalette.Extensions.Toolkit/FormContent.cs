// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class FormContent : BaseObservable, IFormContent
{
    public virtual string DataJson { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual string StateJson { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual string TemplateJson { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual ICommandResult SubmitForm(string inputs, string data) => SubmitForm(inputs);

    public virtual ICommandResult SubmitForm(string inputs) => CommandResult.KeepOpen();
}
