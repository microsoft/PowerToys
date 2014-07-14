"""Create new objects of various types.  Deprecated.

This module is no longer required except for backward compatibility.
Objects of most types can now be created by calling the type object.
"""
from warnings import warnpy3k
warnpy3k("The 'new' module has been removed in Python 3.0; use the 'types' "
            "module instead.", stacklevel=2)
del warnpy3k

from types import ClassType as classobj
from types import FunctionType as function
from types import InstanceType as instance
from types import MethodType as instancemethod
from types import ModuleType as module

from types import CodeType as code
