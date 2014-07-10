#!/usr/bin/env python

""" This module tries to retrieve as much platform-identifying data as
    possible. It makes this information available via function APIs.

    If called from the command line, it prints the platform
    information concatenated as single string to stdout. The output
    format is useable as part of a filename.

"""
#    This module is maintained by Marc-Andre Lemburg <mal@egenix.com>.
#    If you find problems, please submit bug reports/patches via the
#    Python bug tracker (http://bugs.python.org) and assign them to "lemburg".
#
#    Note: Please keep this module compatible to Python 1.5.2.
#
#    Still needed:
#    * more support for WinCE
#    * support for MS-DOS (PythonDX ?)
#    * support for Amiga and other still unsupported platforms running Python
#    * support for additional Linux distributions
#
#    Many thanks to all those who helped adding platform-specific
#    checks (in no particular order):
#
#      Charles G Waldman, David Arnold, Gordon McMillan, Ben Darnell,
#      Jeff Bauer, Cliff Crawford, Ivan Van Laningham, Josef
#      Betancourt, Randall Hopper, Karl Putland, John Farrell, Greg
#      Andruk, Just van Rossum, Thomas Heller, Mark R. Levinson, Mark
#      Hammond, Bill Tutt, Hans Nowak, Uwe Zessin (OpenVMS support),
#      Colin Kong, Trent Mick, Guido van Rossum, Anthony Baxter
#
#    History:
#
#    <see CVS and SVN checkin messages for history>
#
#    1.0.7 - added DEV_NULL
#    1.0.6 - added linux_distribution()
#    1.0.5 - fixed Java support to allow running the module on Jython
#    1.0.4 - added IronPython support
#    1.0.3 - added normalization of Windows system name
#    1.0.2 - added more Windows support
#    1.0.1 - reformatted to make doc.py happy
#    1.0.0 - reformatted a bit and checked into Python CVS
#    0.8.0 - added sys.version parser and various new access
#            APIs (python_version(), python_compiler(), etc.)
#    0.7.2 - fixed architecture() to use sizeof(pointer) where available
#    0.7.1 - added support for Caldera OpenLinux
#    0.7.0 - some fixes for WinCE; untabified the source file
#    0.6.2 - support for OpenVMS - requires version 1.5.2-V006 or higher and
#            vms_lib.getsyi() configured
#    0.6.1 - added code to prevent 'uname -p' on platforms which are
#            known not to support it
#    0.6.0 - fixed win32_ver() to hopefully work on Win95,98,NT and Win2k;
#            did some cleanup of the interfaces - some APIs have changed
#    0.5.5 - fixed another type in the MacOS code... should have
#            used more coffee today ;-)
#    0.5.4 - fixed a few typos in the MacOS code
#    0.5.3 - added experimental MacOS support; added better popen()
#            workarounds in _syscmd_ver() -- still not 100% elegant
#            though
#    0.5.2 - fixed uname() to return '' instead of 'unknown' in all
#            return values (the system uname command tends to return
#            'unknown' instead of just leaving the field emtpy)
#    0.5.1 - included code for slackware dist; added exception handlers
#            to cover up situations where platforms don't have os.popen
#            (e.g. Mac) or fail on socket.gethostname(); fixed libc
#            detection RE
#    0.5.0 - changed the API names referring to system commands to *syscmd*;
#            added java_ver(); made syscmd_ver() a private
#            API (was system_ver() in previous versions) -- use uname()
#            instead; extended the win32_ver() to also return processor
#            type information
#    0.4.0 - added win32_ver() and modified the platform() output for WinXX
#    0.3.4 - fixed a bug in _follow_symlinks()
#    0.3.3 - fixed popen() and "file" command invokation bugs
#    0.3.2 - added architecture() API and support for it in platform()
#    0.3.1 - fixed syscmd_ver() RE to support Windows NT
#    0.3.0 - added system alias support
#    0.2.3 - removed 'wince' again... oh well.
#    0.2.2 - added 'wince' to syscmd_ver() supported platforms
#    0.2.1 - added cache logic and changed the platform string format
#    0.2.0 - changed the API to use functions instead of module globals
#            since some action take too long to be run on module import
#    0.1.0 - first release
#
#    You can always get the latest version of this module at:
#
#             http://www.egenix.com/files/python/platform.py
#
#    If that URL should fail, try contacting the author.

__copyright__ = """
    Copyright (c) 1999-2000, Marc-Andre Lemburg; mailto:mal@lemburg.com
    Copyright (c) 2000-2010, eGenix.com Software GmbH; mailto:info@egenix.com

    Permission to use, copy, modify, and distribute this software and its
    documentation for any purpose and without fee or royalty is hereby granted,
    provided that the above copyright notice appear in all copies and that
    both that copyright notice and this permission notice appear in
    supporting documentation or portions thereof, including modifications,
    that you make.

    EGENIX.COM SOFTWARE GMBH DISCLAIMS ALL WARRANTIES WITH REGARD TO
    THIS SOFTWARE, INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND
    FITNESS, IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL,
    INDIRECT OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING
    FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT,
    NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION
    WITH THE USE OR PERFORMANCE OF THIS SOFTWARE !

"""

__version__ = '1.0.7'

import sys,string,os,re

### Globals & Constants

# Determine the platform's /dev/null device
try:
    DEV_NULL = os.devnull
except AttributeError:
    # os.devnull was added in Python 2.4, so emulate it for earlier
    # Python versions
    if sys.platform in ('dos','win32','win16','os2'):
        # Use the old CP/M NUL as device name
        DEV_NULL = 'NUL'
    else:
        # Standard Unix uses /dev/null
        DEV_NULL = '/dev/null'

### Platform specific APIs

_libc_search = re.compile(r'(__libc_init)'
                          '|'
                          '(GLIBC_([0-9.]+))'
                          '|'
                          '(libc(_\w+)?\.so(?:\.(\d[0-9.]*))?)')

