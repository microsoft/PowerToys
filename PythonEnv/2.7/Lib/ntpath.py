# Module 'ntpath' -- common operations on WinNT/Win95 pathnames
"""Common pathname manipulations, WindowsNT/95 version.

Instead of importing this module directly, import os and refer to this
module as os.path.
"""

import os
import sys
import stat
import genericpath
import warnings

from genericpath import *

__all__ = ["normcase","isabs","join","splitdrive","split","splitext",
           "basename","dirname","commonprefix","getsize","getmtime",
           "getatime","getctime", "islink","exists","lexists","isdir","isfile",
           "ismount","walk","expanduser","expandvars","normpath","abspath",
           "splitunc","curdir","pardir","sep","pathsep","defpath","altsep",
           "extsep","devnull","realpath","supports_unicode_filenames","relpath"]

# strings representing various path-related bits and pieces
curdir = '.'
pardir = '..'
extsep = '.'
sep = '\\'
pathsep = ';'
altsep = '/'
defpath = '.;C:\\bin'
if 'ce' in sys.builtin_module_names:
    defpath = '\\Windows'
elif 'os2' in sys.builtin_module_names:
    # OS/2 w/ VACPP
    altsep = '/'
devnull = 'nul'

# Normalize the case of a pathname and map slashes to backslashes.
# Other normalizations (such as optimizing '../' away) are not done
# (this is done by normpath).

def normcase(s):
    """Normalize case of pathname.

    Makes all characters lowercase and all slashes into backslashes."""
    return s.replace("/", "\\").lower()


# Return whether a path is absolute.
# Trivial in Posix, harder on the Mac or MS-DOS.
# For DOS it is absolute if it starts with a slash or backslash (current
# volume), or if a pathname after the volume letter and colon / UNC resource
# starts with a slash or backslash.

def isabs(s):
    """Test whether a path is absolute"""
    s = splitdrive(s)[1]
    return s != '' and s[:1] in '/\\'


# Join two (or more) paths.

def join(a, *p):
    """Join two or more pathname components, inserting "\\" as needed.
    If any component is an absolute path, all previous path components
    will be discarded."""
    path = a
    for b in p:
        b_wins = 0  # set to 1 iff b makes path irrelevant
        if path == "":
            b_wins = 1

        elif isabs(b):
            # This probably wipes out path so far.  However, it's more
            # complicated if path begins with a drive letter:
            #     1. join('c:', '/a') == 'c:/a'
            #     2. join('c:/', '/a') == 'c:/a'
            # But
            #     3. join('c:/a', '/b') == '/b'
            #     4. join('c:', 'd:/') = 'd:/'
            #     5. join('c:/', 'd:/') = 'd:/'
            if path[1:2] != ":" or b[1:2] == ":":
                # Path doesn't start with a drive letter, or cases 4 and 5.
                b_wins = 1

            # Else path has a drive letter, and b doesn't but is absolute.
            elif len(path) > 3 or (len(path) == 3 and
                                   path[-1] not in "/\\"):
                # case 3
                b_wins = 1

        if b_wins:
            path = b
        else:
            # Join, and ensure there's a separator.
            assert len(path) > 0
            if path[-1] in "/\\":
                if b and b[0] in "/\\":
                    path += b[1:]
                else:
                    path += b
            elif path[-1] == ":":
                path += b
            elif b:
                if b[0] in "/\\":
                    path += b
                else:
                    path += "\\" + b
            else:
                # path is not empty and does not end with a backslash,
                # but b is empty; since, e.g., split('a/') produces
                # ('a', ''), it's best if join() adds a backslash in
                # this case.
                path += '\\'

    return path


# Split a path in a drive specification (a drive letter followed by a
# colon) and the path specification.
# It is always true that drivespec + pathspec == p
def splitdrive(p):
    """Split a pathname into drive and path specifiers. Returns a 2-tuple
"(drive,path)";  either part may be empty"""
    if p[1:2] == ':':
        return p[0:2], p[2:]
    return '', p


