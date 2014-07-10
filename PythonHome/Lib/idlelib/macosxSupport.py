"""
A number of functions that enhance IDLE on Mac OSX.
"""
import sys
import Tkinter
from os import path


import warnings

def runningAsOSXApp():
    warnings.warn("runningAsOSXApp() is deprecated, use isAquaTk()",
                        DeprecationWarning, stacklevel=2)
    return isAquaTk()

def isCarbonAquaTk(root):
    warnings.warn("isCarbonAquaTk(root) is deprecated, use isCarbonTk()",
                        DeprecationWarning, stacklevel=2)
    return isCarbonTk()

_tk_type = None

def _initializeTkVariantTests(root):
    """
    Initializes OS X Tk variant values for
    isAquaTk(), isCarbonTk(), isCocoaTk(), and isXQuartz().
    """
    global _tk_type
    if sys.platform == 'darwin':
        ws = root.tk.call('tk', 'windowingsystem')
        if 'x11' in ws:
            _tk_type = "xquartz"
        elif 'aqua' not in ws:
            _tk_type = "other"
        elif 'AppKit' in root.tk.call('winfo', 'server', '.'):
            _tk_type = "cocoa"
        else:
            _tk_type = "carbon"
    else:
        _tk_type = "other"

def isAquaTk():
    """
    Returns True if IDLE is using a native OS X Tk (Cocoa or Carbon).
    """
    assert _tk_type is not None
    return _tk_type == "cocoa" or _tk_type == "carbon"

def isCarbonTk():
    """
    Returns True if IDLE is using a Carbon Aqua Tk (instead of the
    newer Cocoa Aqua Tk).
    """
    assert _tk_type is not None
    return _tk_type == "carbon"

def isCocoaTk():
    """
    Returns True if IDLE is using a Cocoa Aqua Tk.
    """
    assert _tk_type is not None
    return _tk_type == "cocoa"

def isXQuartz():
    """
    Returns True if IDLE is using an OS X X11 Tk.
    """
    assert _tk_type is not None
    return _tk_type == "xquartz"

def tkVersionWarning(root):
    """
    Returns a string warning message if the Tk version in use appears to
    be one known to cause problems with IDLE.
    1. Apple Cocoa-based Tk 8.5.7 shipped with Mac OS X 10.6 is unusable.
    2. Apple Cocoa-based Tk 8.5.9 in OS X 10.7 and 10.8 is better but
        can still crash unexpectedly.
    """

    if isCocoaTk():
        patchlevel = root.tk.call('info', 'patchlevel')
        if patchlevel not in ('8.5.7', '8.5.9'):
            return False
        return (r"WARNING: The version of Tcl/Tk ({0}) in use may"
                r" be unstable.\n"
                r"Visit http://www.python.org/download/mac/tcltk/"
                r" for current information.".format(patchlevel))
    else:
        return False

def addOpenEventSupport(root, flist):
    """
    This ensures that the application will respond to open AppleEvents, which
    makes is feasible to use IDLE as the default application for python files.
    """
    def doOpenFile(*args):
        for fn in args:
            flist.open(fn)

    # The command below is a hook in aquatk that is called whenever the app
    # receives a file open event. The callback can have multiple arguments,
    # one for every file that should be opened.
    root.createcommand("::tk::mac::OpenDocument", doOpenFile)

def hideTkConsole(root):
    try:
        root.tk.call('console', 'hide')
    except Tkinter.TclError:
        # Some versions of the Tk framework don't have a console object
        pass