def libc_ver(executable=sys.executable,lib='',version='',

             chunksize=2048):

    """ Tries to determine the libc version that the file executable
        (which defaults to the Python interpreter) is linked against.

        Returns a tuple of strings (lib,version) which default to the
        given parameters in case the lookup fails.

        Note that the function has intimate knowledge of how different
        libc versions add symbols to the executable and thus is probably
        only useable for executables compiled using gcc.

        The file is read and scanned in chunks of chunksize bytes.

    """
    if hasattr(os.path, 'realpath'):
        # Python 2.2 introduced os.path.realpath(); it is used
        # here to work around problems with Cygwin not being
        # able to open symlinks for reading
        executable = os.path.realpath(executable)
    f = open(executable,'rb')
    binary = f.read(chunksize)
    pos = 0
    while 1:
        m = _libc_search.search(binary,pos)
        if not m:
            binary = f.read(chunksize)
            if not binary:
                break
            pos = 0
            continue
        libcinit,glibc,glibcversion,so,threads,soversion = m.groups()
        if libcinit and not lib:
            lib = 'libc'
        elif glibc:
            if lib != 'glibc':
                lib = 'glibc'
                version = glibcversion
            elif glibcversion > version:
                version = glibcversion
        elif so:
            if lib != 'glibc':
                lib = 'libc'
                if soversion and soversion > version:
                    version = soversion
                if threads and version[-len(threads):] != threads:
                    version = version + threads
        pos = m.end()
    f.close()
    return lib,version

def _dist_try_harder(distname,version,id):

    """ Tries some special tricks to get the distribution
        information in case the default method fails.

        Currently supports older SuSE Linux, Caldera OpenLinux and
        Slackware Linux distributions.

    """
    if os.path.exists('/var/adm/inst-log/info'):
        # SuSE Linux stores distribution information in that file
        info = open('/var/adm/inst-log/info').readlines()
        distname = 'SuSE'
        for line in info:
            tv = string.split(line)
            if len(tv) == 2:
                tag,value = tv
            else:
                continue
            if tag == 'MIN_DIST_VERSION':
                version = string.strip(value)
            elif tag == 'DIST_IDENT':
                values = string.split(value,'-')
                id = values[2]
        return distname,version,id

    if os.path.exists('/etc/.installed'):
        # Caldera OpenLinux has some infos in that file (thanks to Colin Kong)
        info = open('/etc/.installed').readlines()
        for line in info:
            pkg = string.split(line,'-')
            if len(pkg) >= 2 and pkg[0] == 'OpenLinux':
                # XXX does Caldera support non Intel platforms ? If yes,
                #     where can we find the needed id ?
                return 'OpenLinux',pkg[1],id

    if os.path.isdir('/usr/lib/setup'):
        # Check for slackware version tag file (thanks to Greg Andruk)
        verfiles = os.listdir('/usr/lib/setup')
        for n in range(len(verfiles)-1, -1, -1):
            if verfiles[n][:14] != 'slack-version-':
                del verfiles[n]
        if verfiles:
            verfiles.sort()
            distname = 'slackware'
            version = verfiles[-1][14:]
            return distname,version,id

    return distname,version,id

_release_filename = re.compile(r'(\w+)[-_](release|version)')
_lsb_release_version = re.compile(r'(.+)'
                                   ' release '
                                   '([\d.]+)'
                                   '[^(]*(?:\((.+)\))?')
_release_version = re.compile(r'([^0-9]+)'
                               '(?: release )?'
                               '([\d.]+)'
                               '[^(]*(?:\((.+)\))?')

# See also http://www.novell.com/coolsolutions/feature/11251.html
# and http://linuxmafia.com/faq/Admin/release-files.html
# and http://data.linux-ntfs.org/rpm/whichrpm
# and http://www.die.net/doc/linux/man/man1/lsb_release.1.html

_supported_dists = (
    'SuSE', 'debian', 'fedora', 'redhat', 'centos',
    'mandrake', 'mandriva', 'rocks', 'slackware', 'yellowdog', 'gentoo',
    'UnitedLinux', 'turbolinux')

def _parse_release_file(firstline):

    # Default to empty 'version' and 'id' strings.  Both defaults are used
    # when 'firstline' is empty.  'id' defaults to empty when an id can not
    # be deduced.
    version = ''
    id = ''

    # Parse the first line
    m = _lsb_release_version.match(firstline)
    if m is not None:
        # LSB format: "distro release x.x (codename)"
        return tuple(m.groups())

    # Pre-LSB format: "distro x.x (codename)"
    m = _release_version.match(firstline)
    if m is not None:
        return tuple(m.groups())

    # Unknown format... take the first two words
    l = string.split(string.strip(firstline))
    if l:
        version = l[0]
        if len(l) > 1:
            id = l[1]
    return '', version, id

def linux_distribution(distname='', version='', id='',

                       supported_dists=_supported_dists,
                       full_distribution_name=1):

    """ Tries to determine the name of the Linux OS distribution name.

        The function first looks for a distribution release file in
        /etc and then reverts to _dist_try_harder() in case no
        suitable files are found.

        supported_dists may be given to define the set of Linux
        distributions to look for. It defaults to a list of currently
        supported Linux distributions identified by their release file
        name.

        If full_distribution_name is true (default), the full
        distribution read from the OS is returned. Otherwise the short
        name taken from supported_dists is used.

        Returns a tuple (distname,version,id) which default to the
        args given as parameters.

    """
    try:
        etc = os.listdir('/etc')
    except os.error:
        # Probably not a Unix system
        return distname,version,id
    etc.sort()
    for file in etc:
        m = _release_filename.match(file)
        if m is not None:
            _distname,dummy = m.groups()
            if _distname in supported_dists:
                distname = _distname
                break
    else:
        return _dist_try_harder(distname,version,id)

    # Read the first line
    f = open('/etc/'+file, 'r')
    firstline = f.readline()
    f.close()
    _distname, _version, _id = _parse_release_file(firstline)

    if _distname and full_distribution_name:
        distname = _distname
    if _version:
        version = _version
    if _id:
        id = _id
    return distname, version, id

