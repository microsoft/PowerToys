"""Spawn a command with pipes to its stdin, stdout, and optionally stderr.

The normal os.popen(cmd, mode) call spawns a shell command and provides a
file interface to just the input or output of the process depending on
whether mode is 'r' or 'w'.  This module provides the functions popen2(cmd)
and popen3(cmd) which return two or three pipes to the spawned command.
"""

import os
import sys
import warnings
warnings.warn("The popen2 module is deprecated.  Use the subprocess module.",
              DeprecationWarning, stacklevel=2)

__all__ = ["popen2", "popen3", "popen4"]

try:
    MAXFD = os.sysconf('SC_OPEN_MAX')
except (AttributeError, ValueError):
    MAXFD = 256

_active = []

def _cleanup():
    for inst in _active[:]:
        if inst.poll(_deadstate=sys.maxint) >= 0:
            try:
                _active.remove(inst)
            except ValueError:
                # This can happen if two threads create a new Popen instance.
                # It's harmless that it was already removed, so ignore.
                pass

class Popen3:
    """Class representing a child process.  Normally, instances are created
    internally by the functions popen2() and popen3()."""

    sts = -1                    # Child not completed yet

    def __init__(self, cmd, capturestderr=False, bufsize=-1):
        """The parameter 'cmd' is the shell command to execute in a
        sub-process.  On UNIX, 'cmd' may be a sequence, in which case arguments
        will be passed directly to the program without shell intervention (as
        with os.spawnv()).  If 'cmd' is a string it will be passed to the shell
        (as with os.system()).   The 'capturestderr' flag, if true, specifies
        that the object should capture standard error output of the child
        process.  The default is false.  If the 'bufsize' parameter is
        specified, it specifies the size of the I/O buffers to/from the child
        process."""
        _cleanup()
        self.cmd = cmd
        p2cread, p2cwrite = os.pipe()
        c2pread, c2pwrite = os.pipe()
        if capturestderr:
            errout, errin = os.pipe()
        self.pid = os.fork()
        if self.pid == 0:
            # Child
            os.dup2(p2cread, 0)
            os.dup2(c2pwrite, 1)
            if capturestderr:
                os.dup2(errin, 2)
            self._run_child(cmd)
        os.close(p2cread)
        self.tochild = os.fdopen(p2cwrite, 'w', bufsize)
        os.close(c2pwrite)
        self.fromchild = os.fdopen(c2pread, 'r', bufsize)
        if capturestderr:
            os.close(errin)
            self.childerr = os.fdopen(errout, 'r', bufsize)
        else:
            self.childerr = None

    def __del__(self):
        # In case the child hasn't been waited on, check if it's done.
        self.poll(_deadstate=sys.maxint)
        if self.sts < 0:
            if _active is not None:
                # Child is still running, keep us alive until we can wait on it.
                _active.append(self)

    def _run_child(self, cmd):
        if isinstance(cmd, basestring):
            cmd = ['/bin/sh', '-c', cmd]
        os.closerange(3, MAXFD)
        try:
            os.execvp(cmd[0], cmd)
        finally:
            os._exit(1)

    def poll(self, _deadstate=None):
        """Return the exit status of the child process if it has finished,
        or -1 if it hasn't finished yet."""
        if self.sts < 0:
            try:
                pid, sts = os.waitpid(self.pid, os.WNOHANG)
                # pid will be 0 if self.pid hasn't terminated
                if pid == self.pid:
                    self.sts = sts
            except os.error:
                if _deadstate is not None:
                    self.sts = _deadstate
        return self.sts

    def wait(self):
        """Wait for and return the exit status of the child process."""
        if self.sts < 0:
            pid, sts = os.waitpid(self.pid, 0)
            # This used to be a test, but it is believed to be
            # always true, so I changed it to an assertion - mvl
            assert pid == self.pid
            self.sts = sts
        return self.sts


