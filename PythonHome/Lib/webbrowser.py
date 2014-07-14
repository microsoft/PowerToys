#! /usr/bin/env python
"""Interfaces for launching and remotely controlling Web browsers."""
# Maintained by Georg Brandl.

import os
import shlex
import sys
import stat
import subprocess
import time

__all__ = ["Error", "open", "open_new", "open_new_tab", "get", "register"]

class Error(Exception):
    pass

_browsers = {}          # Dictionary of available browser controllers
_tryorder = []          # Preference order of available browsers

def register(name, klass, instance=None, update_tryorder=1):
    """Register a browser connector and, optionally, connection."""
    _browsers[name.lower()] = [klass, instance]
    if update_tryorder > 0:
        _tryorder.append(name)
    elif update_tryorder < 0:
        _tryorder.insert(0, name)

def get(using=None):
    """Return a browser launcher instance appropriate for the environment."""
    if using is not None:
        alternatives = [using]
    else:
        alternatives = _tryorder
    for browser in alternatives:
        if '%s' in browser:
            # User gave us a command line, split it into name and args
            browser = shlex.split(browser)
            if browser[-1] == '&':
                return BackgroundBrowser(browser[:-1])
            else:
                return GenericBrowser(browser)
        else:
            # User gave us a browser name or path.
            try:
                command = _browsers[browser.lower()]
            except KeyError:
                command = _synthesize(browser)
            if command[1] is not None:
                return command[1]
            elif command[0] is not None:
                return command[0]()
    raise Error("could not locate runnable browser")

# Please note: the following definition hides a builtin function.
# It is recommended one does "import webbrowser" and uses webbrowser.open(url)
# instead of "from webbrowser import *".

def open(url, new=0, autoraise=True):
    for name in _tryorder:
        browser = get(name)
        if browser.open(url, new, autoraise):
            return True
    return False

def open_new(url):
    return open(url, 1)

def open_new_tab(url):
    return open(url, 2)


def _synthesize(browser, update_tryorder=1):
    """Attempt to synthesize a controller base on existing controllers.

    This is useful to create a controller when a user specifies a path to
    an entry in the BROWSER environment variable -- we can copy a general
    controller to operate using a specific installation of the desired
    browser in this way.

    If we can't create a controller in this way, or if there is no
    executable for the requested browser, return [None, None].

    """
    cmd = browser.split()[0]
    if not _iscommand(cmd):
        return [None, None]
    name = os.path.basename(cmd)
    try:
        command = _browsers[name.lower()]
    except KeyError:
        return [None, None]
    # now attempt to clone to fit the new name:
    controller = command[1]
    if controller and name.lower() == controller.basename:
        import copy
        controller = copy.copy(controller)
        controller.name = browser
        controller.basename = os.path.basename(browser)
        register(browser, None, controller, update_tryorder)
        return [None, controller]
    return [None, None]


if sys.platform[:3] == "win":
    def _isexecutable(cmd):
        cmd = cmd.lower()
        if os.path.isfile(cmd) and cmd.endswith((".exe", ".bat")):
            return True
        for ext in ".exe", ".bat":
            if os.path.isfile(cmd + ext):
                return True
        return False
else:
    def _isexecutable(cmd):
        if os.path.isfile(cmd):
            mode = os.stat(cmd)[stat.ST_MODE]
            if mode & stat.S_IXUSR or mode & stat.S_IXGRP or mode & stat.S_IXOTH:
                return True
        return False

def _iscommand(cmd):
    """Return True if cmd is executable or can be found on the executable
    search path."""
    if _isexecutable(cmd):
        return True
    path = os.environ.get("PATH")
    if not path:
        return False
    for d in path.split(os.pathsep):
        exe = os.path.join(d, cmd)
        if _isexecutable(exe):
            return True
    return False


# General parent classes

class BaseBrowser(object):
    """Parent class for all browsers. Do not use directly."""

    args = ['%s']

    def __init__(self, name=""):
        self.name = name
        self.basename = name

    def open(self, url, new=0, autoraise=True):
        raise NotImplementedError

    def open_new(self, url):
        return self.open(url, 1)

    def open_new_tab(self, url):
        return self.open(url, 2)


