#------------------------------------------------------------------------
#           Copyright (c) 1997-2001 by Total Control Software
#                         All Rights Reserved
#------------------------------------------------------------------------
#
# Module Name:  dbShelve.py
#
# Description:  A reimplementation of the standard shelve.py that
#               forces the use of cPickle, and DB.
#
# Creation Date:    11/3/97 3:39:04PM
#
# License:      This is free software.  You may use this software for any
#               purpose including modification/redistribution, so long as
#               this header remains intact and that you do not claim any
#               rights of ownership or authorship of this software.  This
#               software has been tested, but no warranty is expressed or
#               implied.
#
# 13-Dec-2000:  Updated to be used with the new bsddb3 package.
#               Added DBShelfCursor class.
#
#------------------------------------------------------------------------

"""Manage shelves of pickled objects using bsddb database files for the
storage.
"""

#------------------------------------------------------------------------

import sys
absolute_import = (sys.version_info[0] >= 3)
if absolute_import :
    # Because this syntaxis is not valid before Python 2.5
    exec("from . import db")
else :
    import db

if sys.version_info[0] >= 3 :
    import cPickle  # Will be converted to "pickle" by "2to3"
else :
    if sys.version_info < (2, 6) :
        import cPickle
    else :
        # When we drop support for python 2.4
        # we could use: (in 2.5 we need a __future__ statement)
        #
        #    with warnings.catch_warnings():
        #        warnings.filterwarnings(...)
        #        ...
        #
        # We can not use "with" as is, because it would be invalid syntax
        # in python 2.4 and (with no __future__) 2.5.
        # Here we simulate "with" following PEP 343 :
        import warnings
        w = warnings.catch_warnings()
        w.__enter__()
        try :
            warnings.filterwarnings('ignore',
                message='the cPickle module has been removed in Python 3.0',
                category=DeprecationWarning)
            import cPickle
        finally :
            w.__exit__()
        del w

HIGHEST_PROTOCOL = cPickle.HIGHEST_PROTOCOL
def _dumps(object, protocol):
    return cPickle.dumps(object, protocol=protocol)

if sys.version_info < (2, 6) :
    from UserDict import DictMixin as MutableMapping
else :
    import collections
    MutableMapping = collections.MutableMapping

#------------------------------------------------------------------------


def open(filename, flags=db.DB_CREATE, mode=0660, filetype=db.DB_HASH,
         dbenv=None, dbname=None):
    """
    A simple factory function for compatibility with the standard
    shleve.py module.  It can be used like this, where key is a string
    and data is a pickleable object:

        from bsddb import dbshelve
        db = dbshelve.open(filename)

        db[key] = data

        db.close()
    """
    if type(flags) == type(''):
        sflag = flags
        if sflag == 'r':
            flags = db.DB_RDONLY
        elif sflag == 'rw':
            flags = 0
        elif sflag == 'w':
            flags =  db.DB_CREATE
        elif sflag == 'c':
            flags =  db.DB_CREATE
        elif sflag == 'n':
            flags = db.DB_TRUNCATE | db.DB_CREATE
        else:
            raise db.DBError, "flags should be one of 'r', 'w', 'c' or 'n' or use the bsddb.db.DB_* flags"

    d = DBShelf(dbenv)
    d.open(filename, dbname, filetype, flags, mode)
    return d

#---------------------------------------------------------------------------

class DBShelveError(db.DBError): pass


