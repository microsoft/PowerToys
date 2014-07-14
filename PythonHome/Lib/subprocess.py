# subprocess - Subprocesses with accessible I/O streams
#
# For more information about this module, see PEP 324.
#
# Copyright (c) 2003-2005 by Peter Astrand <astrand@lysator.liu.se>
#
# Licensed to PSF under a Contributor Agreement.
# See http://www.python.org/2.4/license for licensing details.

r"""subprocess - Subprocesses with accessible I/O streams

This module allows you to spawn processes, connect to their
input/output/error pipes, and obtain their return codes.  This module
intends to replace several older modules and functions:

os.system
os.spawn*
os.popen*
popen2.*
commands.*

Information about how the subprocess module can be used to replace these
modules and functions can be found below.



Using the subprocess module
===========================
This module defines one class called Popen:

class Popen(args, bufsize=0, executable=None,
            stdin=None, stdout=None, stderr=None,
            preexec_fn=None, close_fds=False, shell=False,
            cwd=None, env=None, universal_newlines=False,
            startupinfo=None, creationflags=0):


Arguments are:

args should be a string, or a sequence of program arguments.  The
program to execute is normally the first item in the args sequence or
string, but can be explicitly set by using the executable argument.

On UNIX, with shell=False (default): In this case, the Popen class
uses os.execvp() to execute the child program.  args should normally
be a sequence.  A string will be treated as a sequence with the string
as the only item (the program to execute).

On UNIX, with shell=True: If args is a string, it specifies the
command string to execute through the shell.  If args is a sequence,
the first item specifies the command string, and any additional items
will be treated as additional shell arguments.

On Windows: the Popen class uses CreateProcess() to execute the child
program, which operates on strings.  If args is a sequence, it will be
converted to a string using the list2cmdline method.  Please note that
not all MS Windows applications interpret the command line the same
way: The list2cmdline is designed for applications using the same
rules as the MS C runtime.

bufsize, if given, has the same meaning as the corresponding argument
to the built-in open() function: 0 means unbuffered, 1 means line
buffered, any other positive value means use a buffer of
(approximately) that size.  A negative bufsize means to use the system
default, which usually means fully buffered.  The default value for
bufsize is 0 (unbuffered).

stdin, stdout and stderr specify the executed programs' standard
input, standard output and standard error file handles, respectively.
Valid values are PIPE, an existing file descriptor (a positive
integer), an existing file object, and None.  PIPE indicates that a
new pipe to the child should be created.  With None, no redirection
will occur; the child's file handles will be inherited from the
parent.  Additionally, stderr can be STDOUT, which indicates that the
stderr data from the applications should be captured into the same
file handle as for stdout.

If preexec_fn is set to a callable object, this object will be called
in the child process just before the child is executed.

If close_fds is true, all file descriptors except 0, 1 and 2 will be
closed before the child process is executed.

if shell is true, the specified command will be executed through the
shell.

If cwd is not None, the current directory will be changed to cwd
before the child is executed.

If env is not None, it defines the environment variables for the new
process.

If universal_newlines is true, the file objects stdout and stderr are
opened as a text files, but lines may be terminated by any of '\n',
the Unix end-of-line convention, '\r', the Macintosh convention or
'\r\n', the Windows convention.  All of these external representations
are seen as '\n' by the Python program.  Note: This feature is only
available if Python is built with universal newline support (the
default).  Also, the newlines attribute of the file objects stdout,
stdin and stderr are not updated by the communicate() method.

The startupinfo and creationflags, if given, will be passed to the
underlying CreateProcess() function.  They can specify things such as
appearance of the main window and priority for the new process.
(Windows only)


This module also defines some shortcut functions:

call(*popenargs, **kwargs):
    Run command with arguments.  Wait for command to complete, then
    return the returncode attribute.

    The arguments are the same as for the Popen constructor.  Example:

    retcode = call(["ls", "-l"])

check_call(*popenargs, **kwargs):
    Run command with arguments.  Wait for command to complete.  If the
    exit code was zero then return, otherwise raise
    CalledProcessError.  The CalledProcessError object will have the
    return code in the returncode attribute.

    The arguments are the same as for the Popen constructor.  Example:

    check_call(["ls", "-l"])

check_output(*popenargs, **kwargs):
    Run command with arguments and return its output as a byte string.

    If the exit code was non-zero it raises a CalledProcessError.  The
    CalledProcessError object will have the return code in the returncode
    attribute and output in the output attribute.

    The arguments are the same as for the Popen constructor.  Example:

    output = check_output(["ls", "-l", "/dev/null"])


Exceptions
----------
Exceptions raised in the child process, before the new program has
started to execute, will be re-raised in the parent.  Additionally,
the exception object will have one extra attribute called
'child_traceback', which is a string containing traceback information
from the child's point of view.

The most common exception raised is OSError.  This occurs, for
example, when trying to execute a non-existent file.  Applications
should prepare for OSErrors.

A ValueError will be raised if Popen is called with invalid arguments.

check_call() and check_output() will raise CalledProcessError, if the
called process returns a non-zero return code.


Security
--------
Unlike some other popen functions, this implementation will never call
/bin/sh implicitly.  This means that all characters, including shell
metacharacters, can safely be passed to child processes.


Popen objects
=============
Instances of the Popen class have the following methods:

poll()
    Check if child process has terminated.  Returns returncode
    attribute.

wait()
    Wait for child process to terminate.  Returns returncode attribute.

communicate(input=None)
    Interact with process: Send data to stdin.  Read data from stdout
    and stderr, until end-of-file is reached.  Wait for process to
    terminate.  The optional input argument should be a string to be
    sent to the child process, or None, if no data should be sent to
    the child.

    communicate() returns a tuple (stdout, stderr).

    Note: The data read is buffered in memory, so do not use this
    method if the data size is large or unlimited.

The following attributes are also available:

stdin
    If the stdin argument is PIPE, this attribute is a file object
    that provides input to the child process.  Otherwise, it is None.

stdout
    If the stdout argument is PIPE, this attribute is a file object
    that provides output from the child process.  Otherwise, it is
    None.

stderr
    If the stderr argument is PIPE, this attribute is file object that
    provides error output from the child process.  Otherwise, it is
    None.

pid
    The process ID of the child process.

returncode
    The child return code.  A None value indicates that the process
    hasn't terminated yet.  A negative value -N indicates that the
    child was terminated by signal N (UNIX only).


Replacing older functions with the subprocess module
====================================================
In this section, "a ==> b" means that b can be used as a replacement
for a.

Note: All functions in this section fail (more or less) silently if
the executed program cannot be found; this module raises an OSError
exception.

In the following examples, we assume that the subprocess module is
imported with "from subprocess import *".


Replacing /bin/sh shell backquote
---------------------------------
output=`mycmd myarg`
==>
output = Popen(["mycmd", "myarg"], stdout=PIPE).communicate()[0]


Replacing shell pipe line
-------------------------
output=`dmesg | grep hda`
==>
p1 = Popen(["dmesg"], stdout=PIPE)
p2 = Popen(["grep", "hda"], stdin=p1.stdout, stdout=PIPE)
output = p2.communicate()[0]


Replacing os.system()
---------------------
sts = os.system("mycmd" + " myarg")
==>
p = Popen("mycmd" + " myarg", shell=True)
pid, sts = os.waitpid(p.pid, 0)

Note:

* Calling the program through the shell is usually not required.

* It's easier to look at the returncode attribute than the
  exitstatus.

A more real-world example would look like this:

try:
    retcode = call("mycmd" + " myarg", shell=True)
    if retcode < 0:
        print >>sys.stderr, "Child was terminated by signal", -retcode
    else:
        print >>sys.stderr, "Child returned", retcode
except OSError, e:
    print >>sys.stderr, "Execution failed:", e


Replacing os.spawn*
-------------------
P_NOWAIT example:

pid = os.spawnlp(os.P_NOWAIT, "/bin/mycmd", "mycmd", "myarg")
==>
pid = Popen(["/bin/mycmd", "myarg"]).pid


P_WAIT example:

retcode = os.spawnlp(os.P_WAIT, "/bin/mycmd", "mycmd", "myarg")
==>
retcode = call(["/bin/mycmd", "myarg"])


Vector example:

os.spawnvp(os.P_NOWAIT, path, args)
==>
Popen([path] + args[1:])


Environment example:

os.spawnlpe(os.P_NOWAIT, "/bin/mycmd", "mycmd", "myarg", env)
==>
Popen(["/bin/mycmd", "myarg"], env={"PATH": "/usr/bin"})


Replacing os.popen*
-------------------
pipe = os.popen("cmd", mode='r', bufsize)
==>
pipe = Popen("cmd", shell=True, bufsize=bufsize, stdout=PIPE).stdout

pipe = os.popen("cmd", mode='w', bufsize)
==>
pipe = Popen("cmd", shell=True, bufsize=bufsize, stdin=PIPE).stdin


(child_stdin, child_stdout) = os.popen2("cmd", mode, bufsize)
==>
p = Popen("cmd", shell=True, bufsize=bufsize,
          stdin=PIPE, stdout=PIPE, close_fds=True)
(child_stdin, child_stdout) = (p.stdin, p.stdout)


(child_stdin,
 child_stdout,
 child_stderr) = os.popen3("cmd", mode, bufsize)
==>
p = Popen("cmd", shell=True, bufsize=bufsize,
          stdin=PIPE, stdout=PIPE, stderr=PIPE, close_fds=True)
(child_stdin,
 child_stdout,
 child_stderr) = (p.stdin, p.stdout, p.stderr)


(child_stdin, child_stdout_and_stderr) = os.popen4("cmd", mode,
                                                   bufsize)
==>
p = Popen("cmd", shell=True, bufsize=bufsize,
          stdin=PIPE, stdout=PIPE, stderr=STDOUT, close_fds=True)
(child_stdin, child_stdout_and_stderr) = (p.stdin, p.stdout)

On Unix, os.popen2, os.popen3 and os.popen4 also accept a sequence as
the command to execute, in which case arguments will be passed
directly to the program without shell intervention.  This usage can be
replaced as follows:

(child_stdin, child_stdout) = os.popen2(["/bin/ls", "-l"], mode,
                                        bufsize)
==>
p = Popen(["/bin/ls", "-l"], bufsize=bufsize, stdin=PIPE, stdout=PIPE)
(child_stdin, child_stdout) = (p.stdin, p.stdout)

Return code handling translates as follows:

pipe = os.popen("cmd", 'w')
...
rc = pipe.close()
if rc is not None and rc % 256:
    print "There were some errors"
==>
process = Popen("cmd", 'w', shell=True, stdin=PIPE)
...
process.stdin.close()
if process.wait() != 0:
    print "There were some errors"


Replacing popen2.*
------------------
(child_stdout, child_stdin) = popen2.popen2("somestring", bufsize, mode)
==>
p = Popen(["somestring"], shell=True, bufsize=bufsize
          stdin=PIPE, stdout=PIPE, close_fds=True)
(child_stdout, child_stdin) = (p.stdout, p.stdin)

On Unix, popen2 also accepts a sequence as the command to execute, in
which case arguments will be passed directly to the program without
shell intervention.  This usage can be replaced as follows:

(child_stdout, child_stdin) = popen2.popen2(["mycmd", "myarg"], bufsize,
                                            mode)
==>
p = Popen(["mycmd", "myarg"], bufsize=bufsize,
          stdin=PIPE, stdout=PIPE, close_fds=True)
(child_stdout, child_stdin) = (p.stdout, p.stdin)

The popen2.Popen3 and popen2.Popen4 basically works as subprocess.Popen,
except that:

* subprocess.Popen raises an exception if the execution fails
* the capturestderr argument is replaced with the stderr argument.
* stdin=PIPE and stdout=PIPE must be specified.
* popen2 closes all filedescriptors by default, but you have to specify
  close_fds=True with subprocess.Popen.
"""