class GenericBrowser(BaseBrowser):
    """Class for all browsers started with a command
       and without remote functionality."""

    def __init__(self, name):
        if isinstance(name, basestring):
            self.name = name
            self.args = ["%s"]
        else:
            # name should be a list with arguments
            self.name = name[0]
            self.args = name[1:]
        self.basename = os.path.basename(self.name)

    def open(self, url, new=0, autoraise=True):
        cmdline = [self.name] + [arg.replace("%s", url)
                                 for arg in self.args]
        try:
            if sys.platform[:3] == 'win':
                p = subprocess.Popen(cmdline)
            else:
                p = subprocess.Popen(cmdline, close_fds=True)
            return not p.wait()
        except OSError:
            return False


class BackgroundBrowser(GenericBrowser):
    """Class for all browsers which are to be started in the
       background."""

    def open(self, url, new=0, autoraise=True):
        cmdline = [self.name] + [arg.replace("%s", url)
                                 for arg in self.args]
        try:
            if sys.platform[:3] == 'win':
                p = subprocess.Popen(cmdline)
            else:
                setsid = getattr(os, 'setsid', None)
                if not setsid:
                    setsid = getattr(os, 'setpgrp', None)
                p = subprocess.Popen(cmdline, close_fds=True, preexec_fn=setsid)
            return (p.poll() is None)
        except OSError:
            return False


class UnixBrowser(BaseBrowser):
    """Parent class for all Unix browsers with remote functionality."""

    raise_opts = None
    remote_args = ['%action', '%s']
    remote_action = None
    remote_action_newwin = None
    remote_action_newtab = None
    background = False
    redirect_stdout = True

    def _invoke(self, args, remote, autoraise):
        raise_opt = []
        if remote and self.raise_opts:
            # use autoraise argument only for remote invocation
            autoraise = int(autoraise)
            opt = self.raise_opts[autoraise]
            if opt: raise_opt = [opt]

        cmdline = [self.name] + raise_opt + args

        if remote or self.background:
            inout = file(os.devnull, "r+")
        else:
            # for TTY browsers, we need stdin/out
            inout = None
        # if possible, put browser in separate process group, so
        # keyboard interrupts don't affect browser as well as Python
        setsid = getattr(os, 'setsid', None)
        if not setsid:
            setsid = getattr(os, 'setpgrp', None)

        p = subprocess.Popen(cmdline, close_fds=True, stdin=inout,
                             stdout=(self.redirect_stdout and inout or None),
                             stderr=inout, preexec_fn=setsid)
        if remote:
            # wait five seconds. If the subprocess is not finished, the
            # remote invocation has (hopefully) started a new instance.
            time.sleep(1)
            rc = p.poll()
            if rc is None:
                time.sleep(4)
                rc = p.poll()
                if rc is None:
                    return True
            # if remote call failed, open() will try direct invocation
            return not rc
        elif self.background:
            if p.poll() is None:
                return True
            else:
                return False
        else:
            return not p.wait()

    def open(self, url, new=0, autoraise=True):
        if new == 0:
            action = self.remote_action
        elif new == 1:
            action = self.remote_action_newwin
        elif new == 2:
            if self.remote_action_newtab is None:
                action = self.remote_action_newwin
            else:
                action = self.remote_action_newtab
        else:
            raise Error("Bad 'new' parameter to open(); " +
                        "expected 0, 1, or 2, got %s" % new)

        args = [arg.replace("%s", url).replace("%action", action)
                for arg in self.remote_args]
        success = self._invoke(args, True, autoraise)
        if not success:
            # remote invocation failed, try straight way
            args = [arg.replace("%s", url) for arg in self.args]
            return self._invoke(args, False, False)
        else:
            return True


class Mozilla(UnixBrowser):
    """Launcher class for Mozilla/Netscape browsers."""

    raise_opts = ["-noraise", "-raise"]
    remote_args = ['-remote', 'openURL(%s%action)']
    remote_action = ""
    remote_action_newwin = ",new-window"
    remote_action_newtab = ",new-tab"
    background = True

