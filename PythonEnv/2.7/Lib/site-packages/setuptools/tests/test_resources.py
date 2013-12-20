#!/usr/bin/python
# -*- coding: utf-8 -*-
# NOTE: the shebang and encoding lines are for ScriptHeaderTests; do not remove
from unittest import TestCase, makeSuite; from pkg_resources import *
from setuptools.command.easy_install import get_script_header, is_sh
from setuptools.compat import StringIO, iteritems
import os, pkg_resources, sys, tempfile, shutil
try: frozenset
except NameError:
    from sets import ImmutableSet as frozenset

def safe_repr(obj, short=False):
    """ copied from Python2.7"""
    try:
        result = repr(obj)
    except Exception:
        result = object.__repr__(obj)
    if not short or len(result) < _MAX_LENGTH:
        return result
    return result[:_MAX_LENGTH] + ' [truncated]...'

class Metadata(EmptyProvider):
    """Mock object to return metadata as if from an on-disk distribution"""

    def __init__(self,*pairs):
        self.metadata = dict(pairs)

    def has_metadata(self,name):
        return name in self.metadata

    def get_metadata(self,name):
        return self.metadata[name]

    def get_metadata_lines(self,name):
        return yield_lines(self.get_metadata(name))

class DistroTests(TestCase):

    def testCollection(self):
        # empty path should produce no distributions
        ad = Environment([], platform=None, python=None)
        self.assertEqual(list(ad), [])
        self.assertEqual(ad['FooPkg'],[])
        ad.add(Distribution.from_filename("FooPkg-1.3_1.egg"))
        ad.add(Distribution.from_filename("FooPkg-1.4-py2.4-win32.egg"))
        ad.add(Distribution.from_filename("FooPkg-1.2-py2.4.egg"))

        # Name is in there now
        self.assertTrue(ad['FooPkg'])
        # But only 1 package
        self.assertEqual(list(ad), ['foopkg'])

        # Distributions sort by version
        self.assertEqual(
            [dist.version for dist in ad['FooPkg']], ['1.4','1.3-1','1.2']
        )
        # Removing a distribution leaves sequence alone
        ad.remove(ad['FooPkg'][1])
        self.assertEqual(
            [dist.version for dist in ad['FooPkg']], ['1.4','1.2']
        )
        # And inserting adds them in order
        ad.add(Distribution.from_filename("FooPkg-1.9.egg"))
        self.assertEqual(
            [dist.version for dist in ad['FooPkg']], ['1.9','1.4','1.2']
        )

        ws = WorkingSet([])
        foo12 = Distribution.from_filename("FooPkg-1.2-py2.4.egg")
        foo14 = Distribution.from_filename("FooPkg-1.4-py2.4-win32.egg")
        req, = parse_requirements("FooPkg>=1.3")

        # Nominal case: no distros on path, should yield all applicable
        self.assertEqual(ad.best_match(req,ws).version, '1.9')
        # If a matching distro is already installed, should return only that
        ws.add(foo14); self.assertEqual(ad.best_match(req,ws).version, '1.4')

        # If the first matching distro is unsuitable, it's a version conflict
        ws = WorkingSet([]); ws.add(foo12); ws.add(foo14)
        self.assertRaises(VersionConflict, ad.best_match, req, ws)

        # If more than one match on the path, the first one takes precedence
        ws = WorkingSet([]); ws.add(foo14); ws.add(foo12); ws.add(foo14);
        self.assertEqual(ad.best_match(req,ws).version, '1.4')

    def checkFooPkg(self,d):
        self.assertEqual(d.project_name, "FooPkg")
        self.assertEqual(d.key, "foopkg")
        self.assertEqual(d.version, "1.3-1")
        self.assertEqual(d.py_version, "2.4")
        self.assertEqual(d.platform, "win32")
        self.assertEqual(d.parsed_version, parse_version("1.3-1"))

    def testDistroBasics(self):
        d = Distribution(
            "/some/path",
            project_name="FooPkg",version="1.3-1",py_version="2.4",platform="win32"
        )
        self.checkFooPkg(d)

        d = Distribution("/some/path")
        self.assertEqual(d.py_version, sys.version[:3])
        self.assertEqual(d.platform, None)

    def testDistroParse(self):
        d = Distribution.from_filename("FooPkg-1.3_1-py2.4-win32.egg")
        self.checkFooPkg(d)
        d = Distribution.from_filename("FooPkg-1.3_1-py2.4-win32.egg-info")
        self.checkFooPkg(d)

    def testDistroMetadata(self):
        d = Distribution(
            "/some/path", project_name="FooPkg", py_version="2.4", platform="win32",
            metadata = Metadata(
                ('PKG-INFO',"Metadata-Version: 1.0\nVersion: 1.3-1\n")
            )
        )
        self.checkFooPkg(d)


    def distRequires(self, txt):
        return Distribution("/foo", metadata=Metadata(('depends.txt', txt)))

    def checkRequires(self, dist, txt, extras=()):
        self.assertEqual(
            list(dist.requires(extras)),
            list(parse_requirements(txt))
        )

    def testDistroDependsSimple(self):
        for v in "Twisted>=1.5", "Twisted>=1.5\nZConfig>=2.0":
            self.checkRequires(self.distRequires(v), v)


    def testResolve(self):
        ad = Environment([]); ws = WorkingSet([])
        # Resolving no requirements -> nothing to install
        self.assertEqual( list(ws.resolve([],ad)), [] )
        # Request something not in the collection -> DistributionNotFound
        self.assertRaises(
            DistributionNotFound, ws.resolve, parse_requirements("Foo"), ad
        )
        Foo = Distribution.from_filename(
            "/foo_dir/Foo-1.2.egg",
            metadata=Metadata(('depends.txt', "[bar]\nBaz>=2.0"))
        )
        ad.add(Foo); ad.add(Distribution.from_filename("Foo-0.9.egg"))

        # Request thing(s) that are available -> list to activate
        for i in range(3):
            targets = list(ws.resolve(parse_requirements("Foo"), ad))
            self.assertEqual(targets, [Foo])
            list(map(ws.add,targets))
        self.assertRaises(VersionConflict, ws.resolve,
            parse_requirements("Foo==0.9"), ad)
        ws = WorkingSet([]) # reset

        # Request an extra that causes an unresolved dependency for "Baz"
        self.assertRaises(
            DistributionNotFound, ws.resolve,parse_requirements("Foo[bar]"), ad
        )
        Baz = Distribution.from_filename(
            "/foo_dir/Baz-2.1.egg", metadata=Metadata(('depends.txt', "Foo"))
        )
        ad.add(Baz)

        # Activation list now includes resolved dependency
        self.assertEqual(
            list(ws.resolve(parse_requirements("Foo[bar]"), ad)), [Foo,Baz]
        )
        # Requests for conflicting versions produce VersionConflict
        self.assertRaises( VersionConflict,
            ws.resolve, parse_requirements("Foo==1.2\nFoo!=1.2"), ad
        )

    def testDistroDependsOptions(self):
        d = self.distRequires("""
            Twisted>=1.5
            [docgen]
            ZConfig>=2.0
            docutils>=0.3
            [fastcgi]
            fcgiapp>=0.1""")
        self.checkRequires(d,"Twisted>=1.5")
        self.checkRequires(
            d,"Twisted>=1.5 ZConfig>=2.0 docutils>=0.3".split(), ["docgen"]
        )
        self.checkRequires(
            d,"Twisted>=1.5 fcgiapp>=0.1".split(), ["fastcgi"]
        )
        self.checkRequires(
            d,"Twisted>=1.5 ZConfig>=2.0 docutils>=0.3 fcgiapp>=0.1".split(),
            ["docgen","fastcgi"]
        )
        self.checkRequires(
            d,"Twisted>=1.5 fcgiapp>=0.1 ZConfig>=2.0 docutils>=0.3".split(),
            ["fastcgi", "docgen"]
        )
        self.assertRaises(UnknownExtra, d.requires, ["foo"])