import sys
mswindows = (sys.platform == "win32")

import os
import types
import traceback
import gc
import signal
import errno

# Exception classes used by this module.
class CalledProcessError(Exception):
    """This exception is raised when a process run by check_call() or
    check_output() returns a non-zero exit status.
    The exit status will be stored in the returncode attribute;
    check_output() will also store the output in the output attribute.
    """
    def __init__(self, returncode, cmd, output=None):
        self.returncode = returncode
        self.cmd = cmd
        self.output = output
    def __str__(self):
        return "Command '%s' returned non-zero exit status %d" % (self.cmd, self.returncode)


if mswindows:
    import threading
    import msvcrt
    import _subprocess
    class STARTUPINFO:
        dwFlags = 0
        hStdInput = None
        hStdOutput = None
        hStdError = None
        wShowWindow = 0
    class pywintypes:
        error = IOError
else:
    import select
    _has_poll = hasattr(select, 'poll')
    import fcntl
    import pickle

    # When select or poll has indicated that the file is writable,
    # we can write up to _PIPE_BUF bytes without risk of blocking.
    # POSIX defines PIPE_BUF as >= 512.
    _PIPE_BUF = getattr(select, 'PIPE_BUF', 512)


__all__ = ["Popen", "PIPE", "STDOUT", "call", "check_call",
           "check_output", "CalledProcessError"]