Netscape = Mozilla


class Galeon(UnixBrowser):
    """Launcher class for Galeon/Epiphany browsers."""

    raise_opts = ["-noraise", ""]
    remote_args = ['%action', '%s']
    remote_action = "-n"
    remote_action_newwin = "-w"
    background = True


class Chrome(UnixBrowser):
    "Launcher class for Google Chrome browser."

    remote_args = ['%action', '%s']
    remote_action = ""
    remote_action_newwin = "--new-window"
    remote_action_newtab = ""
    background = True

Chromium = Chrome


class Opera(UnixBrowser):
    "Launcher class for Opera browser."

    raise_opts = ["-noraise", ""]
    remote_args = ['-remote', 'openURL(%s%action)']
    remote_action = ""
    remote_action_newwin = ",new-window"
    remote_action_newtab = ",new-page"
    background = True


class Elinks(UnixBrowser):
    "Launcher class for Elinks browsers."

    remote_args = ['-remote', 'openURL(%s%action)']
    remote_action = ""
    remote_action_newwin = ",new-window"
    remote_action_newtab = ",new-tab"
    background = False

    # elinks doesn't like its stdout to be redirected -
    # it uses redirected stdout as a signal to do -dump
    redirect_stdout = False


class Konqueror(BaseBrowser):
    """Controller for the KDE File Manager (kfm, or Konqueror).

    See the output of ``kfmclient --commands``
    for more information on the Konqueror remote-control interface.
    """

    def open(self, url, new=0, autoraise=True):
        # XXX Currently I know no way to prevent KFM from opening a new win.
        if new == 2:
            action = "newTab"
        else:
            action = "openURL"

        devnull = file(os.devnull, "r+")
        # if possible, put browser in separate process group, so
        # keyboard interrupts don't affect browser as well as Python
        setsid = getattr(os, 'setsid', None)
        if not setsid:
            setsid = getattr(os, 'setpgrp', None)

        try:
            p = subprocess.Popen(["kfmclient", action, url],
                                 close_fds=True, stdin=devnull,
                                 stdout=devnull, stderr=devnull)
        except OSError:
            # fall through to next variant
            pass
        else:
            p.wait()
            # kfmclient's return code unfortunately has no meaning as it seems
            return True

        try:
            p = subprocess.Popen(["konqueror", "--silent", url],
                                 close_fds=True, stdin=devnull,
                                 stdout=devnull, stderr=devnull,
                                 preexec_fn=setsid)
        except OSError:
            # fall through to next variant
            pass
        else:
            if p.poll() is None:
                # Should be running now.
                return True

        try:
            p = subprocess.Popen(["kfm", "-d", url],
                                 close_fds=True, stdin=devnull,
                                 stdout=devnull, stderr=devnull,
                                 preexec_fn=setsid)
        except OSError:
            return False
        else:
            return (p.poll() is None)


class Grail(BaseBrowser):
    # There should be a way to maintain a connection to Grail, but the
    # Grail remote control protocol doesn't really allow that at this
    # point.  It probably never will!
    def _find_grail_rc(self):
        import glob
        import pwd
        import socket
        import tempfile
        tempdir = os.path.join(tempfile.gettempdir(),
                               ".grail-unix")
        user = pwd.getpwuid(os.getuid())[0]
        filename = os.path.join(tempdir, user + "-*")
        maybes = glob.glob(filename)
        if not maybes:
            return None
        s = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
        for fn in maybes:
            # need to PING each one until we find one that's live
            try:
                s.connect(fn)
            except socket.error:
                # no good; attempt to clean it out, but don't fail:
                try:
                    os.unlink(fn)
                except IOError:
                    pass
            else:
                return s

    def _remote(self, action):
        s = self._find_grail_rc()
        if not s:
            return 0
        s.send(action)
        s.close()
        return 1

    def open(self, url, new=0, autoraise=True):
        if new:
            ok = self._remote("LOADNEW " + url)
        else:
            ok = self._remote("LOAD " + url)
        return ok


#
# Platform support for Unix
#

# These are the right tests because all these Unix browsers require either
# a console terminal or an X display to run.