class EntryPointTests(TestCase):

    def assertfields(self, ep):
        self.assertEqual(ep.name,"foo")
        self.assertEqual(ep.module_name,"setuptools.tests.test_resources")
        self.assertEqual(ep.attrs, ("EntryPointTests",))
        self.assertEqual(ep.extras, ("x",))
        self.assertTrue(ep.load() is EntryPointTests)
        self.assertEqual(
            str(ep),
            "foo = setuptools.tests.test_resources:EntryPointTests [x]"
        )

    def setUp(self):
        self.dist = Distribution.from_filename(
            "FooPkg-1.2-py2.4.egg", metadata=Metadata(('requires.txt','[x]')))

    def testBasics(self):
        ep = EntryPoint(
            "foo", "setuptools.tests.test_resources", ["EntryPointTests"],
            ["x"], self.dist
        )
        self.assertfields(ep)

    def testParse(self):
        s = "foo = setuptools.tests.test_resources:EntryPointTests [x]"
        ep = EntryPoint.parse(s, self.dist)
        self.assertfields(ep)

        ep = EntryPoint.parse("bar baz=  spammity[PING]")
        self.assertEqual(ep.name,"bar baz")
        self.assertEqual(ep.module_name,"spammity")
        self.assertEqual(ep.attrs, ())
        self.assertEqual(ep.extras, ("ping",))

        ep = EntryPoint.parse(" fizzly =  wocka:foo")
        self.assertEqual(ep.name,"fizzly")
        self.assertEqual(ep.module_name,"wocka")
        self.assertEqual(ep.attrs, ("foo",))
        self.assertEqual(ep.extras, ())

    def testRejects(self):
        for ep in [
            "foo", "x=1=2", "x=a:b:c", "q=x/na", "fez=pish:tush-z", "x=f[a]>2",
        ]:
            try: EntryPoint.parse(ep)
            except ValueError: pass
            else: raise AssertionError("Should've been bad", ep)

    def checkSubMap(self, m):
        self.assertEqual(len(m), len(self.submap_expect))
        for key, ep in iteritems(self.submap_expect):
            self.assertEqual(repr(m.get(key)), repr(ep))

    submap_expect = dict(
        feature1=EntryPoint('feature1', 'somemodule', ['somefunction']),
        feature2=EntryPoint('feature2', 'another.module', ['SomeClass'], ['extra1','extra2']),
        feature3=EntryPoint('feature3', 'this.module', extras=['something'])
    )
    submap_str = """
            # define features for blah blah
            feature1 = somemodule:somefunction
            feature2 = another.module:SomeClass [extra1,extra2]
            feature3 = this.module [something]
    """

    def testParseList(self):
        self.checkSubMap(EntryPoint.parse_group("xyz", self.submap_str))
        self.assertRaises(ValueError, EntryPoint.parse_group, "x a", "foo=bar")
        self.assertRaises(ValueError, EntryPoint.parse_group, "x",
            ["foo=baz", "foo=bar"])

    def testParseMap(self):
        m = EntryPoint.parse_map({'xyz':self.submap_str})
        self.checkSubMap(m['xyz'])
        self.assertEqual(list(m.keys()),['xyz'])
        m = EntryPoint.parse_map("[xyz]\n"+self.submap_str)
        self.checkSubMap(m['xyz'])
        self.assertEqual(list(m.keys()),['xyz'])
        self.assertRaises(ValueError, EntryPoint.parse_map, ["[xyz]", "[xyz]"])
        self.assertRaises(ValueError, EntryPoint.parse_map, self.submap_str)

