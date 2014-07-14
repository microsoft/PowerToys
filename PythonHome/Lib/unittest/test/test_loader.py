import sys
import types


import unittest


class Test_TestLoader(unittest.TestCase):

    ### Tests for TestLoader.loadTestsFromTestCase
    ################################################################

    # "Return a suite of all tests cases contained in the TestCase-derived
    # class testCaseClass"
    def test_loadTestsFromTestCase(self):
        class Foo(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass
            def foo_bar(self): pass

        tests = unittest.TestSuite([Foo('test_1'), Foo('test_2')])

        loader = unittest.TestLoader()
        self.assertEqual(loader.loadTestsFromTestCase(Foo), tests)

    # "Return a suite of all tests cases contained in the TestCase-derived
    # class testCaseClass"
    #
    # Make sure it does the right thing even if no tests were found
    def test_loadTestsFromTestCase__no_matches(self):
        class Foo(unittest.TestCase):
            def foo_bar(self): pass

        empty_suite = unittest.TestSuite()

        loader = unittest.TestLoader()
        self.assertEqual(loader.loadTestsFromTestCase(Foo), empty_suite)

    # "Return a suite of all tests cases contained in the TestCase-derived
    # class testCaseClass"
    #
    # What happens if loadTestsFromTestCase() is given an object
    # that isn't a subclass of TestCase? Specifically, what happens
    # if testCaseClass is a subclass of TestSuite?
    #
    # This is checked for specifically in the code, so we better add a
    # test for it.
    def test_loadTestsFromTestCase__TestSuite_subclass(self):
        class NotATestCase(unittest.TestSuite):
            pass

        loader = unittest.TestLoader()
        try:
            loader.loadTestsFromTestCase(NotATestCase)
        except TypeError:
            pass
        else:
            self.fail('Should raise TypeError')

    # "Return a suite of all tests cases contained in the TestCase-derived
    # class testCaseClass"
    #
    # Make sure loadTestsFromTestCase() picks up the default test method
    # name (as specified by TestCase), even though the method name does
    # not match the default TestLoader.testMethodPrefix string
    def test_loadTestsFromTestCase__default_method_name(self):
        class Foo(unittest.TestCase):
            def runTest(self):
                pass

        loader = unittest.TestLoader()
        # This has to be false for the test to succeed
        self.assertFalse('runTest'.startswith(loader.testMethodPrefix))

        suite = loader.loadTestsFromTestCase(Foo)
        self.assertIsInstance(suite, loader.suiteClass)
        self.assertEqual(list(suite), [Foo('runTest')])

    ################################################################
    ### /Tests for TestLoader.loadTestsFromTestCase

    ### Tests for TestLoader.loadTestsFromModule
    ################################################################

    # "This method searches `module` for classes derived from TestCase"
    def test_loadTestsFromModule__TestCase_subclass(self):
        m = types.ModuleType('m')
        class MyTestCase(unittest.TestCase):
            def test(self):
                pass
        m.testcase_1 = MyTestCase

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromModule(m)
        self.assertIsInstance(suite, loader.suiteClass)

        expected = [loader.suiteClass([MyTestCase('test')])]
        self.assertEqual(list(suite), expected)

    # "This method searches `module` for classes derived from TestCase"
    #
    # What happens if no tests are found (no TestCase instances)?
    def test_loadTestsFromModule__no_TestCase_instances(self):
        m = types.ModuleType('m')

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromModule(m)
        self.assertIsInstance(suite, loader.suiteClass)
        self.assertEqual(list(suite), [])

    # "This method searches `module` for classes derived from TestCase"
    #
    # What happens if no tests are found (TestCases instances, but no tests)?
    def test_loadTestsFromModule__no_TestCase_tests(self):
        m = types.ModuleType('m')
        class MyTestCase(unittest.TestCase):
            pass
        m.testcase_1 = MyTestCase

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromModule(m)
        self.assertIsInstance(suite, loader.suiteClass)

        self.assertEqual(list(suite), [loader.suiteClass()])

    # "This method searches `module` for classes derived from TestCase"s
    #
    # What happens if loadTestsFromModule() is given something other
    # than a module?
    #
    # XXX Currently, it succeeds anyway. This flexibility
    # should either be documented or loadTestsFromModule() should
    # raise a TypeError
    #
    # XXX Certain people are using this behaviour. We'll add a test for it
    def test_loadTestsFromModule__not_a_module(self):
        class MyTestCase(unittest.TestCase):
            def test(self):
                pass

        class NotAModule(object):
            test_2 = MyTestCase

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromModule(NotAModule)

        reference = [unittest.TestSuite([MyTestCase('test')])]
        self.assertEqual(list(suite), reference)


    # Check that loadTestsFromModule honors (or not) a module
    # with a load_tests function.
    def test_loadTestsFromModule__load_tests(self):
        m = types.ModuleType('m')
        class MyTestCase(unittest.TestCase):
            def test(self):
                pass
        m.testcase_1 = MyTestCase

        load_tests_args = []
        def load_tests(loader, tests, pattern):
            self.assertIsInstance(tests, unittest.TestSuite)
            load_tests_args.extend((loader, tests, pattern))
            return tests
        m.load_tests = load_tests

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromModule(m)
        self.assertIsInstance(suite, unittest.TestSuite)
        self.assertEqual(load_tests_args, [loader, suite, None])

        load_tests_args = []
        suite = loader.loadTestsFromModule(m, use_load_tests=False)
        self.assertEqual(load_tests_args, [])

    def test_loadTestsFromModule__faulty_load_tests(self):
        m = types.ModuleType('m')

        def load_tests(loader, tests, pattern):
            raise TypeError('some failure')
        m.load_tests = load_tests

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromModule(m)
        self.assertIsInstance(suite, unittest.TestSuite)
        self.assertEqual(suite.countTestCases(), 1)
        test = list(suite)[0]

        self.assertRaisesRegexp(TypeError, "some failure", test.m)

    ################################################################
    ### /Tests for TestLoader.loadTestsFromModule()

    ### Tests for TestLoader.loadTestsFromName()
    ################################################################

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    #
    # Is ValueError raised in response to an empty name?
    def test_loadTestsFromName__empty_name(self):
        loader = unittest.TestLoader()

        try:
            loader.loadTestsFromName('')
        except ValueError, e:
            self.assertEqual(str(e), "Empty module name")
        else:
            self.fail("TestLoader.loadTestsFromName failed to raise ValueError")

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    #
    # What happens when the name contains invalid characters?
    def test_loadTestsFromName__malformed_name(self):
        loader = unittest.TestLoader()

        # XXX Should this raise ValueError or ImportError?
        try:
            loader.loadTestsFromName('abc () //')
        except ValueError:
            pass
        except ImportError:
            pass
        else:
            self.fail("TestLoader.loadTestsFromName failed to raise ValueError")

    # "The specifier name is a ``dotted name'' that may resolve ... to a
    # module"
    #
    # What happens when a module by that name can't be found?
    def test_loadTestsFromName__unknown_module_name(self):
        loader = unittest.TestLoader()

        try:
            loader.loadTestsFromName('sdasfasfasdf')
        except ImportError, e:
            self.assertEqual(str(e), "No module named sdasfasfasdf")
        else:
            self.fail("TestLoader.loadTestsFromName failed to raise ImportError")

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    #
    # What happens when the module is found, but the attribute can't?
    def test_loadTestsFromName__unknown_attr_name(self):
        loader = unittest.TestLoader()

        try:
            loader.loadTestsFromName('unittest.sdasfasfasdf')
        except AttributeError, e:
            self.assertEqual(str(e), "'module' object has no attribute 'sdasfasfasdf'")
        else:
            self.fail("TestLoader.loadTestsFromName failed to raise AttributeError")

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    #
    # What happens when we provide the module, but the attribute can't be
    # found?
    def test_loadTestsFromName__relative_unknown_name(self):
        loader = unittest.TestLoader()

        try:
            loader.loadTestsFromName('sdasfasfasdf', unittest)
        except AttributeError, e:
            self.assertEqual(str(e), "'module' object has no attribute 'sdasfasfasdf'")
        else:
            self.fail("TestLoader.loadTestsFromName failed to raise AttributeError")

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    # ...
    # "The method optionally resolves name relative to the given module"
    #
    # Does loadTestsFromName raise ValueError when passed an empty
    # name relative to a provided module?
    #
    # XXX Should probably raise a ValueError instead of an AttributeError
    def test_loadTestsFromName__relative_empty_name(self):
        loader = unittest.TestLoader()

        try:
            loader.loadTestsFromName('', unittest)
        except AttributeError:
            pass
        else:
            self.fail("Failed to raise AttributeError")

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    # ...
    # "The method optionally resolves name relative to the given module"
    #
    # What happens when an impossible name is given, relative to the provided
    # `module`?
    def test_loadTestsFromName__relative_malformed_name(self):
        loader = unittest.TestLoader()

        # XXX Should this raise AttributeError or ValueError?
        try:
            loader.loadTestsFromName('abc () //', unittest)
        except ValueError:
            pass
        except AttributeError:
            pass
        else:
            self.fail("TestLoader.loadTestsFromName failed to raise ValueError")

    # "The method optionally resolves name relative to the given module"
    #
    # Does loadTestsFromName raise TypeError when the `module` argument
    # isn't a module object?
    #
    # XXX Accepts the not-a-module object, ignoring the object's type
    # This should raise an exception or the method name should be changed
    #
    # XXX Some people are relying on this, so keep it for now
    def test_loadTestsFromName__relative_not_a_module(self):
        class MyTestCase(unittest.TestCase):
            def test(self):
                pass

        class NotAModule(object):
            test_2 = MyTestCase

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromName('test_2', NotAModule)

        reference = [MyTestCase('test')]
        self.assertEqual(list(suite), reference)

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    #
    # Does it raise an exception if the name resolves to an invalid
    # object?
    def test_loadTestsFromName__relative_bad_object(self):
        m = types.ModuleType('m')
        m.testcase_1 = object()

        loader = unittest.TestLoader()
        try:
            loader.loadTestsFromName('testcase_1', m)
        except TypeError:
            pass
        else:
            self.fail("Should have raised TypeError")

    # "The specifier name is a ``dotted name'' that may
    # resolve either to ... a test case class"
    def test_loadTestsFromName__relative_TestCase_subclass(self):
        m = types.ModuleType('m')
        class MyTestCase(unittest.TestCase):
            def test(self):
                pass
        m.testcase_1 = MyTestCase

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromName('testcase_1', m)
        self.assertIsInstance(suite, loader.suiteClass)
        self.assertEqual(list(suite), [MyTestCase('test')])

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    def test_loadTestsFromName__relative_TestSuite(self):
        m = types.ModuleType('m')
        class MyTestCase(unittest.TestCase):
            def test(self):
                pass
        m.testsuite = unittest.TestSuite([MyTestCase('test')])

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromName('testsuite', m)
        self.assertIsInstance(suite, loader.suiteClass)

        self.assertEqual(list(suite), [MyTestCase('test')])

    # "The specifier name is a ``dotted name'' that may resolve ... to
    # ... a test method within a test case class"
    def test_loadTestsFromName__relative_testmethod(self):
        m = types.ModuleType('m')
        class MyTestCase(unittest.TestCase):
            def test(self):
                pass
        m.testcase_1 = MyTestCase

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromName('testcase_1.test', m)
        self.assertIsInstance(suite, loader.suiteClass)

        self.assertEqual(list(suite), [MyTestCase('test')])

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    #
    # Does loadTestsFromName() raise the proper exception when trying to
    # resolve "a test method within a test case class" that doesn't exist
    # for the given name (relative to a provided module)?
    def test_loadTestsFromName__relative_invalid_testmethod(self):
        m = types.ModuleType('m')
        class MyTestCase(unittest.TestCase):
            def test(self):
                pass
        m.testcase_1 = MyTestCase

        loader = unittest.TestLoader()
        try:
            loader.loadTestsFromName('testcase_1.testfoo', m)
        except AttributeError, e:
            self.assertEqual(str(e), "type object 'MyTestCase' has no attribute 'testfoo'")
        else:
            self.fail("Failed to raise AttributeError")

    # "The specifier name is a ``dotted name'' that may resolve ... to
    # ... a callable object which returns a ... TestSuite instance"
    def test_loadTestsFromName__callable__TestSuite(self):
        m = types.ModuleType('m')
        testcase_1 = unittest.FunctionTestCase(lambda: None)
        testcase_2 = unittest.FunctionTestCase(lambda: None)
        def return_TestSuite():
            return unittest.TestSuite([testcase_1, testcase_2])
        m.return_TestSuite = return_TestSuite

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromName('return_TestSuite', m)
        self.assertIsInstance(suite, loader.suiteClass)
        self.assertEqual(list(suite), [testcase_1, testcase_2])

    # "The specifier name is a ``dotted name'' that may resolve ... to
    # ... a callable object which returns a TestCase ... instance"
    def test_loadTestsFromName__callable__TestCase_instance(self):
        m = types.ModuleType('m')
        testcase_1 = unittest.FunctionTestCase(lambda: None)
        def return_TestCase():
            return testcase_1
        m.return_TestCase = return_TestCase

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromName('return_TestCase', m)
        self.assertIsInstance(suite, loader.suiteClass)
        self.assertEqual(list(suite), [testcase_1])

    # "The specifier name is a ``dotted name'' that may resolve ... to
    # ... a callable object which returns a TestCase ... instance"
    #*****************************************************************
    #Override the suiteClass attribute to ensure that the suiteClass
    #attribute is used
    def test_loadTestsFromName__callable__TestCase_instance_ProperSuiteClass(self):
        class SubTestSuite(unittest.TestSuite):
            pass
        m = types.ModuleType('m')
        testcase_1 = unittest.FunctionTestCase(lambda: None)
        def return_TestCase():
            return testcase_1
        m.return_TestCase = return_TestCase

        loader = unittest.TestLoader()
        loader.suiteClass = SubTestSuite
        suite = loader.loadTestsFromName('return_TestCase', m)
        self.assertIsInstance(suite, loader.suiteClass)
        self.assertEqual(list(suite), [testcase_1])

    # "The specifier name is a ``dotted name'' that may resolve ... to
    # ... a test method within a test case class"
    #*****************************************************************
    #Override the suiteClass attribute to ensure that the suiteClass
    #attribute is used
    def test_loadTestsFromName__relative_testmethod_ProperSuiteClass(self):
        class SubTestSuite(unittest.TestSuite):
            pass
        m = types.ModuleType('m')
        class MyTestCase(unittest.TestCase):
            def test(self):
                pass
        m.testcase_1 = MyTestCase

        loader = unittest.TestLoader()
        loader.suiteClass=SubTestSuite
        suite = loader.loadTestsFromName('testcase_1.test', m)
        self.assertIsInstance(suite, loader.suiteClass)

        self.assertEqual(list(suite), [MyTestCase('test')])

    # "The specifier name is a ``dotted name'' that may resolve ... to
    # ... a callable object which returns a TestCase or TestSuite instance"
    #
    # What happens if the callable returns something else?
    def test_loadTestsFromName__callable__wrong_type(self):
        m = types.ModuleType('m')
        def return_wrong():
            return 6
        m.return_wrong = return_wrong

        loader = unittest.TestLoader()
        try:
            loader.loadTestsFromName('return_wrong', m)
        except TypeError:
            pass
        else:
            self.fail("TestLoader.loadTestsFromName failed to raise TypeError")

    # "The specifier can refer to modules and packages which have not been
    # imported; they will be imported as a side-effect"
    def test_loadTestsFromName__module_not_loaded(self):
        # We're going to try to load this module as a side-effect, so it
        # better not be loaded before we try.
        #
        module_name = 'unittest.test.dummy'
        sys.modules.pop(module_name, None)

        loader = unittest.TestLoader()
        try:
            suite = loader.loadTestsFromName(module_name)

            self.assertIsInstance(suite, loader.suiteClass)
            self.assertEqual(list(suite), [])

            # module should now be loaded, thanks to loadTestsFromName()
            self.assertIn(module_name, sys.modules)
        finally:
            if module_name in sys.modules:
                del sys.modules[module_name]

    ################################################################
    ### Tests for TestLoader.loadTestsFromName()

    ### Tests for TestLoader.loadTestsFromNames()
    ################################################################

    # "Similar to loadTestsFromName(), but takes a sequence of names rather
    # than a single name."
    #
    # What happens if that sequence of names is empty?
    def test_loadTestsFromNames__empty_name_list(self):
        loader = unittest.TestLoader()

        suite = loader.loadTestsFromNames([])
        self.assertIsInstance(suite, loader.suiteClass)
        self.assertEqual(list(suite), [])

    # "Similar to loadTestsFromName(), but takes a sequence of names rather
    # than a single name."
    # ...
    # "The method optionally resolves name relative to the given module"
    #
    # What happens if that sequence of names is empty?
    #
    # XXX Should this raise a ValueError or just return an empty TestSuite?
    def test_loadTestsFromNames__relative_empty_name_list(self):
        loader = unittest.TestLoader()

        suite = loader.loadTestsFromNames([], unittest)
        self.assertIsInstance(suite, loader.suiteClass)
        self.assertEqual(list(suite), [])

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    #
    # Is ValueError raised in response to an empty name?
    def test_loadTestsFromNames__empty_name(self):
        loader = unittest.TestLoader()

        try:
            loader.loadTestsFromNames([''])
        except ValueError, e:
            self.assertEqual(str(e), "Empty module name")
        else:
            self.fail("TestLoader.loadTestsFromNames failed to raise ValueError")

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    #
    # What happens when presented with an impossible module name?
    def test_loadTestsFromNames__malformed_name(self):
        loader = unittest.TestLoader()

        # XXX Should this raise ValueError or ImportError?
        try:
            loader.loadTestsFromNames(['abc () //'])
        except ValueError:
            pass
        except ImportError:
            pass
        else:
            self.fail("TestLoader.loadTestsFromNames failed to raise ValueError")

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    #
    # What happens when no module can be found for the given name?
    def test_loadTestsFromNames__unknown_module_name(self):
        loader = unittest.TestLoader()

        try:
            loader.loadTestsFromNames(['sdasfasfasdf'])
        except ImportError, e:
            self.assertEqual(str(e), "No module named sdasfasfasdf")
        else:
            self.fail("TestLoader.loadTestsFromNames failed to raise ImportError")

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    #
    # What happens when the module can be found, but not the attribute?
    def test_loadTestsFromNames__unknown_attr_name(self):
        loader = unittest.TestLoader()

        try:
            loader.loadTestsFromNames(['unittest.sdasfasfasdf', 'unittest'])
        except AttributeError, e:
            self.assertEqual(str(e), "'module' object has no attribute 'sdasfasfasdf'")
        else:
            self.fail("TestLoader.loadTestsFromNames failed to raise AttributeError")

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    # ...
    # "The method optionally resolves name relative to the given module"
    #
    # What happens when given an unknown attribute on a specified `module`
    # argument?
    def test_loadTestsFromNames__unknown_name_relative_1(self):
        loader = unittest.TestLoader()

        try:
            loader.loadTestsFromNames(['sdasfasfasdf'], unittest)
        except AttributeError, e:
            self.assertEqual(str(e), "'module' object has no attribute 'sdasfasfasdf'")
        else:
            self.fail("TestLoader.loadTestsFromName failed to raise AttributeError")

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    # ...
    # "The method optionally resolves name relative to the given module"
    #
    # Do unknown attributes (relative to a provided module) still raise an
    # exception even in the presence of valid attribute names?
    def test_loadTestsFromNames__unknown_name_relative_2(self):
        loader = unittest.TestLoader()

        try:
            loader.loadTestsFromNames(['TestCase', 'sdasfasfasdf'], unittest)
        except AttributeError, e:
            self.assertEqual(str(e), "'module' object has no attribute 'sdasfasfasdf'")
        else:
            self.fail("TestLoader.loadTestsFromName failed to raise AttributeError")

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    # ...
    # "The method optionally resolves name relative to the given module"
    #
    # What happens when faced with the empty string?
    #
    # XXX This currently raises AttributeError, though ValueError is probably
    # more appropriate
    def test_loadTestsFromNames__relative_empty_name(self):
        loader = unittest.TestLoader()

        try:
            loader.loadTestsFromNames([''], unittest)
        except AttributeError:
            pass
        else:
            self.fail("Failed to raise ValueError")

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    # ...
    # "The method optionally resolves name relative to the given module"
    #
    # What happens when presented with an impossible attribute name?
    def test_loadTestsFromNames__relative_malformed_name(self):
        loader = unittest.TestLoader()

        # XXX Should this raise AttributeError or ValueError?
        try:
            loader.loadTestsFromNames(['abc () //'], unittest)
        except AttributeError:
            pass
        except ValueError:
            pass
        else:
            self.fail("TestLoader.loadTestsFromNames failed to raise ValueError")

    # "The method optionally resolves name relative to the given module"
    #
    # Does loadTestsFromNames() make sure the provided `module` is in fact
    # a module?
    #
    # XXX This validation is currently not done. This flexibility should
    # either be documented or a TypeError should be raised.
    def test_loadTestsFromNames__relative_not_a_module(self):
        class MyTestCase(unittest.TestCase):
            def test(self):
                pass

        class NotAModule(object):
            test_2 = MyTestCase

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromNames(['test_2'], NotAModule)

        reference = [unittest.TestSuite([MyTestCase('test')])]
        self.assertEqual(list(suite), reference)

    # "The specifier name is a ``dotted name'' that may resolve either to
    # a module, a test case class, a TestSuite instance, a test method
    # within a test case class, or a callable object which returns a
    # TestCase or TestSuite instance."
    #
    # Does it raise an exception if the name resolves to an invalid
    # object?
    def test_loadTestsFromNames__relative_bad_object(self):
        m = types.ModuleType('m')
        m.testcase_1 = object()

        loader = unittest.TestLoader()
        try:
            loader.loadTestsFromNames(['testcase_1'], m)
        except TypeError:
            pass
        else:
            self.fail("Should have raised TypeError")

    # "The specifier name is a ``dotted name'' that may resolve ... to
    # ... a test case class"
    def test_loadTestsFromNames__relative_TestCase_subclass(self):
        m = types.ModuleType('m')
        class MyTestCase(unittest.TestCase):
            def test(self):
                pass
        m.testcase_1 = MyTestCase

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromNames(['testcase_1'], m)
        self.assertIsInstance(suite, loader.suiteClass)

        expected = loader.suiteClass([MyTestCase('test')])
        self.assertEqual(list(suite), [expected])

    # "The specifier name is a ``dotted name'' that may resolve ... to
    # ... a TestSuite instance"
    def test_loadTestsFromNames__relative_TestSuite(self):
        m = types.ModuleType('m')
        class MyTestCase(unittest.TestCase):
            def test(self):
                pass
        m.testsuite = unittest.TestSuite([MyTestCase('test')])

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromNames(['testsuite'], m)
        self.assertIsInstance(suite, loader.suiteClass)

        self.assertEqual(list(suite), [m.testsuite])

    # "The specifier name is a ``dotted name'' that may resolve ... to ... a
    # test method within a test case class"
    def test_loadTestsFromNames__relative_testmethod(self):
        m = types.ModuleType('m')
        class MyTestCase(unittest.TestCase):
            def test(self):
                pass
        m.testcase_1 = MyTestCase

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromNames(['testcase_1.test'], m)
        self.assertIsInstance(suite, loader.suiteClass)

        ref_suite = unittest.TestSuite([MyTestCase('test')])
        self.assertEqual(list(suite), [ref_suite])

    # "The specifier name is a ``dotted name'' that may resolve ... to ... a
    # test method within a test case class"
    #
    # Does the method gracefully handle names that initially look like they
    # resolve to "a test method within a test case class" but don't?
    def test_loadTestsFromNames__relative_invalid_testmethod(self):
        m = types.ModuleType('m')
        class MyTestCase(unittest.TestCase):
            def test(self):
                pass
        m.testcase_1 = MyTestCase

        loader = unittest.TestLoader()
        try:
            loader.loadTestsFromNames(['testcase_1.testfoo'], m)
        except AttributeError, e:
            self.assertEqual(str(e), "type object 'MyTestCase' has no attribute 'testfoo'")
        else:
            self.fail("Failed to raise AttributeError")

    # "The specifier name is a ``dotted name'' that may resolve ... to
    # ... a callable object which returns a ... TestSuite instance"
    def test_loadTestsFromNames__callable__TestSuite(self):
        m = types.ModuleType('m')
        testcase_1 = unittest.FunctionTestCase(lambda: None)
        testcase_2 = unittest.FunctionTestCase(lambda: None)
        def return_TestSuite():
            return unittest.TestSuite([testcase_1, testcase_2])
        m.return_TestSuite = return_TestSuite

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromNames(['return_TestSuite'], m)
        self.assertIsInstance(suite, loader.suiteClass)

        expected = unittest.TestSuite([testcase_1, testcase_2])
        self.assertEqual(list(suite), [expected])

    # "The specifier name is a ``dotted name'' that may resolve ... to
    # ... a callable object which returns a TestCase ... instance"
    def test_loadTestsFromNames__callable__TestCase_instance(self):
        m = types.ModuleType('m')
        testcase_1 = unittest.FunctionTestCase(lambda: None)
        def return_TestCase():
            return testcase_1
        m.return_TestCase = return_TestCase

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromNames(['return_TestCase'], m)
        self.assertIsInstance(suite, loader.suiteClass)

        ref_suite = unittest.TestSuite([testcase_1])
        self.assertEqual(list(suite), [ref_suite])

    # "The specifier name is a ``dotted name'' that may resolve ... to
    # ... a callable object which returns a TestCase or TestSuite instance"
    #
    # Are staticmethods handled correctly?
    def test_loadTestsFromNames__callable__call_staticmethod(self):
        m = types.ModuleType('m')
        class Test1(unittest.TestCase):
            def test(self):
                pass

        testcase_1 = Test1('test')
        class Foo(unittest.TestCase):
            @staticmethod
            def foo():
                return testcase_1
        m.Foo = Foo

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromNames(['Foo.foo'], m)
        self.assertIsInstance(suite, loader.suiteClass)

        ref_suite = unittest.TestSuite([testcase_1])
        self.assertEqual(list(suite), [ref_suite])

    # "The specifier name is a ``dotted name'' that may resolve ... to
    # ... a callable object which returns a TestCase or TestSuite instance"
    #
    # What happens when the callable returns something else?
    def test_loadTestsFromNames__callable__wrong_type(self):
        m = types.ModuleType('m')
        def return_wrong():
            return 6
        m.return_wrong = return_wrong

        loader = unittest.TestLoader()
        try:
            loader.loadTestsFromNames(['return_wrong'], m)
        except TypeError:
            pass
        else:
            self.fail("TestLoader.loadTestsFromNames failed to raise TypeError")

    # "The specifier can refer to modules and packages which have not been
    # imported; they will be imported as a side-effect"
    def test_loadTestsFromNames__module_not_loaded(self):
        # We're going to try to load this module as a side-effect, so it
        # better not be loaded before we try.
        #
        module_name = 'unittest.test.dummy'
        sys.modules.pop(module_name, None)

        loader = unittest.TestLoader()
        try:
            suite = loader.loadTestsFromNames([module_name])

            self.assertIsInstance(suite, loader.suiteClass)
            self.assertEqual(list(suite), [unittest.TestSuite()])

            # module should now be loaded, thanks to loadTestsFromName()
            self.assertIn(module_name, sys.modules)
        finally:
            if module_name in sys.modules:
                del sys.modules[module_name]

    ################################################################
    ### /Tests for TestLoader.loadTestsFromNames()

    ### Tests for TestLoader.getTestCaseNames()
    ################################################################

    # "Return a sorted sequence of method names found within testCaseClass"
    #
    # Test.foobar is defined to make sure getTestCaseNames() respects
    # loader.testMethodPrefix
    def test_getTestCaseNames(self):
        class Test(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass
            def foobar(self): pass

        loader = unittest.TestLoader()

        self.assertEqual(loader.getTestCaseNames(Test), ['test_1', 'test_2'])

    # "Return a sorted sequence of method names found within testCaseClass"
    #
    # Does getTestCaseNames() behave appropriately if no tests are found?
    def test_getTestCaseNames__no_tests(self):
        class Test(unittest.TestCase):
            def foobar(self): pass

        loader = unittest.TestLoader()

        self.assertEqual(loader.getTestCaseNames(Test), [])

    # "Return a sorted sequence of method names found within testCaseClass"
    #
    # Are not-TestCases handled gracefully?
    #
    # XXX This should raise a TypeError, not return a list
    #
    # XXX It's too late in the 2.5 release cycle to fix this, but it should
    # probably be revisited for 2.6
    def test_getTestCaseNames__not_a_TestCase(self):
        class BadCase(int):
            def test_foo(self):
                pass

        loader = unittest.TestLoader()
        names = loader.getTestCaseNames(BadCase)

        self.assertEqual(names, ['test_foo'])

    # "Return a sorted sequence of method names found within testCaseClass"
    #
    # Make sure inherited names are handled.
    #
    # TestP.foobar is defined to make sure getTestCaseNames() respects
    # loader.testMethodPrefix
    def test_getTestCaseNames__inheritance(self):
        class TestP(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass
            def foobar(self): pass

        class TestC(TestP):
            def test_1(self): pass
            def test_3(self): pass

        loader = unittest.TestLoader()

        names = ['test_1', 'test_2', 'test_3']
        self.assertEqual(loader.getTestCaseNames(TestC), names)

    ################################################################
    ### /Tests for TestLoader.getTestCaseNames()

    ### Tests for TestLoader.testMethodPrefix
    ################################################################

    # "String giving the prefix of method names which will be interpreted as
    # test methods"
    #
    # Implicit in the documentation is that testMethodPrefix is respected by
    # all loadTestsFrom* methods.
    def test_testMethodPrefix__loadTestsFromTestCase(self):
        class Foo(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass
            def foo_bar(self): pass

        tests_1 = unittest.TestSuite([Foo('foo_bar')])
        tests_2 = unittest.TestSuite([Foo('test_1'), Foo('test_2')])

        loader = unittest.TestLoader()
        loader.testMethodPrefix = 'foo'
        self.assertEqual(loader.loadTestsFromTestCase(Foo), tests_1)

        loader.testMethodPrefix = 'test'
        self.assertEqual(loader.loadTestsFromTestCase(Foo), tests_2)

    # "String giving the prefix of method names which will be interpreted as
    # test methods"
    #
    # Implicit in the documentation is that testMethodPrefix is respected by
    # all loadTestsFrom* methods.
    def test_testMethodPrefix__loadTestsFromModule(self):
        m = types.ModuleType('m')
        class Foo(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass
            def foo_bar(self): pass
        m.Foo = Foo

        tests_1 = [unittest.TestSuite([Foo('foo_bar')])]
        tests_2 = [unittest.TestSuite([Foo('test_1'), Foo('test_2')])]

        loader = unittest.TestLoader()
        loader.testMethodPrefix = 'foo'
        self.assertEqual(list(loader.loadTestsFromModule(m)), tests_1)

        loader.testMethodPrefix = 'test'
        self.assertEqual(list(loader.loadTestsFromModule(m)), tests_2)

    # "String giving the prefix of method names which will be interpreted as
    # test methods"
    #
    # Implicit in the documentation is that testMethodPrefix is respected by
    # all loadTestsFrom* methods.
    def test_testMethodPrefix__loadTestsFromName(self):
        m = types.ModuleType('m')
        class Foo(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass
            def foo_bar(self): pass
        m.Foo = Foo

        tests_1 = unittest.TestSuite([Foo('foo_bar')])
        tests_2 = unittest.TestSuite([Foo('test_1'), Foo('test_2')])

        loader = unittest.TestLoader()
        loader.testMethodPrefix = 'foo'
        self.assertEqual(loader.loadTestsFromName('Foo', m), tests_1)

        loader.testMethodPrefix = 'test'
        self.assertEqual(loader.loadTestsFromName('Foo', m), tests_2)

    # "String giving the prefix of method names which will be interpreted as
    # test methods"
    #
    # Implicit in the documentation is that testMethodPrefix is respected by
    # all loadTestsFrom* methods.
    def test_testMethodPrefix__loadTestsFromNames(self):
        m = types.ModuleType('m')
        class Foo(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass
            def foo_bar(self): pass
        m.Foo = Foo

        tests_1 = unittest.TestSuite([unittest.TestSuite([Foo('foo_bar')])])
        tests_2 = unittest.TestSuite([Foo('test_1'), Foo('test_2')])
        tests_2 = unittest.TestSuite([tests_2])

        loader = unittest.TestLoader()
        loader.testMethodPrefix = 'foo'
        self.assertEqual(loader.loadTestsFromNames(['Foo'], m), tests_1)

        loader.testMethodPrefix = 'test'
        self.assertEqual(loader.loadTestsFromNames(['Foo'], m), tests_2)

    # "The default value is 'test'"
    def test_testMethodPrefix__default_value(self):
        loader = unittest.TestLoader()
        self.assertTrue(loader.testMethodPrefix == 'test')

    ################################################################
    ### /Tests for TestLoader.testMethodPrefix

    ### Tests for TestLoader.sortTestMethodsUsing
    ################################################################

    # "Function to be used to compare method names when sorting them in
    # getTestCaseNames() and all the loadTestsFromX() methods"
    def test_sortTestMethodsUsing__loadTestsFromTestCase(self):
        def reversed_cmp(x, y):
            return -cmp(x, y)

        class Foo(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass

        loader = unittest.TestLoader()
        loader.sortTestMethodsUsing = reversed_cmp

        tests = loader.suiteClass([Foo('test_2'), Foo('test_1')])
        self.assertEqual(loader.loadTestsFromTestCase(Foo), tests)

    # "Function to be used to compare method names when sorting them in
    # getTestCaseNames() and all the loadTestsFromX() methods"
    def test_sortTestMethodsUsing__loadTestsFromModule(self):
        def reversed_cmp(x, y):
            return -cmp(x, y)

        m = types.ModuleType('m')
        class Foo(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass
        m.Foo = Foo

        loader = unittest.TestLoader()
        loader.sortTestMethodsUsing = reversed_cmp

        tests = [loader.suiteClass([Foo('test_2'), Foo('test_1')])]
        self.assertEqual(list(loader.loadTestsFromModule(m)), tests)

    # "Function to be used to compare method names when sorting them in
    # getTestCaseNames() and all the loadTestsFromX() methods"
    def test_sortTestMethodsUsing__loadTestsFromName(self):
        def reversed_cmp(x, y):
            return -cmp(x, y)

        m = types.ModuleType('m')
        class Foo(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass
        m.Foo = Foo

        loader = unittest.TestLoader()
        loader.sortTestMethodsUsing = reversed_cmp

        tests = loader.suiteClass([Foo('test_2'), Foo('test_1')])
        self.assertEqual(loader.loadTestsFromName('Foo', m), tests)

    # "Function to be used to compare method names when sorting them in
    # getTestCaseNames() and all the loadTestsFromX() methods"
    def test_sortTestMethodsUsing__loadTestsFromNames(self):
        def reversed_cmp(x, y):
            return -cmp(x, y)

        m = types.ModuleType('m')
        class Foo(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass
        m.Foo = Foo

        loader = unittest.TestLoader()
        loader.sortTestMethodsUsing = reversed_cmp

        tests = [loader.suiteClass([Foo('test_2'), Foo('test_1')])]
        self.assertEqual(list(loader.loadTestsFromNames(['Foo'], m)), tests)

    # "Function to be used to compare method names when sorting them in
    # getTestCaseNames()"
    #
    # Does it actually affect getTestCaseNames()?
    def test_sortTestMethodsUsing__getTestCaseNames(self):
        def reversed_cmp(x, y):
            return -cmp(x, y)

        class Foo(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass

        loader = unittest.TestLoader()
        loader.sortTestMethodsUsing = reversed_cmp

        test_names = ['test_2', 'test_1']
        self.assertEqual(loader.getTestCaseNames(Foo), test_names)

    # "The default value is the built-in cmp() function"
    def test_sortTestMethodsUsing__default_value(self):
        loader = unittest.TestLoader()
        self.assertTrue(loader.sortTestMethodsUsing is cmp)

    # "it can be set to None to disable the sort."
    #
    # XXX How is this different from reassigning cmp? Are the tests returned
    # in a random order or something? This behaviour should die
    def test_sortTestMethodsUsing__None(self):
        class Foo(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass

        loader = unittest.TestLoader()
        loader.sortTestMethodsUsing = None

        test_names = ['test_2', 'test_1']
        self.assertEqual(set(loader.getTestCaseNames(Foo)), set(test_names))

    ################################################################
    ### /Tests for TestLoader.sortTestMethodsUsing

    ### Tests for TestLoader.suiteClass
    ################################################################

    # "Callable object that constructs a test suite from a list of tests."
    def test_suiteClass__loadTestsFromTestCase(self):
        class Foo(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass
            def foo_bar(self): pass

        tests = [Foo('test_1'), Foo('test_2')]

        loader = unittest.TestLoader()
        loader.suiteClass = list
        self.assertEqual(loader.loadTestsFromTestCase(Foo), tests)

    # It is implicit in the documentation for TestLoader.suiteClass that
    # all TestLoader.loadTestsFrom* methods respect it. Let's make sure
    def test_suiteClass__loadTestsFromModule(self):
        m = types.ModuleType('m')
        class Foo(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass
            def foo_bar(self): pass
        m.Foo = Foo

        tests = [[Foo('test_1'), Foo('test_2')]]

        loader = unittest.TestLoader()
        loader.suiteClass = list
        self.assertEqual(loader.loadTestsFromModule(m), tests)

    # It is implicit in the documentation for TestLoader.suiteClass that
    # all TestLoader.loadTestsFrom* methods respect it. Let's make sure
    def test_suiteClass__loadTestsFromName(self):
        m = types.ModuleType('m')
        class Foo(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass
            def foo_bar(self): pass
        m.Foo = Foo

        tests = [Foo('test_1'), Foo('test_2')]

        loader = unittest.TestLoader()
        loader.suiteClass = list
        self.assertEqual(loader.loadTestsFromName('Foo', m), tests)

    # It is implicit in the documentation for TestLoader.suiteClass that
    # all TestLoader.loadTestsFrom* methods respect it. Let's make sure
    def test_suiteClass__loadTestsFromNames(self):
        m = types.ModuleType('m')
        class Foo(unittest.TestCase):
            def test_1(self): pass
            def test_2(self): pass
            def foo_bar(self): pass
        m.Foo = Foo

        tests = [[Foo('test_1'), Foo('test_2')]]

        loader = unittest.TestLoader()
        loader.suiteClass = list
        self.assertEqual(loader.loadTestsFromNames(['Foo'], m), tests)

    # "The default value is the TestSuite class"
    def test_suiteClass__default_value(self):
        loader = unittest.TestLoader()
        self.assertIs(loader.suiteClass, unittest.TestSuite)

    # Make sure the dotted name resolution works even if the actual
    # function doesn't have the same name as is used to find it.
    def test_loadTestsFromName__function_with_different_name_than_method(self):
        # lambdas have the name '<lambda>'.
        m = types.ModuleType('m')
        class MyTestCase(unittest.TestCase):
            test = lambda: 1
        m.testcase_1 = MyTestCase

        loader = unittest.TestLoader()
        suite = loader.loadTestsFromNames(['testcase_1.test'], m)
        self.assertIsInstance(suite, loader.suiteClass)

        ref_suite = unittest.TestSuite([MyTestCase('test')])
        self.assertEqual(list(suite), [ref_suite])

if __name__ == '__main__':
    unittest.main()
