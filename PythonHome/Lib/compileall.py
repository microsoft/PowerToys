"""Module/script to byte-compile all .py files to .pyc (or .pyo) files.

When called as a script with arguments, this compiles the directories
given as arguments recursively; the -l option prevents it from
recursing into directories.

Without arguments, if compiles all modules on sys.path, without
recursing into subdirectories.  (Even though it should do so for
packages -- for now, you'll have to deal with packages separately.)

See module py_compile for details of the actual byte-compilation.
"""
import os
import sys
import py_compile
import struct
import imp

__all__ = ["compile_dir","compile_file","compile_path"]

def compile_dir(dir, maxlevels=10, ddir=None,
                force=0, rx=None, quiet=0):
    """Byte-compile all modules in the given directory tree.

    Arguments (only dir is required):

    dir:       the directory to byte-compile
    maxlevels: maximum recursion level (default 10)
    ddir:      the directory that will be prepended to the path to the
               file as it is compiled into each byte-code file.
    force:     if 1, force compilation, even if timestamps are up-to-date
    quiet:     if 1, be quiet during compilation
    """
    if not quiet:
        print 'Listing', dir, '...'
    try:
        names = os.listdir(dir)
    except os.error:
        print "Can't list", dir
        names = []
    names.sort()
    success = 1
    for name in names:
        fullname = os.path.join(dir, name)
        if ddir is not None:
            dfile = os.path.join(ddir, name)
        else:
            dfile = None
        if not os.path.isdir(fullname):
            if not compile_file(fullname, ddir, force, rx, quiet):
                success = 0
        elif maxlevels > 0 and \
             name != os.curdir and name != os.pardir and \
             os.path.isdir(fullname) and \
             not os.path.islink(fullname):
            if not compile_dir(fullname, maxlevels - 1, dfile, force, rx,
                               quiet):
                success = 0
    return success

def compile_file(fullname, ddir=None, force=0, rx=None, quiet=0):
    """Byte-compile one file.

    Arguments (only fullname is required):

    fullname:  the file to byte-compile
    ddir:      if given, the directory name compiled in to the
               byte-code file.
    force:     if 1, force compilation, even if timestamps are up-to-date
    quiet:     if 1, be quiet during compilation
    """
    success = 1
    name = os.path.basename(fullname)
    if ddir is not None:
        dfile = os.path.join(ddir, name)
    else:
        dfile = None
    if rx is not None:
        mo = rx.search(fullname)
        if mo:
            return success
    if os.path.isfile(fullname):
        head, tail = name[:-3], name[-3:]
        if tail == '.py':
            if not force:
                try:
                    mtime = int(os.stat(fullname).st_mtime)
                    expect = struct.pack('<4sl', imp.get_magic(), mtime)
                    cfile = fullname + (__debug__ and 'c' or 'o')
                    with open(cfile, 'rb') as chandle:
                        actual = chandle.read(8)
                    if expect == actual:
                        return success
                except IOError:
                    pass
            if not quiet:
                print 'Compiling', fullname, '...'
            try:
                ok = py_compile.compile(fullname, None, dfile, True)
            except py_compile.PyCompileError,err:
                if quiet:
                    print 'Compiling', fullname, '...'
                print err.msg
                success = 0
            except IOError, e:
                print "Sorry", e
                success = 0
            else:
                if ok == 0:
                    success = 0
    return success

def compile_path(skip_curdir=1, maxlevels=0, force=0, quiet=0):
    """Byte-compile all module on sys.path.

    Arguments (all optional):

    skip_curdir: if true, skip current directory (default true)
    maxlevels:   max recursion level (default 0)
    force: as for compile_dir() (default 0)
    quiet: as for compile_dir() (default 0)
    """
    success = 1
    for dir in sys.path:
        if (not dir or dir == os.curdir) and skip_curdir:
            print 'Skipping current directory'
        else:
            success = success and compile_dir(dir, maxlevels, None,
                                              force, quiet=quiet)
    return success

def expand_args(args, flist):
    """read names in flist and append to args"""
    expanded = args[:]
    if flist:
        try:
            if flist == '-':
                fd = sys.stdin
            else:
                fd = open(flist)
            while 1:
                line = fd.readline()
                if not line:
                    break
                expanded.append(line[:-1])
        except IOError:
            print "Error reading file list %s" % flist
            raise
    return expanded

def main():
    """Script main program."""
    import getopt
    try:
        opts, args = getopt.getopt(sys.argv[1:], 'lfqd:x:i:')
    except getopt.error, msg:
        print msg
        print "usage: python compileall.py [-l] [-f] [-q] [-d destdir] " \
              "[-x regexp] [-i list] [directory|file ...]"
        print
        print "arguments: zero or more file and directory names to compile; " \
              "if no arguments given, "
        print "           defaults to the equivalent of -l sys.path"
        print
        print "options:"
        print "-l: don't recurse into subdirectories"
        print "-f: force rebuild even if timestamps are up-to-date"
        print "-q: output only error messages"
        print "-d destdir: directory to prepend to file paths for use in " \
              "compile-time tracebacks and in"
        print "            runtime tracebacks in cases where the source " \
              "file is unavailable"
        print "-x regexp: skip files matching the regular expression regexp; " \
              "the regexp is searched for"
        print "           in the full path of each file considered for " \
              "compilation"
        print "-i file: add all the files and directories listed in file to " \
              "the list considered for"
        print '         compilation; if "-", names are read from stdin'

        sys.exit(2)
    maxlevels = 10
    ddir = None
    force = 0
    quiet = 0
    rx = None
    flist = None
    for o, a in opts:
        if o == '-l': maxlevels = 0
        if o == '-d': ddir = a
        if o == '-f': force = 1
        if o == '-q': quiet = 1
        if o == '-x':
            import re
            rx = re.compile(a)
        if o == '-i': flist = a
    if ddir:
        if len(args) != 1 and not os.path.isdir(args[0]):
            print "-d destdir require exactly one directory argument"
            sys.exit(2)
    success = 1
    try:
        if args or flist:
            try:
                if flist:
                    args = expand_args(args, flist)
            except IOError:
                success = 0
            if success:
                for arg in args:
                    if os.path.isdir(arg):
                        if not compile_dir(arg, maxlevels, ddir,
                                           force, rx, quiet):
                            success = 0
                    else:
                        if not compile_file(arg, ddir, force, rx, quiet):
                            success = 0
        else:
            success = compile_path()
    except KeyboardInterrupt:
        print "\n[interrupted]"
        success = 0
    return success

if __name__ == '__main__':
    exit_status = int(not main())
    sys.exit(exit_status)
