from glob import glob
from distutils.util import convert_path
import distutils.command.build_py as orig
import os
import sys
import fnmatch
import textwrap

try:
    from setuptools.lib2to3_ex import Mixin2to3
except ImportError:
    class Mixin2to3:
        def run_2to3(self, files, doctests=True):
            "do nothing"


class build_py(orig.build_py, Mixin2to3):
    """Enhanced 'build_py' command that includes data files with packages

    The data files are specified via a 'package_data' argument to 'setup()'.
    See 'setuptools.dist.Distribution' for more details.

    Also, this version of the 'build_py' command allows you to specify both
    'py_modules' and 'packages' in the same setup operation.
    """

    def finalize_options(self):
        orig.build_py.finalize_options(self)
        self.package_data = self.distribution.package_data
        self.exclude_package_data = (self.distribution.exclude_package_data or
                                     {})
        if 'data_files' in self.__dict__:
            del self.__dict__['data_files']
        self.__updated_files = []
        self.__doctests_2to3 = []

    def run(self):
        """Build modules, packages, and copy data files to build directory"""
        if not self.py_modules and not self.packages:
            return

        if self.py_modules:
            self.build_modules()

        if self.packages:
            self.build_packages()
            self.build_package_data()

        self.run_2to3(self.__updated_files, False)
        self.run_2to3(self.__updated_files, True)
        self.run_2to3(self.__doctests_2to3, True)

        # Only compile actual .py files, using our base class' idea of what our
        # output files are.
        self.byte_compile(orig.build_py.get_outputs(self, include_bytecode=0))

    def __getattr__(self, attr):
        if attr == 'data_files':  # lazily compute data files
            self.data_files = files = self._get_data_files()
            return files
        return orig.build_py.__getattr__(self, attr)

    def build_module(self, module, module_file, package):
        outfile, copied = orig.build_py.build_module(self, module, module_file,
                                                     package)
        if copied:
            self.__updated_files.append(outfile)
        return outfile, copied

    def _get_data_files(self):
        """Generate list of '(package,src_dir,build_dir,filenames)' tuples"""
        self.analyze_manifest()
        data = []
        for package in self.packages or ():
            # Locate package source directory
            src_dir = self.get_package_dir(package)

            # Compute package build directory
            build_dir = os.path.join(*([self.build_lib] + package.split('.')))

            # Length of path to strip from found files
            plen = len(src_dir) + 1

            # Strip directory from globbed filenames
            filenames = [
                file[plen:] for file in self.find_data_files(package, src_dir)
            ]
            data.append((package, src_dir, build_dir, filenames))
        return data

    def find_data_files(self, package, src_dir):
        """Return filenames for package's data files in 'src_dir'"""
        globs = (self.package_data.get('', [])
                 + self.package_data.get(package, []))
        files = self.manifest_files.get(package, [])[:]
        for pattern in globs:
            # Each pattern has to be converted to a platform-specific path
            files.extend(glob(os.path.join(src_dir, convert_path(pattern))))
        return self.exclude_data_files(package, src_dir, files)

    def build_package_data(self):
        """Copy data files into build directory"""
        for package, src_dir, build_dir, filenames in self.data_files:
            for filename in filenames:
                target = os.path.join(build_dir, filename)
                self.mkpath(os.path.dirname(target))
                srcfile = os.path.join(src_dir, filename)
                outf, copied = self.copy_file(srcfile, target)
                srcfile = os.path.abspath(srcfile)
                if (copied and
                        srcfile in self.distribution.convert_2to3_doctests):
                    self.__doctests_2to3.append(outf)

    def analyze_manifest(self):
        self.manifest_files = mf = {}
        if not self.distribution.include_package_data:
            return
        src_dirs = {}
        for package in self.packages or ():
            # Locate package source directory
            src_dirs[assert_relative(self.get_package_dir(package))] = package

        self.run_command('egg_info')
        ei_cmd = self.get_finalized_command('egg_info')
        for path in ei_cmd.filelist.files:
            d, f = os.path.split(assert_relative(path))
            prev = None
            oldf = f
            while d and d != prev and d not in src_dirs:
                prev = d
                d, df = os.path.split(d)
                f = os.path.join(df, f)
            if d in src_dirs:
                if path.endswith('.py') and f == oldf:
                    continue  # it's a module, not data
                mf.setdefault(src_dirs[d], []).append(path)

    def get_data_files(self):
        pass  # kludge 2.4 for lazy computation

    if sys.version < "2.4":  # Python 2.4 already has this code
        def get_outputs(self, include_bytecode=1):
            """Return complete list of files copied to the build directory

            This includes both '.py' files and data files, as well as '.pyc'
            and '.pyo' files if 'include_bytecode' is true.  (This method is
            needed for the 'install_lib' command to do its job properly, and to
            generate a correct installation manifest.)
            """
            return orig.build_py.get_outputs(self, include_bytecode) + [
                os.path.join(build_dir, filename)
                for package, src_dir, build_dir, filenames in self.data_files
                for filename in filenames
            ]

    def check_package(self, package, package_dir):
        """Check namespace packages' __init__ for declare_namespace"""
        try:
            return self.packages_checked[package]
        except KeyError:
            pass

        init_py = orig.build_py.check_package(self, package, package_dir)
        self.packages_checked[package] = init_py

        if not init_py or not self.distribution.namespace_packages:
            return init_py

        for pkg in self.distribution.namespace_packages:
            if pkg == package or pkg.startswith(package + '.'):
                break
        else:
            return init_py

        f = open(init_py, 'rbU')
        if 'declare_namespace'.encode() not in f.read():
            from distutils.errors import DistutilsError

            raise DistutilsError(
                "Namespace package problem: %s is a namespace package, but "
                "its\n__init__.py does not call declare_namespace()! Please "
                'fix it.\n(See the setuptools manual under '
                '"Namespace Packages" for details.)\n"' % (package,)
            )
        f.close()
        return init_py

    def initialize_options(self):
        self.packages_checked = {}
        orig.build_py.initialize_options(self)

    def get_package_dir(self, package):
        res = orig.build_py.get_package_dir(self, package)
        if self.distribution.src_root is not None:
            return os.path.join(self.distribution.src_root, res)
        return res

    def exclude_data_files(self, package, src_dir, files):
        """Filter filenames for package's data files in 'src_dir'"""
        globs = (self.exclude_package_data.get('', [])
                 + self.exclude_package_data.get(package, []))
        bad = []
        for pattern in globs:
            bad.extend(
                fnmatch.filter(
                    files, os.path.join(src_dir, convert_path(pattern))
                )
            )
        bad = dict.fromkeys(bad)
        seen = {}
        return [
            f for f in files if f not in bad
            and f not in seen and seen.setdefault(f, 1)  # ditch dupes
        ]


def assert_relative(path):
    if not os.path.isabs(path):
        return path
    from distutils.errors import DistutilsSetupError

    msg = textwrap.dedent("""
        Error: setup script specifies an absolute path:

            %s

        setup() arguments must *always* be /-separated paths relative to the
        setup.py directory, *never* absolute paths.
        """).lstrip() % path
    raise DistutilsSetupError(msg)
