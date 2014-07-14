"""Utility functions for copying and archiving files and directory trees.

XXX The functions here don't copy the resource fork or other metadata on Mac.

"""

import os
import sys
import stat
from os.path import abspath
import fnmatch
import collections
import errno

try:
    from pwd import getpwnam
except ImportError:
    getpwnam = None

try:
    from grp import getgrnam
except ImportError:
    getgrnam = None

__all__ = ["copyfileobj", "copyfile", "copymode", "copystat", "copy", "copy2",
           "copytree", "move", "rmtree", "Error", "SpecialFileError",
           "ExecError", "make_archive", "get_archive_formats",
           "register_archive_format", "unregister_archive_format",
           "ignore_patterns"]

class Error(EnvironmentError):
    pass

class SpecialFileError(EnvironmentError):
    """Raised when trying to do a kind of operation (e.g. copying) which is
    not supported on a special file (e.g. a named pipe)"""

class ExecError(EnvironmentError):
    """Raised when a command could not be executed"""

try:
    WindowsError
except NameError:
    WindowsError = None

def copyfileobj(fsrc, fdst, length=16*1024):
    """copy data from file-like object fsrc to file-like object fdst"""
    while 1:
        buf = fsrc.read(length)
        if not buf:
            break
        fdst.write(buf)

def _samefile(src, dst):
    # Macintosh, Unix.
    if hasattr(os.path, 'samefile'):
        try:
            return os.path.samefile(src, dst)
        except OSError:
            return False

    # All other platforms: check for same pathname.
    return (os.path.normcase(os.path.abspath(src)) ==
            os.path.normcase(os.path.abspath(dst)))

def copyfile(src, dst):
    """Copy data from src to dst"""
    if _samefile(src, dst):
        raise Error("`%s` and `%s` are the same file" % (src, dst))

    for fn in [src, dst]:
        try:
            st = os.stat(fn)
        except OSError:
            # File most likely does not exist
            pass
        else:
            # XXX What about other special files? (sockets, devices...)
            if stat.S_ISFIFO(st.st_mode):
                raise SpecialFileError("`%s` is a named pipe" % fn)

    with open(src, 'rb') as fsrc:
        with open(dst, 'wb') as fdst:
            copyfileobj(fsrc, fdst)

def copymode(src, dst):
    """Copy mode bits from src to dst"""
    if hasattr(os, 'chmod'):
        st = os.stat(src)
        mode = stat.S_IMODE(st.st_mode)
        os.chmod(dst, mode)

def copystat(src, dst):
    """Copy all stat info (mode bits, atime, mtime, flags) from src to dst"""
    st = os.stat(src)
    mode = stat.S_IMODE(st.st_mode)
    if hasattr(os, 'utime'):
        os.utime(dst, (st.st_atime, st.st_mtime))
    if hasattr(os, 'chmod'):
        os.chmod(dst, mode)
    if hasattr(os, 'chflags') and hasattr(st, 'st_flags'):
        try:
            os.chflags(dst, st.st_flags)
        except OSError, why:
            for err in 'EOPNOTSUPP', 'ENOTSUP':
                if hasattr(errno, err) and why.errno == getattr(errno, err):
                    break
            else:
                raise

def copy(src, dst):
    """Copy data and mode bits ("cp src dst").

    The destination may be a directory.

    """
    if os.path.isdir(dst):
        dst = os.path.join(dst, os.path.basename(src))
    copyfile(src, dst)
    copymode(src, dst)

def copy2(src, dst):
    """Copy data and all stat info ("cp -p src dst").

    The destination may be a directory.

    """
    if os.path.isdir(dst):
        dst = os.path.join(dst, os.path.basename(src))
    copyfile(src, dst)
    copystat(src, dst)

def ignore_patterns(*patterns):
    """Function that can be used as copytree() ignore parameter.

    Patterns is a sequence of glob-style patterns
    that are used to exclude files"""
    def _ignore_patterns(path, names):
        ignored_names = []
        for pattern in patterns:
            ignored_names.extend(fnmatch.filter(names, pattern))
        return set(ignored_names)
    return _ignore_patterns

