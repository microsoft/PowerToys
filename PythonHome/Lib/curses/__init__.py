"""curses

The main package for curses support for Python.  Normally used by importing
the package, and perhaps a particular module inside it.

   import curses
   from curses import textpad
   curses.initscr()
   ...

"""

__revision__ = "$Id$"

from _curses import *
from curses.wrapper import wrapper
import os as _os
import sys as _sys

# Some constants, most notably the ACS_* ones, are only added to the C
# _curses module's dictionary after initscr() is called.  (Some
# versions of SGI's curses don't define values for those constants
# until initscr() has been called.)  This wrapper function calls the
# underlying C initscr(), and then copies the constants from the
# _curses module to the curses package's dictionary.  Don't do 'from
# curses import *' if you'll be needing the ACS_* constants.

def initscr():
    import _curses, curses
    # we call setupterm() here because it raises an error
    # instead of calling exit() in error cases.
    setupterm(term=_os.environ.get("TERM", "unknown"),
              fd=_sys.__stdout__.fileno())
    stdscr = _curses.initscr()
    for key, value in _curses.__dict__.items():
        if key[0:4] == 'ACS_' or key in ('LINES', 'COLS'):
            setattr(curses, key, value)

    return stdscr

# This is a similar wrapper for start_color(), which adds the COLORS and
# COLOR_PAIRS variables which are only available after start_color() is
# called.

def start_color():
    import _curses, curses
    retval = _curses.start_color()
    if hasattr(_curses, 'COLORS'):
        curses.COLORS = _curses.COLORS
    if hasattr(_curses, 'COLOR_PAIRS'):
        curses.COLOR_PAIRS = _curses.COLOR_PAIRS
    return retval

# Import Python has_key() implementation if _curses doesn't contain has_key()

try:
    has_key
except NameError:
    from has_key import has_key
