#----------------------------------------------------------------------
#  Copyright (c) 1999-2001, Digital Creations, Fredericksburg, VA, USA
#  and Andrew Kuchling. All rights reserved.
#
#  Redistribution and use in source and binary forms, with or without
#  modification, are permitted provided that the following conditions are
#  met:
#
#    o Redistributions of source code must retain the above copyright
#      notice, this list of conditions, and the disclaimer that follows.
#
#    o Redistributions in binary form must reproduce the above copyright
#      notice, this list of conditions, and the following disclaimer in
#      the documentation and/or other materials provided with the
#      distribution.
#
#    o Neither the name of Digital Creations nor the names of its
#      contributors may be used to endorse or promote products derived
#      from this software without specific prior written permission.
#
#  THIS SOFTWARE IS PROVIDED BY DIGITAL CREATIONS AND CONTRIBUTORS *AS
#  IS* AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
#  TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
#  PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL DIGITAL
#  CREATIONS OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
#  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
#  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS
#  OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
#  ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR
#  TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE
#  USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH
#  DAMAGE.
#----------------------------------------------------------------------


"""Support for Berkeley DB 4.3 through 5.3 with a simple interface.

For the full featured object oriented interface use the bsddb.db module
instead.  It mirrors the Oracle Berkeley DB C API.
"""

import sys
absolute_import = (sys.version_info[0] >= 3)

if (sys.version_info >= (2, 6)) and (sys.version_info < (3, 0)) :
    import warnings
    if sys.py3kwarning and (__name__ != 'bsddb3') :
        warnings.warnpy3k("in 3.x, the bsddb module has been removed; "
                          "please use the pybsddb project instead",
                          DeprecationWarning, 2)
    warnings.filterwarnings("ignore", ".*CObject.*", DeprecationWarning,
                            "bsddb.__init__")

try:
    if __name__ == 'bsddb3':
        # import _pybsddb binary as it should be the more recent version from
        # a standalone pybsddb addon package than the version included with
        # python as bsddb._bsddb.
        if absolute_import :
            # Because this syntaxis is not valid before Python 2.5
            exec("from . import _pybsddb")
        else :
            import _pybsddb
        _bsddb = _pybsddb
        from bsddb3.dbutils import DeadlockWrap as _DeadlockWrap
    else:
        import _bsddb
        from bsddb.dbutils import DeadlockWrap as _DeadlockWrap
except ImportError:
    # Remove ourselves from sys.modules
    import sys
    del sys.modules[__name__]
    raise

# bsddb3 calls it db, but provide _db for backwards compatibility
db = _db = _bsddb
__version__ = db.__version__

error = db.DBError  # So bsddb.error will mean something...

#----------------------------------------------------------------------

import sys, os

from weakref import ref

if sys.version_info < (2, 6) :
    import UserDict
    MutableMapping = UserDict.DictMixin
else :
    import collections
    MutableMapping = collections.MutableMapping

class _iter_mixin(MutableMapping):
    def _make_iter_cursor(self):
        cur = _DeadlockWrap(self.db.cursor)
        key = id(cur)
        self._cursor_refs[key] = ref(cur, self._gen_cref_cleaner(key))
        return cur

    def _gen_cref_cleaner(self, key):
        # use generate the function for the weakref callback here
        # to ensure that we do not hold a strict reference to cur
        # in the callback.
        return lambda ref: self._cursor_refs.pop(key, None)

    def __iter__(self):
        self._kill_iteration = False
        self._in_iter += 1
        try:
            try:
                cur = self._make_iter_cursor()

                # FIXME-20031102-greg: race condition.  cursor could
                # be closed by another thread before this call.

                # since we're only returning keys, we call the cursor
                # methods with flags=0, dlen=0, dofs=0
                key = _DeadlockWrap(cur.first, 0,0,0)[0]
                yield key

                next = getattr(cur, "next")
                while 1:
                    try:
                        key = _DeadlockWrap(next, 0,0,0)[0]
                        yield key
                    except _bsddb.DBCursorClosedError:
                        if self._kill_iteration:
                            raise RuntimeError('Database changed size '
                                               'during iteration.')
                        cur = self._make_iter_cursor()
                        # FIXME-20031101-greg: race condition.  cursor could
                        # be closed by another thread before this call.
                        _DeadlockWrap(cur.set, key,0,0,0)
                        next = getattr(cur, "next")
            except _bsddb.DBNotFoundError:
                pass
            except _bsddb.DBCursorClosedError:
                # the database was modified during iteration.  abort.
                pass