def copytree(src, dst, symlinks=False, ignore=None):
    """Recursively copy a directory tree using copy2().

    The destination directory must not already exist.
    If exception(s) occur, an Error is raised with a list of reasons.

    If the optional symlinks flag is true, symbolic links in the
    source tree result in symbolic links in the destination tree; if
    it is false, the contents of the files pointed to by symbolic
    links are copied.

    The optional ignore argument is a callable. If given, it
    is called with the `src` parameter, which is the directory
    being visited by copytree(), and `names` which is the list of
    `src` contents, as returned by os.listdir():

        callable(src, names) -> ignored_names

    Since copytree() is called recursively, the callable will be
    called once for each directory that is copied. It returns a
    list of names relative to the `src` directory that should
    not be copied.

    XXX Consider this example code rather than the ultimate tool.

    """
    names = os.listdir(src)
    if ignore is not None:
        ignored_names = ignore(src, names)
    else:
        ignored_names = set()

    os.makedirs(dst)
    errors = []
    for name in names:
        if name in ignored_names:
            continue
        srcname = os.path.join(src, name)
        dstname = os.path.join(dst, name)
        try:
            if symlinks and os.path.islink(srcname):
                linkto = os.readlink(srcname)
                os.symlink(linkto, dstname)
            elif os.path.isdir(srcname):
                copytree(srcname, dstname, symlinks, ignore)
            else:
                # Will raise a SpecialFileError for unsupported file types
                copy2(srcname, dstname)
        # catch the Error from the recursive copytree so that we can
        # continue with other files
        except Error, err:
            errors.extend(err.args[0])
        except EnvironmentError, why:
            errors.append((srcname, dstname, str(why)))
    try:
        copystat(src, dst)
    except OSError, why:
        if WindowsError is not None and isinstance(why, WindowsError):
            # Copying file access times may fail on Windows
            pass
        else:
            errors.append((src, dst, str(why)))
    if errors:
        raise Error, errors

def rmtree(path, ignore_errors=False, onerror=None):
    """Recursively delete a directory tree.

    If ignore_errors is set, errors are ignored; otherwise, if onerror
    is set, it is called to handle the error with arguments (func,
    path, exc_info) where func is os.listdir, os.remove, or os.rmdir;
    path is the argument to that function that caused it to fail; and
    exc_info is a tuple returned by sys.exc_info().  If ignore_errors
    is false and onerror is None, an exception is raised.

    """
    if ignore_errors:
        def onerror(*args):
            pass
    elif onerror is None:
        def onerror(*args):
            raise
    try:
        if os.path.islink(path):
            # symlinks to directories are forbidden, see bug #1669
            raise OSError("Cannot call rmtree on a symbolic link")
    except OSError:
        onerror(os.path.islink, path, sys.exc_info())
        # can't continue even if onerror hook returns
        return
    names = []
    try:
        names = os.listdir(path)
    except os.error, err:
        onerror(os.listdir, path, sys.exc_info())
    for name in names:
        fullname = os.path.join(path, name)
        try:
            mode = os.lstat(fullname).st_mode
        except os.error:
            mode = 0
        if stat.S_ISDIR(mode):
            rmtree(fullname, ignore_errors, onerror)
        else:
            try:
                os.remove(fullname)
            except os.error, err:
                onerror(os.remove, fullname, sys.exc_info())
    try:
        os.rmdir(path)
    except os.error:
        onerror(os.rmdir, path, sys.exc_info())


def _basename(path):
    # A basename() variant which first strips the trailing slash, if present.
    # Thus we always get the last component of the path, even for directories.
    sep = os.path.sep + (os.path.altsep or '')
    return os.path.basename(path.rstrip(sep))

def move(src, dst):
    """Recursively move a file or directory to another location. This is
    similar to the Unix "mv" command.

    If the destination is a directory or a symlink to a directory, the source
    is moved inside the directory. The destination path must not already
    exist.

    If the destination already exists but is not a directory, it may be
    overwritten depending on os.rename() semantics.

    If the destination is on our current filesystem, then rename() is used.
    Otherwise, src is copied to the destination and then removed.
    A lot more could be done here...  A look at a mv.c shows a lot of
    the issues this implementation glosses over.

    """
    real_dst = dst
    if os.path.isdir(dst):
        if _samefile(src, dst):
            # We might be on a case insensitive filesystem,
            # perform the rename anyway.
            os.rename(src, dst)
            return

        real_dst = os.path.join(dst, _basename(src))
        if os.path.exists(real_dst):
            raise Error, "Destination path '%s' already exists" % real_dst
    try:
        os.rename(src, real_dst)
    except OSError:
        if os.path.isdir(src):
            if _destinsrc(src, dst):
                raise Error, "Cannot move a directory '%s' into itself '%s'." % (src, dst)
            copytree(src, real_dst, symlinks=True)
            rmtree(src)
        else:
            copy2(src, real_dst)
            os.unlink(src)

