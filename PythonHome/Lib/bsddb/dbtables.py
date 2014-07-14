#-----------------------------------------------------------------------
#
# Copyright (C) 2000, 2001 by Autonomous Zone Industries
# Copyright (C) 2002 Gregory P. Smith
#
# License:      This is free software.  You may use this software for any
#               purpose including modification/redistribution, so long as
#               this header remains intact and that you do not claim any
#               rights of ownership or authorship of this software.  This
#               software has been tested, but no warranty is expressed or
#               implied.
#
#   --  Gregory P. Smith <greg@krypto.org>

# This provides a simple database table interface built on top of
# the Python Berkeley DB 3 interface.
#
_cvsid = '$Id$'

import re
import sys
import copy
import random
import struct


if sys.version_info[0] >= 3 :
    import pickle
else :
    if sys.version_info < (2, 6) :
        import cPickle as pickle
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
            import cPickle as pickle
        finally :
            w.__exit__()
        del w

try:
    # For Pythons w/distutils pybsddb
    from bsddb3 import db
except ImportError:
    # For Python 2.3
    from bsddb import db

class TableDBError(StandardError):
    pass
class TableAlreadyExists(TableDBError):
    pass


class Cond:
    """This condition matches everything"""
    def __call__(self, s):
        return 1

class ExactCond(Cond):
    """Acts as an exact match condition function"""
    def __init__(self, strtomatch):
        self.strtomatch = strtomatch
    def __call__(self, s):
        return s == self.strtomatch

class PrefixCond(Cond):
    """Acts as a condition function for matching a string prefix"""
    def __init__(self, prefix):
        self.prefix = prefix
    def __call__(self, s):
        return s[:len(self.prefix)] == self.prefix

class PostfixCond(Cond):
    """Acts as a condition function for matching a string postfix"""
    def __init__(self, postfix):
        self.postfix = postfix
    def __call__(self, s):
        return s[-len(self.postfix):] == self.postfix

class LikeCond(Cond):
    """
    Acts as a function that will match using an SQL 'LIKE' style
    string.  Case insensitive and % signs are wild cards.
    This isn't perfect but it should work for the simple common cases.
    """
    def __init__(self, likestr, re_flags=re.IGNORECASE):
        # escape python re characters
        chars_to_escape = '.*+()[]?'
        for char in chars_to_escape :
            likestr = likestr.replace(char, '\\'+char)
        # convert %s to wildcards
        self.likestr = likestr.replace('%', '.*')
        self.re = re.compile('^'+self.likestr+'$', re_flags)
    def __call__(self, s):
        return self.re.match(s)

#
# keys used to store database metadata
#
_table_names_key = '__TABLE_NAMES__'  # list of the tables in this db
_columns = '._COLUMNS__'  # table_name+this key contains a list of columns

def _columns_key(table):
    return table + _columns

#
# these keys are found within table sub databases
#
_data =  '._DATA_.'  # this+column+this+rowid key contains table data
_rowid = '._ROWID_.' # this+rowid+this key contains a unique entry for each
                     # row in the table.  (no data is stored)
_rowid_str_len = 8   # length in bytes of the unique rowid strings


def _data_key(table, col, rowid):
    return table + _data + col + _data + rowid

def _search_col_data_key(table, col):
    return table + _data + col + _data

def _search_all_data_key(table):
    return table + _data

def _rowid_key(table, rowid):
    return table + _rowid + rowid + _rowid

def _search_rowid_key(table):
    return table + _rowid

def contains_metastrings(s) :
    """Verify that the given string does not contain any
    metadata strings that might interfere with dbtables database operation.
    """
    if (s.find(_table_names_key) >= 0 or
        s.find(_columns) >= 0 or
        s.find(_data) >= 0 or
        s.find(_rowid) >= 0):
        # Then
        return 1
    else:
        return 0