# When Python 2.4 not supported in bsddb3, we can change this to "finally"
        except :
            self._in_iter -= 1
            raise

        self._in_iter -= 1

    def iteritems(self):
        if not self.db:
            return
        self._kill_iteration = False
        self._in_iter += 1
        try:
            try:
                cur = self._make_iter_cursor()

                # FIXME-20031102-greg: race condition.  cursor could
                # be closed by another thread before this call.

                kv = _DeadlockWrap(cur.first)
                key = kv[0]
                yield kv

                next = getattr(cur, "next")
                while 1:
                    try:
                        kv = _DeadlockWrap(next)
                        key = kv[0]
                        yield kv
                    except _bsddb.DBCursorClosedError:
                        if self._kill_iteration:
                            raise RuntimeError('Database changed size '
                                               'during iteration.')
                        cur = self._make_iter_cursor()
                        # FIXME-20031101-greg: race condition.  cursor could
                        # be closed by another thread before this call.
                        _DeadlockWrap(cur.set, key,0,0,0)
                        next = getattr(cur, "next")
            except _bsddb.DBNotFoundError:
                pass
            except _bsddb.DBCursorClosedError:
                # the database was modified during iteration.  abort.
                pass
# When Python 2.4 not supported in bsddb3, we can change this to "finally"
        except :
            self._in_iter -= 1
            raise

        self._in_iter -= 1


class _DBWithCursor(_iter_mixin):
    """
    A simple wrapper around DB that makes it look like the bsddbobject in
    the old module.  It uses a cursor as needed to provide DB traversal.
    """
    def __init__(self, db):
        self.db = db
        self.db.set_get_returns_none(0)

        # FIXME-20031101-greg: I believe there is still the potential
        # for deadlocks in a multithreaded environment if someone
        # attempts to use the any of the cursor interfaces in one
        # thread while doing a put or delete in another thread.  The
        # reason is that _checkCursor and _closeCursors are not atomic
        # operations.  Doing our own locking around self.dbc,
        # self.saved_dbc_key and self._cursor_refs could prevent this.
        # TODO: A test case demonstrating the problem needs to be written.

        # self.dbc is a DBCursor object used to implement the
        # first/next/previous/last/set_location methods.
        self.dbc = None
        self.saved_dbc_key = None

        # a collection of all DBCursor objects currently allocated
        # by the _iter_mixin interface.
        self._cursor_refs = {}
        self._in_iter = 0
        self._kill_iteration = False

    def __del__(self):
        self.close()

    def _checkCursor(self):
        if self.dbc is None:
            self.dbc = _DeadlockWrap(self.db.cursor)
            if self.saved_dbc_key is not None:
                _DeadlockWrap(self.dbc.set, self.saved_dbc_key)
                self.saved_dbc_key = None

    # This method is needed for all non-cursor DB calls to avoid
    # Berkeley DB deadlocks (due to being opened with DB_INIT_LOCK
    # and DB_THREAD to be thread safe) when intermixing database
    # operations that use the cursor internally with those that don't.
    def _closeCursors(self, save=1):
        if self.dbc:
            c = self.dbc
            self.dbc = None
            if save:
                try:
                    self.saved_dbc_key = _DeadlockWrap(c.current, 0,0,0)[0]
                except db.DBError:
                    pass
            _DeadlockWrap(c.close)
            del c
        for cref in self._cursor_refs.values():
            c = cref()
            if c is not None:
                _DeadlockWrap(c.close)

    def _checkOpen(self):
        if self.db is None:
            raise error, "BSDDB object has already been closed"

    def isOpen(self):
        return self.db is not None

    def __len__(self):
        self._checkOpen()
        return _DeadlockWrap(lambda: len(self.db))  # len(self.db)

    if sys.version_info >= (2, 6) :
        def __repr__(self) :
            if self.isOpen() :
                return repr(dict(_DeadlockWrap(self.db.items)))
            return repr(dict())

    def __getitem__(self, key):
        self._checkOpen()
        return _DeadlockWrap(lambda: self.db[key])  # self.db[key]

    def __setitem__(self, key, value):
        self._checkOpen()
        self._closeCursors()
        if self._in_iter and key not in self:
            self._kill_iteration = True
        def wrapF():
            self.db[key] = value
        _DeadlockWrap(wrapF)  # self.db[key] = value

    def __delitem__(self, key):
        self._checkOpen()
        self._closeCursors()
        if self._in_iter and key in self:
            self._kill_iteration = True
        def wrapF():
            del self.db[key]
        _DeadlockWrap(wrapF)  # del self.db[key]

    def close(self):
        self._closeCursors(save=0)
        if self.dbc is not None:
            _DeadlockWrap(self.dbc.close)
        v = 0
        if self.db is not None:
            v = _DeadlockWrap(self.db.close)
        self.dbc = None
        self.db = None
        return v

    def keys(self):
        self._checkOpen()
        return _DeadlockWrap(self.db.keys)

    def has_key(self, key):
        self._checkOpen()
        return _DeadlockWrap(self.db.has_key, key)

    def set_location(self, key):
        self._checkOpen()
        self._checkCursor()
        return _DeadlockWrap(self.dbc.set_range, key)

    def next(self):  # Renamed by "2to3"
        self._checkOpen()
        self._checkCursor()
        rv = _DeadlockWrap(getattr(self.dbc, "next"))
        return rv

    if sys.version_info[0] >= 3 :  # For "2to3" conversion
        next = __next__

    def previous(self):
        self._checkOpen()
        self._checkCursor()
        rv = _DeadlockWrap(self.dbc.prev)
        return rv

    def first(self):
        self._checkOpen()
        # fix 1725856: don't needlessly try to restore our cursor position
        self.saved_dbc_key = None
        self._checkCursor()
        rv = _DeadlockWrap(self.dbc.first)
        return rv

    def last(self):
        self._checkOpen()
        # fix 1725856: don't needlessly try to restore our cursor position
        self.saved_dbc_key = None
        self._checkCursor()
        rv = _DeadlockWrap(self.dbc.last)
        return rv

    def sync(self):
        self._checkOpen()
        return _DeadlockWrap(self.db.sync)


