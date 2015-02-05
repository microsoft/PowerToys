using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin.Features
{
    public interface IExclusiveQuery
    {
        bool IsExclusiveQuery(Query query);
    }
}
