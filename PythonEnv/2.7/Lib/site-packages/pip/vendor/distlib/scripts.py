# -*- coding: utf-8 -*-
#
# Copyright (C) 2013 Vinay Sajip.
# Licensed to the Python Software Foundation under a contributor agreement.
# See LICENSE.txt and CONTRIBUTORS.txt.
#
import logging
import os
import re
import struct
import sys

from . import DistlibException
from .compat import sysconfig, fsencode, detect_encoding
from .resources import finder
from .util import FileOperator, get_export_entry, convert_path, get_executable

logger = logging.getLogger(__name__)

# check if Python is called on the first line with this expression
FIRST_LINE_RE = re.compile(b'^#!.*pythonw?[0-9.]*([ \t].*)?$')
SCRIPT_TEMPLATE = '''%(shebang)s
if __name__ == '__main__':
    import sys, re

    def _resolve(module, func):
        __import__(module)
        mod = sys.modules[module]
        parts = func.split('.')
        result = getattr(mod, parts.pop(0))
        for p in parts:
            result = getattr(result, p)
        return result

    try:
        sys.argv[0] = re.sub('-script.pyw?$', '', sys.argv[0])

        func = _resolve('%(module)s', '%(func)s')
        rc = func() # None interpreted as 0
    except Exception as e:  # only supporting Python >= 2.6
        sys.stderr.write('%%s\\n' %% e)
        rc = 1
    sys.exit(rc)
'''