# To maintain backwards compatibility:

def dist(distname='',version='',id='',

         supported_dists=_supported_dists):

    """ Tries to determine the name of the Linux OS distribution name.

        The function first looks for a distribution release file in
        /etc and then reverts to _dist_try_harder() in case no
        suitable files are found.

        Returns a tuple (distname,version,id) which default to the
        args given as parameters.

    """
    return linux_distribution(distname, version, id,
                              supported_dists=supported_dists,
                              full_distribution_name=0)

class _popen:

    """ Fairly portable (alternative) popen implementation.

        This is mostly needed in case os.popen() is not available, or
        doesn't work as advertised, e.g. in Win9X GUI programs like
        PythonWin or IDLE.

        Writing to the pipe is currently not supported.

    """
    tmpfile = ''
    pipe = None
    bufsize = None
    mode = 'r'

    def __init__(self,cmd,mode='r',bufsize=None):

        if mode != 'r':
            raise ValueError,'popen()-emulation only supports read mode'
        import tempfile
        self.tmpfile = tmpfile = tempfile.mktemp()
        os.system(cmd + ' > %s' % tmpfile)
        self.pipe = open(tmpfile,'rb')
        self.bufsize = bufsize
        self.mode = mode

    def read(self):

        return self.pipe.read()

    def readlines(self):

        if self.bufsize is not None:
            return self.pipe.readlines()

    def close(self,

              remove=os.unlink,error=os.error):

        if self.pipe:
            rc = self.pipe.close()
        else:
            rc = 255
        if self.tmpfile:
            try:
                remove(self.tmpfile)
            except error:
                pass
        return rc

    # Alias
    __del__ = close

def popen(cmd, mode='r', bufsize=None):

    """ Portable popen() interface.
    """
    # Find a working popen implementation preferring win32pipe.popen
    # over os.popen over _popen
    popen = None
    if os.environ.get('OS','') == 'Windows_NT':
        # On NT win32pipe should work; on Win9x it hangs due to bugs
        # in the MS C lib (see MS KnowledgeBase article Q150956)
        try:
            import win32pipe
        except ImportError:
            pass
        else:
            popen = win32pipe.popen
    if popen is None:
        if hasattr(os,'popen'):
            popen = os.popen
            # Check whether it works... it doesn't in GUI programs
            # on Windows platforms
            if sys.platform == 'win32': # XXX Others too ?
                try:
                    popen('')
                except os.error:
                    popen = _popen
        else:
            popen = _popen
    if bufsize is None:
        return popen(cmd,mode)
    else:
        return popen(cmd,mode,bufsize)

def _norm_version(version, build=''):

    """ Normalize the version and build strings and return a single
        version string using the format major.minor.build (or patchlevel).
    """
    l = string.split(version,'.')
    if build:
        l.append(build)
    try:
        ints = map(int,l)
    except ValueError:
        strings = l
    else:
        strings = map(str,ints)
    version = string.join(strings[:3],'.')
    return version

_ver_output = re.compile(r'(?:([\w ]+) ([\w.]+) '
                         '.*'
                         '\[.* ([\d.]+)\])')

# Examples of VER command output:
#
#   Windows 2000:  Microsoft Windows 2000 [Version 5.00.2195]
#   Windows XP:    Microsoft Windows XP [Version 5.1.2600]
#   Windows Vista: Microsoft Windows [Version 6.0.6002]
#
# Note that the "Version" string gets localized on different
# Windows versions.

def _syscmd_ver(system='', release='', version='',

               supported_platforms=('win32','win16','dos','os2')):

    """ Tries to figure out the OS version used and returns
        a tuple (system,release,version).

        It uses the "ver" shell command for this which is known
        to exists on Windows, DOS and OS/2. XXX Others too ?

        In case this fails, the given parameters are used as
        defaults.

    """
    if sys.platform not in supported_platforms:
        return system,release,version

    # Try some common cmd strings
    for cmd in ('ver','command /c ver','cmd /c ver'):
        try:
            pipe = popen(cmd)
            info = pipe.read()
            if pipe.close():
                raise os.error,'command failed'
            # XXX How can I suppress shell errors from being written
            #     to stderr ?
        except os.error,why:
            #print 'Command %s failed: %s' % (cmd,why)
            continue
        except IOError,why:
            #print 'Command %s failed: %s' % (cmd,why)
            continue
        else:
            break
    else:
        return system,release,version

    # Parse the output
    info = string.strip(info)
    m = _ver_output.match(info)
    if m is not None:
        system,release,version = m.groups()
        # Strip trailing dots from version and release
        if release[-1] == '.':
            release = release[:-1]
        if version[-1] == '.':
            version = version[:-1]
        # Normalize the version and build strings (eliminating additional
        # zeros)
        version = _norm_version(version)
    return system,release,version

def _win32_getvalue(key,name,default=''):

    """ Read a value for name from the registry key.

        In case this fails, default is returned.

    """
    try:
        # Use win32api if available
        from win32api import RegQueryValueEx
    except ImportError:
        # On Python 2.0 and later, emulate using _winreg
        import _winreg
        RegQueryValueEx = _winreg.QueryValueEx
    try:
        return RegQueryValueEx(key,name)
    except:
        return default

