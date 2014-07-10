#
# Instant Python
# $Id: tkFileDialog.py 36560 2004-07-18 06:16:08Z tim_one $
#
# tk common file dialogues
#
# this module provides interfaces to the native file dialogues
# available in Tk 4.2 and newer, and the directory dialogue available
# in Tk 8.3 and newer.
#
# written by Fredrik Lundh, May 1997.
#

#
# options (all have default values):
#
# - defaultextension: added to filename if not explicitly given
#
# - filetypes: sequence of (label, pattern) tuples.  the same pattern
#   may occur with several patterns.  use "*" as pattern to indicate
#   all files.
#
# - initialdir: initial directory.  preserved by dialog instance.
#
# - initialfile: initial file (ignored by the open dialog).  preserved
#   by dialog instance.
#
# - parent: which window to place the dialog on top of
#
# - title: dialog title
#
# - multiple: if true user may select more than one file
#
# options for the directory chooser:
#
# - initialdir, parent, title: see above
#
# - mustexist: if true, user must pick an existing directory
#
#


from tkCommonDialog import Dialog

class _Dialog(Dialog):

    def _fixoptions(self):
        try:
            # make sure "filetypes" is a tuple
            self.options["filetypes"] = tuple(self.options["filetypes"])
        except KeyError:
            pass

    def _fixresult(self, widget, result):
        if result:
            # keep directory and filename until next time
            import os
            # convert Tcl path objects to strings
            try:
                result = result.string
            except AttributeError:
                # it already is a string
                pass
            path, file = os.path.split(result)
            self.options["initialdir"] = path
            self.options["initialfile"] = file
        self.filename = result # compatibility
        return result


#
# file dialogs

class Open(_Dialog):
    "Ask for a filename to open"

    command = "tk_getOpenFile"

    def _fixresult(self, widget, result):
        if isinstance(result, tuple):
            # multiple results:
            result = tuple([getattr(r, "string", r) for r in result])
            if result:
                import os
                path, file = os.path.split(result[0])
                self.options["initialdir"] = path
                # don't set initialfile or filename, as we have multiple of these
            return result
        if not widget.tk.wantobjects() and "multiple" in self.options:
            # Need to split result explicitly
            return self._fixresult(widget, widget.tk.splitlist(result))
        return _Dialog._fixresult(self, widget, result)

class SaveAs(_Dialog):
    "Ask for a filename to save as"

    command = "tk_getSaveFile"


# the directory dialog has its own _fix routines.
class Directory(Dialog):
    "Ask for a directory"

    command = "tk_chooseDirectory"

    def _fixresult(self, widget, result):
        if result:
            # convert Tcl path objects to strings
            try:
                result = result.string
            except AttributeError:
                # it already is a string
                pass
            # keep directory until next time
            self.options["initialdir"] = result
        self.directory = result # compatibility
        return result

#
# convenience stuff

def askopenfilename(**options):
    "Ask for a filename to open"

    return Open(**options).show()

def asksaveasfilename(**options):
    "Ask for a filename to save as"

    return SaveAs(**options).show()

def askopenfilenames(**options):
    """Ask for multiple filenames to open

    Returns a list of filenames or empty list if
    cancel button selected
    """
    options["multiple"]=1
    return Open(**options).show()

# FIXME: are the following  perhaps a bit too convenient?

def askopenfile(mode = "r", **options):
    "Ask for a filename to open, and returned the opened file"

    filename = Open(**options).show()
    if filename:
        return open(filename, mode)
    return None

def askopenfiles(mode = "r", **options):
    """Ask for multiple filenames and return the open file
    objects

    returns a list of open file objects or an empty list if
    cancel selected
    """

    files = askopenfilenames(**options)
    if files:
        ofiles=[]
        for filename in files:
            ofiles.append(open(filename, mode))
        files=ofiles
    return files


def asksaveasfile(mode = "w", **options):
    "Ask for a filename to save as, and returned the opened file"

    filename = SaveAs(**options).show()
    if filename:
        return open(filename, mode)
    return None

def askdirectory (**options):
    "Ask for a directory, and return the file name"
    return Directory(**options).show()

# --------------------------------------------------------------------
# test stuff

if __name__ == "__main__":
    # Since the file name may contain non-ASCII characters, we need
    # to find an encoding that likely supports the file name, and
    # displays correctly on the terminal.

    # Start off with UTF-8
    enc = "utf-8"
    import sys

    # See whether CODESET is defined
    try:
        import locale
        locale.setlocale(locale.LC_ALL,'')
        enc = locale.nl_langinfo(locale.CODESET)
    except (ImportError, AttributeError):
        pass

    # dialog for openening files

    openfilename=askopenfilename(filetypes=[("all files", "*")])
    try:
        fp=open(openfilename,"r")
        fp.close()
    except:
        print "Could not open File: "
        print sys.exc_info()[1]

    print "open", openfilename.encode(enc)

    # dialog for saving files

    saveasfilename=asksaveasfilename()
    print "saveas", saveasfilename.encode(enc)
