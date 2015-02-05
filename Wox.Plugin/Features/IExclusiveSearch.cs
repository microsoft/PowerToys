using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin.Features
{
    public interface IExclusiveSearch
    {
        bool IsExclusiveSearch(Query query);
    }
}
