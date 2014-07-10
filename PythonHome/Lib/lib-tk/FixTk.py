import sys, os

# Delay import _tkinter until we have set TCL_LIBRARY,
# so that Tcl_FindExecutable has a chance to locate its
# encoding directory.

# Unfortunately, we cannot know the TCL_LIBRARY directory
# if we don't know the tcl version, which we cannot find out
# without import Tcl. Fortunately, Tcl will itself look in
# <TCL_LIBRARY>\..\tcl<TCL_VERSION>, so anything close to
# the real Tcl library will do.

# Expand symbolic links on Vista
try:
    import ctypes
    ctypes.windll.kernel32.GetFinalPathNameByHandleW
except (ImportError, AttributeError):
    def convert_path(s):
        return s
else:
    def convert_path(s):
        assert isinstance(s, str)   # sys.prefix contains only bytes
        udir = s.decode("mbcs")
        hdir = ctypes.windll.kernel32.\
            CreateFileW(udir, 0x80, # FILE_READ_ATTRIBUTES
                        1,          # FILE_SHARE_READ
                        None, 3,    # OPEN_EXISTING
                        0x02000000, # FILE_FLAG_BACKUP_SEMANTICS
                        None)
        if hdir == -1:
            # Cannot open directory, give up
            return s
        buf = ctypes.create_unicode_buffer(u"", 32768)
        res = ctypes.windll.kernel32.\
            GetFinalPathNameByHandleW(hdir, buf, len(buf),
                                      0) # VOLUME_NAME_DOS
        ctypes.windll.kernel32.CloseHandle(hdir)
        if res == 0:
            # Conversion failed (e.g. network location)
            return s
        s = buf[:res].encode("mbcs")
        # Ignore leading \\?\
        if s.startswith("\\\\?\\"):
            s = s[4:]
        if s.startswith("UNC"):
            s = "\\" + s[3:]
        return s

prefix = os.path.join(sys.prefix,"tcl")
if not os.path.exists(prefix):
    # devdir/../tcltk/lib
    prefix = os.path.join(sys.prefix, os.path.pardir, "tcltk", "lib")
    prefix = os.path.abspath(prefix)
# if this does not exist, no further search is needed
if os.path.exists(prefix):
    prefix = convert_path(prefix)
    if "TCL_LIBRARY" not in os.environ:
        for name in os.listdir(prefix):
            if name.startswith("tcl"):
                tcldir = os.path.join(prefix,name)
                if os.path.isdir(tcldir):
                    os.environ["TCL_LIBRARY"] = tcldir
    # Compute TK_LIBRARY, knowing that it has the same version
    # as Tcl
    import _tkinter
    ver = str(_tkinter.TCL_VERSION)
    if "TK_LIBRARY" not in os.environ:
        v = os.path.join(prefix, 'tk'+ver)
        if os.path.exists(os.path.join(v, "tclIndex")):
            os.environ['TK_LIBRARY'] = v
    # We don't know the Tix version, so we must search the entire
    # directory
    if "TIX_LIBRARY" not in os.environ:
        for name in os.listdir(prefix):
            if name.startswith("tix"):
                tixdir = os.path.join(prefix,name)
                if os.path.isdir(tixdir):
                    os.environ["TIX_LIBRARY"] = tixdir