def win32_ver(release='',version='',csd='',ptype=''):

    """ Get additional version information from the Windows Registry
        and return a tuple (version,csd,ptype) referring to version
        number, CSD level (service pack), and OS type (multi/single
        processor).

        As a hint: ptype returns 'Uniprocessor Free' on single
        processor NT machines and 'Multiprocessor Free' on multi
        processor machines. The 'Free' refers to the OS version being
        free of debugging code. It could also state 'Checked' which
        means the OS version uses debugging code, i.e. code that
        checks arguments, ranges, etc. (Thomas Heller).

        Note: this function works best with Mark Hammond's win32
        package installed, but also on Python 2.3 and later. It
        obviously only runs on Win32 compatible platforms.

    """
    # XXX Is there any way to find out the processor type on WinXX ?
    # XXX Is win32 available on Windows CE ?
    #
    # Adapted from code posted by Karl Putland to comp.lang.python.
    #
    # The mappings between reg. values and release names can be found
    # here: http://msdn.microsoft.com/library/en-us/sysinfo/base/osversioninfo_str.asp

    # Import the needed APIs
    try:
        import win32api
        from win32api import RegQueryValueEx, RegOpenKeyEx, \
             RegCloseKey, GetVersionEx
        from win32con import HKEY_LOCAL_MACHINE, VER_PLATFORM_WIN32_NT, \
             VER_PLATFORM_WIN32_WINDOWS, VER_NT_WORKSTATION
    except ImportError:
        # Emulate the win32api module using Python APIs
        try:
            sys.getwindowsversion
        except AttributeError:
            # No emulation possible, so return the defaults...
            return release,version,csd,ptype
        else:
            # Emulation using _winreg (added in Python 2.0) and
            # sys.getwindowsversion() (added in Python 2.3)
            import _winreg
            GetVersionEx = sys.getwindowsversion
            RegQueryValueEx = _winreg.QueryValueEx
            RegOpenKeyEx = _winreg.OpenKeyEx
            RegCloseKey = _winreg.CloseKey
            HKEY_LOCAL_MACHINE = _winreg.HKEY_LOCAL_MACHINE
            VER_PLATFORM_WIN32_WINDOWS = 1
            VER_PLATFORM_WIN32_NT = 2
            VER_NT_WORKSTATION = 1
            VER_NT_SERVER = 3
            REG_SZ = 1

    # Find out the registry key and some general version infos
    winver = GetVersionEx()
    maj,min,buildno,plat,csd = winver
    version = '%i.%i.%i' % (maj,min,buildno & 0xFFFF)
    if hasattr(winver, "service_pack"):
        if winver.service_pack != "":
            csd = 'SP%s' % winver.service_pack_major
    else:
        if csd[:13] == 'Service Pack ':
            csd = 'SP' + csd[13:]

    if plat == VER_PLATFORM_WIN32_WINDOWS:
        regkey = 'SOFTWARE\\Microsoft\\Windows\\CurrentVersion'
        # Try to guess the release name
        if maj == 4:
            if min == 0:
                release = '95'
            elif min == 10:
                release = '98'
            elif min == 90:
                release = 'Me'
            else:
                release = 'postMe'
        elif maj == 5:
            release = '2000'

    elif plat == VER_PLATFORM_WIN32_NT:
        regkey = 'SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion'
        if maj <= 4:
            release = 'NT'
        elif maj == 5:
            if min == 0:
                release = '2000'
            elif min == 1:
                release = 'XP'
            elif min == 2:
                release = '2003Server'
            else:
                release = 'post2003'
        elif maj == 6:
            if hasattr(winver, "product_type"):
                product_type = winver.product_type
            else:
                product_type = VER_NT_WORKSTATION
                # Without an OSVERSIONINFOEX capable sys.getwindowsversion(),
                # or help from the registry, we cannot properly identify
                # non-workstation versions.
                try:
                    key = RegOpenKeyEx(HKEY_LOCAL_MACHINE, regkey)
                    name, type = RegQueryValueEx(key, "ProductName")
                    # Discard any type that isn't REG_SZ
                    if type == REG_SZ and name.find("Server") != -1:
                        product_type = VER_NT_SERVER
                except WindowsError:
                    # Use default of VER_NT_WORKSTATION
                    pass

            if min == 0:
                if product_type == VER_NT_WORKSTATION:
                    release = 'Vista'
                else:
                    release = '2008Server'
            elif min == 1:
                if product_type == VER_NT_WORKSTATION:
                    release = '7'
                else:
                    release = '2008ServerR2'
            elif min == 2:
                if product_type == VER_NT_WORKSTATION:
                    release = '8'
                else:
                    release = '2012Server'
            else:
                release = 'post2012Server'

    else:
        if not release:
            # E.g. Win3.1 with win32s
            release = '%i.%i' % (maj,min)
        return release,version,csd,ptype

    # Open the registry key
    try:
        keyCurVer = RegOpenKeyEx(HKEY_LOCAL_MACHINE, regkey)
        # Get a value to make sure the key exists...
        RegQueryValueEx(keyCurVer, 'SystemRoot')
    except:
        return release,version,csd,ptype

    # Parse values
    #subversion = _win32_getvalue(keyCurVer,
    #                            'SubVersionNumber',
    #                            ('',1))[0]
    #if subversion:
    #   release = release + subversion # 95a, 95b, etc.
    build = _win32_getvalue(keyCurVer,
                            'CurrentBuildNumber',
                            ('',1))[0]
    ptype = _win32_getvalue(keyCurVer,
                           'CurrentType',
                           (ptype,1))[0]

    # Normalize version
    version = _norm_version(version,build)

    # Close key
    RegCloseKey(keyCurVer)
    return release,version,csd,ptype

def _mac_ver_lookup(selectors,default=None):

    from gestalt import gestalt
    import MacOS
    l = []
    append = l.append
    for selector in selectors:
        try:
            append(gestalt(selector))
        except (RuntimeError, MacOS.Error):
            append(default)
    return l

def _bcd2str(bcd):

    return hex(bcd)[2:]