class bsdTableDB :
    def __init__(self, filename, dbhome, create=0, truncate=0, mode=0600,
                 recover=0, dbflags=0):
        """bsdTableDB(filename, dbhome, create=0, truncate=0, mode=0600)

        Open database name in the dbhome Berkeley DB directory.
        Use keyword arguments when calling this constructor.
        """
        self.db = None
        myflags = db.DB_THREAD
        if create:
            myflags |= db.DB_CREATE
        flagsforenv = (db.DB_INIT_MPOOL | db.DB_INIT_LOCK | db.DB_INIT_LOG |
                       db.DB_INIT_TXN | dbflags)
        # DB_AUTO_COMMIT isn't a valid flag for env.open()
        try:
            dbflags |= db.DB_AUTO_COMMIT
        except AttributeError:
            pass
        if recover:
            flagsforenv = flagsforenv | db.DB_RECOVER
        self.env = db.DBEnv()
        # enable auto deadlock avoidance
        self.env.set_lk_detect(db.DB_LOCK_DEFAULT)
        self.env.open(dbhome, myflags | flagsforenv)
        if truncate:
            myflags |= db.DB_TRUNCATE
        self.db = db.DB(self.env)
        # this code relies on DBCursor.set* methods to raise exceptions
        # rather than returning None
        self.db.set_get_returns_none(1)
        # allow duplicate entries [warning: be careful w/ metadata]
        self.db.set_flags(db.DB_DUP)
        self.db.open(filename, db.DB_BTREE, dbflags | myflags, mode)
        self.dbfilename = filename

        if sys.version_info[0] >= 3 :
            class cursor_py3k(object) :
                def __init__(self, dbcursor) :
                    self._dbcursor = dbcursor

                def close(self) :
                    return self._dbcursor.close()

                def set_range(self, search) :
                    v = self._dbcursor.set_range(bytes(search, "iso8859-1"))
                    if v is not None :
                        v = (v[0].decode("iso8859-1"),
                                v[1].decode("iso8859-1"))
                    return v

                def __next__(self) :
                    v = getattr(self._dbcursor, "next")()
                    if v is not None :
                        v = (v[0].decode("iso8859-1"),
                                v[1].decode("iso8859-1"))
                    return v

            class db_py3k(object) :
                def __init__(self, db) :
                    self._db = db

                def cursor(self, txn=None) :
                    return cursor_py3k(self._db.cursor(txn=txn))

                def has_key(self, key, txn=None) :
                    return getattr(self._db,"has_key")(bytes(key, "iso8859-1"),
                            txn=txn)

                def put(self, key, value, flags=0, txn=None) :
                    key = bytes(key, "iso8859-1")
                    if value is not None :
                        value = bytes(value, "iso8859-1")
                    return self._db.put(key, value, flags=flags, txn=txn)

                def put_bytes(self, key, value, txn=None) :
                    key = bytes(key, "iso8859-1")
                    return self._db.put(key, value, txn=txn)

                def get(self, key, txn=None, flags=0) :
                    key = bytes(key, "iso8859-1")
                    v = self._db.get(key, txn=txn, flags=flags)
                    if v is not None :
                        v = v.decode("iso8859-1")
                    return v

                def get_bytes(self, key, txn=None, flags=0) :
                    key = bytes(key, "iso8859-1")
                    return self._db.get(key, txn=txn, flags=flags)

                def delete(self, key, txn=None) :
                    key = bytes(key, "iso8859-1")
                    return self._db.delete(key, txn=txn)

                def close (self) :
                    return self._db.close()

            self.db = db_py3k(self.db)
        else :  # Python 2.x
            pass

        # Initialize the table names list if this is a new database
        txn = self.env.txn_begin()
        try:
            if not getattr(self.db, "has_key")(_table_names_key, txn):
                getattr(self.db, "put_bytes", self.db.put) \
                        (_table_names_key, pickle.dumps([], 1), txn=txn)
        # Yes, bare except
        except:
            txn.abort()
            raise
        else:
            txn.commit()
        # TODO verify more of the database's metadata?
        self.__tablecolumns = {}

    def __del__(self):
        self.close()

    def close(self):
        if self.db is not None:
            self.db.close()
            self.db = None
        if self.env is not None:
            self.env.close()
            self.env = None

    def checkpoint(self, mins=0):
        self.env.txn_checkpoint(mins)

    def sync(self):
        self.db.sync()

    def _db_print(self) :
        """Print the database to stdout for debugging"""
        print "******** Printing raw database for debugging ********"
        cur = self.db.cursor()
        try:
            key, data = cur.first()
            while 1:
                print repr({key: data})
                next = cur.next()
                if next:
                    key, data = next
                else:
                    cur.close()
                    return
        except db.DBNotFoundError:
            cur.close()


    def CreateTable(self, table, columns):
        """CreateTable(table, columns) - Create a new table in the database.

        raises TableDBError if it already exists or for other DB errors.
        """
        assert isinstance(columns, list)

        txn = None
        try:
            # checking sanity of the table and column names here on
            # table creation will prevent problems elsewhere.
            if contains_metastrings(table):
                raise ValueError(
                    "bad table name: contains reserved metastrings")
            for column in columns :
                if contains_metastrings(column):
                    raise ValueError(
                        "bad column name: contains reserved metastrings")

            columnlist_key = _columns_key(table)
            if getattr(self.db, "has_key")(columnlist_key):
                raise TableAlreadyExists, "table already exists"

            txn = self.env.txn_begin()
            # store the table's column info
            getattr(self.db, "put_bytes", self.db.put)(columnlist_key,
                    pickle.dumps(columns, 1), txn=txn)

            # add the table name to the tablelist
            tablelist = pickle.loads(getattr(self.db, "get_bytes",
                self.db.get) (_table_names_key, txn=txn, flags=db.DB_RMW))
            tablelist.append(table)
            # delete 1st, in case we opened with DB_DUP
            self.db.delete(_table_names_key, txn=txn)
            getattr(self.db, "put_bytes", self.db.put)(_table_names_key,
                    pickle.dumps(tablelist, 1), txn=txn)

            txn.commit()
            txn = None
        except db.DBError, dberror:
            if txn:
                txn.abort()
            if sys.version_info < (2, 6) :
                raise TableDBError, dberror[1]
            else :
                raise TableDBError, dberror.args[1]


    def ListTableColumns(self, table):
        """Return a list of columns in the given table.
        [] if the table doesn't exist.
        """
        assert isinstance(table, str)
        if contains_metastrings(table):
            raise ValueError, "bad table name: contains reserved metastrings"

        columnlist_key = _columns_key(table)
        if not getattr(self.db, "has_key")(columnlist_key):
            return []
        pickledcolumnlist = getattr(self.db, "get_bytes",
                self.db.get)(columnlist_key)
        if pickledcolumnlist:
            return pickle.loads(pickledcolumnlist)
        else:
            return []

    def ListTables(self):
        """Return a list of tables in this database."""
        pickledtablelist = self.db.get_get(_table_names_key)
        if pickledtablelist:
            return pickle.loads(pickledtablelist)
        else:
            return []

    def CreateOrExtendTable(self, table, columns):
        """CreateOrExtendTable(table, columns)

        Create a new table in the database.

        If a table of this name already exists, extend it to have any
        additional columns present in the given list as well as
        all of its current columns.
        """
        assert isinstance(columns, list)

        try:
            self.CreateTable(table, columns)
        except TableAlreadyExists:
            # the table already existed, add any new columns
            txn = None
            try:
                columnlist_key = _columns_key(table)
                txn = self.env.txn_begin()

                # load the current column list
                oldcolumnlist = pickle.loads(
                    getattr(self.db, "get_bytes",
                        self.db.get)(columnlist_key, txn=txn, flags=db.DB_RMW))
                # create a hash table for fast lookups of column names in the
                # loop below
                oldcolumnhash = {}
                for c in oldcolumnlist:
                    oldcolumnhash[c] = c

                # create a new column list containing both the old and new
                # column names
                newcolumnlist = copy.copy(oldcolumnlist)
                for c in columns:
                    if not c in oldcolumnhash:
                        newcolumnlist.append(c)

                # store the table's new extended column list
                if newcolumnlist != oldcolumnlist :
                    # delete the old one first since we opened with DB_DUP
                    self.db.delete(columnlist_key, txn=txn)
                    getattr(self.db, "put_bytes", self.db.put)(columnlist_key,
                                pickle.dumps(newcolumnlist, 1),
                                txn=txn)

                txn.commit()
                txn = None

                self.__load_column_info(table)
            except db.DBError, dberror:
                if txn:
                    txn.abort()
                if sys.version_info < (2, 6) :
                    raise TableDBError, dberror[1]
                else :
                    raise TableDBError, dberror.args[1]


    def __load_column_info(self, table) :
        """initialize the self.__tablecolumns dict"""
        # check the column names
        try:
            tcolpickles = getattr(self.db, "get_bytes",
                    self.db.get)(_columns_key(table))
        except db.DBNotFoundError:
            raise TableDBError, "unknown table: %r" % (table,)
        if not tcolpickles:
            raise TableDBError, "unknown table: %r" % (table,)
        self.__tablecolumns[table] = pickle.loads(tcolpickles)

    def __new_rowid(self, table, txn) :
        """Create a new unique row identifier"""
        unique = 0
        while not unique:
            # Generate a random 64-bit row ID string
            # (note: might have <64 bits of true randomness
            # but it's plenty for our database id needs!)
            blist = []
            for x in xrange(_rowid_str_len):
                blist.append(random.randint(0,255))
            newid = struct.pack('B'*_rowid_str_len, *blist)

            if sys.version_info[0] >= 3 :
                newid = newid.decode("iso8859-1")  # 8 bits

            # Guarantee uniqueness by adding this key to the database
            try:
                self.db.put(_rowid_key(table, newid), None, txn=txn,
                            flags=db.DB_NOOVERWRITE)
            except db.DBKeyExistError:
                pass
            else:
                unique = 1

        return newid


    def Insert(self, table, rowdict) :
        """Insert(table, datadict) - Insert a new row into the table
        using the keys+values from rowdict as the column values.
        """

        txn = None
        try:
            if not getattr(self.db, "has_key")(_columns_key(table)):
                raise TableDBError, "unknown table"

            # check the validity of each column name
            if not table in self.__tablecolumns:
                self.__load_column_info(table)
            for column in rowdict.keys() :
                if not self.__tablecolumns[table].count(column):
                    raise TableDBError, "unknown column: %r" % (column,)

            # get a unique row identifier for this row
            txn = self.env.txn_begin()
            rowid = self.__new_rowid(table, txn=txn)

            # insert the row values into the table database
            for column, dataitem in rowdict.items():
                # store the value
                self.db.put(_data_key(table, column, rowid), dataitem, txn=txn)

            txn.commit()
            txn = None

        except db.DBError, dberror:
            # WIBNI we could just abort the txn and re-raise the exception?
            # But no, because TableDBError is not related to DBError via
            # inheritance, so it would be backwards incompatible.  Do the next
            # best thing.
            info = sys.exc_info()
            if txn:
                txn.abort()
                self.db.delete(_rowid_key(table, rowid))
            if sys.version_info < (2, 6) :
                raise TableDBError, dberror[1], info[2]
            else :
                raise TableDBError, dberror.args[1], info[2]


    def Modify(self, table, conditions={}, mappings={}):
        """Modify(table, conditions={}, mappings={}) - Modify items in rows matching 'conditions' using mapping functions in 'mappings'

        * table - the table name
        * conditions - a dictionary keyed on column names containing
          a condition callable expecting the data string as an
          argument and returning a boolean.
        * mappings - a dictionary keyed on column names containing a
          condition callable expecting the data string as an argument and
          returning the new string for that column.
        """

        try:
            matching_rowids = self.__Select(table, [], conditions)

            # modify only requested columns
            columns = mappings.keys()
            for rowid in matching_rowids.keys():
                txn = None
                try:
                    for column in columns:
                        txn = self.env.txn_begin()
                        # modify the requested column
                        try:
                            dataitem = self.db.get(
                                _data_key(table, column, rowid),
                                txn=txn)
                            self.db.delete(
                                _data_key(table, column, rowid),
                                txn=txn)
                        except db.DBNotFoundError:
                             # XXXXXXX row key somehow didn't exist, assume no
                             # error
                            dataitem = None
                        dataitem = mappings[column](dataitem)
                        if dataitem is not None:
                            self.db.put(
                                _data_key(table, column, rowid),
                                dataitem, txn=txn)
                        txn.commit()
                        txn = None

                # catch all exceptions here since we call unknown callables
                except:
                    if txn:
                        txn.abort()
                    raise

        except db.DBError, dberror:
            if sys.version_info < (2, 6) :
                raise TableDBError, dberror[1]
            else :
                raise TableDBError, dberror.args[1]

    def Delete(self, table, conditions={}):
        """Delete(table, conditions) - Delete items matching the given
        conditions from the table.

        * conditions - a dictionary keyed on column names containing
          condition functions expecting the data string as an
          argument and returning a boolean.
        """

        try:
            matching_rowids = self.__Select(table, [], conditions)

            # delete row data from all columns
            columns = self.__tablecolumns[table]
            for rowid in matching_rowids.keys():
                txn = None
                try:
                    txn = self.env.txn_begin()
                    for column in columns:
                        # delete the data key
                        try:
                            self.db.delete(_data_key(table, column, rowid),
                                           txn=txn)
                        except db.DBNotFoundError:
                            # XXXXXXX column may not exist, assume no error
                            pass

                    try:
                        self.db.delete(_rowid_key(table, rowid), txn=txn)
                    except db.DBNotFoundError:
                        # XXXXXXX row key somehow didn't exist, assume no error
                        pass
                    txn.commit()
                    txn = None
                except db.DBError, dberror:
                    if txn:
                        txn.abort()
                    raise
        except db.DBError, dberror:
            if sys.version_info < (2, 6) :
                raise TableDBError, dberror[1]
            else :
                raise TableDBError, dberror.args[1]


    def Select(self, table, columns, conditions={}):
        """Select(table, columns, conditions) - retrieve specific row data
        Returns a list of row column->value mapping dictionaries.

        * columns - a list of which column data to return.  If
          columns is None, all columns will be returned.
        * conditions - a dictionary keyed on column names
          containing callable conditions expecting the data string as an
          argument and returning a boolean.
        """
        try:
            if not table in self.__tablecolumns:
                self.__load_column_info(table)
            if columns is None:
                columns = self.__tablecolumns[table]
            matching_rowids = self.__Select(table, columns, conditions)
        except db.DBError, dberror:
            if sys.version_info < (2, 6) :
                raise TableDBError, dberror[1]
            else :
                raise TableDBError, dberror.args[1]
        # return the matches as a list of dictionaries
        return matching_rowids.values()


    def __Select(self, table, columns, conditions):
        """__Select() - Used to implement Select and Delete (above)
        Returns a dictionary keyed on rowids containing dicts
        holding the row data for columns listed in the columns param
        that match the given conditions.
        * conditions is a dictionary keyed on column names
        containing callable conditions expecting the data string as an
        argument and returning a boolean.
        """
        # check the validity of each column name
        if not table in self.__tablecolumns:
            self.__load_column_info(table)
        if columns is None:
            columns = self.tablecolumns[table]
        for column in (columns + conditions.keys()):
            if not self.__tablecolumns[table].count(column):
                raise TableDBError, "unknown column: %r" % (column,)

        # keyed on rows that match so far, containings dicts keyed on
        # column names containing the data for that row and column.
        matching_rowids = {}
        # keys are rowids that do not match
        rejected_rowids = {}

        # attempt to sort the conditions in such a way as to minimize full
        # column lookups
        def cmp_conditions(atuple, btuple):
            a = atuple[1]
            b = btuple[1]
            if type(a) is type(b):

                # Needed for python 3. "cmp" vanished in 3.0.1
                def cmp(a, b) :
                    if a==b : return 0
                    if a<b : return -1
                    return 1

                if isinstance(a, PrefixCond) and isinstance(b, PrefixCond):
                    # longest prefix first
                    return cmp(len(b.prefix), len(a.prefix))
                if isinstance(a, LikeCond) and isinstance(b, LikeCond):
                    # longest likestr first
                    return cmp(len(b.likestr), len(a.likestr))
                return 0
            if isinstance(a, ExactCond):
                return -1
            if isinstance(b, ExactCond):
                return 1
            if isinstance(a, PrefixCond):
                return -1
            if isinstance(b, PrefixCond):
                return 1
            # leave all unknown condition callables alone as equals
            return 0

        if sys.version_info < (2, 6) :
            conditionlist = conditions.items()
            conditionlist.sort(cmp_conditions)
        else :  # Insertion Sort. Please, improve
            conditionlist = []
            for i in conditions.items() :
                for j, k in enumerate(conditionlist) :
                    r = cmp_conditions(k, i)
                    if r == 1 :
                        conditionlist.insert(j, i)
                        break
                else :
                    conditionlist.append(i)

        # Apply conditions to column data to find what we want
        cur = self.db.cursor()
        column_num = -1
        for column, condition in conditionlist:
            column_num = column_num + 1
            searchkey = _search_col_data_key(table, column)
            # speedup: don't linear search columns within loop
            if column in columns:
                savethiscolumndata = 1  # save the data for return
            else:
                savethiscolumndata = 0  # data only used for selection

            try:
                key, data = cur.set_range(searchkey)
                while key[:len(searchkey)] == searchkey:
                    # extract the rowid from the key
                    rowid = key[-_rowid_str_len:]

                    if not rowid in rejected_rowids:
                        # if no condition was specified or the condition
                        # succeeds, add row to our match list.
                        if not condition or condition(data):
                            if not rowid in matching_rowids:
                                matching_rowids[rowid] = {}
                            if savethiscolumndata:
                                matching_rowids[rowid][column] = data
                        else:
                            if rowid in matching_rowids:
                                del matching_rowids[rowid]
                            rejected_rowids[rowid] = rowid

                    key, data = cur.next()

            except db.DBError, dberror:
                if dberror.args[0] != db.DB_NOTFOUND:
                    raise
                continue

        cur.close()

        # we're done selecting rows, garbage collect the reject list
        del rejected_rowids

        # extract any remaining desired column data from the
        # database for the matching rows.
        if len(columns) > 0:
            for rowid, rowdata in matching_rowids.items():
                for column in columns:
                    if column in rowdata:
                        continue
                    try:
                        rowdata[column] = self.db.get(
                            _data_key(table, column, rowid))
                    except db.DBError, dberror:
                        if sys.version_info < (2, 6) :
                            if dberror[0] != db.DB_NOTFOUND:
                                raise
                        else :
                            if dberror.args[0] != db.DB_NOTFOUND:
                                raise
                        rowdata[column] = None

        # return the matches
        return matching_rowids


    def Drop(self, table):
        """Remove an entire table from the database"""
        txn = None
        try:
            txn = self.env.txn_begin()

            # delete the column list
            self.db.delete(_columns_key(table), txn=txn)

            cur = self.db.cursor(txn)

            # delete all keys containing this tables column and row info
            table_key = _search_all_data_key(table)
            while 1:
                try:
                    key, data = cur.set_range(table_key)
                except db.DBNotFoundError:
                    break
                # only delete items in this table
                if key[:len(table_key)] != table_key:
                    break
                cur.delete()

            # delete all rowids used by this table
            table_key = _search_rowid_key(table)
            while 1:
                try:
                    key, data = cur.set_range(table_key)
                except db.DBNotFoundError:
                    break
                # only delete items in this table
                if key[:len(table_key)] != table_key:
                    break
                cur.delete()

            cur.close()

            # delete the tablename from the table name list
            tablelist = pickle.loads(
                getattr(self.db, "get_bytes", self.db.get)(_table_names_key,
                    txn=txn, flags=db.DB_RMW))
            try:
                tablelist.remove(table)
            except ValueError:
                # hmm, it wasn't there, oh well, that's what we want.
                pass
            # delete 1st, incase we opened with DB_DUP
            self.db.delete(_table_names_key, txn=txn)
            getattr(self.db, "put_bytes", self.db.put)(_table_names_key,
                    pickle.dumps(tablelist, 1), txn=txn)

            txn.commit()
            txn = None

            if table in self.__tablecolumns:
                del self.__tablecolumns[table]

        except db.DBError, dberror:
            if txn:
                txn.abort()
            raise TableDBError(dberror.args[1])
