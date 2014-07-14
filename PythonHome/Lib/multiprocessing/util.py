#
# Module providing various facilities to other parts of the package
#
# multiprocessing/util.py
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

import os
import itertools
import weakref
import atexit
import threading        # we want threading to install it's
                        # cleanup function before multiprocessing does
from subprocess import _args_from_interpreter_flags

from multiprocessing.process import current_process, active_children

__all__ = [
    'sub_debug', 'debug', 'info', 'sub_warning', 'get_logger',
    'log_to_stderr', 'get_temp_dir', 'register_after_fork',
    'is_exiting', 'Finalize', 'ForkAwareThreadLock', 'ForkAwareLocal',
    'SUBDEBUG', 'SUBWARNING',
    ]

#
# Logging
#

NOTSET = 0
SUBDEBUG = 5
DEBUG = 10
INFO = 20
SUBWARNING = 25

LOGGER_NAME = 'multiprocessing'
DEFAULT_LOGGING_FORMAT = '[%(levelname)s/%(processName)s] %(message)s'

_logger = None
_log_to_stderr = False

def sub_debug(msg, *args):
    if _logger:
        _logger.log(SUBDEBUG, msg, *args)

def debug(msg, *args):
    if _logger:
        _logger.log(DEBUG, msg, *args)

def info(msg, *args):
    if _logger:
        _logger.log(INFO, msg, *args)

def sub_warning(msg, *args):
    if _logger:
        _logger.log(SUBWARNING, msg, *args)

def get_logger():
    '''
    Returns logger used by multiprocessing
    '''
    global _logger
    import logging, atexit

    logging._acquireLock()
    try:
        if not _logger:

            _logger = logging.getLogger(LOGGER_NAME)
            _logger.propagate = 0
            logging.addLevelName(SUBDEBUG, 'SUBDEBUG')
            logging.addLevelName(SUBWARNING, 'SUBWARNING')

            # XXX multiprocessing should cleanup before logging
            if hasattr(atexit, 'unregister'):
                atexit.unregister(_exit_function)
                atexit.register(_exit_function)
            else:
                atexit._exithandlers.remove((_exit_function, (), {}))
                atexit._exithandlers.append((_exit_function, (), {}))

    finally:
        logging._releaseLock()

    return _logger

def log_to_stderr(level=None):
    '''
    Turn on logging and add a handler which prints to stderr
    '''
    global _log_to_stderr
    import logging

    logger = get_logger()
    formatter = logging.Formatter(DEFAULT_LOGGING_FORMAT)
    handler = logging.StreamHandler()
    handler.setFormatter(formatter)
    logger.addHandler(handler)

    if level:
        logger.setLevel(level)
    _log_to_stderr = True
    return _logger

#
# Function returning a temp directory which will be removed on exit
#

def get_temp_dir():
    # get name of a temp directory which will be automatically cleaned up
    if current_process()._tempdir is None:
        import shutil, tempfile
        tempdir = tempfile.mkdtemp(prefix='pymp-')
        info('created temp directory %s', tempdir)
        Finalize(None, shutil.rmtree, args=[tempdir], exitpriority=-100)
        current_process()._tempdir = tempdir
    return current_process()._tempdir

#
# Support for reinitialization of objects when bootstrapping a child process
#

_afterfork_registry = weakref.WeakValueDictionary()
_afterfork_counter = itertools.count()

def _run_after_forkers():
    items = list(_afterfork_registry.items())
    items.sort()
    for (index, ident, func), obj in items:
        try:
            func(obj)
        except Exception, e:
            info('after forker raised exception %s', e)

def register_after_fork(obj, func):
    _afterfork_registry[(_afterfork_counter.next(), id(obj), func)] = obj

#
# Finalization using weakrefs
#

_finalizer_registry = {}
_finalizer_counter = itertools.count()