if mswindows:
    from _subprocess import (CREATE_NEW_CONSOLE, CREATE_NEW_PROCESS_GROUP,
                             STD_INPUT_HANDLE, STD_OUTPUT_HANDLE,
                             STD_ERROR_HANDLE, SW_HIDE,
                             STARTF_USESTDHANDLES, STARTF_USESHOWWINDOW)

    __all__.extend(["CREATE_NEW_CONSOLE", "CREATE_NEW_PROCESS_GROUP",
                    "STD_INPUT_HANDLE", "STD_OUTPUT_HANDLE",
                    "STD_ERROR_HANDLE", "SW_HIDE",
                    "STARTF_USESTDHANDLES", "STARTF_USESHOWWINDOW"])
try:
    MAXFD = os.sysconf("SC_OPEN_MAX")
except:
    MAXFD = 256

_active = []

def _cleanup():
    for inst in _active[:]:
        res = inst._internal_poll(_deadstate=sys.maxint)
        if res is not None:
            try:
                _active.remove(inst)
            except ValueError:
                # This can happen if two threads create a new Popen instance.
                # It's harmless that it was already removed, so ignore.
                pass

PIPE = -1
STDOUT = -2


def _eintr_retry_call(func, *args):
    while True:
        try:
            return func(*args)
        except (OSError, IOError) as e:
            if e.errno == errno.EINTR:
                continue
            raise


# XXX This function is only used by multiprocessing and the test suite,
# but it's here so that it can be imported when Python is compiled without
# threads.

def _args_from_interpreter_flags():
    """Return a list of command-line arguments reproducing the current
    settings in sys.flags and sys.warnoptions."""
    flag_opt_map = {
        'debug': 'd',
        # 'inspect': 'i',
        # 'interactive': 'i',
        'optimize': 'O',
        'dont_write_bytecode': 'B',
        'no_user_site': 's',
        'no_site': 'S',
        'ignore_environment': 'E',
        'verbose': 'v',
        'bytes_warning': 'b',
        'hash_randomization': 'R',
        'py3k_warning': '3',
    }
    args = []
    for flag, opt in flag_opt_map.items():
        v = getattr(sys.flags, flag)
        if v > 0:
            args.append('-' + opt * v)
    for opt in sys.warnoptions:
        args.append('-W' + opt)
    return args


def call(*popenargs, **kwargs):
    """Run command with arguments.  Wait for command to complete, then
    return the returncode attribute.

    The arguments are the same as for the Popen constructor.  Example:

    retcode = call(["ls", "-l"])
    """
    return Popen(*popenargs, **kwargs).wait()


def check_call(*popenargs, **kwargs):
    """Run command with arguments.  Wait for command to complete.  If
    the exit code was zero then return, otherwise raise
    CalledProcessError.  The CalledProcessError object will have the
    return code in the returncode attribute.

    The arguments are the same as for the Popen constructor.  Example:

    check_call(["ls", "-l"])
    """
    retcode = call(*popenargs, **kwargs)
    if retcode:
        cmd = kwargs.get("args")
        if cmd is None:
            cmd = popenargs[0]
        raise CalledProcessError(retcode, cmd)
    return 0


def check_output(*popenargs, **kwargs):
    r"""Run command with arguments and return its output as a byte string.

    If the exit code was non-zero it raises a CalledProcessError.  The
    CalledProcessError object will have the return code in the returncode
    attribute and output in the output attribute.

    The arguments are the same as for the Popen constructor.  Example:

    >>> check_output(["ls", "-l", "/dev/null"])
    'crw-rw-rw- 1 root root 1, 3 Oct 18  2007 /dev/null\n'

    The stdout argument is not allowed as it is used internally.
    To capture standard error in the result, use stderr=STDOUT.

    >>> check_output(["/bin/sh", "-c",
    ...               "ls -l non_existent_file ; exit 0"],
    ...              stderr=STDOUT)
    'ls: non_existent_file: No such file or directory\n'
    """
    if 'stdout' in kwargs:
        raise ValueError('stdout argument not allowed, it will be overridden.')
    process = Popen(stdout=PIPE, *popenargs, **kwargs)
    output, unused_err = process.communicate()
    retcode = process.poll()
    if retcode:
        cmd = kwargs.get("args")
        if cmd is None:
            cmd = popenargs[0]
        raise CalledProcessError(retcode, cmd, output=output)
    return output


def list2cmdline(seq):
    """
    Translate a sequence of arguments into a command line
    string, using the same rules as the MS C runtime:

    1) Arguments are delimited by white space, which is either a
       space or a tab.

    2) A string surrounded by double quotation marks is
       interpreted as a single argument, regardless of white space
       contained within.  A quoted string can be embedded in an
       argument.

    3) A double quotation mark preceded by a backslash is
       interpreted as a literal double quotation mark.

    4) Backslashes are interpreted literally, unless they
       immediately precede a double quotation mark.

    5) If backslashes immediately precede a double quotation mark,
       every pair of backslashes is interpreted as a literal
       backslash.  If the number of backslashes is odd, the last
       backslash escapes the next double quotation mark as
       described in rule 3.
    """

    # See
    # http://msdn.microsoft.com/en-us/library/17w5ykft.aspx
    # or search http://msdn.microsoft.com for
    # "Parsing C++ Command-Line Arguments"
    result = []
    needquote = False
    for arg in seq:
        bs_buf = []

        # Add a space to separate this argument from the others
        if result:
            result.append(' ')

        needquote = (" " in arg) or ("\t" in arg) or not arg
        if needquote:
            result.append('"')

        for c in arg:
            if c == '\\':
                # Don't know if we need to double yet.
                bs_buf.append(c)
            elif c == '"':
                # Double backslashes.
                result.append('\\' * len(bs_buf)*2)
                bs_buf = []
                result.append('\\"')
            else:
                # Normal char
                if bs_buf:
                    result.extend(bs_buf)
                    bs_buf = []
                result.append(c)

        # Add remaining backslashes, if any.
        if bs_buf:
            result.extend(bs_buf)

        if needquote:
            result.extend(bs_buf)
            result.append('"')

    return ''.join(result)


