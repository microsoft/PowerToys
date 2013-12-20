"""setuptools.command.egg_info

Create a distribution's .egg-info directory and contents"""

# This module should be kept compatible with Python 2.3
import os, re, sys
from setuptools import Command
from distutils.errors import *
from distutils import log
from setuptools.command.sdist import sdist
from setuptools.compat import basestring
from distutils.util import convert_path
from distutils.filelist import FileList as _FileList
from pkg_resources import parse_requirements, safe_name, parse_version, \
    safe_version, yield_lines, EntryPoint, iter_entry_points, to_filename
from setuptools.command.sdist import walk_revctrl

class egg_info(Command):
    description = "create a distribution's .egg-info directory"

    user_options = [
        ('egg-base=', 'e', "directory containing .egg-info directories"
                           " (default: top of the source tree)"),
        ('tag-svn-revision', 'r',
            "Add subversion revision ID to version number"),
        ('tag-date', 'd', "Add date stamp (e.g. 20050528) to version number"),
        ('tag-build=', 'b', "Specify explicit tag to add to version number"),
        ('no-svn-revision', 'R',
            "Don't add subversion revision ID [default]"),
        ('no-date', 'D', "Don't include date stamp [default]"),
    ]

    boolean_options = ['tag-date', 'tag-svn-revision']
    negative_opt = {'no-svn-revision': 'tag-svn-revision',
                    'no-date': 'tag-date'}







    def initialize_options(self):
        self.egg_name = None
        self.egg_version = None
        self.egg_base = None
        self.egg_info = None
        self.tag_build = None
        self.tag_svn_revision = 0
        self.tag_date = 0
        self.broken_egg_info = False
        self.vtags = None

    def save_version_info(self, filename):
        from setuptools.command.setopt import edit_config
        edit_config(
            filename,
            {'egg_info':
                {'tag_svn_revision':0, 'tag_date': 0, 'tag_build': self.tags()}
            }
        )






















    def finalize_options (self):
        self.egg_name = safe_name(self.distribution.get_name())
        self.vtags = self.tags()
        self.egg_version = self.tagged_version()

        try:
            list(
                parse_requirements('%s==%s' % (self.egg_name,self.egg_version))
            )
        except ValueError:
            raise DistutilsOptionError(
                "Invalid distribution name or version syntax: %s-%s" %
                (self.egg_name,self.egg_version)
            )

        if self.egg_base is None:
            dirs = self.distribution.package_dir
            self.egg_base = (dirs or {}).get('',os.curdir)

        self.ensure_dirname('egg_base')
        self.egg_info = to_filename(self.egg_name)+'.egg-info'
        if self.egg_base != os.curdir:
            self.egg_info = os.path.join(self.egg_base, self.egg_info)
        if '-' in self.egg_name: self.check_broken_egg_info()

        # Set package version for the benefit of dumber commands
        # (e.g. sdist, bdist_wininst, etc.)
        #
        self.distribution.metadata.version = self.egg_version

        # If we bootstrapped around the lack of a PKG-INFO, as might be the
        # case in a fresh checkout, make sure that any special tags get added
        # to the version info
        #
        pd = self.distribution._patched_dist
        if pd is not None and pd.key==self.egg_name.lower():
            pd._version = self.egg_version
            pd._parsed_version = parse_version(self.egg_version)
            self.distribution._patched_dist = None


    def write_or_delete_file(self, what, filename, data, force=False):
        """Write `data` to `filename` or delete if empty

        If `data` is non-empty, this routine is the same as ``write_file()``.
        If `data` is empty but not ``None``, this is the same as calling
        ``delete_file(filename)`.  If `data` is ``None``, then this is a no-op
        unless `filename` exists, in which case a warning is issued about the
        orphaned file (if `force` is false), or deleted (if `force` is true).
        """
        if data:
            self.write_file(what, filename, data)
        elif os.path.exists(filename):
            if data is None and not force:
                log.warn(
                    "%s not set in setup(), but %s exists", what, filename
                )
                return
            else:
                self.delete_file(filename)

    def write_file(self, what, filename, data):
        """Write `data` to `filename` (if not a dry run) after announcing it

        `what` is used in a log message to identify what is being written
        to the file.
        """
        log.info("writing %s to %s", what, filename)
        if sys.version_info >= (3,):
            data = data.encode("utf-8")
        if not self.dry_run:
            f = open(filename, 'wb')
            f.write(data)
            f.close()

    def delete_file(self, filename):
        """Delete `filename` (if not a dry run) after announcing it"""
        log.info("deleting %s", filename)
        if not self.dry_run:
            os.unlink(filename)

    def tagged_version(self):
        version = self.distribution.get_version()
        # egg_info may be called more than once for a distribution,
        # in which case the version string already contains all tags.
        if self.vtags and version.endswith(self.vtags):
            return safe_version(version)
        return safe_version(version + self.vtags)

    def run(self):
        self.mkpath(self.egg_info)
        installer = self.distribution.fetch_build_egg
        for ep in iter_entry_points('egg_info.writers'):
            writer = ep.load(installer=installer)
            writer(self, ep.name, os.path.join(self.egg_info,ep.name))

        # Get rid of native_libs.txt if it was put there by older bdist_egg
        nl = os.path.join(self.egg_info, "native_libs.txt")
        if os.path.exists(nl):
            self.delete_file(nl)

        self.find_sources()

    def tags(self):
        version = ''
        if self.tag_build:
            version+=self.tag_build
        if self.tag_svn_revision and (
            os.path.exists('.svn') or os.path.exists('PKG-INFO')
        ):  version += '-r%s' % self.get_svn_revision()
        if self.tag_date:
            import time; version += time.strftime("-%Y%m%d")
        return version

















    @staticmethod
    def get_svn_revision():
        revision = 0
        urlre = re.compile('url="([^"]+)"')
        revre = re.compile('committed-rev="(\d+)"')

        for base,dirs,files in os.walk(os.curdir):
            if '.svn' not in dirs:
                dirs[:] = []
                continue    # no sense walking uncontrolled subdirs
            dirs.remove('.svn')
            f = open(os.path.join(base,'.svn','entries'))
            data = f.read()
            f.close()

            if data.startswith('<?xml'):
                dirurl = urlre.search(data).group(1)    # get repository URL
                localrev = max([int(m.group(1)) for m in revre.finditer(data)]+[0])
            else:
                try: svnver = int(data.splitlines()[0])
                except: svnver=-1
                if svnver<8:
                    log.warn("unrecognized .svn/entries format; skipping %s", base)
                    dirs[:] = []
                    continue

                data = list(map(str.splitlines,data.split('\n\x0c\n')))
                del data[0][0]  # get rid of the '8' or '9' or '10'
                dirurl = data[0][3]
                localrev = max([int(d[9]) for d in data if len(d)>9 and d[9]]+[0])
            if base==os.curdir:
                base_url = dirurl+'/'   # save the root url
            elif not dirurl.startswith(base_url):
                dirs[:] = []
                continue    # not part of the same svn tree, skip it
            revision = max(revision, localrev)

        return str(revision or get_pkg_info_revision())




    def find_sources(self):
        """Generate SOURCES.txt manifest file"""
        manifest_filename = os.path.join(self.egg_info,"SOURCES.txt")
        mm = manifest_maker(self.distribution)
        mm.manifest = manifest_filename
        mm.run()
        self.filelist = mm.filelist

    def check_broken_egg_info(self):
        bei = self.egg_name+'.egg-info'
        if self.egg_base != os.curdir:
            bei = os.path.join(self.egg_base, bei)
        if os.path.exists(bei):
            log.warn(
                "-"*78+'\n'
                "Note: Your current .egg-info directory has a '-' in its name;"
                '\nthis will not work correctly with "setup.py develop".\n\n'
                'Please rename %s to %s to correct this problem.\n'+'-'*78,
                bei, self.egg_info
            )
            self.broken_egg_info = self.egg_info
            self.egg_info = bei     # make it work for now