class Finalize(object):
    '''
    Class which supports object finalization using weakrefs
    '''
    def __init__(self, obj, callback, args=(), kwargs=None, exitpriority=None):
        assert exitpriority is None or type(exitpriority) is int

        if obj is not None:
            self._weakref = weakref.ref(obj, self)
        else:
            assert exitpriority is not None

        self._callback = callback
        self._args = args
        self._kwargs = kwargs or {}
        self._key = (exitpriority, _finalizer_counter.next())
        self._pid = os.getpid()

        _finalizer_registry[self._key] = self

    def __call__(self, wr=None):
        '''
        Run the callback unless it has already been called or cancelled
        '''
        try:
            del _finalizer_registry[self._key]
        except KeyError:
            sub_debug('finalizer no longer registered')
        else:
            if self._pid != os.getpid():
                sub_debug('finalizer ignored because different process')
                res = None
            else:
                sub_debug('finalizer calling %s with args %s and kwargs %s',
                          self._callback, self._args, self._kwargs)
                res = self._callback(*self._args, **self._kwargs)
            self._weakref = self._callback = self._args = \
                            self._kwargs = self._key = None
            return res

    def cancel(self):
        '''
        Cancel finalization of the object
        '''
        try:
            del _finalizer_registry[self._key]
        except KeyError:
            pass
        else:
            self._weakref = self._callback = self._args = \
                            self._kwargs = self._key = None

    def still_active(self):
        '''
        Return whether this finalizer is still waiting to invoke callback
        '''
        return self._key in _finalizer_registry

    def __repr__(self):
        try:
            obj = self._weakref()
        except (AttributeError, TypeError):
            obj = None

        if obj is None:
            return '<Finalize object, dead>'

        x = '<Finalize object, callback=%s' % \
            getattr(self._callback, '__name__', self._callback)
        if self._args:
            x += ', args=' + str(self._args)
        if self._kwargs:
            x += ', kwargs=' + str(self._kwargs)
        if self._key[0] is not None:
            x += ', exitprority=' + str(self._key[0])
        return x + '>'


def _run_finalizers(minpriority=None):
    '''
    Run all finalizers whose exit priority is not None and at least minpriority

    Finalizers with highest priority are called first; finalizers with
    the same priority will be called in reverse order of creation.
    '''
    if _finalizer_registry is None:
        # This function may be called after this module's globals are
        # destroyed.  See the _exit_function function in this module for more
        # notes.
        return

    if minpriority is None:
        f = lambda p : p[0][0] is not None
    else:
        f = lambda p : p[0][0] is not None and p[0][0] >= minpriority

    items = [x for x in _finalizer_registry.items() if f(x)]
    items.sort(reverse=True)

    for key, finalizer in items:
        sub_debug('calling %s', finalizer)
        try:
            finalizer()
        except Exception:
            import traceback
            traceback.print_exc()

    if minpriority is None:
        _finalizer_registry.clear()

#
# Clean up on exit
#

def is_exiting():
    '''
    Returns true if the process is shutting down
    '''
    return _exiting or _exiting is None

_exiting = False

def _exit_function(info=info, debug=debug, _run_finalizers=_run_finalizers,
                   active_children=active_children,
                   current_process=current_process):
    # NB: we hold on to references to functions in the arglist due to the
    # situation described below, where this function is called after this
    # module's globals are destroyed.

    global _exiting

    info('process shutting down')
    debug('running all "atexit" finalizers with priority >= 0')
    _run_finalizers(0)

    if current_process() is not None:
        # NB: we check if the current process is None here because if
        # it's None, any call to ``active_children()`` will throw an
        # AttributeError (active_children winds up trying to get
        # attributes from util._current_process).  This happens in a
        # variety of shutdown circumstances that are not well-understood
        # because module-scope variables are not apparently supposed to
        # be destroyed until after this function is called.  However,
        # they are indeed destroyed before this function is called.  See
        # issues 9775 and 15881.  Also related: 4106, 9205, and 9207.

        for p in active_children():
            if p._daemonic:
                info('calling terminate() for daemon %s', p.name)
                p._popen.terminate()

        for p in active_children():
            info('calling join() for process %s', p.name)
            p.join()

    debug('running the remaining "atexit" finalizers')
    _run_finalizers()

atexit.register(_exit_function)

#
# Some fork aware types
#

class ForkAwareThreadLock(object):
    def __init__(self):
        self._reset()
        register_after_fork(self, ForkAwareThreadLock._reset)

    def _reset(self):
        self._lock = threading.Lock()
        self.acquire = self._lock.acquire
        self.release = self._lock.release

class ForkAwareLocal(threading.local):
    def __init__(self):
        register_after_fork(self, lambda obj : obj.__dict__.clear())
    def __reduce__(self):
        return type(self), ()