def register_X_browsers():

    # use xdg-open if around
    if _iscommand("xdg-open"):
        register("xdg-open", None, BackgroundBrowser("xdg-open"))

    # The default GNOME3 browser
    if "GNOME_DESKTOP_SESSION_ID" in os.environ and _iscommand("gvfs-open"):
        register("gvfs-open", None, BackgroundBrowser("gvfs-open"))

    # The default GNOME browser
    if "GNOME_DESKTOP_SESSION_ID" in os.environ and _iscommand("gnome-open"):
        register("gnome-open", None, BackgroundBrowser("gnome-open"))

    # The default KDE browser
    if "KDE_FULL_SESSION" in os.environ and _iscommand("kfmclient"):
        register("kfmclient", Konqueror, Konqueror("kfmclient"))

    if _iscommand("x-www-browser"):
        register("x-www-browser", None, BackgroundBrowser("x-www-browser"))

    # The Mozilla/Netscape browsers
    for browser in ("mozilla-firefox", "firefox",
                    "mozilla-firebird", "firebird",
                    "iceweasel", "iceape",
                    "seamonkey", "mozilla", "netscape"):
        if _iscommand(browser):
            register(browser, None, Mozilla(browser))

    # Konqueror/kfm, the KDE browser.
    if _iscommand("kfm"):
        register("kfm", Konqueror, Konqueror("kfm"))
    elif _iscommand("konqueror"):
        register("konqueror", Konqueror, Konqueror("konqueror"))

    # Gnome's Galeon and Epiphany
    for browser in ("galeon", "epiphany"):
        if _iscommand(browser):
            register(browser, None, Galeon(browser))

    # Skipstone, another Gtk/Mozilla based browser
    if _iscommand("skipstone"):
        register("skipstone", None, BackgroundBrowser("skipstone"))

    # Google Chrome/Chromium browsers
    for browser in ("google-chrome", "chrome", "chromium", "chromium-browser"):
        if _iscommand(browser):
            register(browser, None, Chrome(browser))

    # Opera, quite popular
    if _iscommand("opera"):
        register("opera", None, Opera("opera"))

    # Next, Mosaic -- old but still in use.
    if _iscommand("mosaic"):
        register("mosaic", None, BackgroundBrowser("mosaic"))

    # Grail, the Python browser. Does anybody still use it?
    if _iscommand("grail"):
        register("grail", Grail, None)

# Prefer X browsers if present
if os.environ.get("DISPLAY"):
    register_X_browsers()

# Also try console browsers
if os.environ.get("TERM"):
    if _iscommand("www-browser"):
        register("www-browser", None, GenericBrowser("www-browser"))
    # The Links/elinks browsers <http://artax.karlin.mff.cuni.cz/~mikulas/links/>
    if _iscommand("links"):
        register("links", None, GenericBrowser("links"))
    if _iscommand("elinks"):
        register("elinks", None, Elinks("elinks"))
    # The Lynx browser <http://lynx.isc.org/>, <http://lynx.browser.org/>
    if _iscommand("lynx"):
        register("lynx", None, GenericBrowser("lynx"))
    # The w3m browser <http://w3m.sourceforge.net/>
    if _iscommand("w3m"):
        register("w3m", None, GenericBrowser("w3m"))

#
# Platform support for Windows
#

if sys.platform[:3] == "win":
    class WindowsDefault(BaseBrowser):
        def open(self, url, new=0, autoraise=True):
            try:
                os.startfile(url)
            except WindowsError:
                # [Error 22] No application is associated with the specified
                # file for this operation: '<URL>'
                return False
            else:
                return True

    _tryorder = []
    _browsers = {}

    # First try to use the default Windows browser
    register("windows-default", WindowsDefault)

    # Detect some common Windows browsers, fallback to IE
    iexplore = os.path.join(os.environ.get("PROGRAMFILES", "C:\\Program Files"),
                            "Internet Explorer\\IEXPLORE.EXE")
    for browser in ("firefox", "firebird", "seamonkey", "mozilla",
                    "netscape", "opera", iexplore):
        if _iscommand(browser):
            register(browser, None, BackgroundBrowser(browser))

