"""build_ext tests
"""
import sys, os, shutil, tempfile, unittest, site, zipfile
from setuptools.command.upload_docs import upload_docs
from setuptools.dist import Distribution

SETUP_PY = """\
from setuptools import setup

setup(name='foo')
"""

class TestUploadDocsTest(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp()
        setup = os.path.join(self.dir, 'setup.py')
        f = open(setup, 'w')
        f.write(SETUP_PY)
        f.close()
        self.old_cwd = os.getcwd()
        os.chdir(self.dir)

        self.upload_dir = os.path.join(self.dir, 'build')
        os.mkdir(self.upload_dir)

        # A test document.
        f = open(os.path.join(self.upload_dir, 'index.html'), 'w')
        f.write("Hello world.")
        f.close()

        # An empty folder.
        os.mkdir(os.path.join(self.upload_dir, 'empty'))

        if sys.version >= "2.6":
            self.old_base = site.USER_BASE
            site.USER_BASE = upload_docs.USER_BASE = tempfile.mkdtemp()
            self.old_site = site.USER_SITE
            site.USER_SITE = upload_docs.USER_SITE = tempfile.mkdtemp()

    def tearDown(self):
        os.chdir(self.old_cwd)
        shutil.rmtree(self.dir)
        if sys.version >= "2.6":
            shutil.rmtree(site.USER_BASE)
            shutil.rmtree(site.USER_SITE)
            site.USER_BASE = self.old_base
            site.USER_SITE = self.old_site

    def test_create_zipfile(self):
        # Test to make sure zipfile creation handles common cases.
        # This explicitly includes a folder containing an empty folder.

        dist = Distribution()

        cmd = upload_docs(dist)
        cmd.upload_dir = self.upload_dir
        cmd.target_dir = self.upload_dir
        tmp_dir = tempfile.mkdtemp()
        tmp_file = os.path.join(tmp_dir, 'foo.zip')
        try:
            zip_file = cmd.create_zipfile(tmp_file)

            assert zipfile.is_zipfile(tmp_file)

            zip_file = zipfile.ZipFile(tmp_file) # woh...

            assert zip_file.namelist() == ['index.html']

            zip_file.close()
        finally:
            shutil.rmtree(tmp_dir)

