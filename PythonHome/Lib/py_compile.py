"""Routine to "compile" a .py file to a .pyc (or .pyo) file.

This module has intimate knowledge of the format of .pyc files.
"""

import __builtin__
import imp
import marshal
import os
import sys
import traceback

MAGIC = imp.get_magic()

__all__ = ["compile", "main", "PyCompileError"]


class PyCompileError(Exception):
    """Exception raised when an error occurs while attempting to
    compile the file.

    To raise this exception, use

        raise PyCompileError(exc_type,exc_value,file[,msg])

    where

        exc_type:   exception type to be used in error message
                    type name can be accesses as class variable
                    'exc_type_name'

        exc_value:  exception value to be used in error message
                    can be accesses as class variable 'exc_value'

        file:       name of file being compiled to be used in error message
                    can be accesses as class variable 'file'

        msg:        string message to be written as error message
                    If no value is given, a default exception message will be given,
                    consistent with 'standard' py_compile output.
                    message (or default) can be accesses as class variable 'msg'

    """

    def __init__(self, exc_type, exc_value, file, msg=''):
        exc_type_name = exc_type.__name__
        if exc_type is SyntaxError:
            tbtext = ''.join(traceback.format_exception_only(exc_type, exc_value))
            errmsg = tbtext.replace('File "<string>"', 'File "%s"' % file)
        else:
            errmsg = "Sorry: %s: %s" % (exc_type_name,exc_value)

        Exception.__init__(self,msg or errmsg,exc_type_name,exc_value,file)

        self.exc_type_name = exc_type_name
        self.exc_value = exc_value
        self.file = file
        self.msg = msg or errmsg

    def __str__(self):
        return self.msg


def wr_long(f, x):
    """Internal; write a 32-bit int to a file in little-endian order."""
    f.write(chr( x        & 0xff))
    f.write(chr((x >> 8)  & 0xff))
    f.write(chr((x >> 16) & 0xff))
    f.write(chr((x >> 24) & 0xff))

def compile(file, cfile=None, dfile=None, doraise=False):
    """Byte-compile one Python source file to Python bytecode.

    Arguments:

    file:    source filename
    cfile:   target filename; defaults to source with 'c' or 'o' appended
             ('c' normally, 'o' in optimizing mode, giving .pyc or .pyo)
    dfile:   purported filename; defaults to source (this is the filename
             that will show up in error messages)
    doraise: flag indicating whether or not an exception should be
             raised when a compile error is found. If an exception
             occurs and this flag is set to False, a string
             indicating the nature of the exception will be printed,
             and the function will return to the caller. If an
             exception occurs and this flag is set to True, a
             PyCompileError exception will be raised.

    Note that it isn't necessary to byte-compile Python modules for
    execution efficiency -- Python itself byte-compiles a module when
    it is loaded, and if it can, writes out the bytecode to the
    corresponding .pyc (or .pyo) file.

    However, if a Python installation is shared between users, it is a
    good idea to byte-compile all modules upon installation, since
    other users may not be able to write in the source directories,
    and thus they won't be able to write the .pyc/.pyo file, and then
    they would be byte-compiling every module each time it is loaded.
    This can slow down program start-up considerably.

    See compileall.py for a script/module that uses this module to
    byte-compile all installed files (or all files in selected
    directories).

    """
    with open(file, 'U') as f:
        try:
            timestamp = long(os.fstat(f.fileno()).st_mtime)
        except AttributeError:
            timestamp = long(os.stat(file).st_mtime)
        codestring = f.read()
    try:
        codeobject = __builtin__.compile(codestring, dfile or file,'exec')
    except Exception,err:
        py_exc = PyCompileError(err.__class__, err, dfile or file)
        if doraise:
            raise py_exc
        else:
            sys.stderr.write(py_exc.msg + '\n')
            return
    if cfile is None:
        cfile = file + (__debug__ and 'c' or 'o')
    with open(cfile, 'wb') as fc:
        fc.write('\0\0\0\0')
        wr_long(fc, timestamp)
        marshal.dump(codeobject, fc)
        fc.flush()
        fc.seek(0, 0)
        fc.write(MAGIC)

def main(args=None):
    """Compile several source files.

    The files named in 'args' (or on the command line, if 'args' is
    not specified) are compiled and the resulting bytecode is cached
    in the normal manner.  This function does not search a directory
    structure to locate source files; it only compiles files named
    explicitly.  If '-' is the only parameter in args, the list of
    files is taken from standard input.

    """
    if args is None:
        args = sys.argv[1:]
    rv = 0
    if args == ['-']:
        while True:
            filename = sys.stdin.readline()
            if not filename:
                break
            filename = filename.rstrip('\n')
            try:
                compile(filename, doraise=True)
            except PyCompileError as error:
                rv = 1
                sys.stderr.write("%s\n" % error.msg)
            except IOError as error:
                rv = 1
                sys.stderr.write("%s\n" % error)
    else:
        for filename in args:
            try:
                compile(filename, doraise=True)
            except PyCompileError as error:
                # return value to indicate at least one failure
                rv = 1
                sys.stderr.write(error.msg)
    return rv

if __name__ == "__main__":
    sys.exit(main())