#----------------------------------------------------------------------
# Compatibility object factory functions

def hashopen(file, flag='c', mode=0666, pgsize=None, ffactor=None, nelem=None,
            cachesize=None, lorder=None, hflags=0):

    flags = _checkflag(flag, file)
    e = _openDBEnv(cachesize)
    d = db.DB(e)
    d.set_flags(hflags)
    if pgsize is not None:    d.set_pagesize(pgsize)
    if lorder is not None:    d.set_lorder(lorder)
    if ffactor is not None:   d.set_h_ffactor(ffactor)
    if nelem is not None:     d.set_h_nelem(nelem)
    d.open(file, db.DB_HASH, flags, mode)
    return _DBWithCursor(d)

#----------------------------------------------------------------------

def btopen(file, flag='c', mode=0666,
            btflags=0, cachesize=None, maxkeypage=None, minkeypage=None,
            pgsize=None, lorder=None):

    flags = _checkflag(flag, file)
    e = _openDBEnv(cachesize)
    d = db.DB(e)
    if pgsize is not None: d.set_pagesize(pgsize)
    if lorder is not None: d.set_lorder(lorder)
    d.set_flags(btflags)
    if minkeypage is not None: d.set_bt_minkey(minkeypage)
    if maxkeypage is not None: d.set_bt_maxkey(maxkeypage)
    d.open(file, db.DB_BTREE, flags, mode)
    return _DBWithCursor(d)

#----------------------------------------------------------------------


def rnopen(file, flag='c', mode=0666,
            rnflags=0, cachesize=None, pgsize=None, lorder=None,
            rlen=None, delim=None, source=None, pad=None):

    flags = _checkflag(flag, file)
    e = _openDBEnv(cachesize)
    d = db.DB(e)
    if pgsize is not None: d.set_pagesize(pgsize)
    if lorder is not None: d.set_lorder(lorder)
    d.set_flags(rnflags)
    if delim is not None: d.set_re_delim(delim)
    if rlen is not None: d.set_re_len(rlen)
    if source is not None: d.set_re_source(source)
    if pad is not None: d.set_re_pad(pad)
    d.open(file, db.DB_RECNO, flags, mode)
    return _DBWithCursor(d)

#----------------------------------------------------------------------

def _openDBEnv(cachesize):
    e = db.DBEnv()
    if cachesize is not None:
        if cachesize >= 20480:
            e.set_cachesize(0, cachesize)
        else:
            raise error, "cachesize must be >= 20480"
    e.set_lk_detect(db.DB_LOCK_DEFAULT)
    e.open('.', db.DB_PRIVATE | db.DB_CREATE | db.DB_THREAD | db.DB_INIT_LOCK | db.DB_INIT_MPOOL)
    return e

def _checkflag(flag, file):
    if flag == 'r':
        flags = db.DB_RDONLY
    elif flag == 'rw':
        flags = 0
    elif flag == 'w':
        flags =  db.DB_CREATE
    elif flag == 'c':
        flags =  db.DB_CREATE
    elif flag == 'n':
        flags = db.DB_CREATE
        #flags = db.DB_CREATE | db.DB_TRUNCATE
        # we used db.DB_TRUNCATE flag for this before but Berkeley DB
        # 4.2.52 changed to disallowed truncate with txn environments.
        if file is not None and os.path.isfile(file):
            os.unlink(file)
    else:
        raise error, "flags should be one of 'r', 'w', 'c' or 'n'"
    return flags | db.DB_THREAD

#----------------------------------------------------------------------


# This is a silly little hack that allows apps to continue to use the
# DB_THREAD flag even on systems without threads without freaking out
# Berkeley DB.
#
# This assumes that if Python was built with thread support then
# Berkeley DB was too.

try:
    # 2to3 automatically changes "import thread" to "import _thread"
    import thread as T
    del T

except ImportError:
    db.DB_THREAD = 0

#----------------------------------------------------------------------