class Popen4(Popen3):
    childerr = None

    def __init__(self, cmd, bufsize=-1):
        _cleanup()
        self.cmd = cmd
        p2cread, p2cwrite = os.pipe()
        c2pread, c2pwrite = os.pipe()
        self.pid = os.fork()
        if self.pid == 0:
            # Child
            os.dup2(p2cread, 0)
            os.dup2(c2pwrite, 1)
            os.dup2(c2pwrite, 2)
            self._run_child(cmd)
        os.close(p2cread)
        self.tochild = os.fdopen(p2cwrite, 'w', bufsize)
        os.close(c2pwrite)
        self.fromchild = os.fdopen(c2pread, 'r', bufsize)


if sys.platform[:3] == "win" or sys.platform == "os2emx":
    # Some things don't make sense on non-Unix platforms.
    del Popen3, Popen4

    def popen2(cmd, bufsize=-1, mode='t'):
        """Execute the shell command 'cmd' in a sub-process. On UNIX, 'cmd' may
        be a sequence, in which case arguments will be passed directly to the
        program without shell intervention (as with os.spawnv()). If 'cmd' is a
        string it will be passed to the shell (as with os.system()). If
        'bufsize' is specified, it sets the buffer size for the I/O pipes. The
        file objects (child_stdout, child_stdin) are returned."""
        w, r = os.popen2(cmd, mode, bufsize)
        return r, w

    def popen3(cmd, bufsize=-1, mode='t'):
        """Execute the shell command 'cmd' in a sub-process. On UNIX, 'cmd' may
        be a sequence, in which case arguments will be passed directly to the
        program without shell intervention (as with os.spawnv()). If 'cmd' is a
        string it will be passed to the shell (as with os.system()). If
        'bufsize' is specified, it sets the buffer size for the I/O pipes. The
        file objects (child_stdout, child_stdin, child_stderr) are returned."""
        w, r, e = os.popen3(cmd, mode, bufsize)
        return r, w, e

    def popen4(cmd, bufsize=-1, mode='t'):
        """Execute the shell command 'cmd' in a sub-process. On UNIX, 'cmd' may
        be a sequence, in which case arguments will be passed directly to the
        program without shell intervention (as with os.spawnv()). If 'cmd' is a
        string it will be passed to the shell (as with os.system()). If
        'bufsize' is specified, it sets the buffer size for the I/O pipes. The
        file objects (child_stdout_stderr, child_stdin) are returned."""
        w, r = os.popen4(cmd, mode, bufsize)
        return r, w
else:
    def popen2(cmd, bufsize=-1, mode='t'):
        """Execute the shell command 'cmd' in a sub-process. On UNIX, 'cmd' may
        be a sequence, in which case arguments will be passed directly to the
        program without shell intervention (as with os.spawnv()). If 'cmd' is a
        string it will be passed to the shell (as with os.system()). If
        'bufsize' is specified, it sets the buffer size for the I/O pipes. The
        file objects (child_stdout, child_stdin) are returned."""
        inst = Popen3(cmd, False, bufsize)
        return inst.fromchild, inst.tochild

    def popen3(cmd, bufsize=-1, mode='t'):
        """Execute the shell command 'cmd' in a sub-process. On UNIX, 'cmd' may
        be a sequence, in which case arguments will be passed directly to the
        program without shell intervention (as with os.spawnv()). If 'cmd' is a
        string it will be passed to the shell (as with os.system()). If
        'bufsize' is specified, it sets the buffer size for the I/O pipes. The
        file objects (child_stdout, child_stdin, child_stderr) are returned."""
        inst = Popen3(cmd, True, bufsize)
        return inst.fromchild, inst.tochild, inst.childerr

    def popen4(cmd, bufsize=-1, mode='t'):
        """Execute the shell command 'cmd' in a sub-process. On UNIX, 'cmd' may
        be a sequence, in which case arguments will be passed directly to the
        program without shell intervention (as with os.spawnv()). If 'cmd' is a
        string it will be passed to the shell (as with os.system()). If
        'bufsize' is specified, it sets the buffer size for the I/O pipes. The
        file objects (child_stdout_stderr, child_stdin) are returned."""
        inst = Popen4(cmd, bufsize)
        return inst.fromchild, inst.tochild

    __all__.extend(["Popen3", "Popen4"])
