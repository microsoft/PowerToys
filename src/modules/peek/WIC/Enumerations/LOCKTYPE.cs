using System;
using System.ComponentModel;

namespace WIC
{
    [Flags]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public enum LOCKTYPE : int
    {
        LOCK_WRITE = 1,
        LOCK_EXCLUSIVE = 2,
        LOCK_ONLYONCE = 4,
    }
}
