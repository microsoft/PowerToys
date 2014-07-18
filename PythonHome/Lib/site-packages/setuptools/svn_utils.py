import os
import re
import sys
from distutils import log
import xml.dom.pulldom
import shlex
import locale
import codecs
import unicodedata
import warnings
from setuptools.compat import unicode, PY2
from setuptools.py31compat import TemporaryDirectory
from xml.sax.saxutils import unescape

try:
    import urlparse
except ImportError:
    import urllib.parse as urlparse

from subprocess import Popen as _Popen, PIPE as _PIPE

#NOTE: Use of the command line options require SVN 1.3 or newer (December 2005)
#      and SVN 1.3 hasn't been supported by the developers since mid 2008.

#subprocess is called several times with shell=(sys.platform=='win32')
#see the follow for more information:
#       http://bugs.python.org/issue8557
#       http://stackoverflow.com/questions/5658622/
#              python-subprocess-popen-environment-path

def _run_command(args, stdout=_PIPE, stderr=_PIPE, encoding=None, stream=0):
    #regarding the shell argument, see: http://bugs.python.org/issue8557
    try:
        proc = _Popen(args, stdout=stdout, stderr=stderr,
                      shell=(sys.platform == 'win32'))

        data = proc.communicate()[stream]
    except OSError:
        return 1, ''

    #doubled checked and
    data = decode_as_string(data, encoding)

    #communciate calls wait()
    return proc.returncode, data


def _get_entry_schedule(entry):
    schedule = entry.getElementsByTagName('schedule')[0]
    return "".join([t.nodeValue
                    for t in schedule.childNodes
                    if t.nodeType == t.TEXT_NODE])


def _get_target_property(target):
    property_text = target.getElementsByTagName('property')[0]
    return "".join([t.nodeValue
                    for t in property_text.childNodes
                    if t.nodeType == t.TEXT_NODE])


def _get_xml_data(decoded_str):
    if PY2:
        #old versions want an encoded string
        data = decoded_str.encode('utf-8')
    else:
        data = decoded_str
    return data


def joinpath(prefix, *suffix):
    if not prefix or prefix == '.':
        return os.path.join(*suffix)
    return os.path.join(prefix, *suffix)

def determine_console_encoding():
    try:
        #try for the preferred encoding
        encoding = locale.getpreferredencoding()

        #see if the locale.getdefaultlocale returns null
        #some versions of python\platforms return US-ASCII
        #when it cannot determine an encoding
        if not encoding or encoding == "US-ASCII":
            encoding = locale.getdefaultlocale()[1]

        if encoding:
            codecs.lookup(encoding)  # make sure a lookup error is not made

    except (locale.Error, LookupError):
        encoding = None

    is_osx = sys.platform == "darwin"
    if not encoding:
        return ["US-ASCII", "utf-8"][is_osx]
    elif encoding.startswith("mac-") and is_osx:
        #certain versions of python would return mac-roman as default
        #OSX as a left over of earlier mac versions.
        return "utf-8"
    else:
        return encoding

_console_encoding = determine_console_encoding()

def decode_as_string(text, encoding=None):
    """
    Decode the console or file output explicitly using getpreferredencoding.
    The text paraemeter should be a encoded string, if not no decode occurs
    If no encoding is given, getpreferredencoding is used.  If encoding is
    specified, that is used instead.  This would be needed for SVN --xml
    output.  Unicode is explicitly put in composed NFC form.

    --xml should be UTF-8 (SVN Issue 2938) the discussion on the Subversion
    DEV List from 2007 seems to indicate the same.
    """
    #text should be a byte string

    if encoding is None:
        encoding = _console_encoding

    if not isinstance(text, unicode):
        text = text.decode(encoding)

    text = unicodedata.normalize('NFC', text)

    return text


def parse_dir_entries(decoded_str):
    '''Parse the entries from a recursive info xml'''
    doc = xml.dom.pulldom.parseString(_get_xml_data(decoded_str))
    entries = list()

    for event, node in doc:
        if event == 'START_ELEMENT' and node.nodeName == 'entry':
            doc.expandNode(node)
            if not _get_entry_schedule(node).startswith('delete'):
                entries.append((node.getAttribute('path'),
                                node.getAttribute('kind')))

    return entries[1:]  # do not want the root directory


