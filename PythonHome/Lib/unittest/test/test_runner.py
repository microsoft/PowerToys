import unittest

from cStringIO import StringIO
import pickle

from .support import LoggingResult, ResultWithNoStartTestRunStopTestRun


class TestCleanUp(unittest.TestCase):

    def testCleanUp(self):
        class TestableTest(unittest.TestCase):
            def testNothing(self):
                pass

        test = TestableTest('testNothing')
        self.assertEqual(test._cleanups, [])

        cleanups = []

        def cleanup1(*args, **kwargs):
            cleanups.append((1, args, kwargs))

        def cleanup2(*args, **kwargs):
            cleanups.append((2, args, kwargs))

        test.addCleanup(cleanup1, 1, 2, 3, four='hello', five='goodbye')
        test.addCleanup(cleanup2)

        self.assertEqual(test._cleanups,
                         [(cleanup1, (1, 2, 3), dict(four='hello', five='goodbye')),
                          (cleanup2, (), {})])

        result = test.doCleanups()
        self.assertTrue(result)

        self.assertEqual(cleanups, [(2, (), {}), (1, (1, 2, 3),
                                    dict(four='hello', five='goodbye'))])

    def testCleanUpWithErrors(self):
        class TestableTest(unittest.TestCase):
            def testNothing(self):
                pass

        class MockResult(object):
            errors = []
            def addError(self, test, exc_info):
                self.errors.append((test, exc_info))

        result = MockResult()
        test = TestableTest('testNothing')
        test._resultForDoCleanups = result

        exc1 = Exception('foo')
        exc2 = Exception('bar')
        def cleanup1():
            raise exc1

        def cleanup2():
            raise exc2

        test.addCleanup(cleanup1)
        test.addCleanup(cleanup2)

        self.assertFalse(test.doCleanups())

        (test1, (Type1, instance1, _)), (test2, (Type2, instance2, _)) = reversed(MockResult.errors)
        self.assertEqual((test1, Type1, instance1), (test, Exception, exc1))
        self.assertEqual((test2, Type2, instance2), (test, Exception, exc2))

    def testCleanupInRun(self):
        blowUp = False
        ordering = []

        class TestableTest(unittest.TestCase):
            def setUp(self):
                ordering.append('setUp')
                if blowUp:
                    raise Exception('foo')

            def testNothing(self):
                ordering.append('test')

            def tearDown(self):
                ordering.append('tearDown')

        test = TestableTest('testNothing')

        def cleanup1():
            ordering.append('cleanup1')
        def cleanup2():
            ordering.append('cleanup2')
        test.addCleanup(cleanup1)
        test.addCleanup(cleanup2)

        def success(some_test):
            self.assertEqual(some_test, test)
            ordering.append('success')

        result = unittest.TestResult()
        result.addSuccess = success

        test.run(result)
        self.assertEqual(ordering, ['setUp', 'test', 'tearDown',
                                    'cleanup2', 'cleanup1', 'success'])

        blowUp = True
        ordering = []
        test = TestableTest('testNothing')
        test.addCleanup(cleanup1)
        test.run(result)
        self.assertEqual(ordering, ['setUp', 'cleanup1'])

    def testTestCaseDebugExecutesCleanups(self):
        ordering = []

        class TestableTest(unittest.TestCase):
            def setUp(self):
                ordering.append('setUp')
                self.addCleanup(cleanup1)

            def testNothing(self):
                ordering.append('test')

            def tearDown(self):
                ordering.append('tearDown')

        test = TestableTest('testNothing')

        def cleanup1():
            ordering.append('cleanup1')
            test.addCleanup(cleanup2)
        def cleanup2():
            ordering.append('cleanup2')

        test.debug()
        self.assertEqual(ordering, ['setUp', 'test', 'tearDown', 'cleanup1', 'cleanup2'])


