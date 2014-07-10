#------------------------------------------------------------------------
#
# Copyright (C) 2000 Autonomous Zone Industries
#
# License:      This is free software.  You may use this software for any
#               purpose including modification/redistribution, so long as
#               this header remains intact and that you do not claim any
#               rights of ownership or authorship of this software.  This
#               software has been tested, but no warranty is expressed or
#               implied.
#
# Author: Gregory P. Smith <greg@krypto.org>
#
# Note: I don't know how useful this is in reality since when a
#       DBLockDeadlockError happens the current transaction is supposed to be
#       aborted.  If it doesn't then when the operation is attempted again
#       the deadlock is still happening...
#       --Robin
#
#------------------------------------------------------------------------


#
# import the time.sleep function in a namespace safe way to allow
# "from bsddb.dbutils import *"
#
from time import sleep as _sleep

import sys
absolute_import = (sys.version_info[0] >= 3)
if absolute_import :
    # Because this syntaxis is not valid before Python 2.5
    exec("from . import db")
else :
    import db

# always sleep at least N seconds between retrys
_deadlock_MinSleepTime = 1.0/128
# never sleep more than N seconds between retrys
_deadlock_MaxSleepTime = 3.14159

# Assign a file object to this for a "sleeping" message to be written to it
# each retry
_deadlock_VerboseFile = None


def DeadlockWrap(function, *_args, **_kwargs):
    """DeadlockWrap(function, *_args, **_kwargs) - automatically retries
    function in case of a database deadlock.

    This is a function intended to be used to wrap database calls such
    that they perform retrys with exponentially backing off sleeps in
    between when a DBLockDeadlockError exception is raised.

    A 'max_retries' parameter may optionally be passed to prevent it
    from retrying forever (in which case the exception will be reraised).

        d = DB(...)
        d.open(...)
        DeadlockWrap(d.put, "foo", data="bar")  # set key "foo" to "bar"
    """
    sleeptime = _deadlock_MinSleepTime
    max_retries = _kwargs.get('max_retries', -1)
    if 'max_retries' in _kwargs:
        del _kwargs['max_retries']
    while True:
        try:
            return function(*_args, **_kwargs)
        except db.DBLockDeadlockError:
            if _deadlock_VerboseFile:
                _deadlock_VerboseFile.write(
                    'dbutils.DeadlockWrap: sleeping %1.3f\n' % sleeptime)
            _sleep(sleeptime)
            # exponential backoff in the sleep time
            sleeptime *= 2
            if sleeptime > _deadlock_MaxSleepTime:
                sleeptime = _deadlock_MaxSleepTime
            max_retries -= 1
            if max_retries == -1:
                raise


#------------------------------------------------------------------------
