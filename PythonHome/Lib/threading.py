"""Thread module emulating a subset of Java's threading model."""

import sys as _sys

try:
    import thread
except ImportError:
    del _sys.modules[__name__]
    raise

import warnings

from collections import deque as _deque
from time import time as _time, sleep as _sleep
from traceback import format_exc as _format_exc

# Note regarding PEP 8 compliant aliases
#  This threading model was originally inspired by Java, and inherited
# the convention of camelCase function and method names from that
# language. While those names are not in any imminent danger of being
# deprecated, starting with Python 2.6, the module now provides a
# PEP 8 compliant alias for any such method name.
# Using the new PEP 8 compliant names also facilitates substitution
# with the multiprocessing module, which doesn't provide the old
# Java inspired names.


# Rename some stuff so "from threading import *" is safe
__all__ = ['activeCount', 'active_count', 'Condition', 'currentThread',
           'current_thread', 'enumerate', 'Event',
           'Lock', 'RLock', 'Semaphore', 'BoundedSemaphore', 'Thread',
           'Timer', 'setprofile', 'settrace', 'local', 'stack_size']

_start_new_thread = thread.start_new_thread
_allocate_lock = thread.allocate_lock
_get_ident = thread.get_ident
ThreadError = thread.error
del thread


# sys.exc_clear is used to work around the fact that except blocks
# don't fully clear the exception until 3.0.
warnings.filterwarnings('ignore', category=DeprecationWarning,
                        module='threading', message='sys.exc_clear')

# Debug support (adapted from ihooks.py).
# All the major classes here derive from _Verbose.  We force that to
# be a new-style class so that all the major classes here are new-style.
# This helps debugging (type(instance) is more revealing for instances
# of new-style classes).

_VERBOSE = False

if __debug__:

    class _Verbose(object):

        def __init__(self, verbose=None):
            if verbose is None:
                verbose = _VERBOSE
            self.__verbose = verbose

        def _note(self, format, *args):
            if self.__verbose:
                format = format % args
                # Issue #4188: calling current_thread() can incur an infinite
                # recursion if it has to create a DummyThread on the fly.
                ident = _get_ident()
                try:
                    name = _active[ident].name
                except KeyError:
                    name = "<OS thread %d>" % ident
                format = "%s: %s\n" % (name, format)
                _sys.stderr.write(format)

else:
    # Disable this when using "python -O"
    class _Verbose(object):
        def __init__(self, verbose=None):
            pass
        def _note(self, *args):
            pass

# Support for profile and trace hooks

_profile_hook = None
_trace_hook = None

def setprofile(func):
    """Set a profile function for all threads started from the threading module.

    The func will be passed to sys.setprofile() for each thread, before its
    run() method is called.

    """
    global _profile_hook
    _profile_hook = func

def settrace(func):
    """Set a trace function for all threads started from the threading module.

    The func will be passed to sys.settrace() for each thread, before its run()
    method is called.

    """
    global _trace_hook
    _trace_hook = func

# Synchronization classes

Lock = _allocate_lock

def RLock(*args, **kwargs):
    """Factory function that returns a new reentrant lock.

    A reentrant lock must be released by the thread that acquired it. Once a
    thread has acquired a reentrant lock, the same thread may acquire it again
    without blocking; the thread must release it once for each time it has
    acquired it.

    """
    return _RLock(*args, **kwargs)