class RequirementsTests(TestCase):

    def testBasics(self):
        r = Requirement.parse("Twisted>=1.2")
        self.assertEqual(str(r),"Twisted>=1.2")
        self.assertEqual(repr(r),"Requirement.parse('Twisted>=1.2')")
        self.assertEqual(r, Requirement("Twisted", [('>=','1.2')], ()))
        self.assertEqual(r, Requirement("twisTed", [('>=','1.2')], ()))
        self.assertNotEqual(r, Requirement("Twisted", [('>=','2.0')], ()))
        self.assertNotEqual(r, Requirement("Zope", [('>=','1.2')], ()))
        self.assertNotEqual(r, Requirement("Zope", [('>=','3.0')], ()))
        self.assertNotEqual(r, Requirement.parse("Twisted[extras]>=1.2"))

    def testOrdering(self):
        r1 = Requirement("Twisted", [('==','1.2c1'),('>=','1.2')], ())
        r2 = Requirement("Twisted", [('>=','1.2'),('==','1.2c1')], ())
        self.assertEqual(r1,r2)
        self.assertEqual(str(r1),str(r2))
        self.assertEqual(str(r2),"Twisted==1.2c1,>=1.2")

    def testBasicContains(self):
        r = Requirement("Twisted", [('>=','1.2')], ())
        foo_dist = Distribution.from_filename("FooPkg-1.3_1.egg")
        twist11  = Distribution.from_filename("Twisted-1.1.egg")
        twist12  = Distribution.from_filename("Twisted-1.2.egg")
        self.assertTrue(parse_version('1.2') in r)
        self.assertTrue(parse_version('1.1') not in r)
        self.assertTrue('1.2' in r)
        self.assertTrue('1.1' not in r)
        self.assertTrue(foo_dist not in r)
        self.assertTrue(twist11 not in r)
        self.assertTrue(twist12 in r)

    def testAdvancedContains(self):
        r, = parse_requirements("Foo>=1.2,<=1.3,==1.9,>2.0,!=2.5,<3.0,==4.5")
        for v in ('1.2','1.2.2','1.3','1.9','2.0.1','2.3','2.6','3.0c1','4.5'):
            self.assertTrue(v in r, (v,r))
        for v in ('1.2c1','1.3.1','1.5','1.9.1','2.0','2.5','3.0','4.0'):
            self.assertTrue(v not in r, (v,r))


    def testOptionsAndHashing(self):
        r1 = Requirement.parse("Twisted[foo,bar]>=1.2")
        r2 = Requirement.parse("Twisted[bar,FOO]>=1.2")
        r3 = Requirement.parse("Twisted[BAR,FOO]>=1.2.0")
        self.assertEqual(r1,r2)
        self.assertEqual(r1,r3)
        self.assertEqual(r1.extras, ("foo","bar"))
        self.assertEqual(r2.extras, ("bar","foo"))  # extras are normalized
        self.assertEqual(hash(r1), hash(r2))
        self.assertEqual(
            hash(r1), hash(("twisted", ((">=",parse_version("1.2")),),
                            frozenset(["foo","bar"])))
        )

    def testVersionEquality(self):
        r1 = Requirement.parse("foo==0.3a2")
        r2 = Requirement.parse("foo!=0.3a4")
        d = Distribution.from_filename

        self.assertTrue(d("foo-0.3a4.egg") not in r1)
        self.assertTrue(d("foo-0.3a1.egg") not in r1)
        self.assertTrue(d("foo-0.3a4.egg") not in r2)

        self.assertTrue(d("foo-0.3a2.egg") in r1)
        self.assertTrue(d("foo-0.3a2.egg") in r2)
        self.assertTrue(d("foo-0.3a3.egg") in r2)
        self.assertTrue(d("foo-0.3a5.egg") in r2)

    def testSetuptoolsProjectName(self):
        """
        The setuptools project should implement the setuptools package.
        """

        self.assertEqual(
            Requirement.parse('setuptools').project_name, 'setuptools')
        # setuptools 0.7 and higher means setuptools.
        self.assertEqual(
            Requirement.parse('setuptools == 0.7').project_name, 'setuptools')
        self.assertEqual(
            Requirement.parse('setuptools == 0.7a1').project_name, 'setuptools')
        self.assertEqual(
            Requirement.parse('setuptools >= 0.7').project_name, 'setuptools')











