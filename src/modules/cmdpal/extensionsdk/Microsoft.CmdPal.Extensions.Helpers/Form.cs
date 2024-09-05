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
