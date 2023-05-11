using System;
using System.ComponentModel;

namespace WIC
{
    [Flags]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public enum STGC : int
    {
        STGC_DEFAULT = 0,
        STGC_OVERWRITE = 1,
        STGC_ONLYIFCURRENT = 2,
        STGC_DANGEROUSLYCOMMITMERELYTODISKCACHE = 4,
        STGC_CONSOLIDATE = 8,
    }
}