# Parse UNC paths
def splitunc(p):
    """Split a pathname into UNC mount point and relative path specifiers.

    Return a 2-tuple (unc, rest); either part may be empty.
    If unc is not empty, it has the form '//host/mount' (or similar
    using backslashes).  unc+rest is always the input path.
    Paths containing drive letters never have an UNC part.
    """
    if p[1:2] == ':':
        return '', p # Drive letter present
    firstTwo = p[0:2]
    if firstTwo == '//' or firstTwo == '\\\\':
        # is a UNC path:
        # vvvvvvvvvvvvvvvvvvvv equivalent to drive letter
        # \\machine\mountpoint\directories...
        #           directory ^^^^^^^^^^^^^^^
        normp = normcase(p)
        index = normp.find('\\', 2)
        if index == -1:
            ##raise RuntimeError, 'illegal UNC path: "' + p + '"'
            return ("", p)
        index = normp.find('\\', index + 1)
        if index == -1:
            index = len(p)
        return p[:index], p[index:]
    return '', p


# Split a path in head (everything up to the last '/') and tail (the
# rest).  After the trailing '/' is stripped, the invariant
# join(head, tail) == p holds.
# The resulting head won't end in '/' unless it is the root.

def split(p):
    """Split a pathname.

    Return tuple (head, tail) where tail is everything after the final slash.
    Either part may be empty."""

    d, p = splitdrive(p)
    # set i to index beyond p's last slash
    i = len(p)
    while i and p[i-1] not in '/\\':
        i = i - 1
    head, tail = p[:i], p[i:]  # now tail has no slashes
    # remove trailing slashes from head, unless it's all slashes
    head2 = head
    while head2 and head2[-1] in '/\\':
        head2 = head2[:-1]
    head = head2 or head
    return d + head, tail


# Split a path in root and extension.
# The extension is everything starting at the last dot in the last
# pathname component; the root is everything before that.
# It is always true that root + ext == p.

def splitext(p):
    return genericpath._splitext(p, sep, altsep, extsep)
splitext.__doc__ = genericpath._splitext.__doc__


# Return the tail (basename) part of a path.

def basename(p):
    """Returns the final component of a pathname"""
    return split(p)[1]


# Return the head (dirname) part of a path.

def dirname(p):
    """Returns the directory component of a pathname"""
    return split(p)[0]

# Is a path a symbolic link?
# This will always return false on systems where posix.lstat doesn't exist.

def islink(path):
    """Test for symbolic link.
    On WindowsNT/95 and OS/2 always returns false
    """
    return False

# alias exists to lexists
lexists = exists

# Is a path a mount point?  Either a root (with or without drive letter)
# or an UNC path with at most a / or \ after the mount point.

def ismount(path):
    """Test whether a path is a mount point (defined as root of drive)"""
    unc, rest = splitunc(path)
    if unc:
        return rest in ("", "/", "\\")
    p = splitdrive(path)[1]
    return len(p) == 1 and p[0] in '/\\'


# Directory tree walk.
# For each directory under top (including top itself, but excluding
# '.' and '..'), func(arg, dirname, filenames) is called, where
# dirname is the name of the directory and filenames is the list
# of files (and subdirectories etc.) in the directory.
# The func may modify the filenames list, to implement a filter,
# or to impose a different order of visiting.

def walk(top, func, arg):
    """Directory tree walk with callback function.

    For each directory in the directory tree rooted at top (including top
    itself, but excluding '.' and '..'), call func(arg, dirname, fnames).
    dirname is the name of the directory, and fnames a list of the names of
    the files and subdirectories in dirname (excluding '.' and '..').  func
    may modify the fnames list in-place (e.g. via del or slice assignment),
    and walk will only recurse into the subdirectories whose names remain in
    fnames; this can be used to implement a filter, or to impose a specific
    order of visiting.  No semantics are defined for, or required of, arg,
    beyond that arg is always passed to func.  It can be used, e.g., to pass
    a filename pattern, or a mutable object designed to accumulate
    statistics.  Passing None for arg is common."""
    warnings.warnpy3k("In 3.x, os.path.walk is removed in favor of os.walk.",
                      stacklevel=2)
    try:
        names = os.listdir(top)
    except os.error:
        return
    func(arg, top, names)
    for name in names:
        name = join(top, name)
        if isdir(name):
            walk(name, func, arg)


