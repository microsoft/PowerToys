# This is just a kludge so that bdist_rpm doesn't guess wrong about the
# distribution name and version, if the egg_info command is going to alter
# them, another kludge to allow you to build old-style non-egg RPMs, and
# finally, a kludge to track .rpm files for uploading when run on Python <2.5.

from distutils.command.bdist_rpm import bdist_rpm as _bdist_rpm
import sys, os

class bdist_rpm(_bdist_rpm):

    def initialize_options(self):
        _bdist_rpm.initialize_options(self)
        self.no_egg = None

    if sys.version<"2.5":
        # Track for uploading any .rpm file(s) moved to self.dist_dir
        def move_file(self, src, dst, level=1):
            _bdist_rpm.move_file(self, src, dst, level)
            if dst==self.dist_dir and src.endswith('.rpm'):
                getattr(self.distribution,'dist_files',[]).append(
                    ('bdist_rpm',
                    src.endswith('.src.rpm') and 'any' or sys.version[:3],
                     os.path.join(dst, os.path.basename(src)))
                )

    def run(self):
        self.run_command('egg_info')    # ensure distro name is up-to-date
        _bdist_rpm.run(self)













    def _make_spec_file(self):
        version = self.distribution.get_version()
        rpmversion = version.replace('-','_')
        spec = _bdist_rpm._make_spec_file(self)
        line23 = '%define version '+version
        line24 = '%define version '+rpmversion
        spec  = [
            line.replace(
                "Source0: %{name}-%{version}.tar",
                "Source0: %{name}-%{unmangled_version}.tar"
            ).replace(
                "setup.py install ",
                "setup.py install --single-version-externally-managed "
            ).replace(
                "%setup",
                "%setup -n %{name}-%{unmangled_version}"
            ).replace(line23,line24)
            for line in spec
        ]
        spec.insert(spec.index(line24)+1, "%define unmangled_version "+version)
        return spec




















