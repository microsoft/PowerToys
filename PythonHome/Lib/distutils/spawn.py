"""distutils.spawn

Provides the 'spawn()' function, a front-end to various platform-
specific functions for launching another program in a sub-process.
Also provides the 'find_executable()' to search the path for a given
executable name.
"""

__revision__ = "$Id$"

import sys
import os

from distutils.errors import DistutilsPlatformError, DistutilsExecError
from distutils.debug import DEBUG
from distutils import log

def spawn(cmd, search_path=1, verbose=0, dry_run=0):
    """Run another program, specified as a command list 'cmd', in a new process.

    'cmd' is just the argument list for the new process, ie.
    cmd[0] is the program to run and cmd[1:] are the rest of its arguments.
    There is no way to run a program with a name different from that of its
    executable.

    If 'search_path' is true (the default), the system's executable
    search path will be used to find the program; otherwise, cmd[0]
    must be the exact path to the executable.  If 'dry_run' is true,
    the command will not actually be run.

    Raise DistutilsExecError if running the program fails in any way; just
    return on success.
    """
    # cmd is documented as a list, but just in case some code passes a tuple
    # in, protect our %-formatting code against horrible death
    cmd = list(cmd)
    if os.name == 'posix':
        _spawn_posix(cmd, search_path, dry_run=dry_run)
    elif os.name == 'nt':
        _spawn_nt(cmd, search_path, dry_run=dry_run)
    elif os.name == 'os2':
        _spawn_os2(cmd, search_path, dry_run=dry_run)
    else:
        raise DistutilsPlatformError, \
              "don't know how to spawn programs on platform '%s'" % os.name

def _nt_quote_args(args):
    """Quote command-line arguments for DOS/Windows conventions.

    Just wraps every argument which contains blanks in double quotes, and
    returns a new argument list.
    """
    # XXX this doesn't seem very robust to me -- but if the Windows guys
    # say it'll work, I guess I'll have to accept it.  (What if an arg
    # contains quotes?  What other magic characters, other than spaces,
    # have to be escaped?  Is there an escaping mechanism other than
    # quoting?)
    for i, arg in enumerate(args):
        if ' ' in arg:
            args[i] = '"%s"' % arg
    return args

def _spawn_nt(cmd, search_path=1, verbose=0, dry_run=0):
    executable = cmd[0]
    cmd = _nt_quote_args(cmd)
    if search_path:
        # either we find one or it stays the same
        executable = find_executable(executable) or executable
    log.info(' '.join([executable] + cmd[1:]))
    if not dry_run:
        # spawn for NT requires a full path to the .exe
        try:
            rc = os.spawnv(os.P_WAIT, executable, cmd)
        except OSError, exc:
            # this seems to happen when the command isn't found
            if not DEBUG:
                cmd = executable
            raise DistutilsExecError, \
                  "command %r failed: %s" % (cmd, exc[-1])
        if rc != 0:
            # and this reflects the command running but failing
            if not DEBUG:
                cmd = executable
            raise DistutilsExecError, \
                  "command %r failed with exit status %d" % (cmd, rc)

def _spawn_os2(cmd, search_path=1, verbose=0, dry_run=0):
    executable = cmd[0]
    if search_path:
        # either we find one or it stays the same
        executable = find_executable(executable) or executable
    log.info(' '.join([executable] + cmd[1:]))
    if not dry_run:
        # spawnv for OS/2 EMX requires a full path to the .exe
        try:
            rc = os.spawnv(os.P_WAIT, executable, cmd)
        except OSError, exc:
            # this seems to happen when the command isn't found
            if not DEBUG:
                cmd = executable
            raise DistutilsExecError, \
                  "command %r failed: %s" % (cmd, exc[-1])
        if rc != 0:
            # and this reflects the command running but failing
            if not DEBUG:
                cmd = executable
            log.debug("command %r failed with exit status %d" % (cmd, rc))
            raise DistutilsExecError, \
                  "command %r failed with exit status %d" % (cmd, rc)

