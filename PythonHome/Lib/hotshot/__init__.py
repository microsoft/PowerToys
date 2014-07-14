"""High-perfomance logging profiler, mostly written in C."""

import _hotshot
from _hotshot import ProfilerError

from warnings import warnpy3k as _warnpy3k
_warnpy3k("The 'hotshot' module is not supported in 3.x, "
          "use the 'profile' module instead.", stacklevel=2)

class Profile:
    def __init__(self, logfn, lineevents=0, linetimings=1):
        self.lineevents = lineevents and 1 or 0
        self.linetimings = (linetimings and lineevents) and 1 or 0
        self._prof = p = _hotshot.profiler(
            logfn, self.lineevents, self.linetimings)

        # Attempt to avoid confusing results caused by the presence of
        # Python wrappers around these functions, but only if we can
        # be sure the methods have not been overridden or extended.
        if self.__class__ is Profile:
            self.close = p.close
            self.start = p.start
            self.stop = p.stop
            self.addinfo = p.addinfo

    def close(self):
        """Close the logfile and terminate the profiler."""
        self._prof.close()

    def fileno(self):
        """Return the file descriptor of the profiler's log file."""
        return self._prof.fileno()

    def start(self):
        """Start the profiler."""
        self._prof.start()

    def stop(self):
        """Stop the profiler."""
        self._prof.stop()

    def addinfo(self, key, value):
        """Add an arbitrary labelled value to the profile log."""
        self._prof.addinfo(key, value)

    # These methods offer the same interface as the profile.Profile class,
    # but delegate most of the work to the C implementation underneath.

    def run(self, cmd):
        """Profile an exec-compatible string in the script
        environment.

        The globals from the __main__ module are used as both the
        globals and locals for the script.
        """
        import __main__
        dict = __main__.__dict__
        return self.runctx(cmd, dict, dict)

    def runctx(self, cmd, globals, locals):
        """Evaluate an exec-compatible string in a specific
        environment.

        The string is compiled before profiling begins.
        """
        code = compile(cmd, "<string>", "exec")
        self._prof.runcode(code, globals, locals)
        return self

    def runcall(self, func, *args, **kw):
        """Profile a single call of a callable.

        Additional positional and keyword arguments may be passed
        along; the result of the call is returned, and exceptions are
        allowed to propogate cleanly, while ensuring that profiling is
        disabled on the way out.
        """
        return self._prof.runcall(func, args, kw)
