using System.Collections.Generic;

namespace Wox.Plugin.Features
{
    public interface IContextMenu
    {
        List<Result> LoadContextMenus(Result selectedResult);
    }
}