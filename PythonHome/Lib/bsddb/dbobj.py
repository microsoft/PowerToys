#-------------------------------------------------------------------------
#  This file contains real Python object wrappers for DB and DBEnv
#  C "objects" that can be usefully subclassed.  The previous SWIG
#  based interface allowed this thanks to SWIG's shadow classes.
#   --  Gregory P. Smith
#-------------------------------------------------------------------------
#
# (C) Copyright 2001  Autonomous Zone Industries
#
# License:  This is free software.  You may use this software for any
#           purpose including modification/redistribution, so long as
#           this header remains intact and that you do not claim any
#           rights of ownership or authorship of this software.  This
#           software has been tested, but no warranty is expressed or
#           implied.
#

#
# TODO it would be *really nice* to have an automatic shadow class populator
# so that new methods don't need to be added  here manually after being
# added to _bsddb.c.
#

import sys
absolute_import = (sys.version_info[0] >= 3)
if absolute_import :
    # Because this syntaxis is not valid before Python 2.5
    exec("from . import db")
else :
    import db

if sys.version_info < (2, 6) :
    from UserDict import DictMixin as MutableMapping
else :
    import collections
    MutableMapping = collections.MutableMapping

class DBEnv:
    def __init__(self, *args, **kwargs):
        self._cobj = db.DBEnv(*args, **kwargs)

    def close(self, *args, **kwargs):
        return self._cobj.close(*args, **kwargs)
    def open(self, *args, **kwargs):
        return self._cobj.open(*args, **kwargs)
    def remove(self, *args, **kwargs):
        return self._cobj.remove(*args, **kwargs)
    def set_shm_key(self, *args, **kwargs):
        return self._cobj.set_shm_key(*args, **kwargs)
    def set_cachesize(self, *args, **kwargs):
        return self._cobj.set_cachesize(*args, **kwargs)
    def set_data_dir(self, *args, **kwargs):
        return self._cobj.set_data_dir(*args, **kwargs)
    def set_flags(self, *args, **kwargs):
        return self._cobj.set_flags(*args, **kwargs)
    def set_lg_bsize(self, *args, **kwargs):
        return self._cobj.set_lg_bsize(*args, **kwargs)
    def set_lg_dir(self, *args, **kwargs):
        return self._cobj.set_lg_dir(*args, **kwargs)
    def set_lg_max(self, *args, **kwargs):
        return self._cobj.set_lg_max(*args, **kwargs)
    def set_lk_detect(self, *args, **kwargs):
        return self._cobj.set_lk_detect(*args, **kwargs)
    if db.version() < (4,5):
        def set_lk_max(self, *args, **kwargs):
            return self._cobj.set_lk_max(*args, **kwargs)
    def set_lk_max_locks(self, *args, **kwargs):
        return self._cobj.set_lk_max_locks(*args, **kwargs)
    def set_lk_max_lockers(self, *args, **kwargs):
        return self._cobj.set_lk_max_lockers(*args, **kwargs)
    def set_lk_max_objects(self, *args, **kwargs):
        return self._cobj.set_lk_max_objects(*args, **kwargs)
    def set_mp_mmapsize(self, *args, **kwargs):
        return self._cobj.set_mp_mmapsize(*args, **kwargs)
    def set_timeout(self, *args, **kwargs):
        return self._cobj.set_timeout(*args, **kwargs)
    def set_tmp_dir(self, *args, **kwargs):
        return self._cobj.set_tmp_dir(*args, **kwargs)
    def txn_begin(self, *args, **kwargs):
        return self._cobj.txn_begin(*args, **kwargs)
    def txn_checkpoint(self, *args, **kwargs):
        return self._cobj.txn_checkpoint(*args, **kwargs)
    def txn_stat(self, *args, **kwargs):
        return self._cobj.txn_stat(*args, **kwargs)
    def set_tx_max(self, *args, **kwargs):
        return self._cobj.set_tx_max(*args, **kwargs)
    def set_tx_timestamp(self, *args, **kwargs):
        return self._cobj.set_tx_timestamp(*args, **kwargs)
    def lock_detect(self, *args, **kwargs):
        return self._cobj.lock_detect(*args, **kwargs)
    def lock_get(self, *args, **kwargs):
        return self._cobj.lock_get(*args, **kwargs)
    def lock_id(self, *args, **kwargs):
        return self._cobj.lock_id(*args, **kwargs)
    def lock_put(self, *args, **kwargs):
        return self._cobj.lock_put(*args, **kwargs)
    def lock_stat(self, *args, **kwargs):
        return self._cobj.lock_stat(*args, **kwargs)
    def log_archive(self, *args, **kwargs):
        return self._cobj.log_archive(*args, **kwargs)

    def set_get_returns_none(self, *args, **kwargs):
        return self._cobj.set_get_returns_none(*args, **kwargs)

    def log_stat(self, *args, **kwargs):
        return self._cobj.log_stat(*args, **kwargs)

    def dbremove(self, *args, **kwargs):
        return self._cobj.dbremove(*args, **kwargs)
    def dbrename(self, *args, **kwargs):
        return self._cobj.dbrename(*args, **kwargs)
    def set_encrypt(self, *args, **kwargs):
        return self._cobj.set_encrypt(*args, **kwargs)

    if db.version() >= (4,4):
        def fileid_reset(self, *args, **kwargs):
            return self._cobj.fileid_reset(*args, **kwargs)

        def lsn_reset(self, *args, **kwargs):
            return self._cobj.lsn_reset(*args, **kwargs)


