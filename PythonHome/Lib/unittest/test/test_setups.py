import sys

from cStringIO import StringIO

import unittest


def resultFactory(*_):
    return unittest.TestResult()


class TestSetups(unittest.TestCase):

    def getRunner(self):
        return unittest.TextTestRunner(resultclass=resultFactory,
                                          stream=StringIO())
    def runTests(self, *cases):
        suite = unittest.TestSuite()
        for case in cases:
            tests = unittest.defaultTestLoader.loadTestsFromTestCase(case)
            suite.addTests(tests)

        runner = self.getRunner()

        # creating a nested suite exposes some potential bugs
        realSuite = unittest.TestSuite()
        realSuite.addTest(suite)
        # adding empty suites to the end exposes potential bugs
        suite.addTest(unittest.TestSuite())
        realSuite.addTest(unittest.TestSuite())
        return runner.run(realSuite)

    def test_setup_class(self):
        class Test(unittest.TestCase):
            setUpCalled = 0
            @classmethod
            def setUpClass(cls):
                Test.setUpCalled += 1
                unittest.TestCase.setUpClass()
            def test_one(self):
                pass
            def test_two(self):
                pass

        result = self.runTests(Test)

        self.assertEqual(Test.setUpCalled, 1)
        self.assertEqual(result.testsRun, 2)
        self.assertEqual(len(result.errors), 0)

    def test_teardown_class(self):
        class Test(unittest.TestCase):
            tearDownCalled = 0
            @classmethod
            def tearDownClass(cls):
                Test.tearDownCalled += 1
                unittest.TestCase.tearDownClass()
            def test_one(self):
                pass
            def test_two(self):
                pass

        result = self.runTests(Test)

        self.assertEqual(Test.tearDownCalled, 1)
        self.assertEqual(result.testsRun, 2)
        self.assertEqual(len(result.errors), 0)

    def test_teardown_class_two_classes(self):
        class Test(unittest.TestCase):
            tearDownCalled = 0
            @classmethod
            def tearDownClass(cls):
                Test.tearDownCalled += 1
                unittest.TestCase.tearDownClass()
            def test_one(self):
                pass
            def test_two(self):
                pass

        class Test2(unittest.TestCase):
            tearDownCalled = 0
            @classmethod
            def tearDownClass(cls):
                Test2.tearDownCalled += 1
                unittest.TestCase.tearDownClass()
            def test_one(self):
                pass
            def test_two(self):
                pass

        result = self.runTests(Test, Test2)

        self.assertEqual(Test.tearDownCalled, 1)
        self.assertEqual(Test2.tearDownCalled, 1)
        self.assertEqual(result.testsRun, 4)
        self.assertEqual(len(result.errors), 0)

    def test_error_in_setupclass(self):
        class BrokenTest(unittest.TestCase):
            @classmethod
            def setUpClass(cls):
                raise TypeError('foo')
            def test_one(self):
                pass
            def test_two(self):
                pass

        result = self.runTests(BrokenTest)

        self.assertEqual(result.testsRun, 0)
        self.assertEqual(len(result.errors), 1)
        error, _ = result.errors[0]
        self.assertEqual(str(error),
                    'setUpClass (%s.BrokenTest)' % __name__)

    def test_error_in_teardown_class(self):
        class Test(unittest.TestCase):
            tornDown = 0
            @classmethod
            def tearDownClass(cls):
                Test.tornDown += 1
                raise TypeError('foo')
            def test_one(self):
                pass
            def test_two(self):
                pass

        class Test2(unittest.TestCase):
            tornDown = 0
            @classmethod
            def tearDownClass(cls):
                Test2.tornDown += 1
                raise TypeError('foo')
            def test_one(self):
                pass
            def test_two(self):
                pass

        result = self.runTests(Test, Test2)
        self.assertEqual(result.testsRun, 4)
        self.assertEqual(len(result.errors), 2)
        self.assertEqual(Test.tornDown, 1)
        self.assertEqual(Test2.tornDown, 1)

        error, _ = result.errors[0]
        self.assertEqual(str(error),
                    'tearDownClass (%s.Test)' % __name__)

    def test_class_not_torndown_when_setup_fails(self):
        class Test(unittest.TestCase):
            tornDown = False
            @classmethod
            def setUpClass(cls):
                raise TypeError
            @classmethod
            def tearDownClass(cls):
                Test.tornDown = True
                raise TypeError('foo')
            def test_one(self):
                pass

        self.runTests(Test)
        self.assertFalse(Test.tornDown)

    def test_class_not_setup_or_torndown_when_skipped(self):
        class Test(unittest.TestCase):
            classSetUp = False
            tornDown = False
            @classmethod
            def setUpClass(cls):
                Test.classSetUp = True
            @classmethod
            def tearDownClass(cls):
                Test.tornDown = True
            def test_one(self):
                pass

        Test = unittest.skip("hop")(Test)
        self.runTests(Test)
        self.assertFalse(Test.classSetUp)
        self.assertFalse(Test.tornDown)

    def test_setup_teardown_order_with_pathological_suite(self):
        results = []

        class Module1(object):
            @staticmethod
            def setUpModule():
                results.append('Module1.setUpModule')
            @staticmethod
            def tearDownModule():
                results.append('Module1.tearDownModule')

        class Module2(object):
            @staticmethod
            def setUpModule():
                results.append('Module2.setUpModule')
            @staticmethod
            def tearDownModule():
                results.append('Module2.tearDownModule')

        class Test1(unittest.TestCase):
            @classmethod
            def setUpClass(cls):
                results.append('setup 1')
            @classmethod
            def tearDownClass(cls):
                results.append('teardown 1')
            def testOne(self):
                results.append('Test1.testOne')
            def testTwo(self):
                results.append('Test1.testTwo')

        class Test2(unittest.TestCase):
            @classmethod
            def setUpClass(cls):
                results.append('setup 2')
            @classmethod
            def tearDownClass(cls):
                results.append('teardown 2')
            def testOne(self):
                results.append('Test2.testOne')
            def testTwo(self):
                results.append('Test2.testTwo')

        class Test3(unittest.TestCase):
            @classmethod
            def setUpClass(cls):
                results.append('setup 3')
            @classmethod
            def tearDownClass(cls):
                results.append('teardown 3')
            def testOne(self):
                results.append('Test3.testOne')
            def testTwo(self):
                results.append('Test3.testTwo')

        Test1.__module__ = Test2.__module__ = 'Module'
        Test3.__module__ = 'Module2'
        sys.modules['Module'] = Module1
        sys.modules['Module2'] = Module2

        first = unittest.TestSuite((Test1('testOne'),))
        second = unittest.TestSuite((Test1('testTwo'),))
        third = unittest.TestSuite((Test2('testOne'),))
        fourth = unittest.TestSuite((Test2('testTwo'),))
        fifth = unittest.TestSuite((Test3('testOne'),))
        sixth = unittest.TestSuite((Test3('testTwo'),))
        suite = unittest.TestSuite((first, second, third, fourth, fifth, sixth))

        runner = self.getRunner()
        result = runner.run(suite)
        self.assertEqual(result.testsRun, 6)
        self.assertEqual(len(result.errors), 0)

        self.assertEqual(results,
                         ['Module1.setUpModule', 'setup 1',
                          'Test1.testOne', 'Test1.testTwo', 'teardown 1',
                          'setup 2', 'Test2.testOne', 'Test2.testTwo',
                          'teardown 2', 'Module1.tearDownModule',
                          'Module2.setUpModule', 'setup 3',
                          'Test3.testOne', 'Test3.testTwo',
                          'teardown 3', 'Module2.tearDownModule'])

    def test_setup_module(self):
        class Module(object):
            moduleSetup = 0
            @staticmethod
            def setUpModule():
                Module.moduleSetup += 1

        class Test(unittest.TestCase):
            def test_one(self):
                pass
            def test_two(self):
                pass
        Test.__module__ = 'Module'
        sys.modules['Module'] = Module

        result = self.runTests(Test)
        self.assertEqual(Module.moduleSetup, 1)
        self.assertEqual(result.testsRun, 2)
        self.assertEqual(len(result.errors), 0)

    def test_error_in_setup_module(self):
        class Module(object):
            moduleSetup = 0
            moduleTornDown = 0
            @staticmethod
            def setUpModule():
                Module.moduleSetup += 1
                raise TypeError('foo')
            @staticmethod
            def tearDownModule():
                Module.moduleTornDown += 1

        class Test(unittest.TestCase):
            classSetUp = False
            classTornDown = False
            @classmethod
            def setUpClass(cls):
                Test.classSetUp = True
            @classmethod
            def tearDownClass(cls):
                Test.classTornDown = True
            def test_one(self):
                pass
            def test_two(self):
                pass

        class Test2(unittest.TestCase):
            def test_one(self):
                pass
            def test_two(self):
                pass
        Test.__module__ = 'Module'
        Test2.__module__ = 'Module'
        sys.modules['Module'] = Module

        result = self.runTests(Test, Test2)
        self.assertEqual(Module.moduleSetup, 1)
        self.assertEqual(Module.moduleTornDown, 0)
        self.assertEqual(result.testsRun, 0)
        self.assertFalse(Test.classSetUp)
        self.assertFalse(Test.classTornDown)
        self.assertEqual(len(result.errors), 1)
        error, _ = result.errors[0]
        self.assertEqual(str(error), 'setUpModule (Module)')

    def test_testcase_with_missing_module(self):
        class Test(unittest.TestCase):
            def test_one(self):
                pass
            def test_two(self):
                pass
        Test.__module__ = 'Module'
        sys.modules.pop('Module', None)

        result = self.runTests(Test)
        self.assertEqual(result.testsRun, 2)

    def test_teardown_module(self):
        class Module(object):
            moduleTornDown = 0
            @staticmethod
            def tearDownModule():
                Module.moduleTornDown += 1

        class Test(unittest.TestCase):
            def test_one(self):
                pass
            def test_two(self):
                pass
        Test.__module__ = 'Module'
        sys.modules['Module'] = Module

        result = self.runTests(Test)
        self.assertEqual(Module.moduleTornDown, 1)
        self.assertEqual(result.testsRun, 2)
        self.assertEqual(len(result.errors), 0)

    def test_error_in_teardown_module(self):
        class Module(object):
            moduleTornDown = 0
            @staticmethod
            def tearDownModule():
                Module.moduleTornDown += 1
                raise TypeError('foo')

        class Test(unittest.TestCase):
            classSetUp = False
            classTornDown = False
            @classmethod
            def setUpClass(cls):
                Test.classSetUp = True
            @classmethod
            def tearDownClass(cls):
                Test.classTornDown = True
            def test_one(self):
                pass
            def test_two(self):
                pass

        class Test2(unittest.TestCase):
            def test_one(self):
                pass
            def test_two(self):
                pass
        Test.__module__ = 'Module'
        Test2.__module__ = 'Module'
        sys.modules['Module'] = Module

        result = self.runTests(Test, Test2)
        self.assertEqual(Module.moduleTornDown, 1)
        self.assertEqual(result.testsRun, 4)
        self.assertTrue(Test.classSetUp)
        self.assertTrue(Test.classTornDown)
        self.assertEqual(len(result.errors), 1)
        error, _ = result.errors[0]
        self.assertEqual(str(error), 'tearDownModule (Module)')

    def test_skiptest_in_setupclass(self):
        class Test(unittest.TestCase):
            @classmethod
            def setUpClass(cls):
                raise unittest.SkipTest('foo')
            def test_one(self):
                pass
            def test_two(self):
                pass

        result = self.runTests(Test)
        self.assertEqual(result.testsRun, 0)
        self.assertEqual(len(result.errors), 0)
        self.assertEqual(len(result.skipped), 1)
        skipped = result.skipped[0][0]
        self.assertEqual(str(skipped), 'setUpClass (%s.Test)' % __name__)

    def test_skiptest_in_setupmodule(self):
        class Test(unittest.TestCase):
            def test_one(self):
                pass
            def test_two(self):
                pass

        class Module(object):
            @staticmethod
            def setUpModule():
                raise unittest.SkipTest('foo')

        Test.__module__ = 'Module'
        sys.modules['Module'] = Module

        result = self.runTests(Test)
        self.assertEqual(result.testsRun, 0)
        self.assertEqual(len(result.errors), 0)
        self.assertEqual(len(result.skipped), 1)
        skipped = result.skipped[0][0]
        self.assertEqual(str(skipped), 'setUpModule (Module)')

    def test_suite_debug_executes_setups_and_teardowns(self):
        ordering = []

        class Module(object):
            @staticmethod
            def setUpModule():
                ordering.append('setUpModule')
            @staticmethod
            def tearDownModule():
                ordering.append('tearDownModule')

        class Test(unittest.TestCase):
            @classmethod
            def setUpClass(cls):
                ordering.append('setUpClass')
            @classmethod
            def tearDownClass(cls):
                ordering.append('tearDownClass')
            def test_something(self):
                ordering.append('test_something')

        Test.__module__ = 'Module'
        sys.modules['Module'] = Module

        suite = unittest.defaultTestLoader.loadTestsFromTestCase(Test)
        suite.debug()
        expectedOrder = ['setUpModule', 'setUpClass', 'test_something', 'tearDownClass', 'tearDownModule']
        self.assertEqual(ordering, expectedOrder)

    def test_suite_debug_propagates_exceptions(self):
        class Module(object):
            @staticmethod
            def setUpModule():
                if phase == 0:
                    raise Exception('setUpModule')
            @staticmethod
            def tearDownModule():
                if phase == 1:
                    raise Exception('tearDownModule')

        class Test(unittest.TestCase):
            @classmethod
            def setUpClass(cls):
                if phase == 2:
                    raise Exception('setUpClass')
            @classmethod
            def tearDownClass(cls):
                if phase == 3:
                    raise Exception('tearDownClass')
            def test_something(self):
                if phase == 4:
                    raise Exception('test_something')

        Test.__module__ = 'Module'
        sys.modules['Module'] = Module

        _suite = unittest.defaultTestLoader.loadTestsFromTestCase(Test)
        suite = unittest.TestSuite()
        suite.addTest(_suite)

        messages = ('setUpModule', 'tearDownModule', 'setUpClass', 'tearDownClass', 'test_something')
        for phase, msg in enumerate(messages):
            with self.assertRaisesRegexp(Exception, msg):
                suite.debug()

if __name__ == '__main__':
    unittest.main()