def _mac_ver_gestalt():
    """
        Thanks to Mark R. Levinson for mailing documentation links and
        code examples for this function. Documentation for the
        gestalt() API is available online at:

           http://www.rgaros.nl/gestalt/
    """
    # Check whether the version info module is available
    try:
        import gestalt
        import MacOS
    except ImportError:
        return None
    # Get the infos
    sysv,sysa = _mac_ver_lookup(('sysv','sysa'))
    # Decode the infos
    if sysv:
        major = (sysv & 0xFF00) >> 8
        minor = (sysv & 0x00F0) >> 4
        patch = (sysv & 0x000F)

        if (major, minor) >= (10, 4):
            # the 'sysv' gestald cannot return patchlevels
            # higher than 9. Apple introduced 3 new
            # gestalt codes in 10.4 to deal with this
            # issue (needed because patch levels can
            # run higher than 9, such as 10.4.11)
            major,minor,patch = _mac_ver_lookup(('sys1','sys2','sys3'))
            release = '%i.%i.%i' %(major, minor, patch)
        else:
            release = '%s.%i.%i' % (_bcd2str(major),minor,patch)

    if sysa:
        machine = {0x1: '68k',
                   0x2: 'PowerPC',
                   0xa: 'i386'}.get(sysa,'')

    versioninfo=('', '', '')
    return release,versioninfo,machine

def _mac_ver_xml():
    fn = '/System/Library/CoreServices/SystemVersion.plist'
    if not os.path.exists(fn):
        return None

    try:
        import plistlib
    except ImportError:
        return None

    pl = plistlib.readPlist(fn)
    release = pl['ProductVersion']
    versioninfo=('', '', '')
    machine = os.uname()[4]
    if machine in ('ppc', 'Power Macintosh'):
        # for compatibility with the gestalt based code
        machine = 'PowerPC'

    return release,versioninfo,machine


def mac_ver(release='',versioninfo=('','',''),machine=''):

    """ Get MacOS version information and return it as tuple (release,
        versioninfo, machine) with versioninfo being a tuple (version,
        dev_stage, non_release_version).

        Entries which cannot be determined are set to the parameter values
        which default to ''. All tuple entries are strings.
    """

    # First try reading the information from an XML file which should
    # always be present
    info = _mac_ver_xml()
    if info is not None:
        return info

    # If that doesn't work for some reason fall back to reading the
    # information using gestalt calls.
    info = _mac_ver_gestalt()
    if info is not None:
        return info

    # If that also doesn't work return the default values
    return release,versioninfo,machine

def _java_getprop(name,default):

    from java.lang import System
    try:
        value = System.getProperty(name)
        if value is None:
            return default
        return value
    except AttributeError:
        return default

def java_ver(release='',vendor='',vminfo=('','',''),osinfo=('','','')):

    """ Version interface for Jython.

        Returns a tuple (release,vendor,vminfo,osinfo) with vminfo being
        a tuple (vm_name,vm_release,vm_vendor) and osinfo being a
        tuple (os_name,os_version,os_arch).

        Values which cannot be determined are set to the defaults
        given as parameters (which all default to '').

    """
    # Import the needed APIs
    try:
        import java.lang
    except ImportError:
        return release,vendor,vminfo,osinfo

    vendor = _java_getprop('java.vendor', vendor)
    release = _java_getprop('java.version', release)
    vm_name, vm_release, vm_vendor = vminfo
    vm_name = _java_getprop('java.vm.name', vm_name)
    vm_vendor = _java_getprop('java.vm.vendor', vm_vendor)
    vm_release = _java_getprop('java.vm.version', vm_release)
    vminfo = vm_name, vm_release, vm_vendor
    os_name, os_version, os_arch = osinfo
    os_arch = _java_getprop('java.os.arch', os_arch)
    os_name = _java_getprop('java.os.name', os_name)
    os_version = _java_getprop('java.os.version', os_version)
    osinfo = os_name, os_version, os_arch

    return release, vendor, vminfo, osinfo

### System name aliasing

def system_alias(system,release,version):

    """ Returns (system,release,version) aliased to common
        marketing names used for some systems.

        It also does some reordering of the information in some cases
        where it would otherwise cause confusion.

    """
    if system == 'Rhapsody':
        # Apple's BSD derivative
        # XXX How can we determine the marketing release number ?
        return 'MacOS X Server',system+release,version

    elif system == 'SunOS':
        # Sun's OS
        if release < '5':
            # These releases use the old name SunOS
            return system,release,version
        # Modify release (marketing release = SunOS release - 3)
        l = string.split(release,'.')
        if l:
            try:
                major = int(l[0])
            except ValueError:
                pass
            else:
                major = major - 3
                l[0] = str(major)
                release = string.join(l,'.')
        if release < '6':
            system = 'Solaris'
        else:
            # XXX Whatever the new SunOS marketing name is...
            system = 'Solaris'

    elif system == 'IRIX64':
        # IRIX reports IRIX64 on platforms with 64-bit support; yet it
        # is really a version and not a different platform, since 32-bit
        # apps are also supported..
        system = 'IRIX'
        if version:
            version = version + ' (64bit)'
        else:
            version = '64bit'

    elif system in ('win32','win16'):
        # In case one of the other tricks
        system = 'Windows'

    return system,release,version

### Various internal helpers

def _platform(*args):

    """ Helper to format the platform string in a filename
        compatible format e.g. "system-version-machine".
    """
    # Format the platform string
    platform = string.join(
        map(string.strip,
            filter(len, args)),
        '-')

    # Cleanup some possible filename obstacles...
    replace = string.replace
    platform = replace(platform,' ','_')
    platform = replace(platform,'/','-')
    platform = replace(platform,'\\','-')
    platform = replace(platform,':','-')
    platform = replace(platform,';','-')
    platform = replace(platform,'"','-')
    platform = replace(platform,'(','-')
    platform = replace(platform,')','-')

    # No need to report 'unknown' information...
    platform = replace(platform,'unknown','')

    # Fold '--'s and remove trailing '-'
    while 1:
        cleaned = replace(platform,'--','-')
        if cleaned == platform:
            break
        platform = cleaned
    while platform[-1] == '-':
        platform = platform[:-1]

    return platform

def _node(default=''):

    """ Helper to determine the node name of this machine.
    """
    try:
        import socket
    except ImportError:
        # No sockets...
        return default
    try:
        return socket.gethostname()
    except socket.error:
        # Still not working...
        return default