class _RLock(_Verbose):
    """A reentrant lock must be released by the thread that acquired it. Once a
       thread has acquired a reentrant lock, the same thread may acquire it
       again without blocking; the thread must release it once for each time it
       has acquired it.
    """

    def __init__(self, verbose=None):
        _Verbose.__init__(self, verbose)
        self.__block = _allocate_lock()
        self.__owner = None
        self.__count = 0

    def __repr__(self):
        owner = self.__owner
        try:
            owner = _active[owner].name
        except KeyError:
            pass
        return "<%s owner=%r count=%d>" % (
                self.__class__.__name__, owner, self.__count)

    def acquire(self, blocking=1):
        """Acquire a lock, blocking or non-blocking.

        When invoked without arguments: if this thread already owns the lock,
        increment the recursion level by one, and return immediately. Otherwise,
        if another thread owns the lock, block until the lock is unlocked. Once
        the lock is unlocked (not owned by any thread), then grab ownership, set
        the recursion level to one, and return. If more than one thread is
        blocked waiting until the lock is unlocked, only one at a time will be
        able to grab ownership of the lock. There is no return value in this
        case.

        When invoked with the blocking argument set to true, do the same thing
        as when called without arguments, and return true.

        When invoked with the blocking argument set to false, do not block. If a
        call without an argument would block, return false immediately;
        otherwise, do the same thing as when called without arguments, and
        return true.

        """
        me = _get_ident()
        if self.__owner == me:
            self.__count = self.__count + 1
            if __debug__:
                self._note("%s.acquire(%s): recursive success", self, blocking)
            return 1
        rc = self.__block.acquire(blocking)
        if rc:
            self.__owner = me
            self.__count = 1
            if __debug__:
                self._note("%s.acquire(%s): initial success", self, blocking)
        else:
            if __debug__:
                self._note("%s.acquire(%s): failure", self, blocking)
        return rc

    __enter__ = acquire

    def release(self):
        """Release a lock, decrementing the recursion level.

        If after the decrement it is zero, reset the lock to unlocked (not owned
        by any thread), and if any other threads are blocked waiting for the
        lock to become unlocked, allow exactly one of them to proceed. If after
        the decrement the recursion level is still nonzero, the lock remains
        locked and owned by the calling thread.

        Only call this method when the calling thread owns the lock. A
        RuntimeError is raised if this method is called when the lock is
        unlocked.

        There is no return value.

        """
        if self.__owner != _get_ident():
            raise RuntimeError("cannot release un-acquired lock")
        self.__count = count = self.__count - 1
        if not count:
            self.__owner = None
            self.__block.release()
            if __debug__:
                self._note("%s.release(): final release", self)
        else:
            if __debug__:
                self._note("%s.release(): non-final release", self)

    def __exit__(self, t, v, tb):
        self.release()

    # Internal methods used by condition variables

    def _acquire_restore(self, count_owner):
        count, owner = count_owner
        self.__block.acquire()
        self.__count = count
        self.__owner = owner
        if __debug__:
            self._note("%s._acquire_restore()", self)

    def _release_save(self):
        if __debug__:
            self._note("%s._release_save()", self)
        count = self.__count
        self.__count = 0
        owner = self.__owner
        self.__owner = None
        self.__block.release()
        return (count, owner)

    def _is_owned(self):
        return self.__owner == _get_ident()


def Condition(*args, **kwargs):
    """Factory function that returns a new condition variable object.

    A condition variable allows one or more threads to wait until they are
    notified by another thread.

    If the lock argument is given and not None, it must be a Lock or RLock
    object, and it is used as the underlying lock. Otherwise, a new RLock object
    is created and used as the underlying lock.

    """
    return _Condition(*args, **kwargs)

class _Condition(_Verbose):
    """Condition variables allow one or more threads to wait until they are
       notified by another thread.
    """

    def __init__(self, lock=None, verbose=None):
        _Verbose.__init__(self, verbose)
        if lock is None:
            lock = RLock()
        self.__lock = lock
        # Export the lock's acquire() and release() methods
        self.acquire = lock.acquire
        self.release = lock.release
        # If the lock defines _release_save() and/or _acquire_restore(),
        # these override the default implementations (which just call
        # release() and acquire() on the lock).  Ditto for _is_owned().
        try:
            self._release_save = lock._release_save
        except AttributeError:
            pass
        try:
            self._acquire_restore = lock._acquire_restore
        except AttributeError:
            pass
        try:
            self._is_owned = lock._is_owned
        except AttributeError:
            pass
        self.__waiters = []

    def __enter__(self):
        return self.__lock.__enter__()

    def __exit__(self, *args):
        return self.__lock.__exit__(*args)

    def __repr__(self):
        return "<Condition(%s, %d)>" % (self.__lock, len(self.__waiters))

    def _release_save(self):
        self.__lock.release()           # No state to save

    def _acquire_restore(self, x):
        self.__lock.acquire()           # Ignore saved state

    def _is_owned(self):
        # Return True if lock is owned by current_thread.
        # This method is called only if __lock doesn't have _is_owned().
        if self.__lock.acquire(0):
            self.__lock.release()
            return False
        else:
            return True

    def wait(self, timeout=None):
        """Wait until notified or until a timeout occurs.

        If the calling thread has not acquired the lock when this method is
        called, a RuntimeError is raised.

        This method releases the underlying lock, and then blocks until it is
        awakened by a notify() or notifyAll() call for the same condition
        variable in another thread, or until the optional timeout occurs. Once
        awakened or timed out, it re-acquires the lock and returns.

        When the timeout argument is present and not None, it should be a
        floating point number specifying a timeout for the operation in seconds
        (or fractions thereof).

        When the underlying lock is an RLock, it is not released using its
        release() method, since this may not actually unlock the lock when it
        was acquired multiple times recursively. Instead, an internal interface
        of the RLock class is used, which really unlocks it even when it has
        been recursively acquired several times. Another internal interface is
        then used to restore the recursion level when the lock is reacquired.

        """
        if not self._is_owned():
            raise RuntimeError("cannot wait on un-acquired lock")
        waiter = _allocate_lock()
        waiter.acquire()
        self.__waiters.append(waiter)
        saved_state = self._release_save()
        try:    # restore state no matter what (e.g., KeyboardInterrupt)
            if timeout is None:
                waiter.acquire()
                if __debug__:
                    self._note("%s.wait(): got it", self)
            else:
                # Balancing act:  We can't afford a pure busy loop, so we
                # have to sleep; but if we sleep the whole timeout time,
                # we'll be unresponsive.  The scheme here sleeps very
                # little at first, longer as time goes on, but never longer
                # than 20 times per second (or the timeout time remaining).
                endtime = _time() + timeout
                delay = 0.0005 # 500 us -> initial delay of 1 ms
                while True:
                    gotit = waiter.acquire(0)
                    if gotit:
                        break
                    remaining = endtime - _time()
                    if remaining <= 0:
                        break
                    delay = min(delay * 2, remaining, .05)
                    _sleep(delay)
                if not gotit:
                    if __debug__:
                        self._note("%s.wait(%s): timed out", self, timeout)
                    try:
                        self.__waiters.remove(waiter)
                    except ValueError:
                        pass
                else:
                    if __debug__:
                        self._note("%s.wait(%s): got it", self, timeout)
        finally:
            self._acquire_restore(saved_state)

    def notify(self, n=1):
        """Wake up one or more threads waiting on this condition, if any.

        If the calling thread has not acquired the lock when this method is
        called, a RuntimeError is raised.

        This method wakes up at most n of the threads waiting for the condition
        variable; it is a no-op if no threads are waiting.

        """
        if not self._is_owned():
            raise RuntimeError("cannot notify on un-acquired lock")
        __waiters = self.__waiters
        waiters = __waiters[:n]
        if not waiters:
            if __debug__:
                self._note("%s.notify(): no waiters", self)
            return
        self._note("%s.notify(): notifying %d waiter%s", self, n,
                   n!=1 and "s" or "")
        for waiter in waiters:
            waiter.release()
            try:
                __waiters.remove(waiter)
            except ValueError:
                pass

    def notifyAll(self):
        """Wake up all threads waiting on this condition.

        If the calling thread has not acquired the lock when this method
        is called, a RuntimeError is raised.

        """
        self.notify(len(self.__waiters))

    notify_all = notifyAll