class FileList(_FileList):
    """File list that accepts only existing, platform-independent paths"""

    def append(self, item):
        if item.endswith('\r'):     # Fix older sdists built on Windows
            item = item[:-1]
        path = convert_path(item)

        if sys.version_info >= (3,):
            try:
                if os.path.exists(path) or os.path.exists(path.encode('utf-8')):
                    self.files.append(path)
            except UnicodeEncodeError:
                # Accept UTF-8 filenames even if LANG=C
                if os.path.exists(path.encode('utf-8')):
                    self.files.append(path)
                else:
                    log.warn("'%s' not %s encodable -- skipping", path,
                        sys.getfilesystemencoding())
        else:
            if os.path.exists(path):
                self.files.append(path)








class manifest_maker(sdist):

    template = "MANIFEST.in"

    def initialize_options (self):
        self.use_defaults = 1
        self.prune = 1
        self.manifest_only = 1
        self.force_manifest = 1

    def finalize_options(self):
        pass

    def run(self):
        self.filelist = FileList()
        if not os.path.exists(self.manifest):
            self.write_manifest()   # it must exist so it'll get in the list
        self.filelist.findall()
        self.add_defaults()
        if os.path.exists(self.template):
            self.read_template()
        self.prune_file_list()
        self.filelist.sort()
        self.filelist.remove_duplicates()
        self.write_manifest()

    def write_manifest (self):
        """Write the file list in 'self.filelist' (presumably as filled in
        by 'add_defaults()' and 'read_template()') to the manifest file
        named by 'self.manifest'.
        """
        # The manifest must be UTF-8 encodable. See #303.
        if sys.version_info >= (3,):
            files = []
            for file in self.filelist.files:
                try:
                    file.encode("utf-8")
                except UnicodeEncodeError:
                    log.warn("'%s' not UTF-8 encodable -- skipping" % file)
                else:
                    files.append(file)
            self.filelist.files = files

        files = self.filelist.files
        if os.sep!='/':
            files = [f.replace(os.sep,'/') for f in files]
        self.execute(write_file, (self.manifest, files),
                     "writing manifest file '%s'" % self.manifest)

    def warn(self, msg):    # suppress missing-file warnings from sdist
        if not msg.startswith("standard file not found:"):
            sdist.warn(self, msg)

    def add_defaults(self):
        sdist.add_defaults(self)
        self.filelist.append(self.template)
        self.filelist.append(self.manifest)
        rcfiles = list(walk_revctrl())
        if rcfiles:
            self.filelist.extend(rcfiles)
        elif os.path.exists(self.manifest):
            self.read_manifest()
        ei_cmd = self.get_finalized_command('egg_info')
        self.filelist.include_pattern("*", prefix=ei_cmd.egg_info)

    def prune_file_list (self):
        build = self.get_finalized_command('build')
        base_dir = self.distribution.get_fullname()
        self.filelist.exclude_pattern(None, prefix=build.build_base)
        self.filelist.exclude_pattern(None, prefix=base_dir)
        sep = re.escape(os.sep)
        self.filelist.exclude_pattern(sep+r'(RCS|CVS|\.svn)'+sep, is_regex=1)


