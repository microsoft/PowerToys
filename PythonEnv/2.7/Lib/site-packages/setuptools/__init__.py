"""Extensions to the 'distutils' for large or complex distributions"""
from setuptools.extension import Extension, Library
from setuptools.dist import Distribution, Feature, _get_unpatched
import distutils.core, setuptools.command
from setuptools.depends import Require
from distutils.core import Command as _Command
from distutils.util import convert_path
import os
import sys

__version__ = '0.9.8'
__all__ = [
    'setup', 'Distribution', 'Feature', 'Command', 'Extension', 'Require',
    'find_packages'
]

bootstrap_install_from = None

# If we run 2to3 on .py files, should we also convert docstrings?
# Default: yes; assume that we can detect doctests reliably
run_2to3_on_doctests = True
# Standard package names for fixer packages
lib2to3_fixer_packages = ['lib2to3.fixes']

def find_packages(where='.', exclude=()):
    """Return a list all Python packages found within directory 'where'

    'where' should be supplied as a "cross-platform" (i.e. URL-style) path; it
    will be converted to the appropriate local path syntax.  'exclude' is a
    sequence of package names to exclude; '*' can be used as a wildcard in the
    names, such that 'foo.*' will exclude all subpackages of 'foo' (but not
    'foo' itself).
    """
    out = []
    stack=[(convert_path(where), '')]
    while stack:
        where,prefix = stack.pop(0)
        for name in os.listdir(where):
            fn = os.path.join(where,name)
            if ('.' not in name and os.path.isdir(fn) and
                os.path.isfile(os.path.join(fn,'__init__.py'))
            ):
                out.append(prefix+name); stack.append((fn,prefix+name+'.'))
    for pat in list(exclude)+['ez_setup']:
        from fnmatch import fnmatchcase
        out = [item for item in out if not fnmatchcase(item,pat)]
    return out

setup = distutils.core.setup

_Command = _get_unpatched(_Command)

class Command(_Command):
    __doc__ = _Command.__doc__

    command_consumes_arguments = False

    def __init__(self, dist, **kw):
        # Add support for keyword arguments
        _Command.__init__(self,dist)
        for k,v in kw.items():
            setattr(self,k,v)

    def reinitialize_command(self, command, reinit_subcommands=0, **kw):
        cmd = _Command.reinitialize_command(self, command, reinit_subcommands)
        for k,v in kw.items():
            setattr(cmd,k,v)    # update command with keywords
        return cmd

import distutils.core
distutils.core.Command = Command    # we can't patch distutils.cmd, alas

def findall(dir = os.curdir):
    """Find all files under 'dir' and return the list of full filenames
    (relative to 'dir').
    """
    all_files = []
    for base, dirs, files in os.walk(dir):
        if base==os.curdir or base.startswith(os.curdir+os.sep):
            base = base[2:]
        if base:
            files = [os.path.join(base, f) for f in files]
        all_files.extend(filter(os.path.isfile, files))
    return all_files

import distutils.filelist
distutils.filelist.findall = findall    # fix findall bug in distutils.

# sys.dont_write_bytecode was introduced in Python 2.6.
if ((hasattr(sys, "dont_write_bytecode") and sys.dont_write_bytecode) or
    (not hasattr(sys, "dont_write_bytecode") and os.environ.get("PYTHONDONTWRITEBYTECODE"))):
    _dont_write_bytecode = True
else:
    _dont_write_bytecode = False