class DBShelf(MutableMapping):
    """A shelf to hold pickled objects, built upon a bsddb DB object.  It
    automatically pickles/unpickles data objects going to/from the DB.
    """
    def __init__(self, dbenv=None):
        self.db = db.DB(dbenv)
        self._closed = True
        if HIGHEST_PROTOCOL:
            self.protocol = HIGHEST_PROTOCOL
        else:
            self.protocol = 1


    def __del__(self):
        self.close()


    def __getattr__(self, name):
        """Many methods we can just pass through to the DB object.
        (See below)
        """
        return getattr(self.db, name)


    #-----------------------------------
    # Dictionary access methods

    def __len__(self):
        return len(self.db)


    def __getitem__(self, key):
        data = self.db[key]
        return cPickle.loads(data)


    def __setitem__(self, key, value):
        data = _dumps(value, self.protocol)
        self.db[key] = data


    def __delitem__(self, key):
        del self.db[key]


    def keys(self, txn=None):
        if txn is not None:
            return self.db.keys(txn)
        else:
            return self.db.keys()

    if sys.version_info >= (2, 6) :
        def __iter__(self) :  # XXX: Load all keys in memory :-(
            for k in self.db.keys() :
                yield k

        # Do this when "DB"  support iteration
        # Or is it enough to pass thru "getattr"?
        #
        # def __iter__(self) :
        #    return self.db.__iter__()


    def open(self, *args, **kwargs):
        self.db.open(*args, **kwargs)
        self._closed = False


    def close(self, *args, **kwargs):
        self.db.close(*args, **kwargs)
        self._closed = True


    def __repr__(self):
        if self._closed:
            return '<DBShelf @ 0x%x - closed>' % (id(self))
        else:
            return repr(dict(self.iteritems()))


    def items(self, txn=None):
        if txn is not None:
            items = self.db.items(txn)
        else:
            items = self.db.items()
        newitems = []

        for k, v in items:
            newitems.append( (k, cPickle.loads(v)) )
        return newitems

    def values(self, txn=None):
        if txn is not None:
            values = self.db.values(txn)
        else:
            values = self.db.values()

        return map(cPickle.loads, values)

    #-----------------------------------
    # Other methods

    def __append(self, value, txn=None):
        data = _dumps(value, self.protocol)
        return self.db.append(data, txn)

    def append(self, value, txn=None):
        if self.get_type() == db.DB_RECNO:
            return self.__append(value, txn=txn)
        raise DBShelveError, "append() only supported when dbshelve opened with filetype=dbshelve.db.DB_RECNO"


    def associate(self, secondaryDB, callback, flags=0):
        def _shelf_callback(priKey, priData, realCallback=callback):
            # Safe in Python 2.x because expresion short circuit
            if sys.version_info[0] < 3 or isinstance(priData, bytes) :
                data = cPickle.loads(priData)
            else :
                data = cPickle.loads(bytes(priData, "iso8859-1"))  # 8 bits
            return realCallback(priKey, data)

        return self.db.associate(secondaryDB, _shelf_callback, flags)


    #def get(self, key, default=None, txn=None, flags=0):
    def get(self, *args, **kw):
        # We do it with *args and **kw so if the default value wasn't
        # given nothing is passed to the extension module.  That way
        # an exception can be raised if set_get_returns_none is turned
        # off.
        data = self.db.get(*args, **kw)
        try:
            return cPickle.loads(data)
        except (EOFError, TypeError, cPickle.UnpicklingError):
            return data  # we may be getting the default value, or None,
                         # so it doesn't need unpickled.

    def get_both(self, key, value, txn=None, flags=0):
        data = _dumps(value, self.protocol)
        data = self.db.get(key, data, txn, flags)
        return cPickle.loads(data)


    def cursor(self, txn=None, flags=0):
        c = DBShelfCursor(self.db.cursor(txn, flags))
        c.protocol = self.protocol
        return c


    def put(self, key, value, txn=None, flags=0):
        data = _dumps(value, self.protocol)
        return self.db.put(key, data, txn, flags)


    def join(self, cursorList, flags=0):
        raise NotImplementedError


    #----------------------------------------------
    # Methods allowed to pass-through to self.db
    #
    #    close,  delete, fd, get_byteswapped, get_type, has_key,
    #    key_range, open, remove, rename, stat, sync,
    #    upgrade, verify, and all set_* methods.


#---------------------------------------------------------------------------

class DBShelfCursor:
    """
    """
    def __init__(self, cursor):
        self.dbc = cursor

    def __del__(self):
        self.close()


    def __getattr__(self, name):
        """Some methods we can just pass through to the cursor object.  (See below)"""
        return getattr(self.dbc, name)


    #----------------------------------------------

    def dup(self, flags=0):
        c = DBShelfCursor(self.dbc.dup(flags))
        c.protocol = self.protocol
        return c


    def put(self, key, value, flags=0):
        data = _dumps(value, self.protocol)
        return self.dbc.put(key, data, flags)


    def get(self, *args):
        count = len(args)  # a method overloading hack
        method = getattr(self, 'get_%d' % count)
        method(*args)

    def get_1(self, flags):
        rec = self.dbc.get(flags)
        return self._extract(rec)

    def get_2(self, key, flags):
        rec = self.dbc.get(key, flags)
        return self._extract(rec)

    def get_3(self, key, value, flags):
        data = _dumps(value, self.protocol)
        rec = self.dbc.get(key, flags)
        return self._extract(rec)


    def current(self, flags=0): return self.get_1(flags|db.DB_CURRENT)
    def first(self, flags=0): return self.get_1(flags|db.DB_FIRST)
    def last(self, flags=0): return self.get_1(flags|db.DB_LAST)
    def next(self, flags=0): return self.get_1(flags|db.DB_NEXT)
    def prev(self, flags=0): return self.get_1(flags|db.DB_PREV)
    def consume(self, flags=0): return self.get_1(flags|db.DB_CONSUME)
    def next_dup(self, flags=0): return self.get_1(flags|db.DB_NEXT_DUP)
    def next_nodup(self, flags=0): return self.get_1(flags|db.DB_NEXT_NODUP)
    def prev_nodup(self, flags=0): return self.get_1(flags|db.DB_PREV_NODUP)


    def get_both(self, key, value, flags=0):
        data = _dumps(value, self.protocol)
        rec = self.dbc.get_both(key, flags)
        return self._extract(rec)


    def set(self, key, flags=0):
        rec = self.dbc.set(key, flags)
        return self._extract(rec)

    def set_range(self, key, flags=0):
        rec = self.dbc.set_range(key, flags)
        return self._extract(rec)

    def set_recno(self, recno, flags=0):
        rec = self.dbc.set_recno(recno, flags)
        return self._extract(rec)

    set_both = get_both

    def _extract(self, rec):
        if rec is None:
            return None
        else:
            key, data = rec
            # Safe in Python 2.x because expresion short circuit
            if sys.version_info[0] < 3 or isinstance(data, bytes) :
                return key, cPickle.loads(data)
            else :
                return key, cPickle.loads(bytes(data, "iso8859-1"))  # 8 bits

    #----------------------------------------------
    # Methods allowed to pass-through to self.dbc
    #
    # close, count, delete, get_recno, join_item


#---------------------------------------------------------------------------
