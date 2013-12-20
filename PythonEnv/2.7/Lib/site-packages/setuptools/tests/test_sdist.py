# -*- coding: utf-8 -*-
"""sdist tests"""


import os
import shutil
import sys
import tempfile
import unittest
import unicodedata

from setuptools.compat import StringIO, unicode
from setuptools.command.sdist import sdist
from setuptools.command.egg_info import manifest_maker
from setuptools.dist import Distribution


SETUP_ATTRS = {
    'name': 'sdist_test',
    'version': '0.0',
    'packages': ['sdist_test'],
    'package_data': {'sdist_test': ['*.txt']}
}


SETUP_PY = """\
from setuptools import setup

setup(**%r)
""" % SETUP_ATTRS


if sys.version_info >= (3,):
    LATIN1_FILENAME = 'smörbröd.py'.encode('latin-1')
else:
    LATIN1_FILENAME = 'sm\xf6rbr\xf6d.py'


# Cannot use context manager because of Python 2.4
def quiet():
    global old_stdout, old_stderr
    old_stdout, old_stderr = sys.stdout, sys.stderr
    sys.stdout, sys.stderr = StringIO(), StringIO()

def unquiet():
    sys.stdout, sys.stderr = old_stdout, old_stderr


# Fake byte literals for Python <= 2.5
def b(s, encoding='utf-8'):
    if sys.version_info >= (3,):
        return s.encode(encoding)
    return s


# Convert to POSIX path
def posix(path):
    if sys.version_info >= (3,) and not isinstance(path, str):
        return path.replace(os.sep.encode('ascii'), b('/'))
    else:
        return path.replace(os.sep, '/')


# HFS Plus uses decomposed UTF-8
def decompose(path):
    if isinstance(path, unicode):
        return unicodedata.normalize('NFD', path)
    try:
        path = path.decode('utf-8')
        path = unicodedata.normalize('NFD', path)
        path = path.encode('utf-8')
    except UnicodeError:
        pass # Not UTF-8
    return path