if sys.platform == 'darwin':
    from distutils import sysconfig
    _cfg_target = None
    _cfg_target_split = None

def _spawn_posix(cmd, search_path=1, verbose=0, dry_run=0):
    log.info(' '.join(cmd))
    if dry_run:
        return
    executable = cmd[0]
    exec_fn = search_path and os.execvp or os.execv
    env = None
    if sys.platform == 'darwin':
        global _cfg_target, _cfg_target_split
        if _cfg_target is None:
            _cfg_target = sysconfig.get_config_var(
                                  'MACOSX_DEPLOYMENT_TARGET') or ''
            if _cfg_target:
                _cfg_target_split = [int(x) for x in _cfg_target.split('.')]
        if _cfg_target:
            # ensure that the deployment target of build process is not less
            # than that used when the interpreter was built. This ensures
            # extension modules are built with correct compatibility values
            cur_target = os.environ.get('MACOSX_DEPLOYMENT_TARGET', _cfg_target)
            if _cfg_target_split > [int(x) for x in cur_target.split('.')]:
                my_msg = ('$MACOSX_DEPLOYMENT_TARGET mismatch: '
                          'now "%s" but "%s" during configure'
                                % (cur_target, _cfg_target))
                raise DistutilsPlatformError(my_msg)
            env = dict(os.environ,
                       MACOSX_DEPLOYMENT_TARGET=cur_target)
            exec_fn = search_path and os.execvpe or os.execve
    pid = os.fork()

    if pid == 0:  # in the child
        try:
            if env is None:
                exec_fn(executable, cmd)
            else:
                exec_fn(executable, cmd, env)
        except OSError, e:
            if not DEBUG:
                cmd = executable
            sys.stderr.write("unable to execute %r: %s\n" %
                             (cmd, e.strerror))
            os._exit(1)

        if not DEBUG:
            cmd = executable
        sys.stderr.write("unable to execute %r for unknown reasons" % cmd)
        os._exit(1)
    else:   # in the parent
        # Loop until the child either exits or is terminated by a signal
        # (ie. keep waiting if it's merely stopped)
        while 1:
            try:
                pid, status = os.waitpid(pid, 0)
            except OSError, exc:
                import errno
                if exc.errno == errno.EINTR:
                    continue
                if not DEBUG:
                    cmd = executable
                raise DistutilsExecError, \
                      "command %r failed: %s" % (cmd, exc[-1])
            if os.WIFSIGNALED(status):
                if not DEBUG:
                    cmd = executable
                raise DistutilsExecError, \
                      "command %r terminated by signal %d" % \
                      (cmd, os.WTERMSIG(status))

            elif os.WIFEXITED(status):
                exit_status = os.WEXITSTATUS(status)
                if exit_status == 0:
                    return   # hey, it succeeded!
                else:
                    if not DEBUG:
                        cmd = executable
                    raise DistutilsExecError, \
                          "command %r failed with exit status %d" % \
                          (cmd, exit_status)

            elif os.WIFSTOPPED(status):
                continue

            else:
                if not DEBUG:
                    cmd = executable
                raise DistutilsExecError, \
                      "unknown error executing %r: termination status %d" % \
                      (cmd, status)

def find_executable(executable, path=None):
    """Tries to find 'executable' in the directories listed in 'path'.

    A string listing directories separated by 'os.pathsep'; defaults to
    os.environ['PATH'].  Returns the complete filename or None if not found.
    """
    if path is None:
        path = os.environ['PATH']
    paths = path.split(os.pathsep)
    base, ext = os.path.splitext(executable)

    if (sys.platform == 'win32' or os.name == 'os2') and (ext != '.exe'):
        executable = executable + '.exe'

    if not os.path.isfile(executable):
        for p in paths:
            f = os.path.join(p, executable)
            if os.path.isfile(f):
                # the file exists, we have a shot at spawn working
                return f
        return None
    else:
        return executable
