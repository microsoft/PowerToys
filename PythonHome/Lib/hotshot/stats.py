"""Statistics analyzer for HotShot."""

import profile
import pstats

import hotshot.log

from hotshot.log import ENTER, EXIT


def load(filename):
    return StatsLoader(filename).load()


class StatsLoader:
    def __init__(self, logfn):
        self._logfn = logfn
        self._code = {}
        self._stack = []
        self.pop_frame = self._stack.pop

    def load(self):
        # The timer selected by the profiler should never be used, so make
        # sure it doesn't work:
        p = Profile()
        p.get_time = _brokentimer
        log = hotshot.log.LogReader(self._logfn)
        taccum = 0
        for event in log:
            what, (filename, lineno, funcname), tdelta = event
            if tdelta > 0:
                taccum += tdelta

            # We multiply taccum to convert from the microseconds we
            # have to the seconds that the profile/pstats module work
            # with; this allows the numbers to have some basis in
            # reality (ignoring calibration issues for now).

            if what == ENTER:
                frame = self.new_frame(filename, lineno, funcname)
                p.trace_dispatch_call(frame, taccum * .000001)
                taccum = 0

            elif what == EXIT:
                frame = self.pop_frame()
                p.trace_dispatch_return(frame, taccum * .000001)
                taccum = 0

            # no further work for line events

        assert not self._stack
        return pstats.Stats(p)

    def new_frame(self, *args):
        # args must be filename, firstlineno, funcname
        # our code objects are cached since we don't need to create
        # new ones every time
        try:
            code = self._code[args]
        except KeyError:
            code = FakeCode(*args)
            self._code[args] = code
        # frame objects are create fresh, since the back pointer will
        # vary considerably
        if self._stack:
            back = self._stack[-1]
        else:
            back = None
        frame = FakeFrame(code, back)
        self._stack.append(frame)
        return frame


class Profile(profile.Profile):
    def simulate_cmd_complete(self):
        pass


class FakeCode:
    def __init__(self, filename, firstlineno, funcname):
        self.co_filename = filename
        self.co_firstlineno = firstlineno
        self.co_name = self.__name__ = funcname


class FakeFrame:
    def __init__(self, code, back):
        self.f_back = back
        self.f_code = code


def _brokentimer():
    raise RuntimeError, "this timer should not be called"
