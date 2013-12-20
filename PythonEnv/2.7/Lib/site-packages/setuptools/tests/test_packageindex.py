"""Package Index Tests
"""
import sys
import unittest
import pkg_resources
from setuptools.compat import urllib2, httplib, HTTPError, unicode
import distutils.errors
import setuptools.package_index
from setuptools.tests.server import IndexServer

class TestPackageIndex(unittest.TestCase):

    def test_bad_url_bad_port(self):
        index = setuptools.package_index.PackageIndex()
        url = 'http://127.0.0.1:0/nonesuch/test_package_index'
        try:
            v = index.open_url(url)
        except Exception:
            v = sys.exc_info()[1]
            self.assertTrue(url in str(v))
        else:
            self.assertTrue(isinstance(v, HTTPError))

    def test_bad_url_typo(self):
        # issue 16
        # easy_install inquant.contentmirror.plone breaks because of a typo
        # in its home URL
        index = setuptools.package_index.PackageIndex(
            hosts=('www.example.com',)
        )

        url = 'url:%20https://svn.plone.org/svn/collective/inquant.contentmirror.plone/trunk'
        try:
            v = index.open_url(url)
        except Exception:
            v = sys.exc_info()[1]
            self.assertTrue(url in str(v))
        else:
            self.assertTrue(isinstance(v, HTTPError))

    def test_bad_url_bad_status_line(self):
        index = setuptools.package_index.PackageIndex(
            hosts=('www.example.com',)
        )

        def _urlopen(*args):
            raise httplib.BadStatusLine('line')

        index.opener = _urlopen
        url = 'http://example.com'
        try:
            v = index.open_url(url)
        except Exception:
            v = sys.exc_info()[1]
            self.assertTrue('line' in str(v))
        else:
            raise AssertionError('Should have raise here!')

    def test_bad_url_double_scheme(self):
        """
        A bad URL with a double scheme should raise a DistutilsError.
        """
        index = setuptools.package_index.PackageIndex(
            hosts=('www.example.com',)
        )

        # issue 20
        url = 'http://http://svn.pythonpaste.org/Paste/wphp/trunk'
        try:
            index.open_url(url)
        except distutils.errors.DistutilsError:
            error = sys.exc_info()[1]
            msg = unicode(error)
            assert 'nonnumeric port' in msg or 'getaddrinfo failed' in msg or 'Name or service not known' in msg
            return
        raise RuntimeError("Did not raise")

    def test_bad_url_screwy_href(self):
        index = setuptools.package_index.PackageIndex(
            hosts=('www.example.com',)
        )

        # issue #160
        if sys.version_info[0] == 2 and sys.version_info[1] == 7:
            # this should not fail
            url = 'http://example.com'
            page = ('<a href="http://www.famfamfam.com]('
                    'http://www.famfamfam.com/">')
            index.process_index(url, page)

    def test_url_ok(self):
        index = setuptools.package_index.PackageIndex(
            hosts=('www.example.com',)
        )
        url = 'file:///tmp/test_package_index'
        self.assertTrue(index.url_ok(url, True))

    def test_links_priority(self):
        """
        Download links from the pypi simple index should be used before
        external download links.
        https://bitbucket.org/tarek/distribute/issue/163

        Usecase :
        - someone uploads a package on pypi, a md5 is generated
        - someone manually copies this link (with the md5 in the url) onto an
          external page accessible from the package page.
        - someone reuploads the package (with a different md5)
        - while easy_installing, an MD5 error occurs because the external link
          is used
        -> Setuptools should use the link from pypi, not the external one.
        """
        if sys.platform.startswith('java'):
            # Skip this test on jython because binding to :0 fails
            return

        # start an index server
        server = IndexServer()
        server.start()
        index_url = server.base_url() + 'test_links_priority/simple/'

        # scan a test index
        pi = setuptools.package_index.PackageIndex(index_url)
        requirement = pkg_resources.Requirement.parse('foobar')
        pi.find_packages(requirement)
        server.stop()

        # the distribution has been found
        self.assertTrue('foobar' in pi)
        # we have only one link, because links are compared without md5
        self.assertTrue(len(pi['foobar'])==1)
        # the link should be from the index
        self.assertTrue('correct_md5' in pi['foobar'][0].location)

    def test_parse_bdist_wininst(self):
        self.assertEqual(setuptools.package_index.parse_bdist_wininst(
            'reportlab-2.5.win32-py2.4.exe'), ('reportlab-2.5', '2.4', 'win32'))
        self.assertEqual(setuptools.package_index.parse_bdist_wininst(
            'reportlab-2.5.win32.exe'), ('reportlab-2.5', None, 'win32'))
        self.assertEqual(setuptools.package_index.parse_bdist_wininst(
            'reportlab-2.5.win-amd64-py2.7.exe'), ('reportlab-2.5', '2.7', 'win-amd64'))
        self.assertEqual(setuptools.package_index.parse_bdist_wininst(
            'reportlab-2.5.win-amd64.exe'), ('reportlab-2.5', None, 'win-amd64'))

    def test__vcs_split_rev_from_url(self):
        """
        Test the basic usage of _vcs_split_rev_from_url
        """
        vsrfu = setuptools.package_index.PackageIndex._vcs_split_rev_from_url
        url, rev = vsrfu('https://example.com/bar@2995')
        self.assertEqual(url, 'https://example.com/bar')
        self.assertEqual(rev, '2995')

class TestContentCheckers(unittest.TestCase):

    def test_md5(self):
        checker = setuptools.package_index.HashChecker.from_url(
            'http://foo/bar#md5=f12895fdffbd45007040d2e44df98478')
        checker.feed('You should probably not be using MD5'.encode('ascii'))
        self.assertEqual(checker.hash.hexdigest(),
            'f12895fdffbd45007040d2e44df98478')
        self.assertTrue(checker.is_valid())

    def test_other_fragment(self):
        "Content checks should succeed silently if no hash is present"
        checker = setuptools.package_index.HashChecker.from_url(
            'http://foo/bar#something%20completely%20different')
        checker.feed('anything'.encode('ascii'))
        self.assertTrue(checker.is_valid())

    def test_blank_md5(self):
        "Content checks should succeed if a hash is empty"
        checker = setuptools.package_index.HashChecker.from_url(
            'http://foo/bar#md5=')
        checker.feed('anything'.encode('ascii'))
        self.assertTrue(checker.is_valid())

    def test_get_hash_name_md5(self):
        checker = setuptools.package_index.HashChecker.from_url(
            'http://foo/bar#md5=f12895fdffbd45007040d2e44df98478')
        self.assertEqual(checker.hash_name, 'md5')

    def test_report(self):
        checker = setuptools.package_index.HashChecker.from_url(
            'http://foo/bar#md5=f12895fdffbd45007040d2e44df98478')
        rep = checker.report(lambda x: x, 'My message about %s')
        self.assertEqual(rep, 'My message about md5')
