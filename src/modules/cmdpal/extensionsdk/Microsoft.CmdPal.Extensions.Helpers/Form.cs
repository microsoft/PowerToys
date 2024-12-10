// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public abstract class Form : IForm
{
    public virtual string Data { get; set; } = string.Empty;

    public virtual string State { get; set; } = string.Empty;

    public virtual string Template { get; set; } = string.Empty;

    public virtual string DataJson() => Data;

    public virtual string StateJson() => State;

    public virtual string TemplateJson() => Template;

    public abstract ICommandResult SubmitForm(string payload);
}