def Semaphore(*args, **kwargs):
    """A factory function that returns a new semaphore.

    Semaphores manage a counter representing the number of release() calls minus
    the number of acquire() calls, plus an initial value. The acquire() method
    blocks if necessary until it can return without making the counter
    negative. If not given, value defaults to 1.

    """
    return _Semaphore(*args, **kwargs)

class _Semaphore(_Verbose):
    """Semaphores manage a counter representing the number of release() calls
       minus the number of acquire() calls, plus an initial value. The acquire()
       method blocks if necessary until it can return without making the counter
       negative. If not given, value defaults to 1.

    """

    # After Tim Peters' semaphore class, but not quite the same (no maximum)

    def __init__(self, value=1, verbose=None):
        if value < 0:
            raise ValueError("semaphore initial value must be >= 0")
        _Verbose.__init__(self, verbose)
        self.__cond = Condition(Lock())
        self.__value = value

    def acquire(self, blocking=1):
        """Acquire a semaphore, decrementing the internal counter by one.

        When invoked without arguments: if the internal counter is larger than
        zero on entry, decrement it by one and return immediately. If it is zero
        on entry, block, waiting until some other thread has called release() to
        make it larger than zero. This is done with proper interlocking so that
        if multiple acquire() calls are blocked, release() will wake exactly one
        of them up. The implementation may pick one at random, so the order in
        which blocked threads are awakened should not be relied on. There is no
        return value in this case.

        When invoked with blocking set to true, do the same thing as when called
        without arguments, and return true.

        When invoked with blocking set to false, do not block. If a call without
        an argument would block, return false immediately; otherwise, do the
        same thing as when called without arguments, and return true.

        """
        rc = False
        with self.__cond:
            while self.__value == 0:
                if not blocking:
                    break
                if __debug__:
                    self._note("%s.acquire(%s): blocked waiting, value=%s",
                            self, blocking, self.__value)
                self.__cond.wait()
            else:
                self.__value = self.__value - 1
                if __debug__:
                    self._note("%s.acquire: success, value=%s",
                            self, self.__value)
                rc = True
        return rc

    __enter__ = acquire

    def release(self):
        """Release a semaphore, incrementing the internal counter by one.

        When the counter is zero on entry and another thread is waiting for it
        to become larger than zero again, wake up that thread.

        """
        with self.__cond:
            self.__value = self.__value + 1
            if __debug__:
                self._note("%s.release: success, value=%s",
                        self, self.__value)
            self.__cond.notify()

    def __exit__(self, t, v, tb):
        self.release()


