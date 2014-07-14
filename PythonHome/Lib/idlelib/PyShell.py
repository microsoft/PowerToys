#! /usr/bin/env python

import os
import os.path
import sys
import string
import getopt
import re
import socket
import time
import threading
import traceback
import types
import io

import linecache
from code import InteractiveInterpreter
from platform import python_version, system

try:
    from Tkinter import *
except ImportError:
    print>>sys.__stderr__, "** IDLE can't import Tkinter.  " \
                           "Your Python may not be configured for Tk. **"
    sys.exit(1)
import tkMessageBox

from idlelib.EditorWindow import EditorWindow, fixwordbreaks
from idlelib.FileList import FileList
from idlelib.ColorDelegator import ColorDelegator
from idlelib.UndoDelegator import UndoDelegator
from idlelib.OutputWindow import OutputWindow
from idlelib.configHandler import idleConf
from idlelib import idlever
from idlelib import rpc
from idlelib import Debugger
from idlelib import RemoteDebugger
from idlelib import macosxSupport

IDENTCHARS = string.ascii_letters + string.digits + "_"
HOST = '127.0.0.1' # python execution server on localhost loopback
PORT = 0  # someday pass in host, port for remote debug capability

try:
    from signal import SIGTERM
except ImportError:
    SIGTERM = 15

# Override warnings module to write to warning_stream.  Initialize to send IDLE
# internal warnings to the console.  ScriptBinding.check_syntax() will
# temporarily redirect the stream to the shell window to display warnings when
# checking user's code.
warning_stream = sys.__stderr__  # None, at least on Windows, if no console.
import warnings

def idle_formatwarning(message, category, filename, lineno, line=None):
    """Format warnings the IDLE way."""

    s = "\nWarning (from warnings module):\n"
    s += '  File \"%s\", line %s\n' % (filename, lineno)
    if line is None:
        line = linecache.getline(filename, lineno)
    line = line.strip()
    if line:
        s += "    %s\n" % line
    s += "%s: %s\n" % (category.__name__, message)
    return s

def idle_showwarning(
        message, category, filename, lineno, file=None, line=None):
    """Show Idle-format warning (after replacing warnings.showwarning).

    The differences are the formatter called, the file=None replacement,
    which can be None, the capture of the consequence AttributeError,
    and the output of a hard-coded prompt.
    """
    if file is None:
        file = warning_stream
    try:
        file.write(idle_formatwarning(
                message, category, filename, lineno, line=line))
        file.write(">>> ")
    except (AttributeError, IOError):
        pass  # if file (probably __stderr__) is invalid, skip warning.

_warnings_showwarning = None

def capture_warnings(capture):
    "Replace warning.showwarning with idle_showwarning, or reverse."

    global _warnings_showwarning
    if capture:
        if _warnings_showwarning is None:
            _warnings_showwarning = warnings.showwarning
            warnings.showwarning = idle_showwarning
    else:
        if _warnings_showwarning is not None:
            warnings.showwarning = _warnings_showwarning
            _warnings_showwarning = None

capture_warnings(True)

def extended_linecache_checkcache(filename=None,
                                  orig_checkcache=linecache.checkcache):
    """Extend linecache.checkcache to preserve the <pyshell#...> entries

    Rather than repeating the linecache code, patch it to save the
    <pyshell#...> entries, call the original linecache.checkcache()
    (skipping them), and then restore the saved entries.

    orig_checkcache is bound at definition time to the original
    method, allowing it to be patched.
    """
    cache = linecache.cache
    save = {}
    for key in list(cache):
        if key[:1] + key[-1:] == '<>':
            save[key] = cache.pop(key)
    orig_checkcache(filename)
    cache.update(save)

# Patch linecache.checkcache():
linecache.checkcache = extended_linecache_checkcache