def parse_externals_xml(decoded_str, prefix=''):
    '''Parse a propget svn:externals xml'''
    prefix = os.path.normpath(prefix)
    prefix = os.path.normcase(prefix)

    doc = xml.dom.pulldom.parseString(_get_xml_data(decoded_str))
    externals = list()

    for event, node in doc:
        if event == 'START_ELEMENT' and node.nodeName == 'target':
            doc.expandNode(node)
            path = os.path.normpath(node.getAttribute('path'))

            if os.path.normcase(path).startswith(prefix):
                path = path[len(prefix)+1:]

            data = _get_target_property(node)
            #data should be decoded already
            for external in parse_external_prop(data):
                externals.append(joinpath(path, external))

    return externals  # do not want the root directory


def parse_external_prop(lines):
    """
    Parse the value of a retrieved svn:externals entry.

    possible token setups (with quotng and backscaping in laters versions)
        URL[@#] EXT_FOLDERNAME
        [-r#] URL EXT_FOLDERNAME
        EXT_FOLDERNAME [-r#] URL
    """
    externals = []
    for line in lines.splitlines():
        line = line.lstrip()  # there might be a "\ "
        if not line:
            continue

        if PY2:
            #shlex handles NULLs just fine and shlex in 2.7 tries to encode
            #as ascii automatiically
            line = line.encode('utf-8')
        line = shlex.split(line)
        if PY2:
            line = [x.decode('utf-8') for x in line]

        #EXT_FOLDERNAME is either the first or last depending on where
        #the URL falls
        if urlparse.urlsplit(line[-1])[0]:
            external = line[0]
        else:
            external = line[-1]

        external = decode_as_string(external, encoding="utf-8")
        externals.append(os.path.normpath(external))

    return externals


def parse_prop_file(filename, key):
    found = False
    f = open(filename, 'rt')
    data = ''
    try:
        for line in iter(f.readline, ''):    # can't use direct iter!
            parts = line.split()
            if len(parts) == 2:
                kind, length = parts
                data = f.read(int(length))
                if kind == 'K' and data == key:
                    found = True
                elif kind == 'V' and found:
                    break
    finally:
        f.close()

    return data


