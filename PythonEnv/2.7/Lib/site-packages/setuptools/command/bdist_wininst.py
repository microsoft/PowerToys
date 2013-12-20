from distutils.command.bdist_wininst import bdist_wininst as _bdist_wininst
import os, sys

class bdist_wininst(_bdist_wininst):
    _good_upload = _bad_upload = None

    def create_exe(self, arcname, fullname, bitmap=None):
        _bdist_wininst.create_exe(self, arcname, fullname, bitmap)
        installer_name = self.get_installer_filename(fullname) 
        if self.target_version:
            pyversion = self.target_version
            # fix 2.5+ bdist_wininst ignoring --target-version spec
            self._bad_upload = ('bdist_wininst', 'any', installer_name)
        else:
            pyversion = 'any'
        self._good_upload = ('bdist_wininst', pyversion, installer_name)
        
    def _fix_upload_names(self):
        good, bad = self._good_upload, self._bad_upload
        dist_files = getattr(self.distribution, 'dist_files', [])
        if bad in dist_files:
            dist_files.remove(bad)
        if good not in dist_files:
            dist_files.append(good)

    def reinitialize_command (self, command, reinit_subcommands=0):
        cmd = self.distribution.reinitialize_command(
            command, reinit_subcommands)
        if command in ('install', 'install_lib'):
            cmd.install_lib = None  # work around distutils bug
        return cmd

    def run(self):
        self._is_running = True
        try:
            _bdist_wininst.run(self)
            self._fix_upload_names()
        finally:
            self._is_running = False


    if not hasattr(_bdist_wininst, 'get_installer_filename'):
        def get_installer_filename(self, fullname):
            # Factored out to allow overriding in subclasses
            if self.target_version:
                # if we create an installer for a specific python version,
                # it's better to include this in the name
                installer_name = os.path.join(self.dist_dir,
                                              "%s.win32-py%s.exe" %
                                               (fullname, self.target_version))
            else:
                installer_name = os.path.join(self.dist_dir,
                                              "%s.win32.exe" % fullname)
            return installer_name
    # get_installer_filename()
    


