# Expand paths beginning with '~' or '~user'.
# '~' means $HOME; '~user' means that user's home directory.
# If the path doesn't begin with '~', or if the user or $HOME is unknown,
# the path is returned unchanged (leaving error reporting to whatever
# function is called with the expanded path as argument).
# See also module 'glob' for expansion of *, ? and [...] in pathnames.
# (A function should also be defined to do full *sh-style environment
# variable expansion.)

def expanduser(path):
    """Expand ~ and ~user constructs.

    If user or $HOME is unknown, do nothing."""
    if path[:1] != '~':
        return path
    i, n = 1, len(path)
    while i < n and path[i] not in '/\\':
        i = i + 1

    if 'HOME' in os.environ:
        userhome = os.environ['HOME']
    elif 'USERPROFILE' in os.environ:
        userhome = os.environ['USERPROFILE']
    elif not 'HOMEPATH' in os.environ:
        return path
    else:
        try:
            drive = os.environ['HOMEDRIVE']
        except KeyError:
            drive = ''
        userhome = join(drive, os.environ['HOMEPATH'])

    if i != 1: #~user
        userhome = join(dirname(userhome), path[1:i])

    return userhome + path[i:]


# Expand paths containing shell variable substitutions.
# The following rules apply:
#       - no expansion within single quotes
#       - '$$' is translated into '$'
#       - '%%' is translated into '%' if '%%' are not seen in %var1%%var2%
#       - ${varname} is accepted.
#       - $varname is accepted.
#       - %varname% is accepted.
#       - varnames can be made out of letters, digits and the characters '_-'
#         (though is not verified in the ${varname} and %varname% cases)
# XXX With COMMAND.COM you can use any characters in a variable name,
# XXX except '^|<>='.

def expandvars(path):
    """Expand shell variables of the forms $var, ${var} and %var%.

    Unknown variables are left unchanged."""
    if '$' not in path and '%' not in path:
        return path
    import string
    varchars = string.ascii_letters + string.digits + '_-'
    res = ''
    index = 0
    pathlen = len(path)
    while index < pathlen:
        c = path[index]
        if c == '\'':   # no expansion within single quotes
            path = path[index + 1:]
            pathlen = len(path)
            try:
                index = path.index('\'')
                res = res + '\'' + path[:index + 1]
            except ValueError:
                res = res + path
                index = pathlen - 1
        elif c == '%':  # variable or '%'
            if path[index + 1:index + 2] == '%':
                res = res + c
                index = index + 1
            else:
                path = path[index+1:]
                pathlen = len(path)
                try:
                    index = path.index('%')
                except ValueError:
                    res = res + '%' + path
                    index = pathlen - 1
                else:
                    var = path[:index]
                    if var in os.environ:
                        res = res + os.environ[var]
                    else:
                        res = res + '%' + var + '%'
        elif c == '$':  # variable or '$$'
            if path[index + 1:index + 2] == '$':
                res = res + c
                index = index + 1
            elif path[index + 1:index + 2] == '{':
                path = path[index+2:]
                pathlen = len(path)
                try:
                    index = path.index('}')
                    var = path[:index]
                    if var in os.environ:
                        res = res + os.environ[var]
                    else:
                        res = res + '${' + var + '}'
                except ValueError:
                    res = res + '${' + path
                    index = pathlen - 1
            else:
                var = ''
                index = index + 1
                c = path[index:index + 1]
                while c != '' and c in varchars:
                    var = var + c
                    index = index + 1
                    c = path[index:index + 1]
                if var in os.environ:
                    res = res + os.environ[var]
                else:
                    res = res + '$' + var
                if c != '':
                    index = index - 1
        else:
            res = res + c
        index = index + 1
    return res


# Normalize a path, e.g. A//B, A/./B and A/foo/../B all become A\B.
# Previously, this function also truncated pathnames to 8+3 format,
# but as this module is called "ntpath", that's obviously wrong!