class PyShellEditorWindow(EditorWindow):
    "Regular text edit window in IDLE, supports breakpoints"

    def __init__(self, *args):
        self.breakpoints = []
        EditorWindow.__init__(self, *args)
        self.text.bind("<<set-breakpoint-here>>", self.set_breakpoint_here)
        self.text.bind("<<clear-breakpoint-here>>", self.clear_breakpoint_here)
        self.text.bind("<<open-python-shell>>", self.flist.open_shell)

        self.breakpointPath = os.path.join(idleConf.GetUserCfgDir(),
                                           'breakpoints.lst')
        # whenever a file is changed, restore breakpoints
        def filename_changed_hook(old_hook=self.io.filename_change_hook,
                                  self=self):
            self.restore_file_breaks()
            old_hook()
        self.io.set_filename_change_hook(filename_changed_hook)
        if self.io.filename:
            self.restore_file_breaks()

    rmenu_specs = [
        ("Cut", "<<cut>>", "rmenu_check_cut"),
        ("Copy", "<<copy>>", "rmenu_check_copy"),
        ("Paste", "<<paste>>", "rmenu_check_paste"),
        ("Set Breakpoint", "<<set-breakpoint-here>>", None),
        ("Clear Breakpoint", "<<clear-breakpoint-here>>", None)
    ]

    def set_breakpoint(self, lineno):
        text = self.text
        filename = self.io.filename
        text.tag_add("BREAK", "%d.0" % lineno, "%d.0" % (lineno+1))
        try:
            i = self.breakpoints.index(lineno)
        except ValueError:  # only add if missing, i.e. do once
            self.breakpoints.append(lineno)
        try:    # update the subprocess debugger
            debug = self.flist.pyshell.interp.debugger
            debug.set_breakpoint_here(filename, lineno)
        except: # but debugger may not be active right now....
            pass

    def set_breakpoint_here(self, event=None):
        text = self.text
        filename = self.io.filename
        if not filename:
            text.bell()
            return
        lineno = int(float(text.index("insert")))
        self.set_breakpoint(lineno)

    def clear_breakpoint_here(self, event=None):
        text = self.text
        filename = self.io.filename
        if not filename:
            text.bell()
            return
        lineno = int(float(text.index("insert")))
        try:
            self.breakpoints.remove(lineno)
        except:
            pass
        text.tag_remove("BREAK", "insert linestart",\
                        "insert lineend +1char")
        try:
            debug = self.flist.pyshell.interp.debugger
            debug.clear_breakpoint_here(filename, lineno)
        except:
            pass

    def clear_file_breaks(self):
        if self.breakpoints:
            text = self.text
            filename = self.io.filename
            if not filename:
                text.bell()
                return
            self.breakpoints = []
            text.tag_remove("BREAK", "1.0", END)
            try:
                debug = self.flist.pyshell.interp.debugger
                debug.clear_file_breaks(filename)
            except:
                pass

    def store_file_breaks(self):
        "Save breakpoints when file is saved"
        # XXX 13 Dec 2002 KBK Currently the file must be saved before it can
        #     be run.  The breaks are saved at that time.  If we introduce
        #     a temporary file save feature the save breaks functionality
        #     needs to be re-verified, since the breaks at the time the
        #     temp file is created may differ from the breaks at the last
        #     permanent save of the file.  Currently, a break introduced
        #     after a save will be effective, but not persistent.
        #     This is necessary to keep the saved breaks synched with the
        #     saved file.
        #
        #     Breakpoints are set as tagged ranges in the text.  Certain
        #     kinds of edits cause these ranges to be deleted: Inserting
        #     or deleting a line just before a breakpoint, and certain
        #     deletions prior to a breakpoint.  These issues need to be
        #     investigated and understood.  It's not clear if they are
        #     Tk issues or IDLE issues, or whether they can actually
        #     be fixed.  Since a modified file has to be saved before it is
        #     run, and since self.breakpoints (from which the subprocess
        #     debugger is loaded) is updated during the save, the visible
        #     breaks stay synched with the subprocess even if one of these
        #     unexpected breakpoint deletions occurs.
        breaks = self.breakpoints
        filename = self.io.filename
        try:
            with open(self.breakpointPath,"r") as old_file:
                lines = old_file.readlines()
        except IOError:
            lines = []
        try:
            with open(self.breakpointPath,"w") as new_file:
                for line in lines:
                    if not line.startswith(filename + '='):
                        new_file.write(line)
                self.update_breakpoints()
                breaks = self.breakpoints
                if breaks:
                    new_file.write(filename + '=' + str(breaks) + '\n')
        except IOError as err:
            if not getattr(self.root, "breakpoint_error_displayed", False):
                self.root.breakpoint_error_displayed = True
                tkMessageBox.showerror(title='IDLE Error',
                    message='Unable to update breakpoint list:\n%s'
                        % str(err),
                    parent=self.text)

    def restore_file_breaks(self):
        self.text.update()   # this enables setting "BREAK" tags to be visible
        if self.io is None:
            # can happen if IDLE closes due to the .update() call
            return
        filename = self.io.filename
        if filename is None:
            return
        if os.path.isfile(self.breakpointPath):
            lines = open(self.breakpointPath,"r").readlines()
            for line in lines:
                if line.startswith(filename + '='):
                    breakpoint_linenumbers = eval(line[len(filename)+1:])
                    for breakpoint_linenumber in breakpoint_linenumbers:
                        self.set_breakpoint(breakpoint_linenumber)

    def update_breakpoints(self):
        "Retrieves all the breakpoints in the current window"
        text = self.text
        ranges = text.tag_ranges("BREAK")
        linenumber_list = self.ranges_to_linenumbers(ranges)
        self.breakpoints = linenumber_list

    def ranges_to_linenumbers(self, ranges):
        lines = []
        for index in range(0, len(ranges), 2):
            lineno = int(float(ranges[index].string))
            end = int(float(ranges[index+1].string))
            while lineno < end:
                lines.append(lineno)
                lineno += 1
        return lines

# XXX 13 Dec 2002 KBK Not used currently
#    def saved_change_hook(self):
#        "Extend base method - clear breaks if module is modified"
#        if not self.get_saved():
#            self.clear_file_breaks()
#        EditorWindow.saved_change_hook(self)

    def _close(self):
        "Extend base method - clear breaks when module is closed"
        self.clear_file_breaks()
        EditorWindow._close(self)


class PyShellFileList(FileList):
    "Extend base class: IDLE supports a shell and breakpoints"

    # override FileList's class variable, instances return PyShellEditorWindow
    # instead of EditorWindow when new edit windows are created.
    EditorWindow = PyShellEditorWindow

    pyshell = None

    def open_shell(self, event=None):
        if self.pyshell:
            self.pyshell.top.wakeup()
        else:
            self.pyshell = PyShell(self)
            if self.pyshell:
                if not self.pyshell.begin():
                    return None
        return self.pyshell


class ModifiedColorDelegator(ColorDelegator):
    "Extend base class: colorizer for the shell window itself"

    def __init__(self):
        ColorDelegator.__init__(self)
        self.LoadTagDefs()

    def recolorize_main(self):
        self.tag_remove("TODO", "1.0", "iomark")
        self.tag_add("SYNC", "1.0", "iomark")
        ColorDelegator.recolorize_main(self)

    def LoadTagDefs(self):
        ColorDelegator.LoadTagDefs(self)
        theme = idleConf.GetOption('main','Theme','name')
        self.tagdefs.update({
            "stdin": {'background':None,'foreground':None},
            "stdout": idleConf.GetHighlight(theme, "stdout"),
            "stderr": idleConf.GetHighlight(theme, "stderr"),
            "console": idleConf.GetHighlight(theme, "console"),
        })

    def removecolors(self):
        # Don't remove shell color tags before "iomark"
        for tag in self.tagdefs:
            self.tag_remove(tag, "iomark", "end")

class ModifiedUndoDelegator(UndoDelegator):
    "Extend base class: forbid insert/delete before the I/O mark"

    def insert(self, index, chars, tags=None):
        try:
            if self.delegate.compare(index, "<", "iomark"):
                self.delegate.bell()
                return
        except TclError:
            pass
        UndoDelegator.insert(self, index, chars, tags)

    def delete(self, index1, index2=None):
        try:
            if self.delegate.compare(index1, "<", "iomark"):
                self.delegate.bell()
                return
        except TclError:
            pass
        UndoDelegator.delete(self, index1, index2)


class MyRPCClient(rpc.RPCClient):

    def handle_EOF(self):
        "Override the base class - just re-raise EOFError"
        raise EOFError