def BoundedSemaphore(*args, **kwargs):
    """A factory function that returns a new bounded semaphore.

    A bounded semaphore checks to make sure its current value doesn't exceed its
    initial value. If it does, ValueError is raised. In most situations
    semaphores are used to guard resources with limited capacity.

    If the semaphore is released too many times it's a sign of a bug. If not
    given, value defaults to 1.

    Like regular semaphores, bounded semaphores manage a counter representing
    the number of release() calls minus the number of acquire() calls, plus an
    initial value. The acquire() method blocks if necessary until it can return
    without making the counter negative. If not given, value defaults to 1.

    """
    return _BoundedSemaphore(*args, **kwargs)

class _BoundedSemaphore(_Semaphore):
    """A bounded semaphore checks to make sure its current value doesn't exceed
       its initial value. If it does, ValueError is raised. In most situations
       semaphores are used to guard resources with limited capacity.
    """

    def __init__(self, value=1, verbose=None):
        _Semaphore.__init__(self, value, verbose)
        self._initial_value = value

    def release(self):
        """Release a semaphore, incrementing the internal counter by one.

        When the counter is zero on entry and another thread is waiting for it
        to become larger than zero again, wake up that thread.

        If the number of releases exceeds the number of acquires,
        raise a ValueError.

        """
        with self._Semaphore__cond:
            if self._Semaphore__value >= self._initial_value:
                raise ValueError("Semaphore released too many times")
            self._Semaphore__value += 1
            self._Semaphore__cond.notify()


def Event(*args, **kwargs):
    """A factory function that returns a new event.

    Events manage a flag that can be set to true with the set() method and reset
    to false with the clear() method. The wait() method blocks until the flag is
    true.

    """
    return _Event(*args, **kwargs)

class _Event(_Verbose):
    """A factory function that returns a new event object. An event manages a
       flag that can be set to true with the set() method and reset to false
       with the clear() method. The wait() method blocks until the flag is true.

    """

    # After Tim Peters' event class (without is_posted())

    def __init__(self, verbose=None):
        _Verbose.__init__(self, verbose)
        self.__cond = Condition(Lock())
        self.__flag = False

    def _reset_internal_locks(self):
        # private!  called by Thread._reset_internal_locks by _after_fork()
        self.__cond.__init__()

    def isSet(self):
        'Return true if and only if the internal flag is true.'
        return self.__flag

    is_set = isSet

    def set(self):
        """Set the internal flag to true.

        All threads waiting for the flag to become true are awakened. Threads
        that call wait() once the flag is true will not block at all.

        """
        self.__cond.acquire()
        try:
            self.__flag = True
            self.__cond.notify_all()
        finally:
            self.__cond.release()

    def clear(self):
        """Reset the internal flag to false.

        Subsequently, threads calling wait() will block until set() is called to
        set the internal flag to true again.

        """
        self.__cond.acquire()
        try:
            self.__flag = False
        finally:
            self.__cond.release()

    def wait(self, timeout=None):
        """Block until the internal flag is true.

        If the internal flag is true on entry, return immediately. Otherwise,
        block until another thread calls set() to set the flag to true, or until
        the optional timeout occurs.

        When the timeout argument is present and not None, it should be a
        floating point number specifying a timeout for the operation in seconds
        (or fractions thereof).

        This method returns the internal flag on exit, so it will always return
        True except if a timeout is given and the operation times out.

        """
        self.__cond.acquire()
        try:
            if not self.__flag:
                self.__cond.wait(timeout)
            return self.__flag
        finally:
            self.__cond.release()

# Helper to generate new thread names
_counter = 0
def _newname(template="Thread-%d"):
    global _counter
    _counter = _counter + 1
    return template % _counter

# Active thread administration
_active_limbo_lock = _allocate_lock()
_active = {}    # maps thread id to Thread object
_limbo = {}


# Main class for threads

