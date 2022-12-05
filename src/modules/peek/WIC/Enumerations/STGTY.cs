using System.ComponentModel;

namespace WIC
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public enum STGTY : int
    {
        STGTY_STORAGE   = 1,
        STGTY_STREAM    = 2,
        STGTY_LOCKBYTES = 3,
        STGTY_PROPERTY  = 4,
     }
}
