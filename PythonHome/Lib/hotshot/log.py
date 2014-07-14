import _hotshot
import os.path
import parser
import symbol

from _hotshot import \
     WHAT_ENTER, \
     WHAT_EXIT, \
     WHAT_LINENO, \
     WHAT_DEFINE_FILE, \
     WHAT_DEFINE_FUNC, \
     WHAT_ADD_INFO


__all__ = ["LogReader", "ENTER", "EXIT", "LINE"]


ENTER = WHAT_ENTER
EXIT  = WHAT_EXIT
LINE  = WHAT_LINENO


class LogReader:
    def __init__(self, logfn):
        # fileno -> filename
        self._filemap = {}
        # (fileno, lineno) -> filename, funcname
        self._funcmap = {}

        self._reader = _hotshot.logreader(logfn)
        self._nextitem = self._reader.next
        self._info = self._reader.info
        if 'current-directory' in self._info:
            self.cwd = self._info['current-directory']
        else:
            self.cwd = None

        # This mirrors the call stack of the profiled code as the log
        # is read back in.  It contains tuples of the form:
        #
        #   (file name, line number of function def, function name)
        #
        self._stack = []
        self._append = self._stack.append
        self._pop = self._stack.pop

    def close(self):
        self._reader.close()

    def fileno(self):
        """Return the file descriptor of the log reader's log file."""
        return self._reader.fileno()

    def addinfo(self, key, value):
        """This method is called for each additional ADD_INFO record.

        This can be overridden by applications that want to receive
        these events.  The default implementation does not need to be
        called by alternate implementations.

        The initial set of ADD_INFO records do not pass through this
        mechanism; this is only needed to receive notification when
        new values are added.  Subclasses can inspect self._info after
        calling LogReader.__init__().
        """
        pass

    def get_filename(self, fileno):
        try:
            return self._filemap[fileno]
        except KeyError:
            raise ValueError, "unknown fileno"

    def get_filenames(self):
        return self._filemap.values()

    def get_fileno(self, filename):
        filename = os.path.normcase(os.path.normpath(filename))
        for fileno, name in self._filemap.items():
            if name == filename:
                return fileno
        raise ValueError, "unknown filename"

    def get_funcname(self, fileno, lineno):
        try:
            return self._funcmap[(fileno, lineno)]
        except KeyError:
            raise ValueError, "unknown function location"

    # Iteration support:
    # This adds an optional (& ignored) parameter to next() so that the
    # same bound method can be used as the __getitem__() method -- this
    # avoids using an additional method call which kills the performance.

    def next(self, index=0):
        while 1:
            # This call may raise StopIteration:
            what, tdelta, fileno, lineno = self._nextitem()

            # handle the most common cases first

            if what == WHAT_ENTER:
                filename, funcname = self._decode_location(fileno, lineno)
                t = (filename, lineno, funcname)
                self._append(t)
                return what, t, tdelta

            if what == WHAT_EXIT:
                try:
                    return what, self._pop(), tdelta
                except IndexError:
                    raise StopIteration

            if what == WHAT_LINENO:
                filename, firstlineno, funcname = self._stack[-1]
                return what, (filename, lineno, funcname), tdelta

            if what == WHAT_DEFINE_FILE:
                filename = os.path.normcase(os.path.normpath(tdelta))
                self._filemap[fileno] = filename
            elif what == WHAT_DEFINE_FUNC:
                filename = self._filemap[fileno]
                self._funcmap[(fileno, lineno)] = (filename, tdelta)
            elif what == WHAT_ADD_INFO:
                # value already loaded into self.info; call the
                # overridable addinfo() handler so higher-level code
                # can pick up the new value
                if tdelta == 'current-directory':
                    self.cwd = lineno
                self.addinfo(tdelta, lineno)
            else:
                raise ValueError, "unknown event type"

    def __iter__(self):
        return self

    #
    #  helpers
    #

    def _decode_location(self, fileno, lineno):
        try:
            return self._funcmap[(fileno, lineno)]
        except KeyError:
            #
            # This should only be needed when the log file does not
            # contain all the DEFINE_FUNC records needed to allow the
            # function name to be retrieved from the log file.
            #
            if self._loadfile(fileno):
                filename = funcname = None
            try:
                filename, funcname = self._funcmap[(fileno, lineno)]
            except KeyError:
                filename = self._filemap.get(fileno)
                funcname = None
                self._funcmap[(fileno, lineno)] = (filename, funcname)
        return filename, funcname

    def _loadfile(self, fileno):
        try:
            filename = self._filemap[fileno]
        except KeyError:
            print "Could not identify fileId", fileno
            return 1
        if filename is None:
            return 1
        absname = os.path.normcase(os.path.join(self.cwd, filename))

        try:
            fp = open(absname)
        except IOError:
            return
        st = parser.suite(fp.read())
        fp.close()

        # Scan the tree looking for def and lambda nodes, filling in
        # self._funcmap with all the available information.
        funcdef = symbol.funcdef
        lambdef = symbol.lambdef

        stack = [st.totuple(1)]

        while stack:
            tree = stack.pop()
            try:
                sym = tree[0]
            except (IndexError, TypeError):
                continue
            if sym == funcdef:
                self._funcmap[(fileno, tree[2][2])] = filename, tree[2][1]
            elif sym == lambdef:
                self._funcmap[(fileno, tree[1][2])] = filename, "<lambda>"
            stack.extend(list(tree[1:]))