class ParseTests(TestCase):

    def testEmptyParse(self):
        self.assertEqual(list(parse_requirements('')), [])

    def testYielding(self):
        for inp,out in [
            ([], []), ('x',['x']), ([[]],[]), (' x\n y', ['x','y']),
            (['x\n\n','y'], ['x','y']),
        ]:
            self.assertEqual(list(pkg_resources.yield_lines(inp)),out)

    def testSplitting(self):
        self.assertEqual(
            list(
                pkg_resources.split_sections("""
                    x
                    [Y]
                    z

                    a
                    [b ]
                    # foo
                    c
                    [ d]
                    [q]
                    v
                    """
                )
            ),
            [(None,["x"]), ("Y",["z","a"]), ("b",["c"]), ("d",[]), ("q",["v"])]
        )
        self.assertRaises(ValueError,list,pkg_resources.split_sections("[foo"))

    def testSafeName(self):
        self.assertEqual(safe_name("adns-python"), "adns-python")
        self.assertEqual(safe_name("WSGI Utils"),  "WSGI-Utils")
        self.assertEqual(safe_name("WSGI  Utils"), "WSGI-Utils")
        self.assertEqual(safe_name("Money$$$Maker"), "Money-Maker")
        self.assertNotEqual(safe_name("peak.web"), "peak-web")

    def testSafeVersion(self):
        self.assertEqual(safe_version("1.2-1"), "1.2-1")
        self.assertEqual(safe_version("1.2 alpha"),  "1.2.alpha")
        self.assertEqual(safe_version("2.3.4 20050521"), "2.3.4.20050521")
        self.assertEqual(safe_version("Money$$$Maker"), "Money-Maker")
        self.assertEqual(safe_version("peak.web"), "peak.web")

    def testSimpleRequirements(self):
        self.assertEqual(
            list(parse_requirements('Twis-Ted>=1.2-1')),
            [Requirement('Twis-Ted',[('>=','1.2-1')], ())]
        )
        self.assertEqual(
            list(parse_requirements('Twisted >=1.2, \ # more\n<2.0')),
            [Requirement('Twisted',[('>=','1.2'),('<','2.0')], ())]
        )
        self.assertEqual(
            Requirement.parse("FooBar==1.99a3"),
            Requirement("FooBar", [('==','1.99a3')], ())
        )
        self.assertRaises(ValueError,Requirement.parse,">=2.3")
        self.assertRaises(ValueError,Requirement.parse,"x\\")
        self.assertRaises(ValueError,Requirement.parse,"x==2 q")
        self.assertRaises(ValueError,Requirement.parse,"X==1\nY==2")
        self.assertRaises(ValueError,Requirement.parse,"#")

    def testVersionEquality(self):
        def c(s1,s2):
            p1, p2 = parse_version(s1),parse_version(s2)
            self.assertEqual(p1,p2, (s1,s2,p1,p2))

        c('1.2-rc1', '1.2rc1')
        c('0.4', '0.4.0')
        c('0.4.0.0', '0.4.0')
        c('0.4.0-0', '0.4-0')
        c('0pl1', '0.0pl1')
        c('0pre1', '0.0c1')
        c('0.0.0preview1', '0c1')
        c('0.0c1', '0-rc1')
        c('1.2a1', '1.2.a.1'); c('1.2...a', '1.2a')

    def testVersionOrdering(self):
        def c(s1,s2):
            p1, p2 = parse_version(s1),parse_version(s2)
            self.assertTrue(p1<p2, (s1,s2,p1,p2))

        c('2.1','2.1.1')
        c('2a1','2b0')
        c('2a1','2.1')
        c('2.3a1', '2.3')
        c('2.1-1', '2.1-2')
        c('2.1-1', '2.1.1')
        c('2.1', '2.1pl4')
        c('2.1a0-20040501', '2.1')
        c('1.1', '02.1')
        c('A56','B27')
        c('3.2', '3.2.pl0')
        c('3.2-1', '3.2pl1')
        c('3.2pl1', '3.2pl1-1')
        c('0.4', '4.0')
        c('0.0.4', '0.4.0')
        c('0pl1', '0.4pl1')
        c('2.1.0-rc1','2.1.0')
        c('2.1dev','2.1a0')

        torture ="""
        0.80.1-3 0.80.1-2 0.80.1-1 0.79.9999+0.80.0pre4-1
        0.79.9999+0.80.0pre2-3 0.79.9999+0.80.0pre2-2
        0.77.2-1 0.77.1-1 0.77.0-1
        """.split()

        for p,v1 in enumerate(torture):
            for v2 in torture[p+1:]:
                c(v2,v1)








