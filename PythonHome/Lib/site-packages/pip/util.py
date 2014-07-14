import sys
import shutil
import os
import stat
import re
import posixpath
import zipfile
import tarfile
import subprocess
import textwrap

from pip.exceptions import InstallationError, BadCommand, PipError
from pip.backwardcompat import(WindowsError, string_types, raw_input,
                                console_to_str, user_site, PermissionError)
from pip.locations import site_packages, running_under_virtualenv, virtualenv_no_global
from pip.log import logger
from pip._vendor import pkg_resources
from pip._vendor.distlib import version

__all__ = ['rmtree', 'display_path', 'backup_dir',
           'find_command', 'ask', 'Inf',
           'normalize_name', 'splitext',
           'format_size', 'is_installable_dir',
           'is_svn_page', 'file_contents',
           'split_leading_dir', 'has_leading_dir',
           'make_path_relative', 'normalize_path',
           'renames', 'get_terminal_size', 'get_prog',
           'unzip_file', 'untar_file', 'create_download_cache_folder',
           'cache_download', 'unpack_file', 'call_subprocess']


def get_prog():
    try:
        if os.path.basename(sys.argv[0]) in ('__main__.py', '-c'):
            return "%s -m pip" % sys.executable
    except (AttributeError, TypeError, IndexError):
        pass
    return 'pip'


def rmtree(dir, ignore_errors=False):
    shutil.rmtree(dir, ignore_errors=ignore_errors,
                  onerror=rmtree_errorhandler)


def rmtree_errorhandler(func, path, exc_info):
    """On Windows, the files in .svn are read-only, so when rmtree() tries to
    remove them, an exception is thrown.  We catch that here, remove the
    read-only attribute, and hopefully continue without problems."""
    exctype, value = exc_info[:2]
    if not ((exctype is WindowsError and value.args[0] == 5) or #others
            (exctype is OSError and value.args[0] == 13) or #python2.4
            (exctype is PermissionError and value.args[3] == 5) #python3.3
            ):
        raise
    # file type should currently be read only
    if ((os.stat(path).st_mode & stat.S_IREAD) != stat.S_IREAD):
        raise
    # convert to read/write
    os.chmod(path, stat.S_IWRITE)
    # use the original function to repeat the operation
    func(path)


def display_path(path):
    """Gives the display value for a given path, making it relative to cwd
    if possible."""
    path = os.path.normcase(os.path.abspath(path))
    if path.startswith(os.getcwd() + os.path.sep):
        path = '.' + path[len(os.getcwd()):]
    return path


def backup_dir(dir, ext='.bak'):
    """Figure out the name of a directory to back up the given dir to
    (adding .bak, .bak2, etc)"""
    n = 1
    extension = ext
    while os.path.exists(dir + extension):
        n += 1
        extension = ext + str(n)
    return dir + extension


def find_command(cmd, paths=None, pathext=None):
    """Searches the PATH for the given command and returns its path"""
    if paths is None:
        paths = os.environ.get('PATH', '').split(os.pathsep)
    if isinstance(paths, string_types):
        paths = [paths]
    # check if there are funny path extensions for executables, e.g. Windows
    if pathext is None:
        pathext = get_pathext()
    pathext = [ext for ext in pathext.lower().split(os.pathsep) if len(ext)]
    # don't use extensions if the command ends with one of them
    if os.path.splitext(cmd)[1].lower() in pathext:
        pathext = ['']
    # check if we find the command on PATH
    for path in paths:
        # try without extension first
        cmd_path = os.path.join(path, cmd)
        for ext in pathext:
            # then including the extension
            cmd_path_ext = cmd_path + ext
            if os.path.isfile(cmd_path_ext):
                return cmd_path_ext
        if os.path.isfile(cmd_path):
            return cmd_path
    raise BadCommand('Cannot find command %r' % cmd)


def get_pathext(default_pathext=None):
    """Returns the path extensions from environment or a default"""
    if default_pathext is None:
        default_pathext = os.pathsep.join(['.COM', '.EXE', '.BAT', '.CMD'])
    pathext = os.environ.get('PATHEXT', default_pathext)
    return pathext


def ask_path_exists(message, options):
    for action in os.environ.get('PIP_EXISTS_ACTION', '').split():
        if action in options:
            return action
    return ask(message, options)


