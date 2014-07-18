from distutils import log
import distutils.command.install_scripts as orig
import os

from pkg_resources import Distribution, PathMetadata, ensure_directory


class install_scripts(orig.install_scripts):
    """Do normal script install, plus any egg_info wrapper scripts"""

    def initialize_options(self):
        orig.install_scripts.initialize_options(self)
        self.no_ep = False

    def run(self):
        from setuptools.command.easy_install import get_script_args
        from setuptools.command.easy_install import sys_executable

        self.run_command("egg_info")
        if self.distribution.scripts:
            orig.install_scripts.run(self)  # run first to set up self.outfiles
        else:
            self.outfiles = []
        if self.no_ep:
            # don't install entry point scripts into .egg file!
            return

        ei_cmd = self.get_finalized_command("egg_info")
        dist = Distribution(
            ei_cmd.egg_base, PathMetadata(ei_cmd.egg_base, ei_cmd.egg_info),
            ei_cmd.egg_name, ei_cmd.egg_version,
        )
        bs_cmd = self.get_finalized_command('build_scripts')
        executable = getattr(bs_cmd, 'executable', sys_executable)
        is_wininst = getattr(
            self.get_finalized_command("bdist_wininst"), '_is_running', False
        )
        for args in get_script_args(dist, executable, is_wininst):
            self.write_script(*args)

    def write_script(self, script_name, contents, mode="t", *ignored):
        """Write an executable file to the scripts directory"""
        from setuptools.command.easy_install import chmod, current_umask

        log.info("Installing %s script to %s", script_name, self.install_dir)
        target = os.path.join(self.install_dir, script_name)
        self.outfiles.append(target)

        mask = current_umask()
        if not self.dry_run:
            ensure_directory(target)
            f = open(target, "w" + mode)
            f.write(contents)
            f.close()
            chmod(target, 0o777 - mask)