class Popen(object):
    _child_created = False  # Set here since __del__ checks it

    def __init__(self, args, bufsize=0, executable=None,
                 stdin=None, stdout=None, stderr=None,
                 preexec_fn=None, close_fds=False, shell=False,
                 cwd=None, env=None, universal_newlines=False,
                 startupinfo=None, creationflags=0):
        """Create new Popen instance."""
        _cleanup()

        if not isinstance(bufsize, (int, long)):
            raise TypeError("bufsize must be an integer")

        if mswindows:
            if preexec_fn is not None:
                raise ValueError("preexec_fn is not supported on Windows "
                                 "platforms")
            if close_fds and (stdin is not None or stdout is not None or
                              stderr is not None):
                raise ValueError("close_fds is not supported on Windows "
                                 "platforms if you redirect stdin/stdout/stderr")
        else:
            # POSIX
            if startupinfo is not None:
                raise ValueError("startupinfo is only supported on Windows "
                                 "platforms")
            if creationflags != 0:
                raise ValueError("creationflags is only supported on Windows "
                                 "platforms")

        self.stdin = None
        self.stdout = None
        self.stderr = None
        self.pid = None
        self.returncode = None
        self.universal_newlines = universal_newlines

        # Input and output objects. The general principle is like
        # this:
        #
        # Parent                   Child
        # ------                   -----
        # p2cwrite   ---stdin--->  p2cread
        # c2pread    <--stdout---  c2pwrite
        # errread    <--stderr---  errwrite
        #
        # On POSIX, the child objects are file descriptors.  On
        # Windows, these are Windows file handles.  The parent objects
        # are file descriptors on both platforms.  The parent objects
        # are None when not using PIPEs. The child objects are None
        # when not redirecting.

        (p2cread, p2cwrite,
         c2pread, c2pwrite,
         errread, errwrite), to_close = self._get_handles(stdin, stdout, stderr)

        try:
            self._execute_child(args, executable, preexec_fn, close_fds,
                                cwd, env, universal_newlines,
                                startupinfo, creationflags, shell, to_close,
                                p2cread, p2cwrite,
                                c2pread, c2pwrite,
                                errread, errwrite)
        except Exception:
            # Preserve original exception in case os.close raises.
            exc_type, exc_value, exc_trace = sys.exc_info()

            for fd in to_close:
                try:
                    if mswindows:
                        fd.Close()
                    else:
                        os.close(fd)
                except EnvironmentError:
                    pass

            raise exc_type, exc_value, exc_trace

        if mswindows:
            if p2cwrite is not None:
                p2cwrite = msvcrt.open_osfhandle(p2cwrite.Detach(), 0)
            if c2pread is not None:
                c2pread = msvcrt.open_osfhandle(c2pread.Detach(), 0)
            if errread is not None:
                errread = msvcrt.open_osfhandle(errread.Detach(), 0)

        if p2cwrite is not None:
            self.stdin = os.fdopen(p2cwrite, 'wb', bufsize)
        if c2pread is not None:
            if universal_newlines:
                self.stdout = os.fdopen(c2pread, 'rU', bufsize)
            else:
                self.stdout = os.fdopen(c2pread, 'rb', bufsize)
        if errread is not None:
            if universal_newlines:
                self.stderr = os.fdopen(errread, 'rU', bufsize)
            else:
                self.stderr = os.fdopen(errread, 'rb', bufsize)


    def _translate_newlines(self, data):
        data = data.replace("\r\n", "\n")
        data = data.replace("\r", "\n")
        return data


    def __del__(self, _maxint=sys.maxint):
        # If __init__ hasn't had a chance to execute (e.g. if it
        # was passed an undeclared keyword argument), we don't
        # have a _child_created attribute at all.
        if not self._child_created:
            # We didn't get to successfully create a child process.
            return
        # In case the child hasn't been waited on, check if it's done.
        self._internal_poll(_deadstate=_maxint)
        if self.returncode is None and _active is not None:
            # Child is still running, keep us alive until we can wait on it.
            _active.append(self)


    def communicate(self, input=None):
        """Interact with process: Send data to stdin.  Read data from
        stdout and stderr, until end-of-file is reached.  Wait for
        process to terminate.  The optional input argument should be a
        string to be sent to the child process, or None, if no data
        should be sent to the child.

        communicate() returns a tuple (stdout, stderr)."""

        # Optimization: If we are only using one pipe, or no pipe at
        # all, using select() or threads is unnecessary.
        if [self.stdin, self.stdout, self.stderr].count(None) >= 2:
            stdout = None
            stderr = None
            if self.stdin:
                if input:
                    try:
                        self.stdin.write(input)
                    except IOError as e:
                        if e.errno != errno.EPIPE and e.errno != errno.EINVAL:
                            raise
                self.stdin.close()
            elif self.stdout:
                stdout = _eintr_retry_call(self.stdout.read)
                self.stdout.close()
            elif self.stderr:
                stderr = _eintr_retry_call(self.stderr.read)
                self.stderr.close()
            self.wait()
            return (stdout, stderr)

        return self._communicate(input)


    def poll(self):
        return self._internal_poll()


    if mswindows:
        #
        # Windows methods
        #
        def _get_handles(self, stdin, stdout, stderr):
            """Construct and return tuple with IO objects:
            p2cread, p2cwrite, c2pread, c2pwrite, errread, errwrite
            """
            to_close = set()
            if stdin is None and stdout is None and stderr is None:
                return (None, None, None, None, None, None), to_close

            p2cread, p2cwrite = None, None
            c2pread, c2pwrite = None, None
            errread, errwrite = None, None

            if stdin is None:
                p2cread = _subprocess.GetStdHandle(_subprocess.STD_INPUT_HANDLE)
                if p2cread is None:
                    p2cread, _ = _subprocess.CreatePipe(None, 0)
            elif stdin == PIPE:
                p2cread, p2cwrite = _subprocess.CreatePipe(None, 0)
            elif isinstance(stdin, int):
                p2cread = msvcrt.get_osfhandle(stdin)
            else:
                # Assuming file-like object
                p2cread = msvcrt.get_osfhandle(stdin.fileno())
            p2cread = self._make_inheritable(p2cread)
            # We just duplicated the handle, it has to be closed at the end
            to_close.add(p2cread)
            if stdin == PIPE:
                to_close.add(p2cwrite)

            if stdout is None:
                c2pwrite = _subprocess.GetStdHandle(_subprocess.STD_OUTPUT_HANDLE)
                if c2pwrite is None:
                    _, c2pwrite = _subprocess.CreatePipe(None, 0)
            elif stdout == PIPE:
                c2pread, c2pwrite = _subprocess.CreatePipe(None, 0)
            elif isinstance(stdout, int):
                c2pwrite = msvcrt.get_osfhandle(stdout)
            else:
                # Assuming file-like object
                c2pwrite = msvcrt.get_osfhandle(stdout.fileno())
            c2pwrite = self._make_inheritable(c2pwrite)
            # We just duplicated the handle, it has to be closed at the end
            to_close.add(c2pwrite)
            if stdout == PIPE:
                to_close.add(c2pread)

            if stderr is None:
                errwrite = _subprocess.GetStdHandle(_subprocess.STD_ERROR_HANDLE)
                if errwrite is None:
                    _, errwrite = _subprocess.CreatePipe(None, 0)
            elif stderr == PIPE:
                errread, errwrite = _subprocess.CreatePipe(None, 0)
            elif stderr == STDOUT:
                errwrite = c2pwrite
            elif isinstance(stderr, int):
                errwrite = msvcrt.get_osfhandle(stderr)
            else:
                # Assuming file-like object
                errwrite = msvcrt.get_osfhandle(stderr.fileno())
            errwrite = self._make_inheritable(errwrite)
            # We just duplicated the handle, it has to be closed at the end
            to_close.add(errwrite)
            if stderr == PIPE:
                to_close.add(errread)

            return (p2cread, p2cwrite,
                    c2pread, c2pwrite,
                    errread, errwrite), to_close


        def _make_inheritable(self, handle):
            """Return a duplicate of handle, which is inheritable"""
            return _subprocess.DuplicateHandle(_subprocess.GetCurrentProcess(),
                                handle, _subprocess.GetCurrentProcess(), 0, 1,
                                _subprocess.DUPLICATE_SAME_ACCESS)


        def _find_w9xpopen(self):
            """Find and return absolut path to w9xpopen.exe"""
            w9xpopen = os.path.join(
                            os.path.dirname(_subprocess.GetModuleFileName(0)),
                                    "w9xpopen.exe")
            if not os.path.exists(w9xpopen):
                # Eeek - file-not-found - possibly an embedding
                # situation - see if we can locate it in sys.exec_prefix
                w9xpopen = os.path.join(os.path.dirname(sys.exec_prefix),
                                        "w9xpopen.exe")
                if not os.path.exists(w9xpopen):
                    raise RuntimeError("Cannot locate w9xpopen.exe, which is "
                                       "needed for Popen to work with your "
                                       "shell or platform.")
            return w9xpopen


        def _execute_child(self, args, executable, preexec_fn, close_fds,
                           cwd, env, universal_newlines,
                           startupinfo, creationflags, shell, to_close,
                           p2cread, p2cwrite,
                           c2pread, c2pwrite,
                           errread, errwrite):
            """Execute program (MS Windows version)"""

            if not isinstance(args, types.StringTypes):
                args = list2cmdline(args)

            # Process startup details
            if startupinfo is None:
                startupinfo = STARTUPINFO()
            if None not in (p2cread, c2pwrite, errwrite):
                startupinfo.dwFlags |= _subprocess.STARTF_USESTDHANDLES
                startupinfo.hStdInput = p2cread
                startupinfo.hStdOutput = c2pwrite
                startupinfo.hStdError = errwrite

            if shell:
                startupinfo.dwFlags |= _subprocess.STARTF_USESHOWWINDOW
                startupinfo.wShowWindow = _subprocess.SW_HIDE
                comspec = os.environ.get("COMSPEC", "cmd.exe")
                args = '{} /c "{}"'.format (comspec, args)
                if (_subprocess.GetVersion() >= 0x80000000 or
                        os.path.basename(comspec).lower() == "command.com"):
                    # Win9x, or using command.com on NT. We need to
                    # use the w9xpopen intermediate program. For more
                    # information, see KB Q150956
                    # (http://web.archive.org/web/20011105084002/http://support.microsoft.com/support/kb/articles/Q150/9/56.asp)
                    w9xpopen = self._find_w9xpopen()
                    args = '"%s" %s' % (w9xpopen, args)
                    # Not passing CREATE_NEW_CONSOLE has been known to
                    # cause random failures on win9x.  Specifically a
                    # dialog: "Your program accessed mem currently in
                    # use at xxx" and a hopeful warning about the
                    # stability of your system.  Cost is Ctrl+C wont
                    # kill children.
                    creationflags |= _subprocess.CREATE_NEW_CONSOLE

            def _close_in_parent(fd):
                fd.Close()
                to_close.remove(fd)

            # Start the process
            try:
                hp, ht, pid, tid = _subprocess.CreateProcess(executable, args,
                                         # no special security
                                         None, None,
                                         int(not close_fds),
                                         creationflags,
                                         env,
                                         cwd,
                                         startupinfo)
            except pywintypes.error, e:
                # Translate pywintypes.error to WindowsError, which is
                # a subclass of OSError.  FIXME: We should really
                # translate errno using _sys_errlist (or similar), but
                # how can this be done from Python?
                raise WindowsError(*e.args)
            finally:
                # Child is launched. Close the parent's copy of those pipe
                # handles that only the child should have open.  You need
                # to make sure that no handles to the write end of the
                # output pipe are maintained in this process or else the
                # pipe will not close when the child process exits and the
                # ReadFile will hang.
                if p2cread is not None:
                    _close_in_parent(p2cread)
                if c2pwrite is not None:
                    _close_in_parent(c2pwrite)
                if errwrite is not None:
                    _close_in_parent(errwrite)

            # Retain the process handle, but close the thread handle
            self._child_created = True
            self._handle = hp
            self.pid = pid
            ht.Close()

        def _internal_poll(self, _deadstate=None,
                _WaitForSingleObject=_subprocess.WaitForSingleObject,
                _WAIT_OBJECT_0=_subprocess.WAIT_OBJECT_0,
                _GetExitCodeProcess=_subprocess.GetExitCodeProcess):
            """Check if child process has terminated.  Returns returncode
            attribute.

            This method is called by __del__, so it can only refer to objects
            in its local scope.

            """
            if self.returncode is None:
                if _WaitForSingleObject(self._handle, 0) == _WAIT_OBJECT_0:
                    self.returncode = _GetExitCodeProcess(self._handle)
            return self.returncode


        def wait(self):
            """Wait for child process to terminate.  Returns returncode
            attribute."""
            if self.returncode is None:
                _subprocess.WaitForSingleObject(self._handle,
                                                _subprocess.INFINITE)
                self.returncode = _subprocess.GetExitCodeProcess(self._handle)
            return self.returncode


        def _readerthread(self, fh, buffer):
            buffer.append(fh.read())


        def _communicate(self, input):
            stdout = None # Return
            stderr = None # Return

            if self.stdout:
                stdout = []
                stdout_thread = threading.Thread(target=self._readerthread,
                                                 args=(self.stdout, stdout))
                stdout_thread.setDaemon(True)
                stdout_thread.start()
            if self.stderr:
                stderr = []
                stderr_thread = threading.Thread(target=self._readerthread,
                                                 args=(self.stderr, stderr))
                stderr_thread.setDaemon(True)
                stderr_thread.start()

            if self.stdin:
                if input is not None:
                    try:
                        self.stdin.write(input)
                    except IOError as e:
                        if e.errno != errno.EPIPE:
                            raise
                self.stdin.close()

            if self.stdout:
                stdout_thread.join()
            if self.stderr:
                stderr_thread.join()

            # All data exchanged.  Translate lists into strings.
            if stdout is not None:
                stdout = stdout[0]
            if stderr is not None:
                stderr = stderr[0]

            # Translate newlines, if requested.  We cannot let the file
            # object do the translation: It is based on stdio, which is
            # impossible to combine with select (unless forcing no
            # buffering).
            if self.universal_newlines and hasattr(file, 'newlines'):
                if stdout:
                    stdout = self._translate_newlines(stdout)
                if stderr:
                    stderr = self._translate_newlines(stderr)

            self.wait()
            return (stdout, stderr)

        def send_signal(self, sig):
            """Send a signal to the process
            """
            if sig == signal.SIGTERM:
                self.terminate()
            elif sig == signal.CTRL_C_EVENT:
                os.kill(self.pid, signal.CTRL_C_EVENT)
            elif sig == signal.CTRL_BREAK_EVENT:
                os.kill(self.pid, signal.CTRL_BREAK_EVENT)
            else:
                raise ValueError("Unsupported signal: {}".format(sig))

        def terminate(self):
            """Terminates the process
            """
            try:
                _subprocess.TerminateProcess(self._handle, 1)
            except OSError as e:
                # ERROR_ACCESS_DENIED (winerror 5) is received when the
                # process already died.
                if e.winerror != 5:
                    raise
                rc = _subprocess.GetExitCodeProcess(self._handle)
                if rc == _subprocess.STILL_ACTIVE:
                    raise
                self.returncode = rc

        kill = terminate

    else:
        #
        # POSIX methods
        #
        def _get_handles(self, stdin, stdout, stderr):
            """Construct and return tuple with IO objects:
            p2cread, p2cwrite, c2pread, c2pwrite, errread, errwrite
            """
            to_close = set()
            p2cread, p2cwrite = None, None
            c2pread, c2pwrite = None, None
            errread, errwrite = None, None

            if stdin is None:
                pass
            elif stdin == PIPE:
                p2cread, p2cwrite = self.pipe_cloexec()
                to_close.update((p2cread, p2cwrite))
            elif isinstance(stdin, int):
                p2cread = stdin
            else:
                # Assuming file-like object
                p2cread = stdin.fileno()

            if stdout is None:
                pass
            elif stdout == PIPE:
                c2pread, c2pwrite = self.pipe_cloexec()
                to_close.update((c2pread, c2pwrite))
            elif isinstance(stdout, int):
                c2pwrite = stdout
            else:
                # Assuming file-like object
                c2pwrite = stdout.fileno()

            if stderr is None:
                pass
            elif stderr == PIPE:
                errread, errwrite = self.pipe_cloexec()
                to_close.update((errread, errwrite))
            elif stderr == STDOUT:
                errwrite = c2pwrite
            elif isinstance(stderr, int):
                errwrite = stderr
            else:
                # Assuming file-like object
                errwrite = stderr.fileno()

            return (p2cread, p2cwrite,
                    c2pread, c2pwrite,
                    errread, errwrite), to_close


        def _set_cloexec_flag(self, fd, cloexec=True):
            try:
                cloexec_flag = fcntl.FD_CLOEXEC
            except AttributeError:
                cloexec_flag = 1

            old = fcntl.fcntl(fd, fcntl.F_GETFD)
            if cloexec:
                fcntl.fcntl(fd, fcntl.F_SETFD, old | cloexec_flag)
            else:
                fcntl.fcntl(fd, fcntl.F_SETFD, old & ~cloexec_flag)


        def pipe_cloexec(self):
            """Create a pipe with FDs set CLOEXEC."""
            # Pipes' FDs are set CLOEXEC by default because we don't want them
            # to be inherited by other subprocesses: the CLOEXEC flag is removed
            # from the child's FDs by _dup2(), between fork() and exec().
            # This is not atomic: we would need the pipe2() syscall for that.
            r, w = os.pipe()
            self._set_cloexec_flag(r)
            self._set_cloexec_flag(w)
            return r, w


        def _close_fds(self, but):
            if hasattr(os, 'closerange'):
                os.closerange(3, but)
                os.closerange(but + 1, MAXFD)
            else:
                for i in xrange(3, MAXFD):
                    if i == but:
                        continue
                    try:
                        os.close(i)
                    except:
                        pass


        def _execute_child(self, args, executable, preexec_fn, close_fds,
                           cwd, env, universal_newlines,
                           startupinfo, creationflags, shell, to_close,
                           p2cread, p2cwrite,
                           c2pread, c2pwrite,
                           errread, errwrite):
            """Execute program (POSIX version)"""

            if isinstance(args, types.StringTypes):
                args = [args]
            else:
                args = list(args)

            if shell:
                args = ["/bin/sh", "-c"] + args
                if executable:
                    args[0] = executable

            if executable is None:
                executable = args[0]

            def _close_in_parent(fd):
                os.close(fd)
                to_close.remove(fd)

            # For transferring possible exec failure from child to parent
            # The first char specifies the exception type: 0 means
            # OSError, 1 means some other error.
            errpipe_read, errpipe_write = self.pipe_cloexec()
            try:
                try:
                    gc_was_enabled = gc.isenabled()
                    # Disable gc to avoid bug where gc -> file_dealloc ->
                    # write to stderr -> hang.  http://bugs.python.org/issue1336
                    gc.disable()
                    try:
                        self.pid = os.fork()
                    except:
                        if gc_was_enabled:
                            gc.enable()
                        raise
                    self._child_created = True
                    if self.pid == 0:
                        # Child
                        try:
                            # Close parent's pipe ends
                            if p2cwrite is not None:
                                os.close(p2cwrite)
                            if c2pread is not None:
                                os.close(c2pread)
                            if errread is not None:
                                os.close(errread)
                            os.close(errpipe_read)

                            # When duping fds, if there arises a situation
                            # where one of the fds is either 0, 1 or 2, it
                            # is possible that it is overwritten (#12607).
                            if c2pwrite == 0:
                                c2pwrite = os.dup(c2pwrite)
                            if errwrite == 0 or errwrite == 1:
                                errwrite = os.dup(errwrite)

                            # Dup fds for child
                            def _dup2(a, b):
                                # dup2() removes the CLOEXEC flag but
                                # we must do it ourselves if dup2()
                                # would be a no-op (issue #10806).
                                if a == b:
                                    self._set_cloexec_flag(a, False)
                                elif a is not None:
                                    os.dup2(a, b)
                            _dup2(p2cread, 0)
                            _dup2(c2pwrite, 1)
                            _dup2(errwrite, 2)

                            # Close pipe fds.  Make sure we don't close the
                            # same fd more than once, or standard fds.
                            closed = { None }
                            for fd in [p2cread, c2pwrite, errwrite]:
                                if fd not in closed and fd > 2:
                                    os.close(fd)
                                    closed.add(fd)

                            if cwd is not None:
                                os.chdir(cwd)

                            if preexec_fn:
                                preexec_fn()

                            # Close all other fds, if asked for - after
                            # preexec_fn(), which may open FDs.
                            if close_fds:
                                self._close_fds(but=errpipe_write)

                            if env is None:
                                os.execvp(executable, args)
                            else:
                                os.execvpe(executable, args, env)

                        except:
                            exc_type, exc_value, tb = sys.exc_info()
                            # Save the traceback and attach it to the exception object
                            exc_lines = traceback.format_exception(exc_type,
                                                                   exc_value,
                                                                   tb)
                            exc_value.child_traceback = ''.join(exc_lines)
                            os.write(errpipe_write, pickle.dumps(exc_value))

                        # This exitcode won't be reported to applications, so it
                        # really doesn't matter what we return.
                        os._exit(255)

                    # Parent
                    if gc_was_enabled:
                        gc.enable()
                finally:
                    # be sure the FD is closed no matter what
                    os.close(errpipe_write)

                # Wait for exec to fail or succeed; possibly raising exception
                # Exception limited to 1M
                data = _eintr_retry_call(os.read, errpipe_read, 1048576)
            finally:
                if p2cread is not None and p2cwrite is not None:
                    _close_in_parent(p2cread)
                if c2pwrite is not None and c2pread is not None:
                    _close_in_parent(c2pwrite)
                if errwrite is not None and errread is not None:
                    _close_in_parent(errwrite)

                # be sure the FD is closed no matter what
                os.close(errpipe_read)

            if data != "":
                try:
                    _eintr_retry_call(os.waitpid, self.pid, 0)
                except OSError as e:
                    if e.errno != errno.ECHILD:
                        raise
                child_exception = pickle.loads(data)
                raise child_exception


        def _handle_exitstatus(self, sts, _WIFSIGNALED=os.WIFSIGNALED,
                _WTERMSIG=os.WTERMSIG, _WIFEXITED=os.WIFEXITED,
                _WEXITSTATUS=os.WEXITSTATUS):
            # This method is called (indirectly) by __del__, so it cannot
            # refer to anything outside of its local scope.
            if _WIFSIGNALED(sts):
                self.returncode = -_WTERMSIG(sts)
            elif _WIFEXITED(sts):
                self.returncode = _WEXITSTATUS(sts)
            else:
                # Should never happen
                raise RuntimeError("Unknown child exit status!")


        def _internal_poll(self, _deadstate=None, _waitpid=os.waitpid,
                _WNOHANG=os.WNOHANG, _os_error=os.error, _ECHILD=errno.ECHILD):
            """Check if child process has terminated.  Returns returncode
            attribute.

            This method is called by __del__, so it cannot reference anything
            outside of the local scope (nor can any methods it calls).

            """
            if self.returncode is None:
                try:
                    pid, sts = _waitpid(self.pid, _WNOHANG)
                    if pid == self.pid:
                        self._handle_exitstatus(sts)
                except _os_error as e:
                    if _deadstate is not None:
                        self.returncode = _deadstate
                    if e.errno == _ECHILD:
                        # This happens if SIGCLD is set to be ignored or
                        # waiting for child processes has otherwise been
                        # disabled for our process.  This child is dead, we
                        # can't get the status.
                        # http://bugs.python.org/issue15756
                        self.returncode = 0
            return self.returncode


        def wait(self):
            """Wait for child process to terminate.  Returns returncode
            attribute."""
            while self.returncode is None:
                try:
                    pid, sts = _eintr_retry_call(os.waitpid, self.pid, 0)
                except OSError as e:
                    if e.errno != errno.ECHILD:
                        raise
                    # This happens if SIGCLD is set to be ignored or waiting
                    # for child processes has otherwise been disabled for our
                    # process.  This child is dead, we can't get the status.
                    pid = self.pid
                    sts = 0
                # Check the pid and loop as waitpid has been known to return
                # 0 even without WNOHANG in odd situations.  issue14396.
                if pid == self.pid:
                    self._handle_exitstatus(sts)
            return self.returncode


        def _communicate(self, input):
            if self.stdin:
                # Flush stdio buffer.  This might block, if the user has
                # been writing to .stdin in an uncontrolled fashion.
                self.stdin.flush()
                if not input:
                    self.stdin.close()

            if _has_poll:
                stdout, stderr = self._communicate_with_poll(input)
            else:
                stdout, stderr = self._communicate_with_select(input)

            # All data exchanged.  Translate lists into strings.
            if stdout is not None:
                stdout = ''.join(stdout)
            if stderr is not None:
                stderr = ''.join(stderr)

            # Translate newlines, if requested.  We cannot let the file
            # object do the translation: It is based on stdio, which is
            # impossible to combine with select (unless forcing no
            # buffering).
            if self.universal_newlines and hasattr(file, 'newlines'):
                if stdout:
                    stdout = self._translate_newlines(stdout)
                if stderr:
                    stderr = self._translate_newlines(stderr)

            self.wait()
            return (stdout, stderr)


        def _communicate_with_poll(self, input):
            stdout = None # Return
            stderr = None # Return
            fd2file = {}
            fd2output = {}

            poller = select.poll()
            def register_and_append(file_obj, eventmask):
                poller.register(file_obj.fileno(), eventmask)
                fd2file[file_obj.fileno()] = file_obj

            def close_unregister_and_remove(fd):
                poller.unregister(fd)
                fd2file[fd].close()
                fd2file.pop(fd)

            if self.stdin and input:
                register_and_append(self.stdin, select.POLLOUT)

            select_POLLIN_POLLPRI = select.POLLIN | select.POLLPRI
            if self.stdout:
                register_and_append(self.stdout, select_POLLIN_POLLPRI)
                fd2output[self.stdout.fileno()] = stdout = []
            if self.stderr:
                register_and_append(self.stderr, select_POLLIN_POLLPRI)
                fd2output[self.stderr.fileno()] = stderr = []

            input_offset = 0
            while fd2file:
                try:
                    ready = poller.poll()
                except select.error, e:
                    if e.args[0] == errno.EINTR:
                        continue
                    raise

                for fd, mode in ready:
                    if mode & select.POLLOUT:
                        chunk = input[input_offset : input_offset + _PIPE_BUF]
                        try:
                            input_offset += os.write(fd, chunk)
                        except OSError as e:
                            if e.errno == errno.EPIPE:
                                close_unregister_and_remove(fd)
                            else:
                                raise
                        else:
                            if input_offset >= len(input):
                                close_unregister_and_remove(fd)
                    elif mode & select_POLLIN_POLLPRI:
                        data = os.read(fd, 4096)
                        if not data:
                            close_unregister_and_remove(fd)
                        fd2output[fd].append(data)
                    else:
                        # Ignore hang up or errors.
                        close_unregister_and_remove(fd)

            return (stdout, stderr)


        def _communicate_with_select(self, input):
            read_set = []
            write_set = []
            stdout = None # Return
            stderr = None # Return

            if self.stdin and input:
                write_set.append(self.stdin)
            if self.stdout:
                read_set.append(self.stdout)
                stdout = []
            if self.stderr:
                read_set.append(self.stderr)
                stderr = []

            input_offset = 0
            while read_set or write_set:
                try:
                    rlist, wlist, xlist = select.select(read_set, write_set, [])
                except select.error, e:
                    if e.args[0] == errno.EINTR:
                        continue
                    raise

                if self.stdin in wlist:
                    chunk = input[input_offset : input_offset + _PIPE_BUF]
                    try:
                        bytes_written = os.write(self.stdin.fileno(), chunk)
                    except OSError as e:
                        if e.errno == errno.EPIPE:
                            self.stdin.close()
                            write_set.remove(self.stdin)
                        else:
                            raise
                    else:
                        input_offset += bytes_written
                        if input_offset >= len(input):
                            self.stdin.close()
                            write_set.remove(self.stdin)

                if self.stdout in rlist:
                    data = os.read(self.stdout.fileno(), 1024)
                    if data == "":
                        self.stdout.close()
                        read_set.remove(self.stdout)
                    stdout.append(data)

                if self.stderr in rlist:
                    data = os.read(self.stderr.fileno(), 1024)
                    if data == "":
                        self.stderr.close()
                        read_set.remove(self.stderr)
                    stderr.append(data)

            return (stdout, stderr)


        def send_signal(self, sig):
            """Send a signal to the process
            """
            os.kill(self.pid, sig)

        def terminate(self):
            """Terminate the process with SIGTERM
            """
            self.send_signal(signal.SIGTERM)

        def kill(self):
            """Kill the process with SIGKILL
            """
            self.send_signal(signal.SIGKILL)