def ask(message, options):
    """Ask the message interactively, with the given possible responses"""
    while 1:
        if os.environ.get('PIP_NO_INPUT'):
            raise Exception('No input was expected ($PIP_NO_INPUT set); question: %s' % message)
        response = raw_input(message)
        response = response.strip().lower()
        if response not in options:
            print('Your response (%r) was not one of the expected responses: %s' % (
                response, ', '.join(options)))
        else:
            return response


class _Inf(object):
    """I am bigger than everything!"""

    def __eq__(self, other):
        if self is other:
            return True
        else:
            return False

    def __ne__(self, other):
        return not self.__eq__(other)

    def __lt__(self, other):
        return False

    def __le__(self, other):
        return False

    def __gt__(self, other):
        return True

    def __ge__(self, other):
        return True

    def __repr__(self):
        return 'Inf'


Inf = _Inf() #this object is not currently used as a sortable in our code
del _Inf


_normalize_re = re.compile(r'[^a-z]', re.I)


def normalize_name(name):
    return _normalize_re.sub('-', name.lower())


def format_size(bytes):
    if bytes > 1000*1000:
        return '%.1fMB' % (bytes/1000.0/1000)
    elif bytes > 10*1000:
        return '%ikB' % (bytes/1000)
    elif bytes > 1000:
        return '%.1fkB' % (bytes/1000.0)
    else:
        return '%ibytes' % bytes


def is_installable_dir(path):
    """Return True if `path` is a directory containing a setup.py file."""
    if not os.path.isdir(path):
        return False
    setup_py = os.path.join(path, 'setup.py')
    if os.path.isfile(setup_py):
        return True
    return False


def is_svn_page(html):
    """Returns true if the page appears to be the index page of an svn repository"""
    return (re.search(r'<title>[^<]*Revision \d+:', html)
            and re.search(r'Powered by (?:<a[^>]*?>)?Subversion', html, re.I))


def file_contents(filename):
    fp = open(filename, 'rb')
    try:
        return fp.read().decode('utf-8')
    finally:
        fp.close()


def split_leading_dir(path):
    path = str(path)
    path = path.lstrip('/').lstrip('\\')
    if '/' in path and (('\\' in path and path.find('/') < path.find('\\'))
                        or '\\' not in path):
        return path.split('/', 1)
    elif '\\' in path:
        return path.split('\\', 1)
    else:
        return path, ''


def has_leading_dir(paths):
    """Returns true if all the paths have the same leading path name
    (i.e., everything is in one subdirectory in an archive)"""
    common_prefix = None
    for path in paths:
        prefix, rest = split_leading_dir(path)
        if not prefix:
            return False
        elif common_prefix is None:
            common_prefix = prefix
        elif prefix != common_prefix:
            return False
    return True


def make_path_relative(path, rel_to):
    """
    Make a filename relative, where the filename path, and it is
    relative to rel_to

        >>> make_relative_path('/usr/share/something/a-file.pth',
        ...                    '/usr/share/another-place/src/Directory')
        '../../../something/a-file.pth'
        >>> make_relative_path('/usr/share/something/a-file.pth',
        ...                    '/home/user/src/Directory')
        '../../../usr/share/something/a-file.pth'
        >>> make_relative_path('/usr/share/a-file.pth', '/usr/share/')
        'a-file.pth'
    """
    path_filename = os.path.basename(path)
    path = os.path.dirname(path)
    path = os.path.normpath(os.path.abspath(path))
    rel_to = os.path.normpath(os.path.abspath(rel_to))
    path_parts = path.strip(os.path.sep).split(os.path.sep)
    rel_to_parts = rel_to.strip(os.path.sep).split(os.path.sep)
    while path_parts and rel_to_parts and path_parts[0] == rel_to_parts[0]:
        path_parts.pop(0)
        rel_to_parts.pop(0)
    full_parts = ['..']*len(rel_to_parts) + path_parts + [path_filename]
    if full_parts == ['']:
        return '.' + os.path.sep
    return os.path.sep.join(full_parts)


def normalize_path(path):
    """
    Convert a path to its canonical, case-normalized, absolute version.

    """
    return os.path.normcase(os.path.realpath(os.path.expanduser(path)))


def splitext(path):
    """Like os.path.splitext, but take off .tar too"""
    base, ext = posixpath.splitext(path)
    if base.lower().endswith('.tar'):
        ext = base[-4:] + ext
        base = base[:-4]
    return base, ext


