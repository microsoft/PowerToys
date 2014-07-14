IDLE is Python's Tkinter-based Integrated DeveLopment Environment.

IDLE emphasizes a lightweight, clean design with a simple user interface.
Although it is suitable for beginners, even advanced users will find that
IDLE has everything they really need to develop pure Python code.

IDLE features a multi-window text editor with multiple undo, Python colorizing,
and many other capabilities, e.g. smart indent, call tips, and autocompletion.

The editor has comprehensive search functions, including searching through
multiple files.  Class browsers and path browsers provide fast access to
code objects from a top level viewpoint without dealing with code folding.

There is a Python Shell window which features colorizing and command recall.

IDLE executes Python code in a separate process, which is restarted for each
Run (F5) initiated from an editor window.  The environment can also be 
restarted from the Shell window without restarting IDLE.

This enhancement has often been requested, and is now finally available.  The
magic "reload/import *" incantations are no longer required when editing and
testing a module two or three steps down the import chain.

(Personal firewall software may warn about the connection IDLE makes to its
subprocess using this computer's internal loopback interface.  This connection
is not visible on any external interface and no data is sent to or received
from the Internet.)

It is possible to interrupt tightly looping user code, even on Windows.

Applications which cannot support subprocesses and/or sockets can still run
IDLE in a single process.

IDLE has an integrated debugger with stepping, persistent breakpoints, and call
stack visibility.

There is a GUI configuration manager which makes it easy to select fonts,
colors, keybindings, and startup options.  This facility includes a feature
which allows the user to specify additional help sources, either locally or on
the web.

IDLE is coded in 100% pure Python, using the Tkinter GUI toolkit (Tk/Tcl)
and is cross-platform, working on Unix, Mac, and Windows.

IDLE accepts command line arguments.  Try idle -h to see the options.


If you find bugs or have suggestions, let us know about them by using the
Python Bug Tracker:

http://sourceforge.net/projects/python

Patches are always appreciated at the Python Patch Tracker, and change
requests should be posted to the RFE Tracker.

For further details and links, read the Help files and check the IDLE home
page at

http://www.python.org/idle/

There is a mail list for IDLE: idle-dev@python.org.  You can join at

http://mail.python.org/mailman/listinfo/idle-dev