class Thread(_Verbose):
    """A class that represents a thread of control.

    This class can be safely subclassed in a limited fashion.

    """
    __initialized = False
    # Need to store a reference to sys.exc_info for printing
    # out exceptions when a thread tries to use a global var. during interp.
    # shutdown and thus raises an exception about trying to perform some
    # operation on/with a NoneType
    __exc_info = _sys.exc_info
    # Keep sys.exc_clear too to clear the exception just before
    # allowing .join() to return.
    __exc_clear = _sys.exc_clear

    def __init__(self, group=None, target=None, name=None,
                 args=(), kwargs=None, verbose=None):
        """This constructor should always be called with keyword arguments. Arguments are:

        *group* should be None; reserved for future extension when a ThreadGroup
        class is implemented.

        *target* is the callable object to be invoked by the run()
        method. Defaults to None, meaning nothing is called.

        *name* is the thread name. By default, a unique name is constructed of
        the form "Thread-N" where N is a small decimal number.

        *args* is the argument tuple for the target invocation. Defaults to ().

        *kwargs* is a dictionary of keyword arguments for the target
        invocation. Defaults to {}.

        If a subclass overrides the constructor, it must make sure to invoke
        the base class constructor (Thread.__init__()) before doing anything
        else to the thread.

"""
        assert group is None, "group argument must be None for now"
        _Verbose.__init__(self, verbose)
        if kwargs is None:
            kwargs = {}
        self.__target = target
        self.__name = str(name or _newname())
        self.__args = args
        self.__kwargs = kwargs
        self.__daemonic = self._set_daemon()
        self.__ident = None
        self.__started = Event()
        self.__stopped = False
        self.__block = Condition(Lock())
        self.__initialized = True
        # sys.stderr is not stored in the class like
        # sys.exc_info since it can be changed between instances
        self.__stderr = _sys.stderr

    def _reset_internal_locks(self):
        # private!  Called by _after_fork() to reset our internal locks as
        # they may be in an invalid state leading to a deadlock or crash.
        if hasattr(self, '_Thread__block'):  # DummyThread deletes self.__block
            self.__block.__init__()
        self.__started._reset_internal_locks()

    @property
    def _block(self):
        # used by a unittest
        return self.__block

    def _set_daemon(self):
        # Overridden in _MainThread and _DummyThread
        return current_thread().daemon

    def __repr__(self):
        assert self.__initialized, "Thread.__init__() was not called"
        status = "initial"
        if self.__started.is_set():
            status = "started"
        if self.__stopped:
            status = "stopped"
        if self.__daemonic:
            status += " daemon"
        if self.__ident is not None:
            status += " %s" % self.__ident
        return "<%s(%s, %s)>" % (self.__class__.__name__, self.__name, status)

    def start(self):
        """Start the thread's activity.

        It must be called at most once per thread object. It arranges for the
        object's run() method to be invoked in a separate thread of control.

        This method will raise a RuntimeError if called more than once on the
        same thread object.

        """
        if not self.__initialized:
            raise RuntimeError("thread.__init__() not called")
        if self.__started.is_set():
            raise RuntimeError("threads can only be started once")
        if __debug__:
            self._note("%s.start(): starting thread", self)
        with _active_limbo_lock:
            _limbo[self] = self
        try:
            _start_new_thread(self.__bootstrap, ())
        except Exception:
            with _active_limbo_lock:
                del _limbo[self]
            raise
        self.__started.wait()

    def run(self):
        """Method representing the thread's activity.

        You may override this method in a subclass. The standard run() method
        invokes the callable object passed to the object's constructor as the
        target argument, if any, with sequential and keyword arguments taken
        from the args and kwargs arguments, respectively.

        """
        try:
            if self.__target:
                self.__target(*self.__args, **self.__kwargs)
        finally:
            # Avoid a refcycle if the thread is running a function with
            # an argument that has a member that points to the thread.
            del self.__target, self.__args, self.__kwargs

    def __bootstrap(self):
        # Wrapper around the real bootstrap code that ignores
        # exceptions during interpreter cleanup.  Those typically
        # happen when a daemon thread wakes up at an unfortunate
        # moment, finds the world around it destroyed, and raises some
        # random exception *** while trying to report the exception in
        # __bootstrap_inner() below ***.  Those random exceptions
        # don't help anybody, and they confuse users, so we suppress
        # them.  We suppress them only when it appears that the world
        # indeed has already been destroyed, so that exceptions in
        # __bootstrap_inner() during normal business hours are properly
        # reported.  Also, we only suppress them for daemonic threads;
        # if a non-daemonic encounters this, something else is wrong.
        try:
            self.__bootstrap_inner()
        except:
            if self.__daemonic and _sys is None:
                return
            raise

    def _set_ident(self):
        self.__ident = _get_ident()

    def __bootstrap_inner(self):
        try:
            self._set_ident()
            self.__started.set()
            with _active_limbo_lock:
                _active[self.__ident] = self
                del _limbo[self]
            if __debug__:
                self._note("%s.__bootstrap(): thread started", self)

            if _trace_hook:
                self._note("%s.__bootstrap(): registering trace hook", self)
                _sys.settrace(_trace_hook)
            if _profile_hook:
                self._note("%s.__bootstrap(): registering profile hook", self)
                _sys.setprofile(_profile_hook)

            try:
                self.run()
            except SystemExit:
                if __debug__:
                    self._note("%s.__bootstrap(): raised SystemExit", self)
            except:
                if __debug__:
                    self._note("%s.__bootstrap(): unhandled exception", self)
                # If sys.stderr is no more (most likely from interpreter
                # shutdown) use self.__stderr.  Otherwise still use sys (as in
                # _sys) in case sys.stderr was redefined since the creation of
                # self.
                if _sys:
                    _sys.stderr.write("Exception in thread %s:\n%s\n" %
                                      (self.name, _format_exc()))
                else:
                    # Do the best job possible w/o a huge amt. of code to
                    # approximate a traceback (code ideas from
                    # Lib/traceback.py)
                    exc_type, exc_value, exc_tb = self.__exc_info()
                    try:
                        print>>self.__stderr, (
                            "Exception in thread " + self.name +
                            " (most likely raised during interpreter shutdown):")
                        print>>self.__stderr, (
                            "Traceback (most recent call last):")
                        while exc_tb:
                            print>>self.__stderr, (
                                '  File "%s", line %s, in %s' %
                                (exc_tb.tb_frame.f_code.co_filename,
                                    exc_tb.tb_lineno,
                                    exc_tb.tb_frame.f_code.co_name))
                            exc_tb = exc_tb.tb_next
                        print>>self.__stderr, ("%s: %s" % (exc_type, exc_value))
                    # Make sure that exc_tb gets deleted since it is a memory
                    # hog; deleting everything else is just for thoroughness
                    finally:
                        del exc_type, exc_value, exc_tb
            else:
                if __debug__:
                    self._note("%s.__bootstrap(): normal return", self)
            finally:
                # Prevent a race in
                # test_threading.test_no_refcycle_through_target when
                # the exception keeps the target alive past when we
                # assert that it's dead.
                self.__exc_clear()
        finally:
            with _active_limbo_lock:
                self.__stop()
                try:
                    # We don't call self.__delete() because it also
                    # grabs _active_limbo_lock.
                    del _active[_get_ident()]
                except:
                    pass

    def __stop(self):
        # DummyThreads delete self.__block, but they have no waiters to
        # notify anyway (join() is forbidden on them).
        if not hasattr(self, '_Thread__block'):
            return
        self.__block.acquire()
        self.__stopped = True
        self.__block.notify_all()
        self.__block.release()

    def __delete(self):
        "Remove current thread from the dict of currently running threads."

        # Notes about running with dummy_thread:
        #
        # Must take care to not raise an exception if dummy_thread is being
        # used (and thus this module is being used as an instance of
        # dummy_threading).  dummy_thread.get_ident() always returns -1 since
        # there is only one thread if dummy_thread is being used.  Thus
        # len(_active) is always <= 1 here, and any Thread instance created
        # overwrites the (if any) thread currently registered in _active.
        #
        # An instance of _MainThread is always created by 'threading'.  This
        # gets overwritten the instant an instance of Thread is created; both
        # threads return -1 from dummy_thread.get_ident() and thus have the
        # same key in the dict.  So when the _MainThread instance created by
        # 'threading' tries to clean itself up when atexit calls this method
        # it gets a KeyError if another Thread instance was created.
        #
        # This all means that KeyError from trying to delete something from
        # _active if dummy_threading is being used is a red herring.  But
        # since it isn't if dummy_threading is *not* being used then don't
        # hide the exception.

        try:
            with _active_limbo_lock:
                del _active[_get_ident()]
                # There must not be any python code between the previous line
                # and after the lock is released.  Otherwise a tracing function
                # could try to acquire the lock again in the same thread, (in
                # current_thread()), and would block.
        except KeyError:
            if 'dummy_threading' not in _sys.modules:
                raise

    def join(self, timeout=None):
        """Wait until the thread terminates.

        This blocks the calling thread until the thread whose join() method is
        called terminates -- either normally or through an unhandled exception
        or until the optional timeout occurs.

        When the timeout argument is present and not None, it should be a
        floating point number specifying a timeout for the operation in seconds
        (or fractions thereof). As join() always returns None, you must call
        isAlive() after join() to decide whether a timeout happened -- if the
        thread is still alive, the join() call timed out.

        When the timeout argument is not present or None, the operation will
        block until the thread terminates.

        A thread can be join()ed many times.

        join() raises a RuntimeError if an attempt is made to join the current
        thread as that would cause a deadlock. It is also an error to join() a
        thread before it has been started and attempts to do so raises the same
        exception.

        """
        if not self.__initialized:
            raise RuntimeError("Thread.__init__() not called")
        if not self.__started.is_set():
            raise RuntimeError("cannot join thread before it is started")
        if self is current_thread():
            raise RuntimeError("cannot join current thread")

        if __debug__:
            if not self.__stopped:
                self._note("%s.join(): waiting until thread stops", self)
        self.__block.acquire()
        try:
            if timeout is None:
                while not self.__stopped:
                    self.__block.wait()
                if __debug__:
                    self._note("%s.join(): thread stopped", self)
            else:
                deadline = _time() + timeout
                while not self.__stopped:
                    delay = deadline - _time()
                    if delay <= 0:
                        if __debug__:
                            self._note("%s.join(): timed out", self)
                        break
                    self.__block.wait(delay)
                else:
                    if __debug__:
                        self._note("%s.join(): thread stopped", self)
        finally:
            self.__block.release()

    @property
    def name(self):
        """A string used for identification purposes only.

        It has no semantics. Multiple threads may be given the same name. The
        initial name is set by the constructor.

        """
        assert self.__initialized, "Thread.__init__() not called"
        return self.__name

    @name.setter
    def name(self, name):
        assert self.__initialized, "Thread.__init__() not called"
        self.__name = str(name)

    @property
    def ident(self):
        """Thread identifier of this thread or None if it has not been started.

        This is a nonzero integer. See the thread.get_ident() function. Thread
        identifiers may be recycled when a thread exits and another thread is
        created. The identifier is available even after the thread has exited.

        """
        assert self.__initialized, "Thread.__init__() not called"
        return self.__ident

    def isAlive(self):
        """Return whether the thread is alive.

        This method returns True just before the run() method starts until just
        after the run() method terminates. The module function enumerate()
        returns a list of all alive threads.

        """
        assert self.__initialized, "Thread.__init__() not called"
        return self.__started.is_set() and not self.__stopped

    is_alive = isAlive

    @property
    def daemon(self):
        """A boolean value indicating whether this thread is a daemon thread (True) or not (False).

        This must be set before start() is called, otherwise RuntimeError is
        raised. Its initial value is inherited from the creating thread; the
        main thread is not a daemon thread and therefore all threads created in
        the main thread default to daemon = False.

        The entire Python program exits when no alive non-daemon threads are
        left.

        """
        assert self.__initialized, "Thread.__init__() not called"
        return self.__daemonic

    @daemon.setter
    def daemon(self, daemonic):
        if not self.__initialized:
            raise RuntimeError("Thread.__init__() not called")
        if self.__started.is_set():
            raise RuntimeError("cannot set daemon status of active thread");
        self.__daemonic = daemonic

    def isDaemon(self):
        return self.daemon

    def setDaemon(self, daemonic):
        self.daemon = daemonic

    def getName(self):
        return self.name

    def setName(self, name):
        self.name = name

