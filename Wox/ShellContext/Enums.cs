using System;

namespace Wox.ShellContext
{
    [Flags()]
    public enum SHCONTF
    {
        FOLDERS = 0x20,
        NONFOLDERS = 0x40,
        INCLUDEHIDDEN = 0x80,
        INIT_ON_FIRST_NEXT = 0x100,
        NETPRINTERSRCH = 0x200,
        SHAREABLE = 0x400,
        STORAGE = 0x800
    }

    [Flags()]
    public enum SFGAO
    {
        CANCOPY = 0x1,
        CANMOVE = 0x2,
        CANLINK = 0x4,
        STORAGE = 0x8,
        CANRENAME = 0x10,
        CANDELETE = 0x20,
        HASPROPSHEET = 0x40,
        DROPTARGET = 0x100,
        CAPABILITYMASK = 0x177,
        ENCRYPTED = 0x2000,
        ISSLOW = 0x4000,
        GHOSTED = 0x8000,
        LINK = 0x10000,
        SHARE = 0x20000,
        READONLY = 0x40000,
        HIDDEN = 0x80000,
        DISPLAYATTRMASK = 0xFC000,
        FILESYSANCESTOR = 0x10000000,
        FOLDER = 0x20000000,
        FILESYSTEM = 0x40000000,
        HASSUBFOLDER = unchecked((int)0x80000000),
        CONTENTSMASK = unchecked((int)0x80000000),
        VALIDATE = 0x1000000,
        REMOVABLE = 0x2000000,
        COMPRESSED = 0x4000000,
        BROWSABLE = 0x8000000,
        NONENUMERATED = 0x100000,
        NEWCONTENT = 0x200000,
        CANMONIKER = 0x400000,
        HASSTORAGE = 0x400000,
        STREAM = 0x400000,
        STORAGEANCESTOR = 0x800000,
        STORAGECAPMASK = 0x70C50008
    }

    [Flags()]
    public enum SHGNO
    {
        NORMAL = 0x0,
        INFOLDER = 0x1,
        FOREDITING = 0x1000,
        FORADDRESSBAR = 0x4000,
        FORPARSING = 0x8000,
    }

    [Flags()]
    public enum CSIDL
    {
        ADMINTOOLS = 0x30,
        ALTSTARTUP = 0x1d,
        APPDATA = 0x1a,
        BITBUCKET = 10,
        CDBURN_AREA = 0x3b,
        COMMON_ADMINTOOLS = 0x2f,
        COMMON_ALTSTARTUP = 30,
        COMMON_APPDATA = 0x23,
        COMMON_DESKTOPDIRECTORY = 0x19,
        COMMON_DOCUMENTS = 0x2e,
        COMMON_FAVORITES = 0x1f,
        COMMON_MUSIC = 0x35,
        COMMON_PICTURES = 0x36,
        COMMON_PROGRAMS = 0x17,
        COMMON_STARTMENU = 0x16,
        COMMON_STARTUP = 0x18,
        COMMON_TEMPLATES = 0x2d,
        COMMON_VIDEO = 0x37,
        CONTROLS = 3,
        COOKIES = 0x21,
        DESKTOP = 0,
        DESKTOPDIRECTORY = 0x10,
        DRIVES = 0x11,
        FAVORITES = 6,
        FLAG_CREATE = 0x8000,
        FONTS = 20,
        HISTORY = 0x22,
        INTERNET = 1,
        INTERNET_CACHE = 0x20,
        LOCAL_APPDATA = 0x1c,
        MYDOCUMENTS = 12,
        MYMUSIC = 13,
        MYPICTURES = 0x27,
        MYVIDEO = 14,
        NETHOOD = 0x13,
        NETWORK = 0x12,
        PERSONAL = 5,
        PRINTERS = 4,
        PRINTHOOD = 0x1b,
        PROFILE = 40,
        PROFILES = 0x3e,
        PROGRAM_FILES = 0x26,
        PROGRAM_FILES_COMMON = 0x2b,
        PROGRAMS = 2,
        RECENT = 8,
        SENDTO = 9,
        STARTMENU = 11,
        STARTUP = 7,
        SYSTEM = 0x25,
        TEMPLATES = 0x15,
        WINDOWS = 0x24
    }

    [Flags()]
    public enum SHGFI : uint
    {
        ADDOVERLAYS = 0x20,
        ATTR_SPECIFIED = 0x20000,
        ATTRIBUTES = 0x800,
        DISPLAYNAME = 0x200,
        EXETYPE = 0x2000,
        ICON = 0x100,
        ICONLOCATION = 0x1000,
        LARGEICON = 0,
        LINKOVERLAY = 0x8000,
        OPENICON = 2,
        OVERLAYINDEX = 0x40,
        PIDL = 8,
        SELECTED = 0x10000,
        SHELLICONSIZE = 4,
        SMALLICON = 1,
        SYSICONINDEX = 0x4000,
        TYPENAME = 0x400,
        USEFILEATTRIBUTES = 0x10
    }

    [Flags]
    public enum FILE_ATTRIBUTE
    {
        READONLY = 0x00000001,
        HIDDEN = 0x00000002,
        SYSTEM = 0x00000004,
        DIRECTORY = 0x00000010,
        ARCHIVE = 0x00000020,
        DEVICE = 0x00000040,
        NORMAL = 0x00000080,
        TEMPORARY = 0x00000100,
        SPARSE_FILE = 0x00000200,
        REPARSE_POINT = 0x00000400,
        COMPRESSED = 0x00000800,
        OFFLINE = 0x00001000,
        NOT_CONTENT_INDEXED = 0x00002000,
        ENCRYPTED = 0x00004000
    }

    public enum GetCommandStringInformations
    {
        VERB = 0x00000004,
        HELPTEXT = 0x00000005,
        VALIDATE = 0x00000006,
    }

    [Flags]
    public enum CMF : uint
    {
        NORMAL = 0x00000000,
        DEFAULTONLY = 0x00000001,
        VERBSONLY = 0x00000002,
        EXPLORE = 0x00000004,
        NOVERBS = 0x00000008,
        CANRENAME = 0x00000010,
        NODEFAULT = 0x00000020,
        INCLUDESTATIC = 0x00000040,
        EXTENDEDVERBS = 0x00000100,
        RESERVED = 0xffff0000
    }

    [Flags]
    public enum TPM : uint
    {
        LEFTBUTTON = 0x0000,
        RIGHTBUTTON = 0x0002,
        LEFTALIGN = 0x0000,
        CENTERALIGN = 0x0004,
        RIGHTALIGN = 0x0008,
        TOPALIGN = 0x0000,
        VCENTERALIGN = 0x0010,
        BOTTOMALIGN = 0x0020,
        HORIZONTAL = 0x0000,
        VERTICAL = 0x0040,
        NONOTIFY = 0x0080,
        RETURNCMD = 0x0100,
        RECURSE = 0x0001,
        HORPOSANIMATION = 0x0400,
        HORNEGANIMATION = 0x0800,
        VERPOSANIMATION = 0x1000,
        VERNEGANIMATION = 0x2000,
        NOANIMATION = 0x4000,
        LAYOUTRTL = 0x8000
    }

}