class DB(MutableMapping):
    def __init__(self, dbenv, *args, **kwargs):
        # give it the proper DBEnv C object that its expecting
        self._cobj = db.DB(*((dbenv._cobj,) + args), **kwargs)

    # TODO are there other dict methods that need to be overridden?
    def __len__(self):
        return len(self._cobj)
    def __getitem__(self, arg):
        return self._cobj[arg]
    def __setitem__(self, key, value):
        self._cobj[key] = value
    def __delitem__(self, arg):
        del self._cobj[arg]

    if sys.version_info >= (2, 6) :
        def __iter__(self) :
            return self._cobj.__iter__()

    def append(self, *args, **kwargs):
        return self._cobj.append(*args, **kwargs)
    def associate(self, *args, **kwargs):
        return self._cobj.associate(*args, **kwargs)
    def close(self, *args, **kwargs):
        return self._cobj.close(*args, **kwargs)
    def consume(self, *args, **kwargs):
        return self._cobj.consume(*args, **kwargs)
    def consume_wait(self, *args, **kwargs):
        return self._cobj.consume_wait(*args, **kwargs)
    def cursor(self, *args, **kwargs):
        return self._cobj.cursor(*args, **kwargs)
    def delete(self, *args, **kwargs):
        return self._cobj.delete(*args, **kwargs)
    def fd(self, *args, **kwargs):
        return self._cobj.fd(*args, **kwargs)
    def get(self, *args, **kwargs):
        return self._cobj.get(*args, **kwargs)
    def pget(self, *args, **kwargs):
        return self._cobj.pget(*args, **kwargs)
    def get_both(self, *args, **kwargs):
        return self._cobj.get_both(*args, **kwargs)
    def get_byteswapped(self, *args, **kwargs):
        return self._cobj.get_byteswapped(*args, **kwargs)
    def get_size(self, *args, **kwargs):
        return self._cobj.get_size(*args, **kwargs)
    def get_type(self, *args, **kwargs):
        return self._cobj.get_type(*args, **kwargs)
    def join(self, *args, **kwargs):
        return self._cobj.join(*args, **kwargs)
    def key_range(self, *args, **kwargs):
        return self._cobj.key_range(*args, **kwargs)
    def has_key(self, *args, **kwargs):
        return self._cobj.has_key(*args, **kwargs)
    def items(self, *args, **kwargs):
        return self._cobj.items(*args, **kwargs)
    def keys(self, *args, **kwargs):
        return self._cobj.keys(*args, **kwargs)
    def open(self, *args, **kwargs):
        return self._cobj.open(*args, **kwargs)
    def put(self, *args, **kwargs):
        return self._cobj.put(*args, **kwargs)
    def remove(self, *args, **kwargs):
        return self._cobj.remove(*args, **kwargs)
    def rename(self, *args, **kwargs):
        return self._cobj.rename(*args, **kwargs)
    def set_bt_minkey(self, *args, **kwargs):
        return self._cobj.set_bt_minkey(*args, **kwargs)
    def set_bt_compare(self, *args, **kwargs):
        return self._cobj.set_bt_compare(*args, **kwargs)
    def set_cachesize(self, *args, **kwargs):
        return self._cobj.set_cachesize(*args, **kwargs)
    def set_dup_compare(self, *args, **kwargs) :
        return self._cobj.set_dup_compare(*args, **kwargs)
    def set_flags(self, *args, **kwargs):
        return self._cobj.set_flags(*args, **kwargs)
    def set_h_ffactor(self, *args, **kwargs):
        return self._cobj.set_h_ffactor(*args, **kwargs)
    def set_h_nelem(self, *args, **kwargs):
        return self._cobj.set_h_nelem(*args, **kwargs)
    def set_lorder(self, *args, **kwargs):
        return self._cobj.set_lorder(*args, **kwargs)
    def set_pagesize(self, *args, **kwargs):
        return self._cobj.set_pagesize(*args, **kwargs)
    def set_re_delim(self, *args, **kwargs):
        return self._cobj.set_re_delim(*args, **kwargs)
    def set_re_len(self, *args, **kwargs):
        return self._cobj.set_re_len(*args, **kwargs)
    def set_re_pad(self, *args, **kwargs):
        return self._cobj.set_re_pad(*args, **kwargs)
    def set_re_source(self, *args, **kwargs):
        return self._cobj.set_re_source(*args, **kwargs)
    def set_q_extentsize(self, *args, **kwargs):
        return self._cobj.set_q_extentsize(*args, **kwargs)
    def stat(self, *args, **kwargs):
        return self._cobj.stat(*args, **kwargs)
    def sync(self, *args, **kwargs):
        return self._cobj.sync(*args, **kwargs)
    def type(self, *args, **kwargs):
        return self._cobj.type(*args, **kwargs)
    def upgrade(self, *args, **kwargs):
        return self._cobj.upgrade(*args, **kwargs)
    def values(self, *args, **kwargs):
        return self._cobj.values(*args, **kwargs)
    def verify(self, *args, **kwargs):
        return self._cobj.verify(*args, **kwargs)
    def set_get_returns_none(self, *args, **kwargs):
        return self._cobj.set_get_returns_none(*args, **kwargs)

    def set_encrypt(self, *args, **kwargs):
        return self._cobj.set_encrypt(*args, **kwargs)