#
# Platform support for MacOS
#

if sys.platform == 'darwin':
    # Adapted from patch submitted to SourceForge by Steven J. Burr
    class MacOSX(BaseBrowser):
        """Launcher class for Aqua browsers on Mac OS X

        Optionally specify a browser name on instantiation.  Note that this
        will not work for Aqua browsers if the user has moved the application
        package after installation.

        If no browser is specified, the default browser, as specified in the
        Internet System Preferences panel, will be used.
        """
        def __init__(self, name):
            self.name = name

        def open(self, url, new=0, autoraise=True):
            assert "'" not in url
            # hack for local urls
            if not ':' in url:
                url = 'file:'+url

            # new must be 0 or 1
            new = int(bool(new))
            if self.name == "default":
                # User called open, open_new or get without a browser parameter
                script = 'open location "%s"' % url.replace('"', '%22') # opens in default browser
            else:
                # User called get and chose a browser
                if self.name == "OmniWeb":
                    toWindow = ""
                else:
                    # Include toWindow parameter of OpenURL command for browsers
                    # that support it.  0 == new window; -1 == existing
                    toWindow = "toWindow %d" % (new - 1)
                cmd = 'OpenURL "%s"' % url.replace('"', '%22')
                script = '''tell application "%s"
                                activate
                                %s %s
                            end tell''' % (self.name, cmd, toWindow)
            # Open pipe to AppleScript through osascript command
            osapipe = os.popen("osascript", "w")
            if osapipe is None:
                return False
            # Write script to osascript's stdin
            osapipe.write(script)
            rc = osapipe.close()
            return not rc

    class MacOSXOSAScript(BaseBrowser):
        def __init__(self, name):
            self._name = name

        def open(self, url, new=0, autoraise=True):
            if self._name == 'default':
                script = 'open location "%s"' % url.replace('"', '%22') # opens in default browser
            else:
                script = '''
                   tell application "%s"
                       activate
                       open location "%s"
                   end
                   '''%(self._name, url.replace('"', '%22'))

            osapipe = os.popen("osascript", "w")
            if osapipe is None:
                return False

            osapipe.write(script)
            rc = osapipe.close()
            return not rc


    # Don't clear _tryorder or _browsers since OS X can use above Unix support
    # (but we prefer using the OS X specific stuff)
    register("safari", None, MacOSXOSAScript('safari'), -1)
    register("firefox", None, MacOSXOSAScript('firefox'), -1)
    register("MacOSX", None, MacOSXOSAScript('default'), -1)


#
# Platform support for OS/2
#

if sys.platform[:3] == "os2" and _iscommand("netscape"):
    _tryorder = []
    _browsers = {}
    register("os2netscape", None,
             GenericBrowser(["start", "netscape", "%s"]), -1)


# OK, now that we know what the default preference orders for each
# platform are, allow user to override them with the BROWSER variable.
if "BROWSER" in os.environ:
    _userchoices = os.environ["BROWSER"].split(os.pathsep)
    _userchoices.reverse()

    # Treat choices in same way as if passed into get() but do register
    # and prepend to _tryorder
    for cmdline in _userchoices:
        if cmdline != '':
            cmd = _synthesize(cmdline, -1)
            if cmd[1] is None:
                register(cmdline, None, GenericBrowser(cmdline), -1)
    cmdline = None # to make del work if _userchoices was empty
    del cmdline
    del _userchoices

# what to do if _tryorder is now empty?


def main():
    import getopt
    usage = """Usage: %s [-n | -t] url
    -n: open new window
    -t: open new tab""" % sys.argv[0]
    try:
        opts, args = getopt.getopt(sys.argv[1:], 'ntd')
    except getopt.error, msg:
        print >>sys.stderr, msg
        print >>sys.stderr, usage
        sys.exit(1)
    new_win = 0
    for o, a in opts:
        if o == '-n': new_win = 1
        elif o == '-t': new_win = 2
    if len(args) != 1:
        print >>sys.stderr, usage
        sys.exit(1)

    url = args[0]
    open(url, new_win)

    print "\a"

if __name__ == "__main__":
    main()