class ScriptHeaderTests(TestCase):
    non_ascii_exe = '/Users/José/bin/python'

    def test_get_script_header(self):
        if not sys.platform.startswith('java') or not is_sh(sys.executable):
            # This test is for non-Jython platforms
            self.assertEqual(get_script_header('#!/usr/local/bin/python'),
                             '#!%s\n' % os.path.normpath(sys.executable))
            self.assertEqual(get_script_header('#!/usr/bin/python -x'),
                             '#!%s  -x\n' % os.path.normpath(sys.executable))
            self.assertEqual(get_script_header('#!/usr/bin/python',
                                               executable=self.non_ascii_exe),
                             '#!%s -x\n' % self.non_ascii_exe)

    def test_get_script_header_jython_workaround(self):
        # This test doesn't work with Python 3 in some locales
        if (sys.version_info >= (3,) and os.environ.get("LC_CTYPE")
            in (None, "C", "POSIX")):
            return

        class java:
            class lang:
                class System:
                    @staticmethod
                    def getProperty(property):
                        return ""
        sys.modules["java"] = java

        platform = sys.platform
        sys.platform = 'java1.5.0_13'
        stdout, stderr = sys.stdout, sys.stderr
        try:
            # A mock sys.executable that uses a shebang line (this file)
            exe = os.path.normpath(os.path.splitext(__file__)[0] + '.py')
            self.assertEqual(
                get_script_header('#!/usr/local/bin/python', executable=exe),
                '#!/usr/bin/env %s\n' % exe)

            # Ensure we generate what is basically a broken shebang line
            # when there's options, with a warning emitted
            sys.stdout = sys.stderr = StringIO()
            self.assertEqual(get_script_header('#!/usr/bin/python -x',
                                               executable=exe),
                             '#!%s  -x\n' % exe)
            self.assertTrue('Unable to adapt shebang line' in sys.stdout.getvalue())
            sys.stdout = sys.stderr = StringIO()
            self.assertEqual(get_script_header('#!/usr/bin/python',
                                               executable=self.non_ascii_exe),
                             '#!%s -x\n' % self.non_ascii_exe)
            self.assertTrue('Unable to adapt shebang line' in sys.stdout.getvalue())
        finally:
            del sys.modules["java"]
            sys.platform = platform
            sys.stdout, sys.stderr = stdout, stderr