class TestSdistTest(unittest.TestCase):

    def setUp(self):
        self.temp_dir = tempfile.mkdtemp()
        f = open(os.path.join(self.temp_dir, 'setup.py'), 'w')
        f.write(SETUP_PY)
        f.close()
        # Set up the rest of the test package
        test_pkg = os.path.join(self.temp_dir, 'sdist_test')
        os.mkdir(test_pkg)
        # *.rst was not included in package_data, so c.rst should not be
        # automatically added to the manifest when not under version control
        for fname in ['__init__.py', 'a.txt', 'b.txt', 'c.rst']:
            # Just touch the files; their contents are irrelevant
            open(os.path.join(test_pkg, fname), 'w').close()

        self.old_cwd = os.getcwd()
        os.chdir(self.temp_dir)

    def tearDown(self):
        os.chdir(self.old_cwd)
        shutil.rmtree(self.temp_dir)

    def test_package_data_in_sdist(self):
        """Regression test for pull request #4: ensures that files listed in
        package_data are included in the manifest even if they're not added to
        version control.
        """

        dist = Distribution(SETUP_ATTRS)
        dist.script_name = 'setup.py'
        cmd = sdist(dist)
        cmd.ensure_finalized()

        # squelch output
        quiet()
        try:
            cmd.run()
        finally:
            unquiet()

        manifest = cmd.filelist.files
        self.assertTrue(os.path.join('sdist_test', 'a.txt') in manifest)
        self.assertTrue(os.path.join('sdist_test', 'b.txt') in manifest)
        self.assertTrue(os.path.join('sdist_test', 'c.rst') not in manifest)

    def test_manifest_is_written_with_utf8_encoding(self):
        # Test for #303.
        dist = Distribution(SETUP_ATTRS)
        dist.script_name = 'setup.py'
        mm = manifest_maker(dist)
        mm.manifest = os.path.join('sdist_test.egg-info', 'SOURCES.txt')
        os.mkdir('sdist_test.egg-info')

        # UTF-8 filename
        filename = os.path.join('sdist_test', 'smörbröd.py')

        # Add UTF-8 filename and write manifest
        quiet()
        try:
            mm.run()
            mm.filelist.files.append(filename)
            mm.write_manifest()
        finally:
            unquiet()

        manifest = open(mm.manifest, 'rbU')
        contents = manifest.read()
        manifest.close()

        # The manifest should be UTF-8 encoded
        try:
            u_contents = contents.decode('UTF-8')
        except UnicodeDecodeError:
            e = sys.exc_info()[1]
            self.fail(e)

        # The manifest should contain the UTF-8 filename
        if sys.version_info >= (3,):
            self.assertTrue(posix(filename) in u_contents)
        else:
            self.assertTrue(posix(filename) in contents)

    # Python 3 only
    if sys.version_info >= (3,):

        def test_write_manifest_allows_utf8_filenames(self):
            # Test for #303.
            dist = Distribution(SETUP_ATTRS)
            dist.script_name = 'setup.py'
            mm = manifest_maker(dist)
            mm.manifest = os.path.join('sdist_test.egg-info', 'SOURCES.txt')
            os.mkdir('sdist_test.egg-info')

            # UTF-8 filename
            filename = os.path.join(b('sdist_test'), b('smörbröd.py'))

            # Add filename and write manifest
            quiet()
            try:
                mm.run()
                u_filename = filename.decode('utf-8')
                mm.filelist.files.append(u_filename)
                # Re-write manifest
                mm.write_manifest()
            finally:
                unquiet()

            manifest = open(mm.manifest, 'rbU')
            contents = manifest.read()
            manifest.close()

            # The manifest should be UTF-8 encoded
            try:
                contents.decode('UTF-8')
            except UnicodeDecodeError:
                e = sys.exc_info()[1]
                self.fail(e)

            # The manifest should contain the UTF-8 filename
            self.assertTrue(posix(filename) in contents)

            # The filelist should have been updated as well
            self.assertTrue(u_filename in mm.filelist.files)

        def test_write_manifest_skips_non_utf8_filenames(self):
            # Test for #303.
            dist = Distribution(SETUP_ATTRS)
            dist.script_name = 'setup.py'
            mm = manifest_maker(dist)
            mm.manifest = os.path.join('sdist_test.egg-info', 'SOURCES.txt')
            os.mkdir('sdist_test.egg-info')

            # Latin-1 filename
            filename = os.path.join(b('sdist_test'), LATIN1_FILENAME)

            # Add filename with surrogates and write manifest
            quiet()
            try:
                mm.run()
                u_filename = filename.decode('utf-8', 'surrogateescape')
                mm.filelist.files.append(u_filename)
                # Re-write manifest
                mm.write_manifest()
            finally:
                unquiet()

            manifest = open(mm.manifest, 'rbU')
            contents = manifest.read()
            manifest.close()

            # The manifest should be UTF-8 encoded
            try:
                contents.decode('UTF-8')
            except UnicodeDecodeError:
                e = sys.exc_info()[1]
                self.fail(e)

            # The Latin-1 filename should have been skipped
            self.assertFalse(posix(filename) in contents)

            # The filelist should have been updated as well
            self.assertFalse(u_filename in mm.filelist.files)

    def test_manifest_is_read_with_utf8_encoding(self):
        # Test for #303.
        dist = Distribution(SETUP_ATTRS)
        dist.script_name = 'setup.py'
        cmd = sdist(dist)
        cmd.ensure_finalized()

        # Create manifest
        quiet()
        try:
            cmd.run()
        finally:
            unquiet()

        # Add UTF-8 filename to manifest
        filename = os.path.join(b('sdist_test'), b('smörbröd.py'))
        cmd.manifest = os.path.join('sdist_test.egg-info', 'SOURCES.txt')
        manifest = open(cmd.manifest, 'ab')
        manifest.write(b('\n')+filename)
        manifest.close()

        # The file must exist to be included in the filelist
        open(filename, 'w').close()

        # Re-read manifest
        cmd.filelist.files = []
        quiet()
        try:
            cmd.read_manifest()
        finally:
            unquiet()

        # The filelist should contain the UTF-8 filename
        if sys.version_info >= (3,):
            filename = filename.decode('utf-8')
        self.assertTrue(filename in cmd.filelist.files)

    # Python 3 only
    if sys.version_info >= (3,):

        def test_read_manifest_skips_non_utf8_filenames(self):
            # Test for #303.
            dist = Distribution(SETUP_ATTRS)
            dist.script_name = 'setup.py'
            cmd = sdist(dist)
            cmd.ensure_finalized()

            # Create manifest
            quiet()
            try:
                cmd.run()
            finally:
                unquiet()

            # Add Latin-1 filename to manifest
            filename = os.path.join(b('sdist_test'), LATIN1_FILENAME)
            cmd.manifest = os.path.join('sdist_test.egg-info', 'SOURCES.txt')
            manifest = open(cmd.manifest, 'ab')
            manifest.write(b('\n')+filename)
            manifest.close()

            # The file must exist to be included in the filelist
            open(filename, 'w').close()

            # Re-read manifest
            cmd.filelist.files = []
            quiet()
            try:
                try:
                    cmd.read_manifest()
                except UnicodeDecodeError:
                    e = sys.exc_info()[1]
                    self.fail(e)
            finally:
                unquiet()

            # The Latin-1 filename should have been skipped
            filename = filename.decode('latin-1')
            self.assertFalse(filename in cmd.filelist.files)

    def test_sdist_with_utf8_encoded_filename(self):
        # Test for #303.
        dist = Distribution(SETUP_ATTRS)
        dist.script_name = 'setup.py'
        cmd = sdist(dist)
        cmd.ensure_finalized()

        # UTF-8 filename
        filename = os.path.join(b('sdist_test'), b('smörbröd.py'))
        open(filename, 'w').close()

        quiet()
        try:
            cmd.run()
        finally:
            unquiet()

        if sys.platform == 'darwin':
            filename = decompose(filename)

        if sys.version_info >= (3,):
            fs_enc = sys.getfilesystemencoding()

            if sys.platform == 'win32':
                if fs_enc == 'cp1252':
                    # Python 3 mangles the UTF-8 filename
                    filename = filename.decode('cp1252')
                    self.assertTrue(filename in cmd.filelist.files)
                else:
                    filename = filename.decode('mbcs')
                    self.assertTrue(filename in cmd.filelist.files)
            else:
                filename = filename.decode('utf-8')
                self.assertTrue(filename in cmd.filelist.files)
        else:
            self.assertTrue(filename in cmd.filelist.files)

    def test_sdist_with_latin1_encoded_filename(self):
        # Test for #303.
        dist = Distribution(SETUP_ATTRS)
        dist.script_name = 'setup.py'
        cmd = sdist(dist)
        cmd.ensure_finalized()

        # Latin-1 filename
        filename = os.path.join(b('sdist_test'), LATIN1_FILENAME)
        open(filename, 'w').close()
        self.assertTrue(os.path.isfile(filename))

        quiet()
        try:
            cmd.run()
        finally:
            unquiet()

        if sys.version_info >= (3,):
            #not all windows systems have a default FS encoding of cp1252
            if sys.platform == 'win32':
                # Latin-1 is similar to Windows-1252 however
                # on mbcs filesys it is not in latin-1 encoding
                fs_enc = sys.getfilesystemencoding()
                if fs_enc == 'mbcs':
                    filename = filename.decode('mbcs')
                else:
                    filename = filename.decode('latin-1')

                self.assertTrue(filename in cmd.filelist.files)
            else:
                # The Latin-1 filename should have been skipped
                filename = filename.decode('latin-1')
                self.assertFalse(filename in cmd.filelist.files)
        else:
            # No conversion takes place under Python 2 and the file
            # is included. We shall keep it that way for BBB.
            self.assertTrue(filename in cmd.filelist.files)


def test_suite():
    return unittest.defaultTestLoader.loadTestsFromName(__name__)