class SvnInfo(object):
    '''
    Generic svn_info object.  No has little knowledge of how to extract
    information.  Use cls.load to instatiate according svn version.

    Paths are not filesystem encoded.
    '''

    @staticmethod
    def get_svn_version():
        # Temp config directory should be enough to check for repository
        # This is needed because .svn always creates .subversion and
        # some operating systems do not handle dot directory correctly.
        # Real queries in real svn repos with be concerned with it creation
        with TemporaryDirectory() as tempdir:
            code, data = _run_command(['svn',
                                       '--config-dir', tempdir,
                                       '--version',
                                       '--quiet'])

        if code == 0 and data:
            return data.strip()
        else:
            return ''

    #svnversion return values (previous implementations return max revision)
    #   4123:4168     mixed revision working copy
    #   4168M         modified working copy
    #   4123S         switched working copy
    #   4123:4168MS   mixed revision, modified, switched working copy
    revision_re = re.compile(r'(?:([\-0-9]+):)?(\d+)([a-z]*)\s*$', re.I)

    @classmethod
    def load(cls, dirname=''):
        normdir = os.path.normpath(dirname)

        # Temp config directory should be enough to check for repository
        # This is needed because .svn always creates .subversion and
        # some operating systems do not handle dot directory correctly.
        # Real queries in real svn repos with be concerned with it creation
        with TemporaryDirectory() as tempdir:
            code, data = _run_command(['svn',
                                       '--config-dir', tempdir,
                                       'info', normdir])

        # Must check for some contents, as some use empty directories
        # in testcases, however only enteries is needed also the info
        # command above MUST have worked
        svn_dir = os.path.join(normdir, '.svn')
        is_svn_wd = (not code or
                     os.path.isfile(os.path.join(svn_dir, 'entries')))

        svn_version = tuple(cls.get_svn_version().split('.'))

        try:
            base_svn_version = tuple(int(x) for x in svn_version[:2])
        except ValueError:
            base_svn_version = tuple()

        if not is_svn_wd:
            #return an instance of this NO-OP class
            return SvnInfo(dirname)

        if code or not base_svn_version or base_svn_version < (1, 3):
            warnings.warn(("No SVN 1.3+ command found: falling back "
                           "on pre 1.7 .svn parsing"), DeprecationWarning)
            return SvnFileInfo(dirname)

        if base_svn_version < (1, 5):
            return Svn13Info(dirname)

        return Svn15Info(dirname)

    def __init__(self, path=''):
        self.path = path
        self._entries = None
        self._externals = None

    def get_revision(self):
        'Retrieve the directory revision informatino using svnversion'
        code, data = _run_command(['svnversion', '-c', self.path])
        if code:
            log.warn("svnversion failed")
            return 0

        parsed = self.revision_re.match(data)
        if parsed:
            return int(parsed.group(2))
        else:
            return 0

    @property
    def entries(self):
        if self._entries is None:
            self._entries = self.get_entries()
        return self._entries

    @property
    def externals(self):
        if self._externals is None:
            self._externals = self.get_externals()
        return self._externals

    def iter_externals(self):
        '''
        Iterate over the svn:external references in the repository path.
        '''
        for item in self.externals:
            yield item

    def iter_files(self):
        '''
        Iterate over the non-deleted file entries in the repository path
        '''
        for item, kind in self.entries:
            if kind.lower() == 'file':
                yield item

    def iter_dirs(self, include_root=True):
        '''
        Iterate over the non-deleted file entries in the repository path
        '''
        if include_root:
            yield self.path
        for item, kind in self.entries:
            if kind.lower() == 'dir':
                yield item

    def get_entries(self):
        return []

    def get_externals(self):
        return []


class Svn13Info(SvnInfo):
    def get_entries(self):
        code, data = _run_command(['svn', 'info', '-R', '--xml', self.path],
                                  encoding="utf-8")

        if code:
            log.debug("svn info failed")
            return []

        return parse_dir_entries(data)

    def get_externals(self):
        #Previous to 1.5 --xml was not supported for svn propget and the -R
        #output format breaks the shlex compatible semantics.
        cmd = ['svn', 'propget', 'svn:externals']
        result = []
        for folder in self.iter_dirs():
            code, lines = _run_command(cmd + [folder], encoding="utf-8")
            if code != 0:
                log.warn("svn propget failed")
                return []
            #lines should a str
            for external in parse_external_prop(lines):
                if folder:
                    external = os.path.join(folder, external)
                result.append(os.path.normpath(external))

        return result


class Svn15Info(Svn13Info):
    def get_externals(self):
        cmd = ['svn', 'propget', 'svn:externals', self.path, '-R', '--xml']
        code, lines = _run_command(cmd, encoding="utf-8")
        if code:
            log.debug("svn propget failed")
            return []
        return parse_externals_xml(lines, prefix=os.path.abspath(self.path))