def write_file (filename, contents):
    """Create a file with the specified name and write 'contents' (a
    sequence of strings without line terminators) to it.
    """
    contents = "\n".join(contents)
    if sys.version_info >= (3,):
        contents = contents.encode("utf-8")
    f = open(filename, "wb")        # always write POSIX-style manifest
    f.write(contents)
    f.close()













def write_pkg_info(cmd, basename, filename):
    log.info("writing %s", filename)
    if not cmd.dry_run:
        metadata = cmd.distribution.metadata
        metadata.version, oldver = cmd.egg_version, metadata.version
        metadata.name, oldname   = cmd.egg_name, metadata.name
        try:
            # write unescaped data to PKG-INFO, so older pkg_resources
            # can still parse it
            metadata.write_pkg_info(cmd.egg_info)
        finally:
            metadata.name, metadata.version = oldname, oldver

        safe = getattr(cmd.distribution,'zip_safe',None)
        from setuptools.command import bdist_egg
        bdist_egg.write_safety_flag(cmd.egg_info, safe)

def warn_depends_obsolete(cmd, basename, filename):
    if os.path.exists(filename):
        log.warn(
            "WARNING: 'depends.txt' is not used by setuptools 0.6!\n"
            "Use the install_requires/extras_require setup() args instead."
        )


def write_requirements(cmd, basename, filename):
    dist = cmd.distribution
    data = ['\n'.join(yield_lines(dist.install_requires or ()))]
    for extra,reqs in (dist.extras_require or {}).items():
        data.append('\n\n[%s]\n%s' % (extra, '\n'.join(yield_lines(reqs))))
    cmd.write_or_delete_file("requirements", filename, ''.join(data))

def write_toplevel_names(cmd, basename, filename):
    pkgs = dict.fromkeys(
        [k.split('.',1)[0]
            for k in cmd.distribution.iter_distribution_names()
        ]
    )
    cmd.write_file("top-level names", filename, '\n'.join(pkgs)+'\n')



def overwrite_arg(cmd, basename, filename):
    write_arg(cmd, basename, filename, True)

def write_arg(cmd, basename, filename, force=False):
    argname = os.path.splitext(basename)[0]
    value = getattr(cmd.distribution, argname, None)
    if value is not None:
        value = '\n'.join(value)+'\n'
    cmd.write_or_delete_file(argname, filename, value, force)

def write_entries(cmd, basename, filename):
    ep = cmd.distribution.entry_points

    if isinstance(ep,basestring) or ep is None:
        data = ep
    elif ep is not None:
        data = []
        for section, contents in ep.items():
            if not isinstance(contents,basestring):
                contents = EntryPoint.parse_group(section, contents)
                contents = '\n'.join(map(str,contents.values()))
            data.append('[%s]\n%s\n\n' % (section,contents))
        data = ''.join(data)

    cmd.write_or_delete_file('entry points', filename, data, True)

def get_pkg_info_revision():
    # See if we can get a -r### off of PKG-INFO, in case this is an sdist of
    # a subversion revision
    #
    if os.path.exists('PKG-INFO'):
        f = open('PKG-INFO','rU')
        for line in f:
            match = re.match(r"Version:.*-r(\d+)\s*$", line)
            if match:
                return int(match.group(1))
        f.close()
    return 0



#
