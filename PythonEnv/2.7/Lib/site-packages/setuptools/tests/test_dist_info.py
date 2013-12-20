"""Test .dist-info style distributions.
"""
import os
import shutil
import tempfile
import unittest
import textwrap

try:
    import ast
except:
    pass

import pkg_resources

from setuptools.tests.py26compat import skipIf

def DALS(s):
    "dedent and left-strip"
    return textwrap.dedent(s).lstrip()

class TestDistInfo(unittest.TestCase):

    def test_distinfo(self):
        dists = {}
        for d in pkg_resources.find_distributions(self.tmpdir):
            dists[d.project_name] = d

        assert len(dists) == 2, dists

        unversioned = dists['UnversionedDistribution']
        versioned = dists['VersionedDistribution']

        assert versioned.version == '2.718' # from filename
        assert unversioned.version == '0.3' # from METADATA

    @skipIf('ast' not in globals(),
        "ast is used to test conditional dependencies (Python >= 2.6)")
    def test_conditional_dependencies(self):
        requires = [pkg_resources.Requirement.parse('splort==4'),
                    pkg_resources.Requirement.parse('quux>=1.1')]

        for d in pkg_resources.find_distributions(self.tmpdir):
            self.assertEqual(d.requires(), requires[:1])
            self.assertEqual(d.requires(extras=('baz',)), requires)
            self.assertEqual(d.extras, ['baz'])

    def setUp(self):
        self.tmpdir = tempfile.mkdtemp()
        versioned = os.path.join(self.tmpdir,
                                 'VersionedDistribution-2.718.dist-info')
        os.mkdir(versioned)
        metadata_file = open(os.path.join(versioned, 'METADATA'), 'w+')
        try:
            metadata_file.write(DALS(
                """
                Metadata-Version: 1.2
                Name: VersionedDistribution
                Requires-Dist: splort (4)
                Provides-Extra: baz
                Requires-Dist: quux (>=1.1); extra == 'baz'
                """))
        finally:
            metadata_file.close()
        unversioned = os.path.join(self.tmpdir,
                                   'UnversionedDistribution.dist-info')
        os.mkdir(unversioned)
        metadata_file = open(os.path.join(unversioned, 'METADATA'), 'w+')
        try:
            metadata_file.write(DALS(
                """
                Metadata-Version: 1.2
                Name: UnversionedDistribution
                Version: 0.3
                Requires-Dist: splort (==4)
                Provides-Extra: baz
                Requires-Dist: quux (>=1.1); extra == 'baz'
                """))
        finally:
            metadata_file.close()

    def tearDown(self):
        shutil.rmtree(self.tmpdir)
