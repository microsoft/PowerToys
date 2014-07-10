#
# Package analogous to 'threading.py' but using processes
#
# multiprocessing/__init__.py
#
# This package is intended to duplicate the functionality (and much of
# the API) of threading.py but uses processes instead of threads.  A
# subpackage 'multiprocessing.dummy' has the same API but is a simple
# wrapper for 'threading'.
#
# Try calling `multiprocessing.doc.main()` to read the html
# documentation in a webbrowser.
#
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

__version__ = '0.70a1'

__all__ = [
    'Process', 'current_process', 'active_children', 'freeze_support',
    'Manager', 'Pipe', 'cpu_count', 'log_to_stderr', 'get_logger',
    'allow_connection_pickling', 'BufferTooShort', 'TimeoutError',
    'Lock', 'RLock', 'Semaphore', 'BoundedSemaphore', 'Condition',
    'Event', 'Queue', 'JoinableQueue', 'Pool', 'Value', 'Array',
    'RawValue', 'RawArray', 'SUBDEBUG', 'SUBWARNING',
    ]

__author__ = 'R. Oudkerk (r.m.oudkerk@gmail.com)'

#
# Imports
#

import os
import sys

from multiprocessing.process import Process, current_process, active_children
from multiprocessing.util import SUBDEBUG, SUBWARNING

#
# Exceptions
#

class ProcessError(Exception):
    pass

class BufferTooShort(ProcessError):
    pass

class TimeoutError(ProcessError):
    pass

class AuthenticationError(ProcessError):
    pass

# This is down here because _multiprocessing uses BufferTooShort
import _multiprocessing

#
# Definitions not depending on native semaphores
#

def Manager():
    '''
    Returns a manager associated with a running server process

    The managers methods such as `Lock()`, `Condition()` and `Queue()`
    can be used to create shared objects.
    '''
    from multiprocessing.managers import SyncManager
    m = SyncManager()
    m.start()
    return m

def Pipe(duplex=True):
    '''
    Returns two connection object connected by a pipe
    '''
    from multiprocessing.connection import Pipe
    return Pipe(duplex)

def cpu_count():
    '''
    Returns the number of CPUs in the system
    '''
    if sys.platform == 'win32':
        try:
            num = int(os.environ['NUMBER_OF_PROCESSORS'])
        except (ValueError, KeyError):
            num = 0
    elif 'bsd' in sys.platform or sys.platform == 'darwin':
        comm = '/sbin/sysctl -n hw.ncpu'
        if sys.platform == 'darwin':
            comm = '/usr' + comm
        try:
            with os.popen(comm) as p:
                num = int(p.read())
        except ValueError:
            num = 0
    else:
        try:
            num = os.sysconf('SC_NPROCESSORS_ONLN')
        except (ValueError, OSError, AttributeError):
            num = 0

    if num >= 1:
        return num
    else:
        raise NotImplementedError('cannot determine number of cpus')

def freeze_support():
    '''
    Check whether this is a fake forked process in a frozen executable.
    If so then run code specified by commandline and exit.
    '''
    if sys.platform == 'win32' and getattr(sys, 'frozen', False):
        from multiprocessing.forking import freeze_support
        freeze_support()

def get_logger():
    '''
    Return package logger -- if it does not already exist then it is created
    '''
    from multiprocessing.util import get_logger
    return get_logger()

def log_to_stderr(level=None):
    '''
    Turn on logging and add a handler which prints to stderr
    '''
    from multiprocessing.util import log_to_stderr
    return log_to_stderr(level)

def allow_connection_pickling():
    '''
    Install support for sending connections and sockets between processes
    '''
    from multiprocessing import reduction

#
# Definitions depending on native semaphores
#

def Lock():
    '''
    Returns a non-recursive lock object
    '''
    from multiprocessing.synchronize import Lock
    return Lock()

def RLock():
    '''
    Returns a recursive lock object
    '''
    from multiprocessing.synchronize import RLock
    return RLock()

def Condition(lock=None):
    '''
    Returns a condition object
    '''
    from multiprocessing.synchronize import Condition
    return Condition(lock)

def Semaphore(value=1):
    '''
    Returns a semaphore object
    '''
    from multiprocessing.synchronize import Semaphore
    return Semaphore(value)

def BoundedSemaphore(value=1):
    '''
    Returns a bounded semaphore object
    '''
    from multiprocessing.synchronize import BoundedSemaphore
    return BoundedSemaphore(value)

def Event():
    '''
    Returns an event object
    '''
    from multiprocessing.synchronize import Event
    return Event()

def Queue(maxsize=0):
    '''
    Returns a queue object
    '''
    from multiprocessing.queues import Queue
    return Queue(maxsize)

def JoinableQueue(maxsize=0):
    '''
    Returns a queue object
    '''
    from multiprocessing.queues import JoinableQueue
    return JoinableQueue(maxsize)

def Pool(processes=None, initializer=None, initargs=(), maxtasksperchild=None):
    '''
    Returns a process pool object
    '''
    from multiprocessing.pool import Pool
    return Pool(processes, initializer, initargs, maxtasksperchild)

def RawValue(typecode_or_type, *args):
    '''
    Returns a shared object
    '''
    from multiprocessing.sharedctypes import RawValue
    return RawValue(typecode_or_type, *args)

def RawArray(typecode_or_type, size_or_initializer):
    '''
    Returns a shared array
    '''
    from multiprocessing.sharedctypes import RawArray
    return RawArray(typecode_or_type, size_or_initializer)

def Value(typecode_or_type, *args, **kwds):
    '''
    Returns a synchronized shared object
    '''
    from multiprocessing.sharedctypes import Value
    return Value(typecode_or_type, *args, **kwds)

def Array(typecode_or_type, size_or_initializer, **kwds):
    '''
    Returns a synchronized shared array
    '''
    from multiprocessing.sharedctypes import Array
    return Array(typecode_or_type, size_or_initializer, **kwds)

#
#
#

if sys.platform == 'win32':

    def set_executable(executable):
        '''
        Sets the path to a python.exe or pythonw.exe binary used to run
        child processes on Windows instead of sys.executable.
        Useful for people embedding Python.
        '''
        from multiprocessing.forking import set_executable
        set_executable(executable)

    __all__ += ['set_executable']
