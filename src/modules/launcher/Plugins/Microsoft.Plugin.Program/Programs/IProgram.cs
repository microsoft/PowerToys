using System.Collections.Generic;
using Wox.Plugin;

namespace Microsoft.Plugin.Program.Programs
{
    public interface IProgram
    {
        List<ContextMenuResult> ContextMenus(IPublicAPI api);
        Result Result(string query, IPublicAPI api);
        string UniqueIdentifier { get; set; }
        string Name { get; }
        string Location { get; }
    }
}
