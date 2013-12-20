import setuptools, sys, glob
from distutils.command.install import install as _install
from distutils.errors import DistutilsArgError

class install(_install):
    """Use easy_install to install the package, w/dependencies"""

    user_options = _install.user_options + [
        ('old-and-unmanageable', None, "Try not to use this!"),
        ('single-version-externally-managed', None,
            "used by system package builders to create 'flat' eggs"),
    ]
    boolean_options = _install.boolean_options + [
        'old-and-unmanageable', 'single-version-externally-managed',
    ]
    new_commands = [
        ('install_egg_info', lambda self: True),
        ('install_scripts',  lambda self: True),
    ]
    _nc = dict(new_commands)

    def initialize_options(self):
        _install.initialize_options(self)
        self.old_and_unmanageable = None
        self.single_version_externally_managed = None
        self.no_compile = None  # make DISTUTILS_DEBUG work right!

    def finalize_options(self):
        _install.finalize_options(self)
        if self.root:
            self.single_version_externally_managed = True
        elif self.single_version_externally_managed:
            if not self.root and not self.record:
                raise DistutilsArgError(
                    "You must specify --record or --root when building system"
                    " packages"
                )

    def handle_extra_path(self):
        if self.root or self.single_version_externally_managed:
            # explicit backward-compatibility mode, allow extra_path to work
            return _install.handle_extra_path(self)

        # Ignore extra_path when installing an egg (or being run by another
        # command without --root or --single-version-externally-managed
        self.path_file = None
        self.extra_dirs = ''


    def run(self):
        # Explicit request for old-style install?  Just do it
        if self.old_and_unmanageable or self.single_version_externally_managed:
            return _install.run(self)

        # Attempt to detect whether we were called from setup() or by another
        # command.  If we were called by setup(), our caller will be the
        # 'run_command' method in 'distutils.dist', and *its* caller will be
        # the 'run_commands' method.  If we were called any other way, our
        # immediate caller *might* be 'run_command', but it won't have been
        # called by 'run_commands'.  This is slightly kludgy, but seems to
        # work.
        #
        caller = sys._getframe(2)
        caller_module = caller.f_globals.get('__name__','')
        caller_name = caller.f_code.co_name

        if caller_module != 'distutils.dist' or caller_name!='run_commands':
            # We weren't called from the command line or setup(), so we
            # should run in backward-compatibility mode to support bdist_*
            # commands.
            _install.run(self)
        else:
            self.do_egg_install()






    def do_egg_install(self):

        easy_install = self.distribution.get_command_class('easy_install')

        cmd = easy_install(
            self.distribution, args="x", root=self.root, record=self.record,
        )
        cmd.ensure_finalized()  # finalize before bdist_egg munges install cmd
        cmd.always_copy_from = '.'  # make sure local-dir eggs get installed

        # pick up setup-dir .egg files only: no .egg-info
        cmd.package_index.scan(glob.glob('*.egg'))

        self.run_command('bdist_egg')
        args = [self.distribution.get_command_obj('bdist_egg').egg_output]

        if setuptools.bootstrap_install_from:
            # Bootstrap self-installation of setuptools
            args.insert(0, setuptools.bootstrap_install_from)

        cmd.args = args
        cmd.run()
        setuptools.bootstrap_install_from = None

# XXX Python 3.1 doesn't see _nc if this is inside the class
install.sub_commands = [
        cmd for cmd in _install.sub_commands if cmd[0] not in install._nc
    ] + install.new_commands
















#
