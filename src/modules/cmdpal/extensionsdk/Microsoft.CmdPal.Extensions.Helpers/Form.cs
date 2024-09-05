// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public class Form: IForm
{
    public string Data { get; set; }
    public string State { get; set; }
    public string Template { get; set; }

    public virtual string DataJson() => Data;
    public virtual string StateJson() => State;
    public virtual string TemplateJson() => Template;
    public virtual ICommandResult SubmitForm(string payload) => throw new NotImplementedException();
}