def overrideRootMenu(root, flist):
    """
    Replace the Tk root menu by something that is more appropriate for
    IDLE with an Aqua Tk.
    """
    # The menu that is attached to the Tk root (".") is also used by AquaTk for
    # all windows that don't specify a menu of their own. The default menubar
    # contains a number of menus, none of which are appropriate for IDLE. The
    # Most annoying of those is an 'About Tck/Tk...' menu in the application
    # menu.
    #
    # This function replaces the default menubar by a mostly empty one, it
    # should only contain the correct application menu and the window menu.
    #
    # Due to a (mis-)feature of TkAqua the user will also see an empty Help
    # menu.
    from Tkinter import Menu, Text, Text
    from idlelib.EditorWindow import prepstr, get_accelerator
    from idlelib import Bindings
    from idlelib import WindowList
    from idlelib.MultiCall import MultiCallCreator

    closeItem = Bindings.menudefs[0][1][-2]

    # Remove the last 3 items of the file menu: a separator, close window and
    # quit. Close window will be reinserted just above the save item, where
    # it should be according to the HIG. Quit is in the application menu.
    del Bindings.menudefs[0][1][-3:]
    Bindings.menudefs[0][1].insert(6, closeItem)

    # Remove the 'About' entry from the help menu, it is in the application
    # menu
    del Bindings.menudefs[-1][1][0:2]

    # Remove the 'Configure' entry from the options menu, it is in the
    # application menu as 'Preferences'
    del Bindings.menudefs[-2][1][0:2]

    menubar = Menu(root)
    root.configure(menu=menubar)
    menudict = {}

    menudict['windows'] = menu = Menu(menubar, name='windows')
    menubar.add_cascade(label='Window', menu=menu, underline=0)

    def postwindowsmenu(menu=menu):
        end = menu.index('end')
        if end is None:
            end = -1

        if end > 0:
            menu.delete(0, end)
        WindowList.add_windows_to_menu(menu)
    WindowList.register_callback(postwindowsmenu)

    def about_dialog(event=None):
        from idlelib import aboutDialog
        aboutDialog.AboutDialog(root, 'About IDLE')

    def config_dialog(event=None):
        from idlelib import configDialog
        root.instance_dict = flist.inversedict
        configDialog.ConfigDialog(root, 'Settings')

    def help_dialog(event=None):
        from idlelib import textView
        fn = path.join(path.abspath(path.dirname(__file__)), 'help.txt')
        textView.view_file(root, 'Help', fn)

    root.bind('<<about-idle>>', about_dialog)
    root.bind('<<open-config-dialog>>', config_dialog)
    root.createcommand('::tk::mac::ShowPreferences', config_dialog)
    if flist:
        root.bind('<<close-all-windows>>', flist.close_all_callback)

        # The binding above doesn't reliably work on all versions of Tk
        # on MacOSX. Adding command definition below does seem to do the
        # right thing for now.
        root.createcommand('exit', flist.close_all_callback)

    if isCarbonTk():
        # for Carbon AquaTk, replace the default Tk apple menu
        menudict['application'] = menu = Menu(menubar, name='apple')
        menubar.add_cascade(label='IDLE', menu=menu)
        Bindings.menudefs.insert(0,
            ('application', [
                ('About IDLE', '<<about-idle>>'),
                    None,
                ]))
        tkversion = root.tk.eval('info patchlevel')
        if tuple(map(int, tkversion.split('.'))) < (8, 4, 14):
            # for earlier AquaTk versions, supply a Preferences menu item
            Bindings.menudefs[0][1].append(
                    ('_Preferences....', '<<open-config-dialog>>'),
                )
    if isCocoaTk():
        # replace default About dialog with About IDLE one
        root.createcommand('tkAboutDialog', about_dialog)
        # replace default "Help" item in Help menu
        root.createcommand('::tk::mac::ShowHelp', help_dialog)
        # remove redundant "IDLE Help" from menu
        del Bindings.menudefs[-1][1][0]

def setupApp(root, flist):
    """
    Perform initial OS X customizations if needed.
    Called from PyShell.main() after initial calls to Tk()

    There are currently three major versions of Tk in use on OS X:
        1. Aqua Cocoa Tk (native default since OS X 10.6)
        2. Aqua Carbon Tk (original native, 32-bit only, deprecated)
        3. X11 (supported by some third-party distributors, deprecated)
    There are various differences among the three that affect IDLE
    behavior, primarily with menus, mouse key events, and accelerators.
    Some one-time customizations are performed here.
    Others are dynamically tested throughout idlelib by calls to the
    isAquaTk(), isCarbonTk(), isCocoaTk(), isXQuartz() functions which
    are initialized here as well.
    """
    _initializeTkVariantTests(root)
    if isAquaTk():
        hideTkConsole(root)
        overrideRootMenu(root, flist)
        addOpenEventSupport(root, flist)