# The timer class was contributed by Itamar Shtull-Trauring

def Timer(*args, **kwargs):
    """Factory function to create a Timer object.

    Timers call a function after a specified number of seconds:

        t = Timer(30.0, f, args=[], kwargs={})
        t.start()
        t.cancel()     # stop the timer's action if it's still waiting

    """
    return _Timer(*args, **kwargs)

class _Timer(Thread):
    """Call a function after a specified number of seconds:

            t = Timer(30.0, f, args=[], kwargs={})
            t.start()
            t.cancel()     # stop the timer's action if it's still waiting

    """

    def __init__(self, interval, function, args=[], kwargs={}):
        Thread.__init__(self)
        self.interval = interval
        self.function = function
        self.args = args
        self.kwargs = kwargs
        self.finished = Event()

    def cancel(self):
        """Stop the timer if it hasn't finished yet"""
        self.finished.set()

    def run(self):
        self.finished.wait(self.interval)
        if not self.finished.is_set():
            self.function(*self.args, **self.kwargs)
        self.finished.set()

# Special thread class to represent the main thread
# This is garbage collected through an exit handler

class _MainThread(Thread):

    def __init__(self):
        Thread.__init__(self, name="MainThread")
        self._Thread__started.set()
        self._set_ident()
        with _active_limbo_lock:
            _active[_get_ident()] = self

    def _set_daemon(self):
        return False

    def _exitfunc(self):
        self._Thread__stop()
        t = _pickSomeNonDaemonThread()
        if t:
            if __debug__:
                self._note("%s: waiting for other threads", self)
        while t:
            t.join()
            t = _pickSomeNonDaemonThread()
        if __debug__:
            self._note("%s: exiting", self)
        self._Thread__delete()