class ScriptMaker(object):
    """
    A class to copy or create scripts from source scripts or callable
    specifications.
    """
    script_template = SCRIPT_TEMPLATE

    executable = None  # for shebangs

    def __init__(self, source_dir, target_dir, add_launchers=True,
                 dry_run=False, fileop=None):
        self.source_dir = source_dir
        self.target_dir = target_dir
        self.add_launchers = add_launchers
        self.force = False
        self.set_mode = False
        self._fileop = fileop or FileOperator(dry_run)

    def _get_alternate_executable(self, executable, flags):
        if 'gui' in flags and os.name == 'nt':
            dn, fn = os.path.split(executable)
            fn = fn.replace('python', 'pythonw')
            executable = os.path.join(dn, fn)
        return executable

    def _get_shebang(self, encoding, post_interp=b'', flags=None):
        if self.executable:
            executable = self.executable
        elif not sysconfig.is_python_build():
            executable = get_executable()
        elif hasattr(sys, 'base_prefix') and sys.prefix != sys.base_prefix:
            executable = os.path.join(
                sysconfig.get_path('scripts'),
               'python%s' % sysconfig.get_config_var('EXE'))
        else:
            executable = os.path.join(
                sysconfig.get_config_var('BINDIR'),
               'python%s%s' % (sysconfig.get_config_var('VERSION'),
                               sysconfig.get_config_var('EXE')))
        if flags:
            executable = self._get_alternate_executable(executable, flags)

        executable = fsencode(executable)
        shebang = b'#!' + executable + post_interp + b'\n'
        # Python parser starts to read a script using UTF-8 until
        # it gets a #coding:xxx cookie. The shebang has to be the
        # first line of a file, the #coding:xxx cookie cannot be
        # written before. So the shebang has to be decodable from
        # UTF-8.
        try:
            shebang.decode('utf-8')
        except UnicodeDecodeError:
            raise ValueError(
                'The shebang (%r) is not decodable from utf-8' % shebang)
        # If the script is encoded to a custom encoding (use a
        # #coding:xxx cookie), the shebang has to be decodable from
        # the script encoding too.
        if encoding != 'utf-8':
            try:
                shebang.decode(encoding)
            except UnicodeDecodeError:
                raise ValueError(
                    'The shebang (%r) is not decodable '
                    'from the script encoding (%r)' % (shebang, encoding))
        return shebang

    def _get_script_text(self, shebang, entry):
        return self.script_template % dict(shebang=shebang,
                                           module=entry.prefix,
                                           func=entry.suffix)

    def _make_script(self, entry, filenames):
        shebang = self._get_shebang('utf-8', flags=entry.flags).decode('utf-8')
        script = self._get_script_text(shebang, entry)
        outname = os.path.join(self.target_dir, entry.name)
        use_launcher = self.add_launchers and os.name == 'nt'
        if use_launcher:
            exename = '%s.exe' % outname
            if 'gui' in entry.flags:
                ext = 'pyw'
                launcher = self._get_launcher('w')
            else:
                ext = 'py'
                launcher = self._get_launcher('t')
            outname = '%s-script.%s' % (outname, ext)
        self._fileop.write_text_file(outname, script, 'utf-8')
        if self.set_mode:
            self._fileop.set_executable_mode([outname])
        filenames.append(outname)
        if use_launcher:
            self._fileop.write_binary_file(exename, launcher)
            filenames.append(exename)

    def _copy_script(self, script, filenames):
        adjust = False
        script = convert_path(script)
        outname = os.path.join(self.target_dir, os.path.basename(script))
        filenames.append(outname)
        script = os.path.join(self.source_dir, script)
        if not self.force and not self._fileop.newer(script, outname):
            logger.debug('not copying %s (up-to-date)', script)
            return

        # Always open the file, but ignore failures in dry-run mode --
        # that way, we'll get accurate feedback if we can read the
        # script.
        try:
            f = open(script, 'rb')
        except IOError:
            if not self.dry_run:
                raise
            f = None
        else:
            encoding, lines = detect_encoding(f.readline)
            f.seek(0)
            first_line = f.readline()
            if not first_line:
                logger.warning('%s: %s is an empty file (skipping)',
                               self.get_command_name(),  script)
                return

            match = FIRST_LINE_RE.match(first_line.replace(b'\r\n', b'\n'))
            if match:
                adjust = True
                post_interp = match.group(1) or b''

        if not adjust:
            if f:
                f.close()
            self._fileop.copy_file(script, outname)
        else:
            logger.info('copying and adjusting %s -> %s', script,
                        self.target_dir)
            if not self._fileop.dry_run:
                shebang = self._get_shebang(encoding, post_interp)
                use_launcher = self.add_launchers and os.name == 'nt'
                if use_launcher:
                    n, e = os.path.splitext(outname)
                    exename = n + '.exe'
                    if b'pythonw' in first_line:
                        launcher = self._get_launcher('w')
                        suffix = '-script.pyw'
                    else:
                        launcher = self._get_launcher('t')
                        suffix = '-script.py'
                    outname = n + suffix
                    filenames[-1] = outname
                self._fileop.write_binary_file(outname, shebang + f.read())
                if use_launcher:
                    self._fileop.write_binary_file(exename, launcher)
                    filenames.append(exename)
            if f:
                f.close()
        if self.set_mode:
            self._fileop.set_executable_mode([outname])

    @property
    def dry_run(self):
        return self._fileop.dry_run

    @dry_run.setter
    def dry_run(self, value):
        self._fileop.dry_run = value

    if os.name == 'nt':
        # Executable launcher support.
        # Launchers are from https://bitbucket.org/vinay.sajip/simple_launcher/

        def _get_launcher(self, kind):
            if struct.calcsize('P') == 8:   # 64-bit
                bits = '64'
            else:
                bits = '32'
            name = '%s%s.exe' % (kind, bits)
            result = finder('distlib').find(name).bytes
            return result

    # Public API follows

    def make(self, specification):
        """
        Make a script.

        :param specification: The specification, which is either a valid export
                              entry specification (to make a script from a
                              callable) or a filename (to make a script by
                              copying from a source location).
        :return: A list of all absolute pathnames written to,
        """
        filenames = []
        entry = get_export_entry(specification)
        if entry is None:
            self._copy_script(specification, filenames)
        else:
            self._make_script(entry, filenames)
        return filenames

    def make_multiple(self, specifications):
        """
        Take a list of specifications and make scripts from them,
        :param specifications: A list of specifications.
        :return: A list of all absolute pathnames written to,
        """
        filenames = []
        for specification in specifications:
            filenames.extend(self.make(specification))
        return filenames