class ModifiedInterpreter(InteractiveInterpreter):

    def __init__(self, tkconsole):
        self.tkconsole = tkconsole
        locals = sys.modules['__main__'].__dict__
        InteractiveInterpreter.__init__(self, locals=locals)
        self.save_warnings_filters = None
        self.restarting = False
        self.subprocess_arglist = None
        self.port = PORT
        self.original_compiler_flags = self.compile.compiler.flags

    _afterid = None
    rpcclt = None
    rpcpid = None

    def spawn_subprocess(self):
        if self.subprocess_arglist is None:
            self.subprocess_arglist = self.build_subprocess_arglist()
        args = self.subprocess_arglist
        self.rpcpid = os.spawnv(os.P_NOWAIT, sys.executable, args)

    def build_subprocess_arglist(self):
        assert (self.port!=0), (
            "Socket should have been assigned a port number.")
        w = ['-W' + s for s in sys.warnoptions]
        if 1/2 > 0: # account for new division
            w.append('-Qnew')
        # Maybe IDLE is installed and is being accessed via sys.path,
        # or maybe it's not installed and the idle.py script is being
        # run from the IDLE source directory.
        del_exitf = idleConf.GetOption('main', 'General', 'delete-exitfunc',
                                       default=False, type='bool')
        if __name__ == 'idlelib.PyShell':
            command = "__import__('idlelib.run').run.main(%r)" % (del_exitf,)
        else:
            command = "__import__('run').main(%r)" % (del_exitf,)
        if sys.platform[:3] == 'win' and ' ' in sys.executable:
            # handle embedded space in path by quoting the argument
            decorated_exec = '"%s"' % sys.executable
        else:
            decorated_exec = sys.executable
        return [decorated_exec] + w + ["-c", command, str(self.port)]

    def start_subprocess(self):
        addr = (HOST, self.port)
        # GUI makes several attempts to acquire socket, listens for connection
        for i in range(3):
            time.sleep(i)
            try:
                self.rpcclt = MyRPCClient(addr)
                break
            except socket.error as err:
                pass
        else:
            self.display_port_binding_error()
            return None
        # if PORT was 0, system will assign an 'ephemeral' port. Find it out:
        self.port = self.rpcclt.listening_sock.getsockname()[1]
        # if PORT was not 0, probably working with a remote execution server
        if PORT != 0:
            # To allow reconnection within the 2MSL wait (cf. Stevens TCP
            # V1, 18.6),  set SO_REUSEADDR.  Note that this can be problematic
            # on Windows since the implementation allows two active sockets on
            # the same address!
            self.rpcclt.listening_sock.setsockopt(socket.SOL_SOCKET,
                                           socket.SO_REUSEADDR, 1)
        self.spawn_subprocess()
        #time.sleep(20) # test to simulate GUI not accepting connection
        # Accept the connection from the Python execution server
        self.rpcclt.listening_sock.settimeout(10)
        try:
            self.rpcclt.accept()
        except socket.timeout as err:
            self.display_no_subprocess_error()
            return None
        self.rpcclt.register("console", self.tkconsole)
        self.rpcclt.register("stdin", self.tkconsole.stdin)
        self.rpcclt.register("stdout", self.tkconsole.stdout)
        self.rpcclt.register("stderr", self.tkconsole.stderr)
        self.rpcclt.register("flist", self.tkconsole.flist)
        self.rpcclt.register("linecache", linecache)
        self.rpcclt.register("interp", self)
        self.transfer_path(with_cwd=True)
        self.poll_subprocess()
        return self.rpcclt

    def restart_subprocess(self, with_cwd=False):
        if self.restarting:
            return self.rpcclt
        self.restarting = True
        # close only the subprocess debugger
        debug = self.getdebugger()
        if debug:
            try:
                # Only close subprocess debugger, don't unregister gui_adap!
                RemoteDebugger.close_subprocess_debugger(self.rpcclt)
            except:
                pass
        # Kill subprocess, spawn a new one, accept connection.
        self.rpcclt.close()
        self.unix_terminate()
        console = self.tkconsole
        was_executing = console.executing
        console.executing = False
        self.spawn_subprocess()
        try:
            self.rpcclt.accept()
        except socket.timeout as err:
            self.display_no_subprocess_error()
            return None
        self.transfer_path(with_cwd=with_cwd)
        console.stop_readline()
        # annotate restart in shell window and mark it
        console.text.delete("iomark", "end-1c")
        if was_executing:
            console.write('\n')
            console.showprompt()
        halfbar = ((int(console.width) - 16) // 2) * '='
        console.write(halfbar + ' RESTART ' + halfbar)
        console.text.mark_set("restart", "end-1c")
        console.text.mark_gravity("restart", "left")
        console.showprompt()
        # restart subprocess debugger
        if debug:
            # Restarted debugger connects to current instance of debug GUI
            gui = RemoteDebugger.restart_subprocess_debugger(self.rpcclt)
            # reload remote debugger breakpoints for all PyShellEditWindows
            debug.load_breakpoints()
        self.compile.compiler.flags = self.original_compiler_flags
        self.restarting = False
        return self.rpcclt

    def __request_interrupt(self):
        self.rpcclt.remotecall("exec", "interrupt_the_server", (), {})

    def interrupt_subprocess(self):
        threading.Thread(target=self.__request_interrupt).start()

    def kill_subprocess(self):
        if self._afterid is not None:
            self.tkconsole.text.after_cancel(self._afterid)
        try:
            self.rpcclt.close()
        except AttributeError:  # no socket
            pass
        self.unix_terminate()
        self.tkconsole.executing = False
        self.rpcclt = None

    def unix_terminate(self):
        "UNIX: make sure subprocess is terminated and collect status"
        if hasattr(os, 'kill'):
            try:
                os.kill(self.rpcpid, SIGTERM)
            except OSError:
                # process already terminated:
                return
            else:
                try:
                    os.waitpid(self.rpcpid, 0)
                except OSError:
                    return

    def transfer_path(self, with_cwd=False):
        if with_cwd:        # Issue 13506
            path = ['']     # include Current Working Directory
            path.extend(sys.path)
        else:
            path = sys.path

        self.runcommand("""if 1:
        import sys as _sys
        _sys.path = %r
        del _sys
        \n""" % (path,))

    active_seq = None

    def poll_subprocess(self):
        clt = self.rpcclt
        if clt is None:
            return
        try:
            response = clt.pollresponse(self.active_seq, wait=0.05)
        except (EOFError, IOError, KeyboardInterrupt):
            # lost connection or subprocess terminated itself, restart
            # [the KBI is from rpc.SocketIO.handle_EOF()]
            if self.tkconsole.closing:
                return
            response = None
            self.restart_subprocess()
        if response:
            self.tkconsole.resetoutput()
            self.active_seq = None
            how, what = response
            console = self.tkconsole.console
            if how == "OK":
                if what is not None:
                    print >>console, repr(what)
            elif how == "EXCEPTION":
                if self.tkconsole.getvar("<<toggle-jit-stack-viewer>>"):
                    self.remote_stack_viewer()
            elif how == "ERROR":
                errmsg = "PyShell.ModifiedInterpreter: Subprocess ERROR:\n"
                print >>sys.__stderr__, errmsg, what
                print >>console, errmsg, what
            # we received a response to the currently active seq number:
            try:
                self.tkconsole.endexecuting()
            except AttributeError:  # shell may have closed
                pass
        # Reschedule myself
        if not self.tkconsole.closing:
            self._afterid = self.tkconsole.text.after(
                self.tkconsole.pollinterval, self.poll_subprocess)

    debugger = None

    def setdebugger(self, debugger):
        self.debugger = debugger

    def getdebugger(self):
        return self.debugger

    def open_remote_stack_viewer(self):
        """Initiate the remote stack viewer from a separate thread.

        This method is called from the subprocess, and by returning from this
        method we allow the subprocess to unblock.  After a bit the shell
        requests the subprocess to open the remote stack viewer which returns a
        static object looking at the last exception.  It is queried through
        the RPC mechanism.

        """
        self.tkconsole.text.after(300, self.remote_stack_viewer)
        return

    def remote_stack_viewer(self):
        from idlelib import RemoteObjectBrowser
        oid = self.rpcclt.remotequeue("exec", "stackviewer", ("flist",), {})
        if oid is None:
            self.tkconsole.root.bell()
            return
        item = RemoteObjectBrowser.StubObjectTreeItem(self.rpcclt, oid)
        from idlelib.TreeWidget import ScrolledCanvas, TreeNode
        top = Toplevel(self.tkconsole.root)
        theme = idleConf.GetOption('main','Theme','name')
        background = idleConf.GetHighlight(theme, 'normal')['background']
        sc = ScrolledCanvas(top, bg=background, highlightthickness=0)
        sc.frame.pack(expand=1, fill="both")
        node = TreeNode(sc.canvas, None, item)
        node.expand()
        # XXX Should GC the remote tree when closing the window

    gid = 0

    def execsource(self, source):
        "Like runsource() but assumes complete exec source"
        filename = self.stuffsource(source)
        self.execfile(filename, source)

    def execfile(self, filename, source=None):
        "Execute an existing file"
        if source is None:
            source = open(filename, "r").read()
        try:
            code = compile(source, filename, "exec")
        except (OverflowError, SyntaxError):
            self.tkconsole.resetoutput()
            tkerr = self.tkconsole.stderr
            print>>tkerr, '*** Error in script or command!\n'
            print>>tkerr, 'Traceback (most recent call last):'
            InteractiveInterpreter.showsyntaxerror(self, filename)
            self.tkconsole.showprompt()
        else:
            self.runcode(code)

    def runsource(self, source):
        "Extend base class method: Stuff the source in the line cache first"
        filename = self.stuffsource(source)
        self.more = 0
        self.save_warnings_filters = warnings.filters[:]
        warnings.filterwarnings(action="error", category=SyntaxWarning)
        if isinstance(source, types.UnicodeType):
            from idlelib import IOBinding
            try:
                source = source.encode(IOBinding.encoding)
            except UnicodeError:
                self.tkconsole.resetoutput()
                self.write("Unsupported characters in input\n")
                return
        try:
            # InteractiveInterpreter.runsource() calls its runcode() method,
            # which is overridden (see below)
            return InteractiveInterpreter.runsource(self, source, filename)
        finally:
            if self.save_warnings_filters is not None:
                warnings.filters[:] = self.save_warnings_filters
                self.save_warnings_filters = None

    def stuffsource(self, source):
        "Stuff source in the filename cache"
        filename = "<pyshell#%d>" % self.gid
        self.gid = self.gid + 1
        lines = source.split("\n")
        linecache.cache[filename] = len(source)+1, 0, lines, filename
        return filename

    def prepend_syspath(self, filename):
        "Prepend sys.path with file's directory if not already included"
        self.runcommand("""if 1:
            _filename = %r
            import sys as _sys
            from os.path import dirname as _dirname
            _dir = _dirname(_filename)
            if not _dir in _sys.path:
                _sys.path.insert(0, _dir)
            del _filename, _sys, _dirname, _dir
            \n""" % (filename,))

    def showsyntaxerror(self, filename=None):
        """Extend base class method: Add Colorizing

        Color the offending position instead of printing it and pointing at it
        with a caret.

        """
        text = self.tkconsole.text
        stuff = self.unpackerror()
        if stuff:
            msg, lineno, offset, line = stuff
            if lineno == 1:
                pos = "iomark + %d chars" % (offset-1)
            else:
                pos = "iomark linestart + %d lines + %d chars" % \
                      (lineno-1, offset-1)
            text.tag_add("ERROR", pos)
            text.see(pos)
            char = text.get(pos)
            if char and char in IDENTCHARS:
                text.tag_add("ERROR", pos + " wordstart", pos)
            self.tkconsole.resetoutput()
            self.write("SyntaxError: %s\n" % str(msg))
        else:
            self.tkconsole.resetoutput()
            InteractiveInterpreter.showsyntaxerror(self, filename)
        self.tkconsole.showprompt()

    def unpackerror(self):
        type, value, tb = sys.exc_info()
        ok = type is SyntaxError
        if ok:
            try:
                msg, (dummy_filename, lineno, offset, line) = value
                if not offset:
                    offset = 0
            except:
                ok = 0
        if ok:
            return msg, lineno, offset, line
        else:
            return None

    def showtraceback(self):
        "Extend base class method to reset output properly"
        self.tkconsole.resetoutput()
        self.checklinecache()
        InteractiveInterpreter.showtraceback(self)
        if self.tkconsole.getvar("<<toggle-jit-stack-viewer>>"):
            self.tkconsole.open_stack_viewer()

    def checklinecache(self):
        c = linecache.cache
        for key in c.keys():
            if key[:1] + key[-1:] != "<>":
                del c[key]

    def runcommand(self, code):
        "Run the code without invoking the debugger"
        # The code better not raise an exception!
        if self.tkconsole.executing:
            self.display_executing_dialog()
            return 0
        if self.rpcclt:
            self.rpcclt.remotequeue("exec", "runcode", (code,), {})
        else:
            exec code in self.locals
        return 1

    def runcode(self, code):
        "Override base class method"
        if self.tkconsole.executing:
            self.interp.restart_subprocess()
        self.checklinecache()
        if self.save_warnings_filters is not None:
            warnings.filters[:] = self.save_warnings_filters
            self.save_warnings_filters = None
        debugger = self.debugger
        try:
            self.tkconsole.beginexecuting()
            if not debugger and self.rpcclt is not None:
                self.active_seq = self.rpcclt.asyncqueue("exec", "runcode",
                                                        (code,), {})
            elif debugger:
                debugger.run(code, self.locals)
            else:
                exec code in self.locals
        except SystemExit:
            if not self.tkconsole.closing:
                if tkMessageBox.askyesno(
                    "Exit?",
                    "Do you want to exit altogether?",
                    default="yes",
                    master=self.tkconsole.text):
                    raise
                else:
                    self.showtraceback()
            else:
                raise
        except:
            if use_subprocess:
                print >>self.tkconsole.stderr, \
                         "IDLE internal error in runcode()"
                self.showtraceback()
                self.tkconsole.endexecuting()
            else:
                if self.tkconsole.canceled:
                    self.tkconsole.canceled = False
                    print >>self.tkconsole.stderr, "KeyboardInterrupt"
                else:
                    self.showtraceback()
        finally:
            if not use_subprocess:
                try:
                    self.tkconsole.endexecuting()
                except AttributeError:  # shell may have closed
                    pass

    def write(self, s):
        "Override base class method"
        self.tkconsole.stderr.write(s)

    def display_port_binding_error(self):
        tkMessageBox.showerror(
            "Port Binding Error",
            "IDLE can't bind to a TCP/IP port, which is necessary to "
            "communicate with its Python execution server.  This might be "
            "because no networking is installed on this computer.  "
            "Run IDLE with the -n command line switch to start without a "
            "subprocess and refer to Help/IDLE Help 'Running without a "
            "subprocess' for further details.",
            master=self.tkconsole.text)

    def display_no_subprocess_error(self):
        tkMessageBox.showerror(
            "Subprocess Startup Error",
            "IDLE's subprocess didn't make connection.  Either IDLE can't "
            "start a subprocess or personal firewall software is blocking "
            "the connection.",
            master=self.tkconsole.text)

    def display_executing_dialog(self):
        tkMessageBox.showerror(
            "Already executing",
            "The Python Shell window is already executing a command; "
            "please wait until it is finished.",
            master=self.tkconsole.text)


class PyShell(OutputWindow):

    shell_title = "Python " + python_version() + " Shell"

    # Override classes
    ColorDelegator = ModifiedColorDelegator
    UndoDelegator = ModifiedUndoDelegator

    # Override menus
    menu_specs = [
        ("file", "_File"),
        ("edit", "_Edit"),
        ("debug", "_Debug"),
        ("options", "_Options"),
        ("windows", "_Windows"),
        ("help", "_Help"),
    ]

    if sys.platform == "darwin":
        menu_specs[-2] = ("windows", "_Window")


    # New classes
    from idlelib.IdleHistory import History

    def __init__(self, flist=None):
        if use_subprocess:
            ms = self.menu_specs
            if ms[2][0] != "shell":
                ms.insert(2, ("shell", "She_ll"))
        self.interp = ModifiedInterpreter(self)
        if flist is None:
            root = Tk()
            fixwordbreaks(root)
            root.withdraw()
            flist = PyShellFileList(root)
        #
        OutputWindow.__init__(self, flist, None, None)
        #
##        self.config(usetabs=1, indentwidth=8, context_use_ps1=1)
        self.usetabs = True
        # indentwidth must be 8 when using tabs.  See note in EditorWindow:
        self.indentwidth = 8
        self.context_use_ps1 = True
        #
        text = self.text
        text.configure(wrap="char")
        text.bind("<<newline-and-indent>>", self.enter_callback)
        text.bind("<<plain-newline-and-indent>>", self.linefeed_callback)
        text.bind("<<interrupt-execution>>", self.cancel_callback)
        text.bind("<<end-of-file>>", self.eof_callback)
        text.bind("<<open-stack-viewer>>", self.open_stack_viewer)
        text.bind("<<toggle-debugger>>", self.toggle_debugger)
        text.bind("<<toggle-jit-stack-viewer>>", self.toggle_jit_stack_viewer)
        if use_subprocess:
            text.bind("<<view-restart>>", self.view_restart_mark)
            text.bind("<<restart-shell>>", self.restart_shell)
        #
        self.save_stdout = sys.stdout
        self.save_stderr = sys.stderr
        self.save_stdin = sys.stdin
        from idlelib import IOBinding
        self.stdin = PseudoInputFile(self, "stdin", IOBinding.encoding)
        self.stdout = PseudoOutputFile(self, "stdout", IOBinding.encoding)
        self.stderr = PseudoOutputFile(self, "stderr", IOBinding.encoding)
        self.console = PseudoOutputFile(self, "console", IOBinding.encoding)
        if not use_subprocess:
            sys.stdout = self.stdout
            sys.stderr = self.stderr
            sys.stdin = self.stdin
        #
        self.history = self.History(self.text)
        #
        self.pollinterval = 50  # millisec

    def get_standard_extension_names(self):
        return idleConf.GetExtensions(shell_only=True)

    reading = False
    executing = False
    canceled = False
    endoffile = False
    closing = False
    _stop_readline_flag = False

    def set_warning_stream(self, stream):
        global warning_stream
        warning_stream = stream

    def get_warning_stream(self):
        return warning_stream

    def toggle_debugger(self, event=None):
        if self.executing:
            tkMessageBox.showerror("Don't debug now",
                "You can only toggle the debugger when idle",
                master=self.text)
            self.set_debugger_indicator()
            return "break"
        else:
            db = self.interp.getdebugger()
            if db:
                self.close_debugger()
            else:
                self.open_debugger()

    def set_debugger_indicator(self):
        db = self.interp.getdebugger()
        self.setvar("<<toggle-debugger>>", not not db)

    def toggle_jit_stack_viewer(self, event=None):
        pass # All we need is the variable

    def close_debugger(self):
        db = self.interp.getdebugger()
        if db:
            self.interp.setdebugger(None)
            db.close()
            if self.interp.rpcclt:
                RemoteDebugger.close_remote_debugger(self.interp.rpcclt)
            self.resetoutput()
            self.console.write("[DEBUG OFF]\n")
            sys.ps1 = ">>> "
            self.showprompt()
        self.set_debugger_indicator()

    def open_debugger(self):
        if self.interp.rpcclt:
            dbg_gui = RemoteDebugger.start_remote_debugger(self.interp.rpcclt,
                                                           self)
        else:
            dbg_gui = Debugger.Debugger(self)
        self.interp.setdebugger(dbg_gui)
        dbg_gui.load_breakpoints()
        sys.ps1 = "[DEBUG ON]\n>>> "
        self.showprompt()
        self.set_debugger_indicator()

    def beginexecuting(self):
        "Helper for ModifiedInterpreter"
        self.resetoutput()
        self.executing = 1

    def endexecuting(self):
        "Helper for ModifiedInterpreter"
        self.executing = 0
        self.canceled = 0
        self.showprompt()

    def close(self):
        "Extend EditorWindow.close()"
        if self.executing:
            response = tkMessageBox.askokcancel(
                "Kill?",
                "The program is still running!\n Do you want to kill it?",
                default="ok",
                parent=self.text)
            if response is False:
                return "cancel"
        self.stop_readline()
        self.canceled = True
        self.closing = True
        return EditorWindow.close(self)

    def _close(self):
        "Extend EditorWindow._close(), shut down debugger and execution server"
        self.close_debugger()
        if use_subprocess:
            self.interp.kill_subprocess()
        # Restore std streams
        sys.stdout = self.save_stdout
        sys.stderr = self.save_stderr
        sys.stdin = self.save_stdin
        # Break cycles
        self.interp = None
        self.console = None
        self.flist.pyshell = None
        self.history = None
        EditorWindow._close(self)

    def ispythonsource(self, filename):
        "Override EditorWindow method: never remove the colorizer"
        return True

    def short_title(self):
        return self.shell_title

    COPYRIGHT = \
          'Type "copyright", "credits" or "license()" for more information.'

    def begin(self):
        self.resetoutput()
        if use_subprocess:
            nosub = ''
            client = self.interp.start_subprocess()
            if not client:
                self.close()
                return False
        else:
            nosub = "==== No Subprocess ===="
        self.write("Python %s on %s\n%s\n%s" %
                   (sys.version, sys.platform, self.COPYRIGHT, nosub))
        self.showprompt()
        import Tkinter
        Tkinter._default_root = None # 03Jan04 KBK What's this?
        return True

    def stop_readline(self):
        if not self.reading:  # no nested mainloop to exit.
            return
        self._stop_readline_flag = True
        self.top.quit()

    def readline(self):
        save = self.reading
        try:
            self.reading = 1
            self.top.mainloop()  # nested mainloop()
        finally:
            self.reading = save
        if self._stop_readline_flag:
            self._stop_readline_flag = False
            return ""
        line = self.text.get("iomark", "end-1c")
        if len(line) == 0:  # may be EOF if we quit our mainloop with Ctrl-C
            line = "\n"
        if isinstance(line, unicode):
            from idlelib import IOBinding
            try:
                line = line.encode(IOBinding.encoding)
            except UnicodeError:
                pass
        self.resetoutput()
        if self.canceled:
            self.canceled = 0
            if not use_subprocess:
                raise KeyboardInterrupt
        if self.endoffile:
            self.endoffile = 0
            line = ""
        return line

    def isatty(self):
        return True

    def cancel_callback(self, event=None):
        try:
            if self.text.compare("sel.first", "!=", "sel.last"):
                return # Active selection -- always use default binding
        except:
            pass
        if not (self.executing or self.reading):
            self.resetoutput()
            self.interp.write("KeyboardInterrupt\n")
            self.showprompt()
            return "break"
        self.endoffile = 0
        self.canceled = 1
        if (self.executing and self.interp.rpcclt):
            if self.interp.getdebugger():
                self.interp.restart_subprocess()
            else:
                self.interp.interrupt_subprocess()
        if self.reading:
            self.top.quit()  # exit the nested mainloop() in readline()
        return "break"

    def eof_callback(self, event):
        if self.executing and not self.reading:
            return # Let the default binding (delete next char) take over
        if not (self.text.compare("iomark", "==", "insert") and
                self.text.compare("insert", "==", "end-1c")):
            return # Let the default binding (delete next char) take over
        if not self.executing:
            self.resetoutput()
            self.close()
        else:
            self.canceled = 0
            self.endoffile = 1
            self.top.quit()
        return "break"

    def linefeed_callback(self, event):
        # Insert a linefeed without entering anything (still autoindented)
        if self.reading:
            self.text.insert("insert", "\n")
            self.text.see("insert")
        else:
            self.newline_and_indent_event(event)
        return "break"

    def enter_callback(self, event):
        if self.executing and not self.reading:
            return # Let the default binding (insert '\n') take over
        # If some text is selected, recall the selection
        # (but only if this before the I/O mark)
        try:
            sel = self.text.get("sel.first", "sel.last")
            if sel:
                if self.text.compare("sel.last", "<=", "iomark"):
                    self.recall(sel, event)
                    return "break"
        except:
            pass
        # If we're strictly before the line containing iomark, recall
        # the current line, less a leading prompt, less leading or
        # trailing whitespace
        if self.text.compare("insert", "<", "iomark linestart"):
            # Check if there's a relevant stdin range -- if so, use it
            prev = self.text.tag_prevrange("stdin", "insert")
            if prev and self.text.compare("insert", "<", prev[1]):
                self.recall(self.text.get(prev[0], prev[1]), event)
                return "break"
            next = self.text.tag_nextrange("stdin", "insert")
            if next and self.text.compare("insert lineend", ">=", next[0]):
                self.recall(self.text.get(next[0], next[1]), event)
                return "break"
            # No stdin mark -- just get the current line, less any prompt
            indices = self.text.tag_nextrange("console", "insert linestart")
            if indices and \
               self.text.compare(indices[0], "<=", "insert linestart"):
                self.recall(self.text.get(indices[1], "insert lineend"), event)
            else:
                self.recall(self.text.get("insert linestart", "insert lineend"), event)
            return "break"
        # If we're between the beginning of the line and the iomark, i.e.
        # in the prompt area, move to the end of the prompt
        if self.text.compare("insert", "<", "iomark"):
            self.text.mark_set("insert", "iomark")
        # If we're in the current input and there's only whitespace
        # beyond the cursor, erase that whitespace first
        s = self.text.get("insert", "end-1c")
        if s and not s.strip():
            self.text.delete("insert", "end-1c")
        # If we're in the current input before its last line,
        # insert a newline right at the insert point
        if self.text.compare("insert", "<", "end-1c linestart"):
            self.newline_and_indent_event(event)
            return "break"
        # We're in the last line; append a newline and submit it
        self.text.mark_set("insert", "end-1c")
        if self.reading:
            self.text.insert("insert", "\n")
            self.text.see("insert")
        else:
            self.newline_and_indent_event(event)
        self.text.tag_add("stdin", "iomark", "end-1c")
        self.text.update_idletasks()
        if self.reading:
            self.top.quit() # Break out of recursive mainloop() in raw_input()
        else:
            self.runit()
        return "break"

    def recall(self, s, event):
        # remove leading and trailing empty or whitespace lines
        s = re.sub(r'^\s*\n', '' , s)
        s = re.sub(r'\n\s*$', '', s)
        lines = s.split('\n')
        self.text.undo_block_start()
        try:
            self.text.tag_remove("sel", "1.0", "end")
            self.text.mark_set("insert", "end-1c")
            prefix = self.text.get("insert linestart", "insert")
            if prefix.rstrip().endswith(':'):
                self.newline_and_indent_event(event)
                prefix = self.text.get("insert linestart", "insert")
            self.text.insert("insert", lines[0].strip())
            if len(lines) > 1:
                orig_base_indent = re.search(r'^([ \t]*)', lines[0]).group(0)
                new_base_indent  = re.search(r'^([ \t]*)', prefix).group(0)
                for line in lines[1:]:
                    if line.startswith(orig_base_indent):
                        # replace orig base indentation with new indentation
                        line = new_base_indent + line[len(orig_base_indent):]
                    self.text.insert('insert', '\n'+line.rstrip())
        finally:
            self.text.see("insert")
            self.text.undo_block_stop()

    def runit(self):
        line = self.text.get("iomark", "end-1c")
        # Strip off last newline and surrounding whitespace.
        # (To allow you to hit return twice to end a statement.)
        i = len(line)
        while i > 0 and line[i-1] in " \t":
            i = i-1
        if i > 0 and line[i-1] == "\n":
            i = i-1
        while i > 0 and line[i-1] in " \t":
            i = i-1
        line = line[:i]
        more = self.interp.runsource(line)

    def open_stack_viewer(self, event=None):
        if self.interp.rpcclt:
            return self.interp.remote_stack_viewer()
        try:
            sys.last_traceback
        except:
            tkMessageBox.showerror("No stack trace",
                "There is no stack trace yet.\n"
                "(sys.last_traceback is not defined)",
                master=self.text)
            return
        from idlelib.StackViewer import StackBrowser
        sv = StackBrowser(self.root, self.flist)

    def view_restart_mark(self, event=None):
        self.text.see("iomark")
        self.text.see("restart")

    def restart_shell(self, event=None):
        "Callback for Run/Restart Shell Cntl-F6"
        self.interp.restart_subprocess(with_cwd=True)

    def showprompt(self):
        self.resetoutput()
        try:
            s = str(sys.ps1)
        except:
            s = ""
        self.console.write(s)
        self.text.mark_set("insert", "end-1c")
        self.set_line_and_column()
        self.io.reset_undo()

    def resetoutput(self):
        source = self.text.get("iomark", "end-1c")
        if self.history:
            self.history.store(source)
        if self.text.get("end-2c") != "\n":
            self.text.insert("end-1c", "\n")
        self.text.mark_set("iomark", "end-1c")
        self.set_line_and_column()
        sys.stdout.softspace = 0

    def write(self, s, tags=()):
        try:
            self.text.mark_gravity("iomark", "right")
            OutputWindow.write(self, s, tags, "iomark")
            self.text.mark_gravity("iomark", "left")
        except:
            pass
        if self.canceled:
            self.canceled = 0
            if not use_subprocess:
                raise KeyboardInterrupt

    def rmenu_check_cut(self):
        try:
            if self.text.compare('sel.first', '<', 'iomark'):
                return 'disabled'
        except TclError: # no selection, so the index 'sel.first' doesn't exist
            return 'disabled'
        return super(PyShell, self).rmenu_check_cut()

    def rmenu_check_paste(self):
        if self.text.compare('insert', '<', 'iomark'):
            return 'disabled'
        return super(PyShell, self).rmenu_check_paste()

class PseudoFile(io.TextIOBase):

    def __init__(self, shell, tags, encoding=None):
        self.shell = shell
        self.tags = tags
        self.softspace = 0
        self._encoding = encoding

    @property
    def encoding(self):
        return self._encoding

    @property
    def name(self):
        return '<%s>' % self.tags

    def isatty(self):
        return True


class PseudoOutputFile(PseudoFile):

    def writable(self):
        return True

    def write(self, s):
        if self.closed:
            raise ValueError("write to closed file")
        if type(s) not in (unicode, str, bytearray):
            # See issue #19481
            if isinstance(s, unicode):
                s = unicode.__getslice__(s, None, None)
            elif isinstance(s, str):
                s = str.__str__(s)
            elif isinstance(s, bytearray):
                s = bytearray.__str__(s)
            else:
                raise TypeError('must be string, not ' + type(s).__name__)
        return self.shell.write(s, self.tags)


class PseudoInputFile(PseudoFile):

    def __init__(self, shell, tags, encoding=None):
        PseudoFile.__init__(self, shell, tags, encoding)
        self._line_buffer = ''

    def readable(self):
        return True

    def read(self, size=-1):
        if self.closed:
            raise ValueError("read from closed file")
        if size is None:
            size = -1
        elif not isinstance(size, int):
            raise TypeError('must be int, not ' + type(size).__name__)
        result = self._line_buffer
        self._line_buffer = ''
        if size < 0:
            while True:
                line = self.shell.readline()
                if not line: break
                result += line
        else:
            while len(result) < size:
                line = self.shell.readline()
                if not line: break
                result += line
            self._line_buffer = result[size:]
            result = result[:size]
        return result

    def readline(self, size=-1):
        if self.closed:
            raise ValueError("read from closed file")
        if size is None:
            size = -1
        elif not isinstance(size, int):
            raise TypeError('must be int, not ' + type(size).__name__)
        line = self._line_buffer or self.shell.readline()
        if size < 0:
            size = len(line)
        eol = line.find('\n', 0, size)
        if eol >= 0:
            size = eol + 1
        self._line_buffer = line[size:]
        return line[:size]

    def close(self):
        self.shell.close()


usage_msg = """\

USAGE: idle  [-deins] [-t title] [file]*
       idle  [-dns] [-t title] (-c cmd | -r file) [arg]*
       idle  [-dns] [-t title] - [arg]*

  -h         print this help message and exit
  -n         run IDLE without a subprocess (see Help/IDLE Help for details)

The following options will override the IDLE 'settings' configuration:

  -e         open an edit window
  -i         open a shell window

The following options imply -i and will open a shell:

  -c cmd     run the command in a shell, or
  -r file    run script from file

  -d         enable the debugger
  -s         run $IDLESTARTUP or $PYTHONSTARTUP before anything else
  -t title   set title of shell window

A default edit window will be bypassed when -c, -r, or - are used.

[arg]* are passed to the command (-c) or script (-r) in sys.argv[1:].

Examples:

idle
        Open an edit window or shell depending on IDLE's configuration.

idle foo.py foobar.py
        Edit the files, also open a shell if configured to start with shell.

idle -est "Baz" foo.py
        Run $IDLESTARTUP or $PYTHONSTARTUP, edit foo.py, and open a shell
        window with the title "Baz".

idle -c "import sys; print sys.argv" "foo"
        Open a shell window and run the command, passing "-c" in sys.argv[0]
        and "foo" in sys.argv[1].

idle -d -s -r foo.py "Hello World"
        Open a shell window, run a startup script, enable the debugger, and
        run foo.py, passing "foo.py" in sys.argv[0] and "Hello World" in
        sys.argv[1].

echo "import sys; print sys.argv" | idle - "foobar"
        Open a shell window, run the script piped in, passing '' in sys.argv[0]
        and "foobar" in sys.argv[1].
"""

def main():
    global flist, root, use_subprocess

    capture_warnings(True)
    use_subprocess = True
    enable_shell = False
    enable_edit = False
    debug = False
    cmd = None
    script = None
    startup = False
    try:
        opts, args = getopt.getopt(sys.argv[1:], "c:deihnr:st:")
    except getopt.error as msg:
        sys.stderr.write("Error: %s\n" % str(msg))
        sys.stderr.write(usage_msg)
        sys.exit(2)
    for o, a in opts:
        if o == '-c':
            cmd = a
            enable_shell = True
        if o == '-d':
            debug = True
            enable_shell = True
        if o == '-e':
            enable_edit = True
        if o == '-h':
            sys.stdout.write(usage_msg)
            sys.exit()
        if o == '-i':
            enable_shell = True
        if o == '-n':
            use_subprocess = False
        if o == '-r':
            script = a
            if os.path.isfile(script):
                pass
            else:
                print "No script file: ", script
                sys.exit()
            enable_shell = True
        if o == '-s':
            startup = True
            enable_shell = True
        if o == '-t':
            PyShell.shell_title = a
            enable_shell = True
    if args and args[0] == '-':
        cmd = sys.stdin.read()
        enable_shell = True
    # process sys.argv and sys.path:
    for i in range(len(sys.path)):
        sys.path[i] = os.path.abspath(sys.path[i])
    if args and args[0] == '-':
        sys.argv = [''] + args[1:]
    elif cmd:
        sys.argv = ['-c'] + args
    elif script:
        sys.argv = [script] + args
    elif args:
        enable_edit = True
        pathx = []
        for filename in args:
            pathx.append(os.path.dirname(filename))
        for dir in pathx:
            dir = os.path.abspath(dir)
            if dir not in sys.path:
                sys.path.insert(0, dir)
    else:
        dir = os.getcwd()
        if not dir in sys.path:
            sys.path.insert(0, dir)
    # check the IDLE settings configuration (but command line overrides)
    edit_start = idleConf.GetOption('main', 'General',
                                    'editor-on-startup', type='bool')
    enable_edit = enable_edit or edit_start
    enable_shell = enable_shell or not enable_edit
    # start editor and/or shell windows:
    root = Tk(className="Idle")

    # set application icon
    icondir = os.path.join(os.path.dirname(__file__), 'Icons')
    if system() == 'Windows':
        iconfile = os.path.join(icondir, 'idle.ico')
        root.wm_iconbitmap(default=iconfile)
    elif TkVersion >= 8.5:
        ext = '.png' if TkVersion >= 8.6 else '.gif'
        iconfiles = [os.path.join(icondir, 'idle_%d%s' % (size, ext))
                     for size in (16, 32, 48)]
        icons = [PhotoImage(file=iconfile) for iconfile in iconfiles]
        root.tk.call('wm', 'iconphoto', str(root), "-default", *icons)

    fixwordbreaks(root)
    root.withdraw()
    flist = PyShellFileList(root)
    macosxSupport.setupApp(root, flist)

    if enable_edit:
        if not (cmd or script):
            for filename in args[:]:
                if flist.open(filename) is None:
                    # filename is a directory actually, disconsider it
                    args.remove(filename)
            if not args:
                flist.new()

    if enable_shell:
        shell = flist.open_shell()
        if not shell:
            return # couldn't open shell
        if macosxSupport.isAquaTk() and flist.dict:
            # On OSX: when the user has double-clicked on a file that causes
            # IDLE to be launched the shell window will open just in front of
            # the file she wants to see. Lower the interpreter window when
            # there are open files.
            shell.top.lower()
    else:
        shell = flist.pyshell

    # Handle remaining options. If any of these are set, enable_shell
    # was set also, so shell must be true to reach here.
    if debug:
        shell.open_debugger()
    if startup:
        filename = os.environ.get("IDLESTARTUP") or \
                   os.environ.get("PYTHONSTARTUP")
        if filename and os.path.isfile(filename):
            shell.interp.execfile(filename)
    if cmd or script:
        shell.interp.runcommand("""if 1:
            import sys as _sys
            _sys.argv = %r
            del _sys
            \n""" % (sys.argv,))
        if cmd:
            shell.interp.execsource(cmd)
        elif script:
            shell.interp.prepend_syspath(script)
            shell.interp.execfile(script)
    elif shell:
        # If there is a shell window and no cmd or script in progress,
        # check for problematic OS X Tk versions and print a warning
        # message in the IDLE shell window; this is less intrusive
        # than always opening a separate window.
        tkversionwarning = macosxSupport.tkVersionWarning(root)
        if tkversionwarning:
            shell.interp.runcommand("print('%s')" % tkversionwarning)

    while flist.inversedict:  # keep IDLE running while files are open.
        root.mainloop()
    root.destroy()
    capture_warnings(False)

if __name__ == "__main__":
    sys.modules['PyShell'] = sys.modules['__main__']
    main()

capture_warnings(False)  # Make sure turned off; see issue 18081
