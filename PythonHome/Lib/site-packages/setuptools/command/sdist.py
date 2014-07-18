from glob import glob
from distutils.util import convert_path
from distutils import log
import distutils.command.sdist as orig
import os
import re
import sys

from setuptools import svn_utils
from setuptools.compat import PY3
import pkg_resources

READMES = ('README', 'README.rst', 'README.txt')


def walk_revctrl(dirname=''):
    """Find all files under revision control"""
    for ep in pkg_resources.iter_entry_points('setuptools.file_finders'):
        for item in ep.load()(dirname):
            yield item


# TODO will need test case
class re_finder(object):
    """
    Finder that locates files based on entries in a file matched by a
    regular expression.
    """

    def __init__(self, path, pattern, postproc=lambda x: x):
        self.pattern = pattern
        self.postproc = postproc
        self.entries_path = convert_path(path)

    def _finder(self, dirname, filename):
        f = open(filename, 'rU')
        try:
            data = f.read()
        finally:
            f.close()
        for match in self.pattern.finditer(data):
            path = match.group(1)
            # postproc was formerly used when the svn finder
            # was an re_finder for calling unescape
            path = self.postproc(path)
            yield svn_utils.joinpath(dirname, path)

    def find(self, dirname=''):
        path = svn_utils.joinpath(dirname, self.entries_path)

        if not os.path.isfile(path):
            # entries file doesn't exist
            return
        for path in self._finder(dirname, path):
            if os.path.isfile(path):
                yield path
            elif os.path.isdir(path):
                for item in self.find(path):
                    yield item

    __call__ = find


def _default_revctrl(dirname=''):
    'Primary svn_cvs entry point'
    for finder in finders:
        for item in finder(dirname):
            yield item


finders = [
    re_finder('CVS/Entries', re.compile(r"^\w?/([^/]+)/", re.M)),
    svn_utils.svn_finder,
]


class sdist(orig.sdist):
    """Smart sdist that finds anything supported by revision control"""

    user_options = [
        ('formats=', None,
         "formats for source distribution (comma-separated list)"),
        ('keep-temp', 'k',
         "keep the distribution tree around after creating " +
         "archive file(s)"),
        ('dist-dir=', 'd',
         "directory to put the source distribution archive(s) in "
         "[default: dist]"),
    ]

    negative_opt = {}

    def run(self):
        self.run_command('egg_info')
        ei_cmd = self.get_finalized_command('egg_info')
        self.filelist = ei_cmd.filelist
        self.filelist.append(os.path.join(ei_cmd.egg_info, 'SOURCES.txt'))
        self.check_readme()

        # Run sub commands
        for cmd_name in self.get_sub_commands():
            self.run_command(cmd_name)

        # Call check_metadata only if no 'check' command
        # (distutils <= 2.6)
        import distutils.command

        if 'check' not in distutils.command.__all__:
            self.check_metadata()

        self.make_distribution()

        dist_files = getattr(self.distribution, 'dist_files', [])
        for file in self.archive_files:
            data = ('sdist', '', file)
            if data not in dist_files:
                dist_files.append(data)

    def __read_template_hack(self):
        # This grody hack closes the template file (MANIFEST.in) if an
        #  exception occurs during read_template.
        # Doing so prevents an error when easy_install attempts to delete the
        #  file.
        try:
            orig.sdist.read_template(self)
        except:
            sys.exc_info()[2].tb_next.tb_frame.f_locals['template'].close()
            raise

    # Beginning with Python 2.7.2, 3.1.4, and 3.2.1, this leaky file handle
    #  has been fixed, so only override the method if we're using an earlier
    #  Python.
    has_leaky_handle = (
        sys.version_info < (2, 7, 2)
        or (3, 0) <= sys.version_info < (3, 1, 4)
        or (3, 2) <= sys.version_info < (3, 2, 1)
    )
    if has_leaky_handle:
        read_template = __read_template_hack

    def add_defaults(self):
        standards = [READMES,
                     self.distribution.script_name]
        for fn in standards:
            if isinstance(fn, tuple):
                alts = fn
                got_it = 0
                for fn in alts:
                    if os.path.exists(fn):
                        got_it = 1
                        self.filelist.append(fn)
                        break

                if not got_it:
                    self.warn("standard file not found: should have one of " +
                              ', '.join(alts))
            else:
                if os.path.exists(fn):
                    self.filelist.append(fn)
                else:
                    self.warn("standard file '%s' not found" % fn)

        optional = ['test/test*.py', 'setup.cfg']
        for pattern in optional:
            files = list(filter(os.path.isfile, glob(pattern)))
            if files:
                self.filelist.extend(files)

        # getting python files
        if self.distribution.has_pure_modules():
            build_py = self.get_finalized_command('build_py')
            self.filelist.extend(build_py.get_source_files())
            # This functionality is incompatible with include_package_data, and
            # will in fact create an infinite recursion if include_package_data
            # is True.  Use of include_package_data will imply that
            # distutils-style automatic handling of package_data is disabled
            if not self.distribution.include_package_data:
                for _, src_dir, _, filenames in build_py.data_files:
                    self.filelist.extend([os.path.join(src_dir, filename)
                                          for filename in filenames])

        if self.distribution.has_ext_modules():
            build_ext = self.get_finalized_command('build_ext')
            self.filelist.extend(build_ext.get_source_files())

        if self.distribution.has_c_libraries():
            build_clib = self.get_finalized_command('build_clib')
            self.filelist.extend(build_clib.get_source_files())

        if self.distribution.has_scripts():
            build_scripts = self.get_finalized_command('build_scripts')
            self.filelist.extend(build_scripts.get_source_files())

    def check_readme(self):
        for f in READMES:
            if os.path.exists(f):
                return
        else:
            self.warn(
                "standard file not found: should have one of " +
                ', '.join(READMES)
            )

    def make_release_tree(self, base_dir, files):
        orig.sdist.make_release_tree(self, base_dir, files)

        # Save any egg_info command line options used to create this sdist
        dest = os.path.join(base_dir, 'setup.cfg')
        if hasattr(os, 'link') and os.path.exists(dest):
            # unlink and re-copy, since it might be hard-linked, and
            # we don't want to change the source version
            os.unlink(dest)
            self.copy_file('setup.cfg', dest)

        self.get_finalized_command('egg_info').save_version_info(dest)

    def _manifest_is_not_generated(self):
        # check for special comment used in 2.7.1 and higher
        if not os.path.isfile(self.manifest):
            return False

        fp = open(self.manifest, 'rbU')
        try:
            first_line = fp.readline()
        finally:
            fp.close()
        return (first_line !=
                '# file GENERATED by distutils, do NOT edit\n'.encode())

    def read_manifest(self):
        """Read the manifest file (named by 'self.manifest') and use it to
        fill in 'self.filelist', the list of files to include in the source
        distribution.
        """
        log.info("reading manifest file '%s'", self.manifest)
        manifest = open(self.manifest, 'rbU')
        for line in manifest:
            # The manifest must contain UTF-8. See #303.
            if PY3:
                try:
                    line = line.decode('UTF-8')
                except UnicodeDecodeError:
                    log.warn("%r not UTF-8 decodable -- skipping" % line)
                    continue
            # ignore comments and blank lines
            line = line.strip()
            if line.startswith('#') or not line:
                continue
            self.filelist.append(line)
        manifest.close()