class SvnFileInfo(SvnInfo):

    def __init__(self, path=''):
        super(SvnFileInfo, self).__init__(path)
        self._directories = None
        self._revision = None

    def _walk_svn(self, base):
        entry_file = joinpath(base, '.svn', 'entries')
        if os.path.isfile(entry_file):
            entries = SVNEntriesFile.load(base)
            yield (base, False, entries.parse_revision())
            for path in entries.get_undeleted_records():
                path = decode_as_string(path)
                path = joinpath(base, path)
                if os.path.isfile(path):
                    yield (path, True, None)
                elif os.path.isdir(path):
                    for item in self._walk_svn(path):
                        yield item

    def _build_entries(self):
        entries = list()

        rev = 0
        for path, isfile, dir_rev in self._walk_svn(self.path):
            if isfile:
                entries.append((path, 'file'))
            else:
                entries.append((path, 'dir'))
                rev = max(rev, dir_rev)

        self._entries = entries
        self._revision = rev

    def get_entries(self):
        if self._entries is None:
            self._build_entries()
        return self._entries

    def get_revision(self):
        if self._revision is None:
            self._build_entries()
        return self._revision

    def get_externals(self):
        prop_files = [['.svn', 'dir-prop-base'],
                      ['.svn', 'dir-props']]
        externals = []

        for dirname in self.iter_dirs():
            prop_file = None
            for rel_parts in prop_files:
                filename = joinpath(dirname, *rel_parts)
                if os.path.isfile(filename):
                    prop_file = filename

            if prop_file is not None:
                ext_prop = parse_prop_file(prop_file, 'svn:externals')
                #ext_prop should be utf-8 coming from svn:externals
                ext_prop = decode_as_string(ext_prop, encoding="utf-8")
                externals.extend(parse_external_prop(ext_prop))

        return externals


def svn_finder(dirname=''):
    #combined externals due to common interface
    #combined externals and entries due to lack of dir_props in 1.7
    info = SvnInfo.load(dirname)
    for path in info.iter_files():
        yield path

    for path in info.iter_externals():
        sub_info = SvnInfo.load(path)
        for sub_path in sub_info.iter_files():
            yield sub_path


class SVNEntriesFile(object):
    def __init__(self, data):
        self.data = data

    @classmethod
    def load(class_, base):
        filename = os.path.join(base, '.svn', 'entries')
        f = open(filename)
        try:
            result = SVNEntriesFile.read(f)
        finally:
            f.close()
        return result

    @classmethod
    def read(class_, fileobj):
        data = fileobj.read()
        is_xml = data.startswith('<?xml')
        class_ = [SVNEntriesFileText, SVNEntriesFileXML][is_xml]
        return class_(data)

    def parse_revision(self):
        all_revs = self.parse_revision_numbers() + [0]
        return max(all_revs)


class SVNEntriesFileText(SVNEntriesFile):
    known_svn_versions = {
        '1.4.x': 8,
        '1.5.x': 9,
        '1.6.x': 10,
    }

    def __get_cached_sections(self):
        return self.sections

    def get_sections(self):
        SECTION_DIVIDER = '\f\n'
        sections = self.data.split(SECTION_DIVIDER)
        sections = [x for x in map(str.splitlines, sections)]
        try:
            # remove the SVN version number from the first line
            svn_version = int(sections[0].pop(0))
            if not svn_version in self.known_svn_versions.values():
                log.warn("Unknown subversion verson %d", svn_version)
        except ValueError:
            return
        self.sections = sections
        self.get_sections = self.__get_cached_sections
        return self.sections

    def is_valid(self):
        return bool(self.get_sections())

    def get_url(self):
        return self.get_sections()[0][4]

    def parse_revision_numbers(self):
        revision_line_number = 9
        rev_numbers = [
            int(section[revision_line_number])
            for section in self.get_sections()
            if (len(section) > revision_line_number
                and section[revision_line_number])
        ]
        return rev_numbers

    def get_undeleted_records(self):
        undeleted = lambda s: s and s[0] and (len(s) < 6 or s[5] != 'delete')
        result = [
            section[0]
            for section in self.get_sections()
            if undeleted(section)
        ]
        return result


class SVNEntriesFileXML(SVNEntriesFile):
    def is_valid(self):
        return True

    def get_url(self):
        "Get repository URL"
        urlre = re.compile('url="([^"]+)"')
        return urlre.search(self.data).group(1)

    def parse_revision_numbers(self):
        revre = re.compile(r'committed-rev="(\d+)"')
        return [
            int(m.group(1))
            for m in revre.finditer(self.data)
        ]

    def get_undeleted_records(self):
        entries_pattern = \
            re.compile(r'name="([^"]+)"(?![^>]+deleted="true")', re.I)
        results = [
            unescape(match.group(1))
            for match in entries_pattern.finditer(self.data)
        ]
        return results


if __name__ == '__main__':
    for name in svn_finder(sys.argv[1]):
        print(name)