def _destinsrc(src, dst):
    src = abspath(src)
    dst = abspath(dst)
    if not src.endswith(os.path.sep):
        src += os.path.sep
    if not dst.endswith(os.path.sep):
        dst += os.path.sep
    return dst.startswith(src)

def _get_gid(name):
    """Returns a gid, given a group name."""
    if getgrnam is None or name is None:
        return None
    try:
        result = getgrnam(name)
    except KeyError:
        result = None
    if result is not None:
        return result[2]
    return None

def _get_uid(name):
    """Returns an uid, given a user name."""
    if getpwnam is None or name is None:
        return None
    try:
        result = getpwnam(name)
    except KeyError:
        result = None
    if result is not None:
        return result[2]
    return None

def _make_tarball(base_name, base_dir, compress="gzip", verbose=0, dry_run=0,
                  owner=None, group=None, logger=None):
    """Create a (possibly compressed) tar file from all the files under
    'base_dir'.

    'compress' must be "gzip" (the default), "bzip2", or None.

    'owner' and 'group' can be used to define an owner and a group for the
    archive that is being built. If not provided, the current owner and group
    will be used.

    The output tar file will be named 'base_name' +  ".tar", possibly plus
    the appropriate compression extension (".gz", or ".bz2").

    Returns the output filename.
    """
    tar_compression = {'gzip': 'gz', 'bzip2': 'bz2', None: ''}
    compress_ext = {'gzip': '.gz', 'bzip2': '.bz2'}

    # flags for compression program, each element of list will be an argument
    if compress is not None and compress not in compress_ext.keys():
        raise ValueError, \
              ("bad value for 'compress': must be None, 'gzip' or 'bzip2'")

    archive_name = base_name + '.tar' + compress_ext.get(compress, '')
    archive_dir = os.path.dirname(archive_name)

    if not os.path.exists(archive_dir):
        if logger is not None:
            logger.info("creating %s", archive_dir)
        if not dry_run:
            os.makedirs(archive_dir)


    # creating the tarball
    import tarfile  # late import so Python build itself doesn't break

    if logger is not None:
        logger.info('Creating tar archive')

    uid = _get_uid(owner)
    gid = _get_gid(group)

    def _set_uid_gid(tarinfo):
        if gid is not None:
            tarinfo.gid = gid
            tarinfo.gname = group
        if uid is not None:
            tarinfo.uid = uid
            tarinfo.uname = owner
        return tarinfo

    if not dry_run:
        tar = tarfile.open(archive_name, 'w|%s' % tar_compression[compress])
        try:
            tar.add(base_dir, filter=_set_uid_gid)
        finally:
            tar.close()

    return archive_name

def _call_external_zip(base_dir, zip_filename, verbose=False, dry_run=False):
    # XXX see if we want to keep an external call here
    if verbose:
        zipoptions = "-r"
    else:
        zipoptions = "-rq"
    from distutils.errors import DistutilsExecError
    from distutils.spawn import spawn
    try:
        spawn(["zip", zipoptions, zip_filename, base_dir], dry_run=dry_run)
    except DistutilsExecError:
        # XXX really should distinguish between "couldn't find
        # external 'zip' command" and "zip failed".
        raise ExecError, \
            ("unable to create zip file '%s': "
            "could neither import the 'zipfile' module nor "
            "find a standalone zip utility") % zip_filename

