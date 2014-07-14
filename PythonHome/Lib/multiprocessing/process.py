#
# Module providing the `Process` class which emulates `threading.Thread`
#
# multiprocessing/process.py
#
# Copyright (c) 2006-2008, R Oudkerk
# All rights reserved.
#
# Redistribution and use in source and binary forms, with or without
# modification, are permitted provided that the following conditions
# are met:
#
# 1. Redistributions of source code must retain the above copyright
#    notice, this list of conditions and the following disclaimer.
# 2. Redistributions in binary form must reproduce the above copyright
#    notice, this list of conditions and the following disclaimer in the
#    documentation and/or other materials provided with the distribution.
# 3. Neither the name of author nor the names of any contributors may be
#    used to endorse or promote products derived from this software
#    without specific prior written permission.
#
# THIS SOFTWARE IS PROVIDED BY THE AUTHOR AND CONTRIBUTORS "AS IS" AND
# ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
# IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
# ARE DISCLAIMED.  IN NO EVENT SHALL THE AUTHOR OR CONTRIBUTORS BE LIABLE
# FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
# DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
# OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
# HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
# LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY
# OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF
# SUCH DAMAGE.
#

__all__ = ['Process', 'current_process', 'active_children']

#
# Imports
#

import os
import sys
import signal
import itertools

#
#
#

try:
    ORIGINAL_DIR = os.path.abspath(os.getcwd())
except OSError:
    ORIGINAL_DIR = None

#
# Public functions
#

def current_process():
    '''
    Return process object representing the current process
    '''
    return _current_process

def active_children():
    '''
    Return list of process objects corresponding to live child processes
    '''
    _cleanup()
    return list(_current_process._children)

#
#
#

def _cleanup():
    # check for processes which have finished
    for p in list(_current_process._children):
        if p._popen.poll() is not None:
            _current_process._children.discard(p)

#
# The `Process` class
#

class Process(object):
    '''
    Process objects represent activity that is run in a separate process

    The class is analagous to `threading.Thread`
    '''
    _Popen = None

    def __init__(self, group=None, target=None, name=None, args=(), kwargs={}):
        assert group is None, 'group argument must be None for now'
        count = _current_process._counter.next()
        self._identity = _current_process._identity + (count,)
        self._authkey = _current_process._authkey
        self._daemonic = _current_process._daemonic
        self._tempdir = _current_process._tempdir
        self._parent_pid = os.getpid()
        self._popen = None
        self._target = target
        self._args = tuple(args)
        self._kwargs = dict(kwargs)
        self._name = name or type(self).__name__ + '-' + \
                     ':'.join(str(i) for i in self._identity)

    def run(self):
        '''
        Method to be run in sub-process; can be overridden in sub-class
        '''
        if self._target:
            self._target(*self._args, **self._kwargs)

    def start(self):
        '''
        Start child process
        '''
        assert self._popen is None, 'cannot start a process twice'
        assert self._parent_pid == os.getpid(), \
               'can only start a process object created by current process'
        assert not _current_process._daemonic, \
               'daemonic processes are not allowed to have children'
        _cleanup()
        if self._Popen is not None:
            Popen = self._Popen
        else:
            from .forking import Popen
        self._popen = Popen(self)
        _current_process._children.add(self)

    def terminate(self):
        '''
        Terminate process; sends SIGTERM signal or uses TerminateProcess()
        '''
        self._popen.terminate()

    def join(self, timeout=None):
        '''
        Wait until child process terminates
        '''
        assert self._parent_pid == os.getpid(), 'can only join a child process'
        assert self._popen is not None, 'can only join a started process'
        res = self._popen.wait(timeout)
        if res is not None:
            _current_process._children.discard(self)

    def is_alive(self):
        '''
        Return whether process is alive
        '''
        if self is _current_process:
            return True
        assert self._parent_pid == os.getpid(), 'can only test a child process'
        if self._popen is None:
            return False
        self._popen.poll()
        return self._popen.returncode is None

    @property
    def name(self):
        return self._name

    @name.setter
    def name(self, name):
        assert isinstance(name, basestring), 'name must be a string'
        self._name = name

    @property
    def daemon(self):
        '''
        Return whether process is a daemon
        '''
        return self._daemonic

    @daemon.setter
    def daemon(self, daemonic):
        '''
        Set whether process is a daemon
        '''
        assert self._popen is None, 'process has already started'
        self._daemonic = daemonic

    @property
    def authkey(self):
        return self._authkey

    @authkey.setter
    def authkey(self, authkey):
        '''
        Set authorization key of process
        '''
        self._authkey = AuthenticationString(authkey)

    @property
    def exitcode(self):
        '''
        Return exit code of process or `None` if it has yet to stop
        '''
        if self._popen is None:
            return self._popen
        return self._popen.poll()

    @property
    def ident(self):
        '''
        Return identifier (PID) of process or `None` if it has yet to start
        '''
        if self is _current_process:
            return os.getpid()
        else:
            return self._popen and self._popen.pid

    pid = ident

    def __repr__(self):
        if self is _current_process:
            status = 'started'
        elif self._parent_pid != os.getpid():
            status = 'unknown'
        elif self._popen is None:
            status = 'initial'
        else:
            if self._popen.poll() is not None:
                status = self.exitcode
            else:
                status = 'started'

        if type(status) is int:
            if status == 0:
                status = 'stopped'
            else:
                status = 'stopped[%s]' % _exitcode_to_name.get(status, status)

        return '<%s(%s, %s%s)>' % (type(self).__name__, self._name,
                                   status, self._daemonic and ' daemon' or '')

    ##

    def _bootstrap(self):
        from . import util
        global _current_process

        try:
            self._children = set()
            self._counter = itertools.count(1)
            try:
                sys.stdin.close()
                sys.stdin = open(os.devnull)
            except (OSError, ValueError):
                pass
            _current_process = self
            util._finalizer_registry.clear()
            util._run_after_forkers()
            util.info('child process calling self.run()')
            try:
                self.run()
                exitcode = 0
            finally:
                util._exit_function()
        except SystemExit, e:
            if not e.args:
                exitcode = 1
            elif isinstance(e.args[0], int):
                exitcode = e.args[0]
            else:
                sys.stderr.write(str(e.args[0]) + '\n')
                sys.stderr.flush()
                exitcode = 1
        except:
            exitcode = 1
            import traceback
            sys.stderr.write('Process %s:\n' % self.name)
            sys.stderr.flush()
            traceback.print_exc()

        util.info('process exiting with exitcode %d' % exitcode)
        return exitcode

#
# We subclass bytes to avoid accidental transmission of auth keys over network
#

class AuthenticationString(bytes):
    def __reduce__(self):
        from .forking import Popen
        if not Popen.thread_is_spawning():
            raise TypeError(
                'Pickling an AuthenticationString object is '
                'disallowed for security reasons'
                )
        return AuthenticationString, (bytes(self),)

#
# Create object representing the main process
#

class _MainProcess(Process):

    def __init__(self):
        self._identity = ()
        self._daemonic = False
        self._name = 'MainProcess'
        self._parent_pid = None
        self._popen = None
        self._counter = itertools.count(1)
        self._children = set()
        self._authkey = AuthenticationString(os.urandom(32))
        self._tempdir = None

_current_process = _MainProcess()
del _MainProcess

#
# Give names to some return codes
#

_exitcode_to_name = {}

for name, signum in signal.__dict__.items():
    if name[:3]=='SIG' and '_' not in name:
        _exitcode_to_name[-signum] = name