class DBSequence:
    def __init__(self, *args, **kwargs):
        self._cobj = db.DBSequence(*args, **kwargs)

    def close(self, *args, **kwargs):
        return self._cobj.close(*args, **kwargs)
    def get(self, *args, **kwargs):
        return self._cobj.get(*args, **kwargs)
    def get_dbp(self, *args, **kwargs):
        return self._cobj.get_dbp(*args, **kwargs)
    def get_key(self, *args, **kwargs):
        return self._cobj.get_key(*args, **kwargs)
    def init_value(self, *args, **kwargs):
        return self._cobj.init_value(*args, **kwargs)
    def open(self, *args, **kwargs):
        return self._cobj.open(*args, **kwargs)
    def remove(self, *args, **kwargs):
        return self._cobj.remove(*args, **kwargs)
    def stat(self, *args, **kwargs):
        return self._cobj.stat(*args, **kwargs)
    def set_cachesize(self, *args, **kwargs):
        return self._cobj.set_cachesize(*args, **kwargs)
    def set_flags(self, *args, **kwargs):
        return self._cobj.set_flags(*args, **kwargs)
    def set_range(self, *args, **kwargs):
        return self._cobj.set_range(*args, **kwargs)
    def get_cachesize(self, *args, **kwargs):
        return self._cobj.get_cachesize(*args, **kwargs)
    def get_flags(self, *args, **kwargs):
        return self._cobj.get_flags(*args, **kwargs)
    def get_range(self, *args, **kwargs):
        return self._cobj.get_range(*args, **kwargs)
