import gc
import os
import sys
import signal
import weakref

from cStringIO import StringIO


import unittest


@unittest.skipUnless(hasattr(os, 'kill'), "Test requires os.kill")
@unittest.skipIf(sys.platform =="win32", "Test cannot run on Windows")
@unittest.skipIf(sys.platform == 'freebsd6', "Test kills regrtest on freebsd6 "
    "if threads have been used")
class TestBreak(unittest.TestCase):
    int_handler = None

    def setUp(self):
        self._default_handler = signal.getsignal(signal.SIGINT)
        if self.int_handler is not None:
            signal.signal(signal.SIGINT, self.int_handler)

    def tearDown(self):
        signal.signal(signal.SIGINT, self._default_handler)
        unittest.signals._results = weakref.WeakKeyDictionary()
        unittest.signals._interrupt_handler = None


    def testInstallHandler(self):
        default_handler = signal.getsignal(signal.SIGINT)
        unittest.installHandler()
        self.assertNotEqual(signal.getsignal(signal.SIGINT), default_handler)

        try:
            pid = os.getpid()
            os.kill(pid, signal.SIGINT)
        except KeyboardInterrupt:
            self.fail("KeyboardInterrupt not handled")

        self.assertTrue(unittest.signals._interrupt_handler.called)

    def testRegisterResult(self):
        result = unittest.TestResult()
        unittest.registerResult(result)

        for ref in unittest.signals._results:
            if ref is result:
                break
            elif ref is not result:
                self.fail("odd object in result set")
        else:
            self.fail("result not found")


    def testInterruptCaught(self):
        default_handler = signal.getsignal(signal.SIGINT)

        result = unittest.TestResult()
        unittest.installHandler()
        unittest.registerResult(result)

        self.assertNotEqual(signal.getsignal(signal.SIGINT), default_handler)

        def test(result):
            pid = os.getpid()
            os.kill(pid, signal.SIGINT)
            result.breakCaught = True
            self.assertTrue(result.shouldStop)

        try:
            test(result)
        except KeyboardInterrupt:
            self.fail("KeyboardInterrupt not handled")
        self.assertTrue(result.breakCaught)


    def testSecondInterrupt(self):
        # Can't use skipIf decorator because the signal handler may have
        # been changed after defining this method.
        if signal.getsignal(signal.SIGINT) == signal.SIG_IGN:
            self.skipTest("test requires SIGINT to not be ignored")
        result = unittest.TestResult()
        unittest.installHandler()
        unittest.registerResult(result)

        def test(result):
            pid = os.getpid()
            os.kill(pid, signal.SIGINT)
            result.breakCaught = True
            self.assertTrue(result.shouldStop)
            os.kill(pid, signal.SIGINT)
            self.fail("Second KeyboardInterrupt not raised")

        try:
            test(result)
        except KeyboardInterrupt:
            pass
        else:
            self.fail("Second KeyboardInterrupt not raised")
        self.assertTrue(result.breakCaught)


    def testTwoResults(self):
        unittest.installHandler()

        result = unittest.TestResult()
        unittest.registerResult(result)
        new_handler = signal.getsignal(signal.SIGINT)

        result2 = unittest.TestResult()
        unittest.registerResult(result2)
        self.assertEqual(signal.getsignal(signal.SIGINT), new_handler)

        result3 = unittest.TestResult()

        def test(result):
            pid = os.getpid()
            os.kill(pid, signal.SIGINT)

        try:
            test(result)
        except KeyboardInterrupt:
            self.fail("KeyboardInterrupt not handled")

        self.assertTrue(result.shouldStop)
        self.assertTrue(result2.shouldStop)
        self.assertFalse(result3.shouldStop)


    def testHandlerReplacedButCalled(self):
        # Can't use skipIf decorator because the signal handler may have
        # been changed after defining this method.
        if signal.getsignal(signal.SIGINT) == signal.SIG_IGN:
            self.skipTest("test requires SIGINT to not be ignored")
        # If our handler has been replaced (is no longer installed) but is
        # called by the *new* handler, then it isn't safe to delay the
        # SIGINT and we should immediately delegate to the default handler
        unittest.installHandler()

        handler = signal.getsignal(signal.SIGINT)
        def new_handler(frame, signum):
            handler(frame, signum)
        signal.signal(signal.SIGINT, new_handler)

        try:
            pid = os.getpid()
            os.kill(pid, signal.SIGINT)
        except KeyboardInterrupt:
            pass
        else:
            self.fail("replaced but delegated handler doesn't raise interrupt")

    def testRunner(self):
        # Creating a TextTestRunner with the appropriate argument should
        # register the TextTestResult it creates
        runner = unittest.TextTestRunner(stream=StringIO())

        result = runner.run(unittest.TestSuite())
        self.assertIn(result, unittest.signals._results)

    def testWeakReferences(self):
        # Calling registerResult on a result should not keep it alive
        result = unittest.TestResult()
        unittest.registerResult(result)

        ref = weakref.ref(result)
        del result

        # For non-reference counting implementations
        gc.collect();gc.collect()
        self.assertIsNone(ref())


    def testRemoveResult(self):
        result = unittest.TestResult()
        unittest.registerResult(result)

        unittest.installHandler()
        self.assertTrue(unittest.removeResult(result))

        # Should this raise an error instead?
        self.assertFalse(unittest.removeResult(unittest.TestResult()))

        try:
            pid = os.getpid()
            os.kill(pid, signal.SIGINT)
        except KeyboardInterrupt:
            pass

        self.assertFalse(result.shouldStop)

    def testMainInstallsHandler(self):
        failfast = object()
        test = object()
        verbosity = object()
        result = object()
        default_handler = signal.getsignal(signal.SIGINT)

        class FakeRunner(object):
            initArgs = []
            runArgs = []
            def __init__(self, *args, **kwargs):
                self.initArgs.append((args, kwargs))
            def run(self, test):
                self.runArgs.append(test)
                return result

        class Program(unittest.TestProgram):
            def __init__(self, catchbreak):
                self.exit = False
                self.verbosity = verbosity
                self.failfast = failfast
                self.catchbreak = catchbreak
                self.testRunner = FakeRunner
                self.test = test
                self.result = None

        p = Program(False)
        p.runTests()

        self.assertEqual(FakeRunner.initArgs, [((), {'buffer': None,
                                                     'verbosity': verbosity,
                                                     'failfast': failfast})])
        self.assertEqual(FakeRunner.runArgs, [test])
        self.assertEqual(p.result, result)

        self.assertEqual(signal.getsignal(signal.SIGINT), default_handler)

        FakeRunner.initArgs = []
        FakeRunner.runArgs = []
        p = Program(True)
        p.runTests()

        self.assertEqual(FakeRunner.initArgs, [((), {'buffer': None,
                                                     'verbosity': verbosity,
                                                     'failfast': failfast})])
        self.assertEqual(FakeRunner.runArgs, [test])
        self.assertEqual(p.result, result)

        self.assertNotEqual(signal.getsignal(signal.SIGINT), default_handler)

    def testRemoveHandler(self):
        default_handler = signal.getsignal(signal.SIGINT)
        unittest.installHandler()
        unittest.removeHandler()
        self.assertEqual(signal.getsignal(signal.SIGINT), default_handler)

        # check that calling removeHandler multiple times has no ill-effect
        unittest.removeHandler()
        self.assertEqual(signal.getsignal(signal.SIGINT), default_handler)

    def testRemoveHandlerAsDecorator(self):
        default_handler = signal.getsignal(signal.SIGINT)
        unittest.installHandler()

        @unittest.removeHandler
        def test():
            self.assertEqual(signal.getsignal(signal.SIGINT), default_handler)

        test()
        self.assertNotEqual(signal.getsignal(signal.SIGINT), default_handler)

@unittest.skipUnless(hasattr(os, 'kill'), "Test requires os.kill")
@unittest.skipIf(sys.platform =="win32", "Test cannot run on Windows")
@unittest.skipIf(sys.platform == 'freebsd6', "Test kills regrtest on freebsd6 "
    "if threads have been used")
class TestBreakDefaultIntHandler(TestBreak):
    int_handler = signal.default_int_handler

@unittest.skipUnless(hasattr(os, 'kill'), "Test requires os.kill")
@unittest.skipIf(sys.platform =="win32", "Test cannot run on Windows")
@unittest.skipIf(sys.platform == 'freebsd6', "Test kills regrtest on freebsd6 "
    "if threads have been used")
class TestBreakSignalIgnored(TestBreak):
    int_handler = signal.SIG_IGN

@unittest.skipUnless(hasattr(os, 'kill'), "Test requires os.kill")
@unittest.skipIf(sys.platform =="win32", "Test cannot run on Windows")
@unittest.skipIf(sys.platform == 'freebsd6', "Test kills regrtest on freebsd6 "
    "if threads have been used")
class TestBreakSignalDefault(TestBreak):
    int_handler = signal.SIG_DFL