def renames(old, new):
    """Like os.renames(), but handles renaming across devices."""
    # Implementation borrowed from os.renames().
    head, tail = os.path.split(new)
    if head and tail and not os.path.exists(head):
        os.makedirs(head)

    shutil.move(old, new)

    head, tail = os.path.split(old)
    if head and tail:
        try:
            os.removedirs(head)
        except OSError:
            pass


def is_local(path):
    """
    Return True if path is within sys.prefix, if we're running in a virtualenv.

    If we're not in a virtualenv, all paths are considered "local."

    """
    if not running_under_virtualenv():
        return True
    return normalize_path(path).startswith(normalize_path(sys.prefix))


def dist_is_local(dist):
    """
    Return True if given Distribution object is installed locally
    (i.e. within current virtualenv).

    Always True if we're not in a virtualenv.

    """
    return is_local(dist_location(dist))


def dist_in_usersite(dist):
    """
    Return True if given Distribution is installed in user site.
    """
    if user_site:
        return normalize_path(dist_location(dist)).startswith(normalize_path(user_site))
    else:
        return False

def dist_in_site_packages(dist):
    """
    Return True if given Distribution is installed in distutils.sysconfig.get_python_lib().
    """
    return normalize_path(dist_location(dist)).startswith(normalize_path(site_packages))


def dist_is_editable(dist):
    """Is distribution an editable install?"""
    #TODO: factor out determining editableness out of FrozenRequirement
    from pip import FrozenRequirement
    req = FrozenRequirement.from_dist(dist, [])
    return req.editable

def get_installed_distributions(local_only=True,
                                skip=('setuptools', 'pip', 'python', 'distribute'),
                                include_editables=True,
                                editables_only=False):
    """
    Return a list of installed Distribution objects.

    If ``local_only`` is True (default), only return installations
    local to the current virtualenv, if in a virtualenv.

    ``skip`` argument is an iterable of lower-case project names to
    ignore; defaults to ('setuptools', 'pip', 'python'). [FIXME also
    skip virtualenv?]

    If ``editables`` is False, don't report editables.

    If ``editables_only`` is True , only report editables.

    """
    if local_only:
        local_test = dist_is_local
    else:
        local_test = lambda d: True

    if include_editables:
        editable_test = lambda d: True
    else:
        editable_test = lambda d: not dist_is_editable(d)

    if editables_only:
        editables_only_test = lambda d: dist_is_editable(d)
    else:
        editables_only_test = lambda d: True

    return [d for d in pkg_resources.working_set
            if local_test(d)
            and d.key not in skip
            and editable_test(d)
            and editables_only_test(d)
            ]


def egg_link_path(dist):
    """
    Return the path for the .egg-link file if it exists, otherwise, None.

    There's 3 scenarios:
    1) not in a virtualenv
       try to find in site.USER_SITE, then site_packages
    2) in a no-global virtualenv
       try to find in site_packages
    3) in a yes-global virtualenv
       try to find in site_packages, then site.USER_SITE  (don't look in global location)

    For #1 and #3, there could be odd cases, where there's an egg-link in 2 locations.
    This method will just return the first one found.
    """
    sites = []
    if running_under_virtualenv():
        if virtualenv_no_global():
            sites.append(site_packages)
        else:
            sites.append(site_packages)
            if user_site:
                sites.append(user_site)
    else:
        if user_site:
            sites.append(user_site)
        sites.append(site_packages)

    for site in sites:
        egglink = os.path.join(site, dist.project_name) + '.egg-link'
        if os.path.isfile(egglink):
            return egglink


def dist_location(dist):
    """
    Get the site-packages location of this distribution. Generally
    this is dist.location, except in the case of develop-installed
    packages, where dist.location is the source code location, and we
    want to know where the egg-link file is.

    """
    egg_link = egg_link_path(dist)
    if egg_link:
        return egg_link
    return dist.location


def get_terminal_size():
    """Returns a tuple (x, y) representing the width(x) and the height(x)
    in characters of the terminal window."""
    def ioctl_GWINSZ(fd):
        try:
            import fcntl
            import termios
            import struct
            cr = struct.unpack('hh', fcntl.ioctl(fd, termios.TIOCGWINSZ,
        '1234'))
        except:
            return None
        if cr == (0, 0):
            return None
        if cr == (0, 0):
            return None
        return cr
    cr = ioctl_GWINSZ(0) or ioctl_GWINSZ(1) or ioctl_GWINSZ(2)
    if not cr:
        try:
            fd = os.open(os.ctermid(), os.O_RDONLY)
            cr = ioctl_GWINSZ(fd)
            os.close(fd)
        except:
            pass
    if not cr:
        cr = (os.environ.get('LINES', 25), os.environ.get('COLUMNS', 80))
    return int(cr[1]), int(cr[0])