class Test_TextTestRunner(unittest.TestCase):
    """Tests for TextTestRunner."""

    def test_init(self):
        runner = unittest.TextTestRunner()
        self.assertFalse(runner.failfast)
        self.assertFalse(runner.buffer)
        self.assertEqual(runner.verbosity, 1)
        self.assertTrue(runner.descriptions)
        self.assertEqual(runner.resultclass, unittest.TextTestResult)


    def test_multiple_inheritance(self):
        class AResult(unittest.TestResult):
            def __init__(self, stream, descriptions, verbosity):
                super(AResult, self).__init__(stream, descriptions, verbosity)

        class ATextResult(unittest.TextTestResult, AResult):
            pass

        # This used to raise an exception due to TextTestResult not passing
        # on arguments in its __init__ super call
        ATextResult(None, None, 1)


    def testBufferAndFailfast(self):
        class Test(unittest.TestCase):
            def testFoo(self):
                pass
        result = unittest.TestResult()
        runner = unittest.TextTestRunner(stream=StringIO(), failfast=True,
                                           buffer=True)
        # Use our result object
        runner._makeResult = lambda: result
        runner.run(Test('testFoo'))

        self.assertTrue(result.failfast)
        self.assertTrue(result.buffer)

    def testRunnerRegistersResult(self):
        class Test(unittest.TestCase):
            def testFoo(self):
                pass
        originalRegisterResult = unittest.runner.registerResult
        def cleanup():
            unittest.runner.registerResult = originalRegisterResult
        self.addCleanup(cleanup)

        result = unittest.TestResult()
        runner = unittest.TextTestRunner(stream=StringIO())
        # Use our result object
        runner._makeResult = lambda: result

        self.wasRegistered = 0
        def fakeRegisterResult(thisResult):
            self.wasRegistered += 1
            self.assertEqual(thisResult, result)
        unittest.runner.registerResult = fakeRegisterResult

        runner.run(unittest.TestSuite())
        self.assertEqual(self.wasRegistered, 1)

    def test_works_with_result_without_startTestRun_stopTestRun(self):
        class OldTextResult(ResultWithNoStartTestRunStopTestRun):
            separator2 = ''
            def printErrors(self):
                pass

        class Runner(unittest.TextTestRunner):
            def __init__(self):
                super(Runner, self).__init__(StringIO())

            def _makeResult(self):
                return OldTextResult()

        runner = Runner()
        runner.run(unittest.TestSuite())

    def test_startTestRun_stopTestRun_called(self):
        class LoggingTextResult(LoggingResult):
            separator2 = ''
            def printErrors(self):
                pass

        class LoggingRunner(unittest.TextTestRunner):
            def __init__(self, events):
                super(LoggingRunner, self).__init__(StringIO())
                self._events = events

            def _makeResult(self):
                return LoggingTextResult(self._events)

        events = []
        runner = LoggingRunner(events)
        runner.run(unittest.TestSuite())
        expected = ['startTestRun', 'stopTestRun']
        self.assertEqual(events, expected)

    def test_pickle_unpickle(self):
        # Issue #7197: a TextTestRunner should be (un)pickleable. This is
        # required by test_multiprocessing under Windows (in verbose mode).
        from StringIO import StringIO as PickleableIO
        # cStringIO objects are not pickleable, but StringIO objects are.
        stream = PickleableIO("foo")
        runner = unittest.TextTestRunner(stream)
        for protocol in range(pickle.HIGHEST_PROTOCOL + 1):
            s = pickle.dumps(runner, protocol=protocol)
            obj = pickle.loads(s)
            # StringIO objects never compare equal, a cheap test instead.
            self.assertEqual(obj.stream.getvalue(), stream.getvalue())

    def test_resultclass(self):
        def MockResultClass(*args):
            return args
        STREAM = object()
        DESCRIPTIONS = object()
        VERBOSITY = object()
        runner = unittest.TextTestRunner(STREAM, DESCRIPTIONS, VERBOSITY,
                                         resultclass=MockResultClass)
        self.assertEqual(runner.resultclass, MockResultClass)

        expectedresult = (runner.stream, DESCRIPTIONS, VERBOSITY)
        self.assertEqual(runner._makeResult(), expectedresult)


if __name__ == '__main__':
    unittest.main()