# os.path.abspath is new in Python 1.5.2:
if not hasattr(os.path,'abspath'):

    def _abspath(path,

                 isabs=os.path.isabs,join=os.path.join,getcwd=os.getcwd,
                 normpath=os.path.normpath):

        if not isabs(path):
            path = join(getcwd(), path)
        return normpath(path)

else:

    _abspath = os.path.abspath

def _follow_symlinks(filepath):

    """ In case filepath is a symlink, follow it until a
        real file is reached.
    """
    filepath = _abspath(filepath)
    while os.path.islink(filepath):
        filepath = os.path.normpath(
            os.path.join(os.path.dirname(filepath),os.readlink(filepath)))
    return filepath

def _syscmd_uname(option,default=''):

    """ Interface to the system's uname command.
    """
    if sys.platform in ('dos','win32','win16','os2'):
        # XXX Others too ?
        return default
    try:
        f = os.popen('uname %s 2> %s' % (option, DEV_NULL))
    except (AttributeError,os.error):
        return default
    output = string.strip(f.read())
    rc = f.close()
    if not output or rc:
        return default
    else:
        return output

def _syscmd_file(target,default=''):

    """ Interface to the system's file command.

        The function uses the -b option of the file command to have it
        ommit the filename in its output and if possible the -L option
        to have the command follow symlinks. It returns default in
        case the command should fail.

    """

    # We do the import here to avoid a bootstrap issue.
    # See c73b90b6dadd changeset.
    #
    # [..]
    # ranlib libpython2.7.a
    # gcc   -o python \
    #        Modules/python.o \
    #        libpython2.7.a -lsocket -lnsl -ldl    -lm
    # Traceback (most recent call last):
    #  File "./setup.py", line 8, in <module>
    #    from platform import machine as platform_machine
    #  File "[..]/build/Lib/platform.py", line 116, in <module>
    #    import sys,string,os,re,subprocess
    #  File "[..]/build/Lib/subprocess.py", line 429, in <module>
    #    import select
    # ImportError: No module named select

    import subprocess

    if sys.platform in ('dos','win32','win16','os2'):
        # XXX Others too ?
        return default
    target = _follow_symlinks(target)
    try:
        proc = subprocess.Popen(['file', target],
                stdout=subprocess.PIPE, stderr=subprocess.STDOUT)

    except (AttributeError,os.error):
        return default
    output = proc.communicate()[0]
    rc = proc.wait()
    if not output or rc:
        return default
    else:
        return output

### Information about the used architecture

# Default values for architecture; non-empty strings override the
# defaults given as parameters
_default_architecture = {
    'win32': ('','WindowsPE'),
    'win16': ('','Windows'),
    'dos': ('','MSDOS'),
}

_architecture_split = re.compile(r'[\s,]').split

def architecture(executable=sys.executable,bits='',linkage=''):

    """ Queries the given executable (defaults to the Python interpreter
        binary) for various architecture information.

        Returns a tuple (bits,linkage) which contains information about
        the bit architecture and the linkage format used for the
        executable. Both values are returned as strings.

        Values that cannot be determined are returned as given by the
        parameter presets. If bits is given as '', the sizeof(pointer)
        (or sizeof(long) on Python version < 1.5.2) is used as
        indicator for the supported pointer size.

        The function relies on the system's "file" command to do the
        actual work. This is available on most if not all Unix
        platforms. On some non-Unix platforms where the "file" command
        does not exist and the executable is set to the Python interpreter
        binary defaults from _default_architecture are used.

    """
    # Use the sizeof(pointer) as default number of bits if nothing
    # else is given as default.
    if not bits:
        import struct
        try:
            size = struct.calcsize('P')
        except struct.error:
            # Older installations can only query longs
            size = struct.calcsize('l')
        bits = str(size*8) + 'bit'

    # Get data from the 'file' system command
    if executable:
        output = _syscmd_file(executable, '')
    else:
        output = ''

    if not output and \
       executable == sys.executable:
        # "file" command did not return anything; we'll try to provide
        # some sensible defaults then...
        if sys.platform in _default_architecture:
            b, l = _default_architecture[sys.platform]
            if b:
                bits = b
            if l:
                linkage = l
        return bits, linkage

    # Split the output into a list of strings omitting the filename
    fileout = _architecture_split(output)[1:]

    if 'executable' not in fileout:
        # Format not supported
        return bits,linkage

    # Bits
    if '32-bit' in fileout:
        bits = '32bit'
    elif 'N32' in fileout:
        # On Irix only
        bits = 'n32bit'
    elif '64-bit' in fileout:
        bits = '64bit'

    # Linkage
    if 'ELF' in fileout:
        linkage = 'ELF'
    elif 'PE' in fileout:
        # E.g. Windows uses this format
        if 'Windows' in fileout:
            linkage = 'WindowsPE'
        else:
            linkage = 'PE'
    elif 'COFF' in fileout:
        linkage = 'COFF'
    elif 'MS-DOS' in fileout:
        linkage = 'MSDOS'
    else:
        # XXX the A.OUT format also falls under this class...
        pass

    return bits,linkage

### Portable uname() interface

_uname_cache = None