def current_umask():
    """Get the current umask which involves having to set it temporarily."""
    mask = os.umask(0)
    os.umask(mask)
    return mask


def unzip_file(filename, location, flatten=True):
    """
    Unzip the file (with path `filename`) to the destination `location`.  All
    files are written based on system defaults and umask (i.e. permissions are
    not preserved), except that regular file members with any execute
    permissions (user, group, or world) have "chmod +x" applied after being
    written. Note that for windows, any execute changes using os.chmod are
    no-ops per the python docs.
    """
    if not os.path.exists(location):
        os.makedirs(location)
    zipfp = open(filename, 'rb')
    try:
        zip = zipfile.ZipFile(zipfp)
        leading = has_leading_dir(zip.namelist()) and flatten
        for info in zip.infolist():
            name = info.filename
            data = zip.read(name)
            fn = name
            if leading:
                fn = split_leading_dir(name)[1]
            fn = os.path.join(location, fn)
            dir = os.path.dirname(fn)
            if not os.path.exists(dir):
                os.makedirs(dir)
            if fn.endswith('/') or fn.endswith('\\'):
                # A directory
                if not os.path.exists(fn):
                    os.makedirs(fn)
            else:
                fp = open(fn, 'wb')
                try:
                    fp.write(data)
                finally:
                    fp.close()
                    mode = info.external_attr >> 16
                    # if mode and regular file and any execute permissions for user/group/world?
                    if mode and stat.S_ISREG(mode) and  mode & 0o111:
                        # make dest file have execute for user/group/world (chmod +x)
                        # no-op on windows per python docs
                        os.chmod(fn, (0o777-current_umask() | 0o111))
    finally:
        zipfp.close()


def untar_file(filename, location):
    """
    Untar the file (with path `filename`) to the destination `location`.
    All files are written based on system defaults and umask (i.e. permissions
    are not preserved), except that regular file members with any execute
    permissions (user, group, or world) have "chmod +x" applied after being
    written.  Note that for windows, any execute changes using os.chmod are
    no-ops per the python docs.
    """
    if not os.path.exists(location):
        os.makedirs(location)
    if filename.lower().endswith('.gz') or filename.lower().endswith('.tgz'):
        mode = 'r:gz'
    elif filename.lower().endswith('.bz2') or filename.lower().endswith('.tbz'):
        mode = 'r:bz2'
    elif filename.lower().endswith('.tar'):
        mode = 'r'
    else:
        logger.warn('Cannot determine compression type for file %s' % filename)
        mode = 'r:*'
    tar = tarfile.open(filename, mode)
    try:
        # note: python<=2.5 doesnt seem to know about pax headers, filter them
        leading = has_leading_dir([
            member.name for member in tar.getmembers()
            if member.name != 'pax_global_header'
        ])
        for member in tar.getmembers():
            fn = member.name
            if fn == 'pax_global_header':
                continue
            if leading:
                fn = split_leading_dir(fn)[1]
            path = os.path.join(location, fn)
            if member.isdir():
                if not os.path.exists(path):
                    os.makedirs(path)
            elif member.issym():
                try:
                    tar._extract_member(member, path)
                except:
                    e = sys.exc_info()[1]
                    # Some corrupt tar files seem to produce this
                    # (specifically bad symlinks)
                    logger.warn(
                        'In the tar file %s the member %s is invalid: %s'
                        % (filename, member.name, e))
                    continue
            else:
                try:
                    fp = tar.extractfile(member)
                except (KeyError, AttributeError):
                    e = sys.exc_info()[1]
                    # Some corrupt tar files seem to produce this
                    # (specifically bad symlinks)
                    logger.warn(
                        'In the tar file %s the member %s is invalid: %s'
                        % (filename, member.name, e))
                    continue
                if not os.path.exists(os.path.dirname(path)):
                    os.makedirs(os.path.dirname(path))
                destfp = open(path, 'wb')
                try:
                    shutil.copyfileobj(fp, destfp)
                finally:
                    destfp.close()
                fp.close()
                # member have any execute permissions for user/group/world?
                if member.mode & 0o111:
                    # make dest file have execute for user/group/world
                    # no-op on windows per python docs
                    os.chmod(path, (0o777-current_umask() | 0o111))
    finally:
        tar.close()