def _demo_posix():
    #
    # Example 1: Simple redirection: Get process list
    #
    plist = Popen(["ps"], stdout=PIPE).communicate()[0]
    print "Process list:"
    print plist

    #
    # Example 2: Change uid before executing child
    #
    if os.getuid() == 0:
        p = Popen(["id"], preexec_fn=lambda: os.setuid(100))
        p.wait()

    #
    # Example 3: Connecting several subprocesses
    #
    print "Looking for 'hda'..."
    p1 = Popen(["dmesg"], stdout=PIPE)
    p2 = Popen(["grep", "hda"], stdin=p1.stdout, stdout=PIPE)
    print repr(p2.communicate()[0])

    #
    # Example 4: Catch execution error
    #
    print
    print "Trying a weird file..."
    try:
        print Popen(["/this/path/does/not/exist"]).communicate()
    except OSError, e:
        if e.errno == errno.ENOENT:
            print "The file didn't exist.  I thought so..."
            print "Child traceback:"
            print e.child_traceback
        else:
            print "Error", e.errno
    else:
        print >>sys.stderr, "Gosh.  No error."


def _demo_windows():
    #
    # Example 1: Connecting several subprocesses
    #
    print "Looking for 'PROMPT' in set output..."
    p1 = Popen("set", stdout=PIPE, shell=True)
    p2 = Popen('find "PROMPT"', stdin=p1.stdout, stdout=PIPE)
    print repr(p2.communicate()[0])

    #
    # Example 2: Simple execution of program
    #
    print "Executing calc..."
    p = Popen("calc")
    p.wait()


if __name__ == "__main__":
    if mswindows:
        _demo_windows()
    else:
        _demo_posix()