def uname():

    """ Fairly portable uname interface. Returns a tuple
        of strings (system,node,release,version,machine,processor)
        identifying the underlying platform.

        Note that unlike the os.uname function this also returns
        possible processor information as an additional tuple entry.

        Entries which cannot be determined are set to ''.

    """
    global _uname_cache
    no_os_uname = 0

    if _uname_cache is not None:
        return _uname_cache

    processor = ''

    # Get some infos from the builtin os.uname API...
    try:
        system,node,release,version,machine = os.uname()
    except AttributeError:
        no_os_uname = 1

    if no_os_uname or not filter(None, (system, node, release, version, machine)):
        # Hmm, no there is either no uname or uname has returned
        #'unknowns'... we'll have to poke around the system then.
        if no_os_uname:
            system = sys.platform
            release = ''
            version = ''
            node = _node()
            machine = ''

        use_syscmd_ver = 1

        # Try win32_ver() on win32 platforms
        if system == 'win32':
            release,version,csd,ptype = win32_ver()
            if release and version:
                use_syscmd_ver = 0
            # Try to use the PROCESSOR_* environment variables
            # available on Win XP and later; see
            # http://support.microsoft.com/kb/888731 and
            # http://www.geocities.com/rick_lively/MANUALS/ENV/MSWIN/PROCESSI.HTM
            if not machine:
                # WOW64 processes mask the native architecture
                if "PROCESSOR_ARCHITEW6432" in os.environ:
                    machine = os.environ.get("PROCESSOR_ARCHITEW6432", '')
                else:
                    machine = os.environ.get('PROCESSOR_ARCHITECTURE', '')
            if not processor:
                processor = os.environ.get('PROCESSOR_IDENTIFIER', machine)

        # Try the 'ver' system command available on some
        # platforms
        if use_syscmd_ver:
            system,release,version = _syscmd_ver(system)
            # Normalize system to what win32_ver() normally returns
            # (_syscmd_ver() tends to return the vendor name as well)
            if system == 'Microsoft Windows':
                system = 'Windows'
            elif system == 'Microsoft' and release == 'Windows':
                # Under Windows Vista and Windows Server 2008,
                # Microsoft changed the output of the ver command. The
                # release is no longer printed.  This causes the
                # system and release to be misidentified.
                system = 'Windows'
                if '6.0' == version[:3]:
                    release = 'Vista'
                else:
                    release = ''

        # In case we still don't know anything useful, we'll try to
        # help ourselves
        if system in ('win32','win16'):
            if not version:
                if system == 'win32':
                    version = '32bit'
                else:
                    version = '16bit'
            system = 'Windows'

        elif system[:4] == 'java':
            release,vendor,vminfo,osinfo = java_ver()
            system = 'Java'
            version = string.join(vminfo,', ')
            if not version:
                version = vendor

    # System specific extensions
    if system == 'OpenVMS':
        # OpenVMS seems to have release and version mixed up
        if not release or release == '0':
            release = version
            version = ''
        # Get processor information
        try:
            import vms_lib
        except ImportError:
            pass
        else:
            csid, cpu_number = vms_lib.getsyi('SYI$_CPU',0)
            if (cpu_number >= 128):
                processor = 'Alpha'
            else:
                processor = 'VAX'
    if not processor:
        # Get processor information from the uname system command
        processor = _syscmd_uname('-p','')

    #If any unknowns still exist, replace them with ''s, which are more portable
    if system == 'unknown':
        system = ''
    if node == 'unknown':
        node = ''
    if release == 'unknown':
        release = ''
    if version == 'unknown':
        version = ''
    if machine == 'unknown':
        machine = ''
    if processor == 'unknown':
        processor = ''

    #  normalize name
    if system == 'Microsoft' and release == 'Windows':
        system = 'Windows'
        release = 'Vista'

    _uname_cache = system,node,release,version,machine,processor
    return _uname_cache

### Direct interfaces to some of the uname() return values

def system():

    """ Returns the system/OS name, e.g. 'Linux', 'Windows' or 'Java'.

        An empty string is returned if the value cannot be determined.

    """
    return uname()[0]

def node():

    """ Returns the computer's network name (which may not be fully
        qualified)

        An empty string is returned if the value cannot be determined.

    """
    return uname()[1]

def release():

    """ Returns the system's release, e.g. '2.2.0' or 'NT'

        An empty string is returned if the value cannot be determined.

    """
    return uname()[2]

def version():

    """ Returns the system's release version, e.g. '#3 on degas'

        An empty string is returned if the value cannot be determined.

    """
    return uname()[3]

def machine():

    """ Returns the machine type, e.g. 'i386'

        An empty string is returned if the value cannot be determined.

    """
    return uname()[4]

def processor():

    """ Returns the (true) processor name, e.g. 'amdk6'

        An empty string is returned if the value cannot be
        determined. Note that many platforms do not provide this
        information or simply return the same value as for machine(),
        e.g.  NetBSD does this.

    """
    return uname()[5]

### Various APIs for extracting information from sys.version

_sys_version_parser = re.compile(
    r'([\w.+]+)\s*'
    '\(#?([^,]+),\s*([\w ]+),\s*([\w :]+)\)\s*'
    '\[([^\]]+)\]?')

_ironpython_sys_version_parser = re.compile(
    r'IronPython\s*'
    '([\d\.]+)'
    '(?: \(([\d\.]+)\))?'
    ' on (.NET [\d\.]+)')

# IronPython covering 2.6 and 2.7
_ironpython26_sys_version_parser = re.compile(
    r'([\d.]+)\s*'
    '\(IronPython\s*'
    '[\d.]+\s*'
    '\(([\d.]+)\) on ([\w.]+ [\d.]+(?: \(\d+-bit\))?)\)'
)

_pypy_sys_version_parser = re.compile(
    r'([\w.+]+)\s*'
    '\(#?([^,]+),\s*([\w ]+),\s*([\w :]+)\)\s*'
    '\[PyPy [^\]]+\]?')

_sys_version_cache = {}