def create_download_cache_folder(folder):
    logger.indent -= 2
    logger.notify('Creating supposed download cache at %s' % folder)
    logger.indent += 2
    os.makedirs(folder)


def cache_download(target_file, temp_location, content_type):
    logger.notify('Storing download in cache at %s' % display_path(target_file))
    shutil.copyfile(temp_location, target_file)
    fp = open(target_file+'.content-type', 'w')
    fp.write(content_type)
    fp.close()


def unpack_file(filename, location, content_type, link):
    filename = os.path.realpath(filename)
    if (content_type == 'application/zip'
        or filename.endswith('.zip')
        or filename.endswith('.pybundle')
        or filename.endswith('.whl')
        or zipfile.is_zipfile(filename)):
        unzip_file(filename, location, flatten=not filename.endswith(('.pybundle', '.whl')))
    elif (content_type == 'application/x-gzip'
          or tarfile.is_tarfile(filename)
          or splitext(filename)[1].lower() in ('.tar', '.tar.gz', '.tar.bz2', '.tgz', '.tbz')):
        untar_file(filename, location)
    elif (content_type and content_type.startswith('text/html')
          and is_svn_page(file_contents(filename))):
        # We don't really care about this
        from pip.vcs.subversion import Subversion
        Subversion('svn+' + link.url).unpack(location)
    else:
        ## FIXME: handle?
        ## FIXME: magic signatures?
        logger.fatal('Cannot unpack file %s (downloaded from %s, content-type: %s); cannot detect archive format'
                     % (filename, location, content_type))
        raise InstallationError('Cannot determine archive format of %s' % location)


def call_subprocess(cmd, show_stdout=True,
                    filter_stdout=None, cwd=None,
                    raise_on_returncode=True,
                    command_level=logger.DEBUG, command_desc=None,
                    extra_environ=None):
    if command_desc is None:
        cmd_parts = []
        for part in cmd:
            if ' ' in part or '\n' in part or '"' in part or "'" in part:
                part = '"%s"' % part.replace('"', '\\"')
            cmd_parts.append(part)
        command_desc = ' '.join(cmd_parts)
    if show_stdout:
        stdout = None
    else:
        stdout = subprocess.PIPE
    logger.log(command_level, "Running command %s" % command_desc)
    env = os.environ.copy()
    if extra_environ:
        env.update(extra_environ)
    try:
        proc = subprocess.Popen(
            cmd, stderr=subprocess.STDOUT, stdin=None, stdout=stdout,
            cwd=cwd, env=env)
    except Exception:
        e = sys.exc_info()[1]
        logger.fatal(
            "Error %s while executing command %s" % (e, command_desc))
        raise
    all_output = []
    if stdout is not None:
        stdout = proc.stdout
        while 1:
            line = console_to_str(stdout.readline())
            if not line:
                break
            line = line.rstrip()
            all_output.append(line + '\n')
            if filter_stdout:
                level = filter_stdout(line)
                if isinstance(level, tuple):
                    level, line = level
                logger.log(level, line)
                if not logger.stdout_level_matches(level):
                    logger.show_progress()
            else:
                logger.info(line)
    else:
        returned_stdout, returned_stderr = proc.communicate()
        all_output = [returned_stdout or '']
    proc.wait()
    if proc.returncode:
        if raise_on_returncode:
            if all_output:
                logger.notify('Complete output from command %s:' % command_desc)
                logger.notify('\n'.join(all_output) + '\n----------------------------------------')
            raise InstallationError(
                "Command %s failed with error code %s in %s"
                % (command_desc, proc.returncode, cwd))
        else:
            logger.warn(
                "Command %s had error code %s in %s"
                % (command_desc, proc.returncode, cwd))
    if stdout is not None:
        return ''.join(all_output)


def is_prerelease(vers):
    """
    Attempt to determine if this is a pre-release using PEP386/PEP426 rules.

    Will return True if it is a pre-release and False if not. Versions are
    assumed to be a pre-release if they cannot be parsed.
    """
    normalized = version._suggest_normalized_version(vers)

    if normalized is None:
        # Cannot normalize, assume it is a pre-release
        return True

    parsed = version._normalized_key(normalized)
    return any([any([y in set(["a", "b", "c", "rc", "dev"]) for y in x]) for x in parsed])