def _pickSomeNonDaemonThread():
    for t in enumerate():
        if not t.daemon and t.is_alive():
            return t
    return None


# Dummy thread class to represent threads not started here.
# These aren't garbage collected when they die, nor can they be waited for.
# If they invoke anything in threading.py that calls current_thread(), they
# leave an entry in the _active dict forever after.
# Their purpose is to return *something* from current_thread().
# They are marked as daemon threads so we won't wait for them
# when we exit (conform previous semantics).

class _DummyThread(Thread):

    def __init__(self):
        Thread.__init__(self, name=_newname("Dummy-%d"))

        # Thread.__block consumes an OS-level locking primitive, which
        # can never be used by a _DummyThread.  Since a _DummyThread
        # instance is immortal, that's bad, so release this resource.
        del self._Thread__block

        self._Thread__started.set()
        self._set_ident()
        with _active_limbo_lock:
            _active[_get_ident()] = self

    def _set_daemon(self):
        return True

    def join(self, timeout=None):
        assert False, "cannot join a dummy thread"


# Global API functions

def currentThread():
    """Return the current Thread object, corresponding to the caller's thread of control.

    If the caller's thread of control was not created through the threading
    module, a dummy thread object with limited functionality is returned.

    """
    try:
        return _active[_get_ident()]
    except KeyError:
        ##print "current_thread(): no current thread for", _get_ident()
        return _DummyThread()

