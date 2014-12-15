using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Infrastructure.Storage
{
    public interface IStorage
    {
        void Load();
        void Save();
    }
}