def _make_zipfile(base_name, base_dir, verbose=0, dry_run=0, logger=None):
    """Create a zip file from all the files under 'base_dir'.

    The output zip file will be named 'base_name' + ".zip".  Uses either the
    "zipfile" Python module (if available) or the InfoZIP "zip" utility
    (if installed and found on the default search path).  If neither tool is
    available, raises ExecError.  Returns the name of the output zip
    file.
    """
    zip_filename = base_name + ".zip"
    archive_dir = os.path.dirname(base_name)

    if not os.path.exists(archive_dir):
        if logger is not None:
            logger.info("creating %s", archive_dir)
        if not dry_run:
            os.makedirs(archive_dir)

    # If zipfile module is not available, try spawning an external 'zip'
    # command.
    try:
        import zipfile
    except ImportError:
        zipfile = None

    if zipfile is None:
        _call_external_zip(base_dir, zip_filename, verbose, dry_run)
    else:
        if logger is not None:
            logger.info("creating '%s' and adding '%s' to it",
                        zip_filename, base_dir)

        if not dry_run:
            with zipfile.ZipFile(zip_filename, "w",
                                 compression=zipfile.ZIP_DEFLATED) as zf:
                for dirpath, dirnames, filenames in os.walk(base_dir):
                    for name in filenames:
                        path = os.path.normpath(os.path.join(dirpath, name))
                        if os.path.isfile(path):
                            zf.write(path, path)
                            if logger is not None:
                                logger.info("adding '%s'", path)

    return zip_filename

_ARCHIVE_FORMATS = {
    'gztar': (_make_tarball, [('compress', 'gzip')], "gzip'ed tar-file"),
    'bztar': (_make_tarball, [('compress', 'bzip2')], "bzip2'ed tar-file"),
    'tar':   (_make_tarball, [('compress', None)], "uncompressed tar file"),
    'zip':   (_make_zipfile, [],"ZIP file")
    }

def get_archive_formats():
    """Returns a list of supported formats for archiving and unarchiving.

    Each element of the returned sequence is a tuple (name, description)
    """
    formats = [(name, registry[2]) for name, registry in
               _ARCHIVE_FORMATS.items()]
    formats.sort()
    return formats

def register_archive_format(name, function, extra_args=None, description=''):
    """Registers an archive format.

    name is the name of the format. function is the callable that will be
    used to create archives. If provided, extra_args is a sequence of
    (name, value) tuples that will be passed as arguments to the callable.
    description can be provided to describe the format, and will be returned
    by the get_archive_formats() function.
    """
    if extra_args is None:
        extra_args = []
    if not isinstance(function, collections.Callable):
        raise TypeError('The %s object is not callable' % function)
    if not isinstance(extra_args, (tuple, list)):
        raise TypeError('extra_args needs to be a sequence')
    for element in extra_args:
        if not isinstance(element, (tuple, list)) or len(element) !=2 :
            raise TypeError('extra_args elements are : (arg_name, value)')

    _ARCHIVE_FORMATS[name] = (function, extra_args, description)

def unregister_archive_format(name):
    del _ARCHIVE_FORMATS[name]

def make_archive(base_name, format, root_dir=None, base_dir=None, verbose=0,
                 dry_run=0, owner=None, group=None, logger=None):
    """Create an archive file (eg. zip or tar).

    'base_name' is the name of the file to create, minus any format-specific
    extension; 'format' is the archive format: one of "zip", "tar", "bztar"
    or "gztar".

    'root_dir' is a directory that will be the root directory of the
    archive; ie. we typically chdir into 'root_dir' before creating the
    archive.  'base_dir' is the directory where we start archiving from;
    ie. 'base_dir' will be the common prefix of all files and
    directories in the archive.  'root_dir' and 'base_dir' both default
    to the current directory.  Returns the name of the archive file.

    'owner' and 'group' are used when creating a tar archive. By default,
    uses the current owner and group.
    """
    save_cwd = os.getcwd()
    if root_dir is not None:
        if logger is not None:
            logger.debug("changing into '%s'", root_dir)
        base_name = os.path.abspath(base_name)
        if not dry_run:
            os.chdir(root_dir)

    if base_dir is None:
        base_dir = os.curdir

    kwargs = {'dry_run': dry_run, 'logger': logger}

    try:
        format_info = _ARCHIVE_FORMATS[format]
    except KeyError:
        raise ValueError, "unknown archive format '%s'" % format

    func = format_info[0]
    for arg, val in format_info[1]:
        kwargs[arg] = val

    if format != 'zip':
        kwargs['owner'] = owner
        kwargs['group'] = group

    try:
        filename = func(base_name, base_dir, **kwargs)
    finally:
        if root_dir is not None:
            if logger is not None:
                logger.debug("changing back to '%s'", save_cwd)
            os.chdir(save_cwd)

    return filename