class NamespaceTests(TestCase):

    def setUp(self):
        self._ns_pkgs = pkg_resources._namespace_packages.copy()
        self._tmpdir = tempfile.mkdtemp(prefix="tests-setuptools-")
        os.makedirs(os.path.join(self._tmpdir, "site-pkgs"))
        self._prev_sys_path = sys.path[:]
        sys.path.append(os.path.join(self._tmpdir, "site-pkgs"))

    def tearDown(self):
        shutil.rmtree(self._tmpdir)
        pkg_resources._namespace_packages = self._ns_pkgs.copy()
        sys.path = self._prev_sys_path[:]

    def _assertIn(self, member, container):
        """ assertIn and assertTrue does not exist in Python2.3"""
        if member not in container:
            standardMsg = '%s not found in %s' % (safe_repr(member),
                                                  safe_repr(container))
            self.fail(self._formatMessage(msg, standardMsg))

    def test_two_levels_deep(self):
        """
        Test nested namespace packages
        Create namespace packages in the following tree :
            site-packages-1/pkg1/pkg2
            site-packages-2/pkg1/pkg2
        Check both are in the _namespace_packages dict and that their __path__
        is correct
        """
        sys.path.append(os.path.join(self._tmpdir, "site-pkgs2"))
        os.makedirs(os.path.join(self._tmpdir, "site-pkgs", "pkg1", "pkg2"))
        os.makedirs(os.path.join(self._tmpdir, "site-pkgs2", "pkg1", "pkg2"))
        ns_str = "__import__('pkg_resources').declare_namespace(__name__)\n"
        for site in ["site-pkgs", "site-pkgs2"]:
            pkg1_init = open(os.path.join(self._tmpdir, site,
                             "pkg1", "__init__.py"), "w")
            pkg1_init.write(ns_str)
            pkg1_init.close()
            pkg2_init = open(os.path.join(self._tmpdir, site,
                             "pkg1", "pkg2", "__init__.py"), "w")
            pkg2_init.write(ns_str)
            pkg2_init.close()
        import pkg1
        self._assertIn("pkg1", pkg_resources._namespace_packages.keys())
        try:
            import pkg1.pkg2
        except ImportError:
            self.fail("Setuptools tried to import the parent namespace package")
        # check the _namespace_packages dict
        self._assertIn("pkg1.pkg2", pkg_resources._namespace_packages.keys())
        self.assertEqual(pkg_resources._namespace_packages["pkg1"], ["pkg1.pkg2"])
        # check the __path__ attribute contains both paths
        self.assertEqual(pkg1.pkg2.__path__, [
                os.path.join(self._tmpdir, "site-pkgs", "pkg1", "pkg2"),
                os.path.join(self._tmpdir, "site-pkgs2", "pkg1", "pkg2") ])