current_thread = currentThread

def activeCount():
    """Return the number of Thread objects currently alive.

    The returned count is equal to the length of the list returned by
    enumerate().

    """
    with _active_limbo_lock:
        return len(_active) + len(_limbo)

active_count = activeCount

def _enumerate():
    # Same as enumerate(), but without the lock. Internal use only.
    return _active.values() + _limbo.values()

def enumerate():
    """Return a list of all Thread objects currently alive.

    The list includes daemonic threads, dummy thread objects created by
    current_thread(), and the main thread. It excludes terminated threads and
    threads that have not yet been started.

    """
    with _active_limbo_lock:
        return _active.values() + _limbo.values()

from thread import stack_size

# Create the main thread object,
# and make it available for the interpreter
# (Py_Main) as threading._shutdown.

_shutdown = _MainThread()._exitfunc

# get thread-local implementation, either from the thread
# module, or from the python fallback

try:
    from thread import _local as local
except ImportError:
    from _threading_local import local


def _after_fork():
    # This function is called by Python/ceval.c:PyEval_ReInitThreads which
    # is called from PyOS_AfterFork.  Here we cleanup threading module state
    # that should not exist after a fork.

    # Reset _active_limbo_lock, in case we forked while the lock was held
    # by another (non-forked) thread.  http://bugs.python.org/issue874900
    global _active_limbo_lock
    _active_limbo_lock = _allocate_lock()

    # fork() only copied the current thread; clear references to others.
    new_active = {}
    current = current_thread()
    with _active_limbo_lock:
        for thread in _enumerate():
            # Any lock/condition variable may be currently locked or in an
            # invalid state, so we reinitialize them.
            if hasattr(thread, '_reset_internal_locks'):
                thread._reset_internal_locks()
            if thread is current:
                # There is only one active thread. We reset the ident to
                # its new value since it can have changed.
                ident = _get_ident()
                thread._Thread__ident = ident
                new_active[ident] = thread
            else:
                # All the others are already stopped.
                thread._Thread__stop()

        _limbo.clear()
        _active.clear()
        _active.update(new_active)
        assert len(_active) == 1


# Self-test code

def _test():

    class BoundedQueue(_Verbose):

        def __init__(self, limit):
            _Verbose.__init__(self)
            self.mon = RLock()
            self.rc = Condition(self.mon)
            self.wc = Condition(self.mon)
            self.limit = limit
            self.queue = _deque()

        def put(self, item):
            self.mon.acquire()
            while len(self.queue) >= self.limit:
                self._note("put(%s): queue full", item)
                self.wc.wait()
            self.queue.append(item)
            self._note("put(%s): appended, length now %d",
                       item, len(self.queue))
            self.rc.notify()
            self.mon.release()

        def get(self):
            self.mon.acquire()
            while not self.queue:
                self._note("get(): queue empty")
                self.rc.wait()
            item = self.queue.popleft()
            self._note("get(): got %s, %d left", item, len(self.queue))
            self.wc.notify()
            self.mon.release()
            return item

    class ProducerThread(Thread):

        def __init__(self, queue, quota):
            Thread.__init__(self, name="Producer")
            self.queue = queue
            self.quota = quota

        def run(self):
            from random import random
            counter = 0
            while counter < self.quota:
                counter = counter + 1
                self.queue.put("%s.%d" % (self.name, counter))
                _sleep(random() * 0.00001)


    class ConsumerThread(Thread):

        def __init__(self, queue, count):
            Thread.__init__(self, name="Consumer")
            self.queue = queue
            self.count = count

        def run(self):
            while self.count > 0:
                item = self.queue.get()
                print item
                self.count = self.count - 1

    NP = 3
    QL = 4
    NI = 5

    Q = BoundedQueue(QL)
    P = []
    for i in range(NP):
        t = ProducerThread(Q, NI)
        t.name = ("Producer-%d" % (i+1))
        P.append(t)
    C = ConsumerThread(Q, NI*NP)
    for t in P:
        t.start()
        _sleep(0.000001)
    C.start()
    for t in P:
        t.join()
    C.join()

if __name__ == '__main__':
    _test()
