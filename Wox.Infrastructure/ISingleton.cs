using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Infrastructure
{
    interface ISingleton<T>
    {
        T Instance { get; }
    }
}
