import os
import tempfile
import shutil
import unittest

import pkg_resources
from setuptools.command import egg_info

ENTRIES_V10 = pkg_resources.resource_string(__name__, 'entries-v10')
"An entries file generated with svn 1.6.17 against the legacy Setuptools repo"

class TestEggInfo(unittest.TestCase):

    def setUp(self):
        self.test_dir = tempfile.mkdtemp()
        os.mkdir(os.path.join(self.test_dir, '.svn'))

        self.old_cwd = os.getcwd()
        os.chdir(self.test_dir)

    def tearDown(self):
        os.chdir(self.old_cwd)
        shutil.rmtree(self.test_dir)

    def _write_entries(self, entries):
        fn = os.path.join(self.test_dir, '.svn', 'entries')
        entries_f = open(fn, 'wb')
        entries_f.write(entries)
        entries_f.close()

    def test_version_10_format(self):
        """
        """
        self._write_entries(ENTRIES_V10)
        rev = egg_info.egg_info.get_svn_revision()
        self.assertEqual(rev, '89000')


def test_suite():
    return unittest.defaultTestLoader.loadTestsFromName(__name__)