def normpath(path):
    """Normalize path, eliminating double slashes, etc."""
    # Preserve unicode (if path is unicode)
    backslash, dot = (u'\\', u'.') if isinstance(path, unicode) else ('\\', '.')
    if path.startswith(('\\\\.\\', '\\\\?\\')):
        # in the case of paths with these prefixes:
        # \\.\ -> device names
        # \\?\ -> literal paths
        # do not do any normalization, but return the path unchanged
        return path
    path = path.replace("/", "\\")
    prefix, path = splitdrive(path)
    # We need to be careful here. If the prefix is empty, and the path starts
    # with a backslash, it could either be an absolute path on the current
    # drive (\dir1\dir2\file) or a UNC filename (\\server\mount\dir1\file). It
    # is therefore imperative NOT to collapse multiple backslashes blindly in
    # that case.
    # The code below preserves multiple backslashes when there is no drive
    # letter. This means that the invalid filename \\\a\b is preserved
    # unchanged, where a\\\b is normalised to a\b. It's not clear that there
    # is any better behaviour for such edge cases.
    if prefix == '':
        # No drive letter - preserve initial backslashes
        while path[:1] == "\\":
            prefix = prefix + backslash
            path = path[1:]
    else:
        # We have a drive letter - collapse initial backslashes
        if path.startswith("\\"):
            prefix = prefix + backslash
            path = path.lstrip("\\")
    comps = path.split("\\")
    i = 0
    while i < len(comps):
        if comps[i] in ('.', ''):
            del comps[i]
        elif comps[i] == '..':
            if i > 0 and comps[i-1] != '..':
                del comps[i-1:i+1]
                i -= 1
            elif i == 0 and prefix.endswith("\\"):
                del comps[i]
            else:
                i += 1
        else:
            i += 1
    # If the path is now empty, substitute '.'
    if not prefix and not comps:
        comps.append(dot)
    return prefix + backslash.join(comps)


# Return an absolute path.
try:
    from nt import _getfullpathname

except ImportError: # not running on Windows - mock up something sensible
    def abspath(path):
        """Return the absolute version of a path."""
        if not isabs(path):
            if isinstance(path, unicode):
                cwd = os.getcwdu()
            else:
                cwd = os.getcwd()
            path = join(cwd, path)
        return normpath(path)

else:  # use native Windows method on Windows
    def abspath(path):
        """Return the absolute version of a path."""

        if path: # Empty path must return current working directory.
            try:
                path = _getfullpathname(path)
            except WindowsError:
                pass # Bad path - return unchanged.
        elif isinstance(path, unicode):
            path = os.getcwdu()
        else:
            path = os.getcwd()
        return normpath(path)

# realpath is a no-op on systems without islink support
realpath = abspath
# Win9x family and earlier have no Unicode filename support.
supports_unicode_filenames = (hasattr(sys, "getwindowsversion") and
                              sys.getwindowsversion()[3] >= 2)

def _abspath_split(path):
    abs = abspath(normpath(path))
    prefix, rest = splitunc(abs)
    is_unc = bool(prefix)
    if not is_unc:
        prefix, rest = splitdrive(abs)
    return is_unc, prefix, [x for x in rest.split(sep) if x]

def relpath(path, start=curdir):
    """Return a relative version of a path"""

    if not path:
        raise ValueError("no path specified")

    start_is_unc, start_prefix, start_list = _abspath_split(start)
    path_is_unc, path_prefix, path_list = _abspath_split(path)

    if path_is_unc ^ start_is_unc:
        raise ValueError("Cannot mix UNC and non-UNC paths (%s and %s)"
                                                            % (path, start))
    if path_prefix.lower() != start_prefix.lower():
        if path_is_unc:
            raise ValueError("path is on UNC root %s, start on UNC root %s"
                                                % (path_prefix, start_prefix))
        else:
            raise ValueError("path is on drive %s, start on drive %s"
                                                % (path_prefix, start_prefix))
    # Work out how much of the filepath is shared by start and path.
    i = 0
    for e1, e2 in zip(start_list, path_list):
        if e1.lower() != e2.lower():
            break
        i += 1

    rel_list = [pardir] * (len(start_list)-i) + path_list[i:]
    if not rel_list:
        return curdir
    return join(*rel_list)

try:
    # The genericpath.isdir implementation uses os.stat and checks the mode
    # attribute to tell whether or not the path is a directory.
    # This is overkill on Windows - just pass the path to GetFileAttributes
    # and check the attribute from there.
    from nt import _isdir as isdir
except ImportError:
    # Use genericpath.isdir as imported above.
    pass
