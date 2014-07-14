import os.path
import sys

# If we are working on a development version of IDLE, we need to prepend the
# parent of this idlelib dir to sys.path.  Otherwise, importing idlelib gets
# the version installed with the Python used to call this module:
idlelib_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, idlelib_dir)

import idlelib.PyShell
idlelib.PyShell.main()