def _sys_version(sys_version=None):

    """ Returns a parsed version of Python's sys.version as tuple
        (name, version, branch, revision, buildno, builddate, compiler)
        referring to the Python implementation name, version, branch,
        revision, build number, build date/time as string and the compiler
        identification string.

        Note that unlike the Python sys.version, the returned value
        for the Python version will always include the patchlevel (it
        defaults to '.0').

        The function returns empty strings for tuple entries that
        cannot be determined.

        sys_version may be given to parse an alternative version
        string, e.g. if the version was read from a different Python
        interpreter.

    """
    # Get the Python version
    if sys_version is None:
        sys_version = sys.version

    # Try the cache first
    result = _sys_version_cache.get(sys_version, None)
    if result is not None:
        return result

    # Parse it
    if 'IronPython' in sys_version:
        # IronPython
        name = 'IronPython'
        if sys_version.startswith('IronPython'):
            match = _ironpython_sys_version_parser.match(sys_version)
        else:
            match = _ironpython26_sys_version_parser.match(sys_version)

        if match is None:
            raise ValueError(
                'failed to parse IronPython sys.version: %s' %
                repr(sys_version))

        version, alt_version, compiler = match.groups()
        buildno = ''
        builddate = ''

    elif sys.platform.startswith('java'):
        # Jython
        name = 'Jython'
        match = _sys_version_parser.match(sys_version)
        if match is None:
            raise ValueError(
                'failed to parse Jython sys.version: %s' %
                repr(sys_version))
        version, buildno, builddate, buildtime, _ = match.groups()
        compiler = sys.platform

    elif "PyPy" in sys_version:
        # PyPy
        name = "PyPy"
        match = _pypy_sys_version_parser.match(sys_version)
        if match is None:
            raise ValueError("failed to parse PyPy sys.version: %s" %
                             repr(sys_version))
        version, buildno, builddate, buildtime = match.groups()
        compiler = ""

    else:
        # CPython
        match = _sys_version_parser.match(sys_version)
        if match is None:
            raise ValueError(
                'failed to parse CPython sys.version: %s' %
                repr(sys_version))
        version, buildno, builddate, buildtime, compiler = \
              match.groups()
        name = 'CPython'
        builddate = builddate + ' ' + buildtime

    if hasattr(sys, 'subversion'):
        # sys.subversion was added in Python 2.5
        _, branch, revision = sys.subversion
    else:
        branch = ''
        revision = ''

    # Add the patchlevel version if missing
    l = string.split(version, '.')
    if len(l) == 2:
        l.append('0')
        version = string.join(l, '.')

    # Build and cache the result
    result = (name, version, branch, revision, buildno, builddate, compiler)
    _sys_version_cache[sys_version] = result
    return result

def python_implementation():

    """ Returns a string identifying the Python implementation.

        Currently, the following implementations are identified:
          'CPython' (C implementation of Python),
          'IronPython' (.NET implementation of Python),
          'Jython' (Java implementation of Python),
          'PyPy' (Python implementation of Python).

    """
    return _sys_version()[0]

def python_version():

    """ Returns the Python version as string 'major.minor.patchlevel'

        Note that unlike the Python sys.version, the returned value
        will always include the patchlevel (it defaults to 0).

    """
    return _sys_version()[1]

def python_version_tuple():

    """ Returns the Python version as tuple (major, minor, patchlevel)
        of strings.

        Note that unlike the Python sys.version, the returned value
        will always include the patchlevel (it defaults to 0).

    """
    return tuple(string.split(_sys_version()[1], '.'))

def python_branch():

    """ Returns a string identifying the Python implementation
        branch.

        For CPython this is the Subversion branch from which the
        Python binary was built.

        If not available, an empty string is returned.

    """

    return _sys_version()[2]

def python_revision():

    """ Returns a string identifying the Python implementation
        revision.

        For CPython this is the Subversion revision from which the
        Python binary was built.

        If not available, an empty string is returned.

    """
    return _sys_version()[3]

def python_build():

    """ Returns a tuple (buildno, builddate) stating the Python
        build number and date as strings.

    """
    return _sys_version()[4:6]

def python_compiler():

    """ Returns a string identifying the compiler used for compiling
        Python.

    """
    return _sys_version()[6]

### The Opus Magnum of platform strings :-)

_platform_cache = {}

def platform(aliased=0, terse=0):

    """ Returns a single string identifying the underlying platform
        with as much useful information as possible (but no more :).

        The output is intended to be human readable rather than
        machine parseable. It may look different on different
        platforms and this is intended.

        If "aliased" is true, the function will use aliases for
        various platforms that report system names which differ from
        their common names, e.g. SunOS will be reported as
        Solaris. The system_alias() function is used to implement
        this.

        Setting terse to true causes the function to return only the
        absolute minimum information needed to identify the platform.

    """
    result = _platform_cache.get((aliased, terse), None)
    if result is not None:
        return result

    # Get uname information and then apply platform specific cosmetics
    # to it...
    system,node,release,version,machine,processor = uname()
    if machine == processor:
        processor = ''
    if aliased:
        system,release,version = system_alias(system,release,version)

    if system == 'Windows':
        # MS platforms
        rel,vers,csd,ptype = win32_ver(version)
        if terse:
            platform = _platform(system,release)
        else:
            platform = _platform(system,release,version,csd)

    elif system in ('Linux',):
        # Linux based systems
        distname,distversion,distid = dist('')
        if distname and not terse:
            platform = _platform(system,release,machine,processor,
                                 'with',
                                 distname,distversion,distid)
        else:
            # If the distribution name is unknown check for libc vs. glibc
            libcname,libcversion = libc_ver(sys.executable)
            platform = _platform(system,release,machine,processor,
                                 'with',
                                 libcname+libcversion)
    elif system == 'Java':
        # Java platforms
        r,v,vminfo,(os_name,os_version,os_arch) = java_ver()
        if terse or not os_name:
            platform = _platform(system,release,version)
        else:
            platform = _platform(system,release,version,
                                 'on',
                                 os_name,os_version,os_arch)

    elif system == 'MacOS':
        # MacOS platforms
        if terse:
            platform = _platform(system,release)
        else:
            platform = _platform(system,release,machine)

    else:
        # Generic handler
        if terse:
            platform = _platform(system,release)
        else:
            bits,linkage = architecture(sys.executable)
            platform = _platform(system,release,machine,processor,bits,linkage)

    _platform_cache[(aliased, terse)] = platform
    return platform

### Command line interface

if __name__ == '__main__':
    # Default is to print the aliased verbose platform string
    terse = ('terse' in sys.argv or '--terse' in sys.argv)
    aliased = (not 'nonaliased' in sys.argv and not '--nonaliased' in sys.argv)
    print platform(aliased,terse)
    sys.exit(0)
