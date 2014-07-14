"""Test case implementation"""

import collections
import sys
import functools
import difflib
import pprint
import re
import types
import warnings

from . import result
from .util import (
    strclass, safe_repr, unorderable_list_difference,
    _count_diff_all_purpose, _count_diff_hashable
)


__unittest = True


DIFF_OMITTED = ('\nDiff is %s characters long. '
                 'Set self.maxDiff to None to see it.')

class SkipTest(Exception):
    """
    Raise this exception in a test to skip it.

    Usually you can use TestCase.skipTest() or one of the skipping decorators
    instead of raising this directly.
    """
    pass

class _ExpectedFailure(Exception):
    """
    Raise this when a test is expected to fail.

    This is an implementation detail.
    """

    def __init__(self, exc_info):
        super(_ExpectedFailure, self).__init__()
        self.exc_info = exc_info

class _UnexpectedSuccess(Exception):
    """
    The test was supposed to fail, but it didn't!
    """
    pass

def _id(obj):
    return obj

def skip(reason):
    """
    Unconditionally skip a test.
    """
    def decorator(test_item):
        if not isinstance(test_item, (type, types.ClassType)):
            @functools.wraps(test_item)
            def skip_wrapper(*args, **kwargs):
                raise SkipTest(reason)
            test_item = skip_wrapper

        test_item.__unittest_skip__ = True
        test_item.__unittest_skip_why__ = reason
        return test_item
    return decorator

def skipIf(condition, reason):
    """
    Skip a test if the condition is true.
    """
    if condition:
        return skip(reason)
    return _id

def skipUnless(condition, reason):
    """
    Skip a test unless the condition is true.
    """
    if not condition:
        return skip(reason)
    return _id


def expectedFailure(func):
    @functools.wraps(func)
    def wrapper(*args, **kwargs):
        try:
            func(*args, **kwargs)
        except Exception:
            raise _ExpectedFailure(sys.exc_info())
        raise _UnexpectedSuccess
    return wrapper


class _AssertRaisesContext(object):
    """A context manager used to implement TestCase.assertRaises* methods."""

    def __init__(self, expected, test_case, expected_regexp=None):
        self.expected = expected
        self.failureException = test_case.failureException
        self.expected_regexp = expected_regexp

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_value, tb):
        if exc_type is None:
            try:
                exc_name = self.expected.__name__
            except AttributeError:
                exc_name = str(self.expected)
            raise self.failureException(
                "{0} not raised".format(exc_name))
        if not issubclass(exc_type, self.expected):
            # let unexpected exceptions pass through
            return False
        self.exception = exc_value # store for later retrieval
        if self.expected_regexp is None:
            return True

        expected_regexp = self.expected_regexp
        if not expected_regexp.search(str(exc_value)):
            raise self.failureException('"%s" does not match "%s"' %
                     (expected_regexp.pattern, str(exc_value)))
        return True


class TestCase(object):
    """A class whose instances are single test cases.

    By default, the test code itself should be placed in a method named
    'runTest'.

    If the fixture may be used for many test cases, create as
    many test methods as are needed. When instantiating such a TestCase
    subclass, specify in the constructor arguments the name of the test method
    that the instance is to execute.

    Test authors should subclass TestCase for their own tests. Construction
    and deconstruction of the test's environment ('fixture') can be
    implemented by overriding the 'setUp' and 'tearDown' methods respectively.

    If it is necessary to override the __init__ method, the base class
    __init__ method must always be called. It is important that subclasses
    should not change the signature of their __init__ method, since instances
    of the classes are instantiated automatically by parts of the framework
    in order to be run.

    When subclassing TestCase, you can set these attributes:
    * failureException: determines which exception will be raised when
        the instance's assertion methods fail; test methods raising this
        exception will be deemed to have 'failed' rather than 'errored'.
    * longMessage: determines whether long messages (including repr of
        objects used in assert methods) will be printed on failure in *addition*
        to any explicit message passed.
    * maxDiff: sets the maximum length of a diff in failure messages
        by assert methods using difflib. It is looked up as an instance
        attribute so can be configured by individual tests if required.
    """

    failureException = AssertionError

    longMessage = False

    maxDiff = 80*8

    # If a string is longer than _diffThreshold, use normal comparison instead
    # of difflib.  See #11763.
    _diffThreshold = 2**16

    # Attribute used by TestSuite for classSetUp

    _classSetupFailed = False

    def __init__(self, methodName='runTest'):
        """Create an instance of the class that will use the named test
           method when executed. Raises a ValueError if the instance does
           not have a method with the specified name.
        """
        self._testMethodName = methodName
        self._resultForDoCleanups = None
        try:
            testMethod = getattr(self, methodName)
        except AttributeError:
            raise ValueError("no such test method in %s: %s" %
                  (self.__class__, methodName))
        self._testMethodDoc = testMethod.__doc__
        self._cleanups = []

        # Map types to custom assertEqual functions that will compare
        # instances of said type in more detail to generate a more useful
        # error message.
        self._type_equality_funcs = {}
        self.addTypeEqualityFunc(dict, 'assertDictEqual')
        self.addTypeEqualityFunc(list, 'assertListEqual')
        self.addTypeEqualityFunc(tuple, 'assertTupleEqual')
        self.addTypeEqualityFunc(set, 'assertSetEqual')
        self.addTypeEqualityFunc(frozenset, 'assertSetEqual')
        try:
            self.addTypeEqualityFunc(unicode, 'assertMultiLineEqual')
        except NameError:
            # No unicode support in this build
            pass

    def addTypeEqualityFunc(self, typeobj, function):
        """Add a type specific assertEqual style function to compare a type.

        This method is for use by TestCase subclasses that need to register
        their own type equality functions to provide nicer error messages.

        Args:
            typeobj: The data type to call this function on when both values
                    are of the same type in assertEqual().
            function: The callable taking two arguments and an optional
                    msg= argument that raises self.failureException with a
                    useful error message when the two arguments are not equal.
        """
        self._type_equality_funcs[typeobj] = function

    def addCleanup(self, function, *args, **kwargs):
        """Add a function, with arguments, to be called when the test is
        completed. Functions added are called on a LIFO basis and are
        called after tearDown on test failure or success.

        Cleanup items are called even if setUp fails (unlike tearDown)."""
        self._cleanups.append((function, args, kwargs))

    def setUp(self):
        "Hook method for setting up the test fixture before exercising it."
        pass

    def tearDown(self):
        "Hook method for deconstructing the test fixture after testing it."
        pass

    @classmethod
    def setUpClass(cls):
        "Hook method for setting up class fixture before running tests in the class."

    @classmethod
    def tearDownClass(cls):
        "Hook method for deconstructing the class fixture after running all tests in the class."

    def countTestCases(self):
        return 1

    def defaultTestResult(self):
        return result.TestResult()

    def shortDescription(self):
        """Returns a one-line description of the test, or None if no
        description has been provided.

        The default implementation of this method returns the first line of
        the specified test method's docstring.
        """
        doc = self._testMethodDoc
        return doc and doc.split("\n")[0].strip() or None


    def id(self):
        return "%s.%s" % (strclass(self.__class__), self._testMethodName)

    def __eq__(self, other):
        if type(self) is not type(other):
            return NotImplemented

        return self._testMethodName == other._testMethodName

    def __ne__(self, other):
        return not self == other

    def __hash__(self):
        return hash((type(self), self._testMethodName))

    def __str__(self):
        return "%s (%s)" % (self._testMethodName, strclass(self.__class__))

    def __repr__(self):
        return "<%s testMethod=%s>" % \
               (strclass(self.__class__), self._testMethodName)

    def _addSkip(self, result, reason):
        addSkip = getattr(result, 'addSkip', None)
        if addSkip is not None:
            addSkip(self, reason)
        else:
            warnings.warn("TestResult has no addSkip method, skips not reported",
                          RuntimeWarning, 2)
            result.addSuccess(self)

    def run(self, result=None):
        orig_result = result
        if result is None:
            result = self.defaultTestResult()
            startTestRun = getattr(result, 'startTestRun', None)
            if startTestRun is not None:
                startTestRun()

        self._resultForDoCleanups = result
        result.startTest(self)

        testMethod = getattr(self, self._testMethodName)
        if (getattr(self.__class__, "__unittest_skip__", False) or
            getattr(testMethod, "__unittest_skip__", False)):
            # If the class or method was skipped.
            try:
                skip_why = (getattr(self.__class__, '__unittest_skip_why__', '')
                            or getattr(testMethod, '__unittest_skip_why__', ''))
                self._addSkip(result, skip_why)
            finally:
                result.stopTest(self)
            return
        try:
            success = False
            try:
                self.setUp()
            except SkipTest as e:
                self._addSkip(result, str(e))
            except KeyboardInterrupt:
                raise
            except:
                result.addError(self, sys.exc_info())
            else:
                try:
                    testMethod()
                except KeyboardInterrupt:
                    raise
                except self.failureException:
                    result.addFailure(self, sys.exc_info())
                except _ExpectedFailure as e:
                    addExpectedFailure = getattr(result, 'addExpectedFailure', None)
                    if addExpectedFailure is not None:
                        addExpectedFailure(self, e.exc_info)
                    else:
                        warnings.warn("TestResult has no addExpectedFailure method, reporting as passes",
                                      RuntimeWarning)
                        result.addSuccess(self)
                except _UnexpectedSuccess:
                    addUnexpectedSuccess = getattr(result, 'addUnexpectedSuccess', None)
                    if addUnexpectedSuccess is not None:
                        addUnexpectedSuccess(self)
                    else:
                        warnings.warn("TestResult has no addUnexpectedSuccess method, reporting as failures",
                                      RuntimeWarning)
                        result.addFailure(self, sys.exc_info())
                except SkipTest as e:
                    self._addSkip(result, str(e))
                except:
                    result.addError(self, sys.exc_info())
                else:
                    success = True

                try:
                    self.tearDown()
                except KeyboardInterrupt:
                    raise
                except:
                    result.addError(self, sys.exc_info())
                    success = False

            cleanUpSuccess = self.doCleanups()
            success = success and cleanUpSuccess
            if success:
                result.addSuccess(self)
        finally:
            result.stopTest(self)
            if orig_result is None:
                stopTestRun = getattr(result, 'stopTestRun', None)
                if stopTestRun is not None:
                    stopTestRun()

    def doCleanups(self):
        """Execute all cleanup functions. Normally called for you after
        tearDown."""
        result = self._resultForDoCleanups
        ok = True
        while self._cleanups:
            function, args, kwargs = self._cleanups.pop(-1)
            try:
                function(*args, **kwargs)
            except KeyboardInterrupt:
                raise
            except:
                ok = False
                result.addError(self, sys.exc_info())
        return ok

    def __call__(self, *args, **kwds):
        return self.run(*args, **kwds)

    def debug(self):
        """Run the test without collecting errors in a TestResult"""
        self.setUp()
        getattr(self, self._testMethodName)()
        self.tearDown()
        while self._cleanups:
            function, args, kwargs = self._cleanups.pop(-1)
            function(*args, **kwargs)

    def skipTest(self, reason):
        """Skip this test."""
        raise SkipTest(reason)

    def fail(self, msg=None):
        """Fail immediately, with the given message."""
        raise self.failureException(msg)

    def assertFalse(self, expr, msg=None):
        """Check that the expression is false."""
        if expr:
            msg = self._formatMessage(msg, "%s is not false" % safe_repr(expr))
            raise self.failureException(msg)

    def assertTrue(self, expr, msg=None):
        """Check that the expression is true."""
        if not expr:
            msg = self._formatMessage(msg, "%s is not true" % safe_repr(expr))
            raise self.failureException(msg)

    def _formatMessage(self, msg, standardMsg):
        """Honour the longMessage attribute when generating failure messages.
        If longMessage is False this means:
        * Use only an explicit message if it is provided
        * Otherwise use the standard message for the assert

        If longMessage is True:
        * Use the standard message
        * If an explicit message is provided, plus ' : ' and the explicit message
        """
        if not self.longMessage:
            return msg or standardMsg
        if msg is None:
            return standardMsg
        try:
            # don't switch to '{}' formatting in Python 2.X
            # it changes the way unicode input is handled
            return '%s : %s' % (standardMsg, msg)
        except UnicodeDecodeError:
            return  '%s : %s' % (safe_repr(standardMsg), safe_repr(msg))


    def assertRaises(self, excClass, callableObj=None, *args, **kwargs):
        """Fail unless an exception of class excClass is raised
           by callableObj when invoked with arguments args and keyword
           arguments kwargs. If a different type of exception is
           raised, it will not be caught, and the test case will be
           deemed to have suffered an error, exactly as for an
           unexpected exception.

           If called with callableObj omitted or None, will return a
           context object used like this::

                with self.assertRaises(SomeException):
                    do_something()

           The context manager keeps a reference to the exception as
           the 'exception' attribute. This allows you to inspect the
           exception after the assertion::

               with self.assertRaises(SomeException) as cm:
                   do_something()
               the_exception = cm.exception
               self.assertEqual(the_exception.error_code, 3)
        """
        context = _AssertRaisesContext(excClass, self)
        if callableObj is None:
            return context
        with context:
            callableObj(*args, **kwargs)

    def _getAssertEqualityFunc(self, first, second):
        """Get a detailed comparison function for the types of the two args.

        Returns: A callable accepting (first, second, msg=None) that will
        raise a failure exception if first != second with a useful human
        readable error message for those types.
        """
        #
        # NOTE(gregory.p.smith): I considered isinstance(first, type(second))
        # and vice versa.  I opted for the conservative approach in case
        # subclasses are not intended to be compared in detail to their super
        # class instances using a type equality func.  This means testing
        # subtypes won't automagically use the detailed comparison.  Callers
        # should use their type specific assertSpamEqual method to compare
        # subclasses if the detailed comparison is desired and appropriate.
        # See the discussion in http://bugs.python.org/issue2578.
        #
        if type(first) is type(second):
            asserter = self._type_equality_funcs.get(type(first))
            if asserter is not None:
                if isinstance(asserter, basestring):
                    asserter = getattr(self, asserter)
                return asserter

        return self._baseAssertEqual

    def _baseAssertEqual(self, first, second, msg=None):
        """The default assertEqual implementation, not type specific."""
        if not first == second:
            standardMsg = '%s != %s' % (safe_repr(first), safe_repr(second))
            msg = self._formatMessage(msg, standardMsg)
            raise self.failureException(msg)

    def assertEqual(self, first, second, msg=None):
        """Fail if the two objects are unequal as determined by the '=='
           operator.
        """
        assertion_func = self._getAssertEqualityFunc(first, second)
        assertion_func(first, second, msg=msg)

    def assertNotEqual(self, first, second, msg=None):
        """Fail if the two objects are equal as determined by the '!='
           operator.
        """
        if not first != second:
            msg = self._formatMessage(msg, '%s == %s' % (safe_repr(first),
                                                          safe_repr(second)))
            raise self.failureException(msg)


    def assertAlmostEqual(self, first, second, places=None, msg=None, delta=None):
        """Fail if the two objects are unequal as determined by their
           difference rounded to the given number of decimal places
           (default 7) and comparing to zero, or by comparing that the
           between the two objects is more than the given delta.

           Note that decimal places (from zero) are usually not the same
           as significant digits (measured from the most signficant digit).

           If the two objects compare equal then they will automatically
           compare almost equal.
        """
        if first == second:
            # shortcut
            return
        if delta is not None and places is not None:
            raise TypeError("specify delta or places not both")

        if delta is not None:
            if abs(first - second) <= delta:
                return

            standardMsg = '%s != %s within %s delta' % (safe_repr(first),
                                                        safe_repr(second),
                                                        safe_repr(delta))
        else:
            if places is None:
                places = 7

            if round(abs(second-first), places) == 0:
                return

            standardMsg = '%s != %s within %r places' % (safe_repr(first),
                                                          safe_repr(second),
                                                          places)
        msg = self._formatMessage(msg, standardMsg)
        raise self.failureException(msg)

    def assertNotAlmostEqual(self, first, second, places=None, msg=None, delta=None):
        """Fail if the two objects are equal as determined by their
           difference rounded to the given number of decimal places
           (default 7) and comparing to zero, or by comparing that the
           between the two objects is less than the given delta.

           Note that decimal places (from zero) are usually not the same
           as significant digits (measured from the most signficant digit).

           Objects that are equal automatically fail.
        """
        if delta is not None and places is not None:
            raise TypeError("specify delta or places not both")
        if delta is not None:
            if not (first == second) and abs(first - second) > delta:
                return
            standardMsg = '%s == %s within %s delta' % (safe_repr(first),
                                                        safe_repr(second),
                                                        safe_repr(delta))
        else:
            if places is None:
                places = 7
            if not (first == second) and round(abs(second-first), places) != 0:
                return
            standardMsg = '%s == %s within %r places' % (safe_repr(first),
                                                         safe_repr(second),
                                                         places)

        msg = self._formatMessage(msg, standardMsg)
        raise self.failureException(msg)

    # Synonyms for assertion methods

    # The plurals are undocumented.  Keep them that way to discourage use.
    # Do not add more.  Do not remove.
    # Going through a deprecation cycle on these would annoy many people.
    assertEquals = assertEqual
    assertNotEquals = assertNotEqual
    assertAlmostEquals = assertAlmostEqual
    assertNotAlmostEquals = assertNotAlmostEqual
    assert_ = assertTrue

    # These fail* assertion method names are pending deprecation and will
    # be a DeprecationWarning in 3.2; http://bugs.python.org/issue2578
    def _deprecate(original_func):
        def deprecated_func(*args, **kwargs):
            warnings.warn(
                'Please use {0} instead.'.format(original_func.__name__),
                PendingDeprecationWarning, 2)
            return original_func(*args, **kwargs)
        return deprecated_func

    failUnlessEqual = _deprecate(assertEqual)
    failIfEqual = _deprecate(assertNotEqual)
    failUnlessAlmostEqual = _deprecate(assertAlmostEqual)
    failIfAlmostEqual = _deprecate(assertNotAlmostEqual)
    failUnless = _deprecate(assertTrue)
    failUnlessRaises = _deprecate(assertRaises)
    failIf = _deprecate(assertFalse)

    def assertSequenceEqual(self, seq1, seq2, msg=None, seq_type=None):
        """An equality assertion for ordered sequences (like lists and tuples).

        For the purposes of this function, a valid ordered sequence type is one
        which can be indexed, has a length, and has an equality operator.

        Args:
            seq1: The first sequence to compare.
            seq2: The second sequence to compare.
            seq_type: The expected datatype of the sequences, or None if no
                    datatype should be enforced.
            msg: Optional message to use on failure instead of a list of
                    differences.
        """
        if seq_type is not None:
            seq_type_name = seq_type.__name__
            if not isinstance(seq1, seq_type):
                raise self.failureException('First sequence is not a %s: %s'
                                        % (seq_type_name, safe_repr(seq1)))
            if not isinstance(seq2, seq_type):
                raise self.failureException('Second sequence is not a %s: %s'
                                        % (seq_type_name, safe_repr(seq2)))
        else:
            seq_type_name = "sequence"

        differing = None
        try:
            len1 = len(seq1)
        except (TypeError, NotImplementedError):
            differing = 'First %s has no length.    Non-sequence?' % (
                    seq_type_name)

        if differing is None:
            try:
                len2 = len(seq2)
            except (TypeError, NotImplementedError):
                differing = 'Second %s has no length.    Non-sequence?' % (
                        seq_type_name)

        if differing is None:
            if seq1 == seq2:
                return

            seq1_repr = safe_repr(seq1)
            seq2_repr = safe_repr(seq2)
            if len(seq1_repr) > 30:
                seq1_repr = seq1_repr[:30] + '...'
            if len(seq2_repr) > 30:
                seq2_repr = seq2_repr[:30] + '...'
            elements = (seq_type_name.capitalize(), seq1_repr, seq2_repr)
            differing = '%ss differ: %s != %s\n' % elements

            for i in xrange(min(len1, len2)):
                try:
                    item1 = seq1[i]
                except (TypeError, IndexError, NotImplementedError):
                    differing += ('\nUnable to index element %d of first %s\n' %
                                 (i, seq_type_name))
                    break

                try:
                    item2 = seq2[i]
                except (TypeError, IndexError, NotImplementedError):
                    differing += ('\nUnable to index element %d of second %s\n' %
                                 (i, seq_type_name))
                    break

                if item1 != item2:
                    differing += ('\nFirst differing element %d:\n%s\n%s\n' %
                                 (i, item1, item2))
                    break
            else:
                if (len1 == len2 and seq_type is None and
                    type(seq1) != type(seq2)):
                    # The sequences are the same, but have differing types.
                    return

            if len1 > len2:
                differing += ('\nFirst %s contains %d additional '
                             'elements.\n' % (seq_type_name, len1 - len2))
                try:
                    differing += ('First extra element %d:\n%s\n' %
                                  (len2, seq1[len2]))
                except (TypeError, IndexError, NotImplementedError):
                    differing += ('Unable to index element %d '
                                  'of first %s\n' % (len2, seq_type_name))
            elif len1 < len2:
                differing += ('\nSecond %s contains %d additional '
                             'elements.\n' % (seq_type_name, len2 - len1))
                try:
                    differing += ('First extra element %d:\n%s\n' %
                                  (len1, seq2[len1]))
                except (TypeError, IndexError, NotImplementedError):
                    differing += ('Unable to index element %d '
                                  'of second %s\n' % (len1, seq_type_name))
        standardMsg = differing
        diffMsg = '\n' + '\n'.join(
            difflib.ndiff(pprint.pformat(seq1).splitlines(),
                          pprint.pformat(seq2).splitlines()))
        standardMsg = self._truncateMessage(standardMsg, diffMsg)
        msg = self._formatMessage(msg, standardMsg)
        self.fail(msg)

    def _truncateMessage(self, message, diff):
        max_diff = self.maxDiff
        if max_diff is None or len(diff) <= max_diff:
            return message + diff
        return message + (DIFF_OMITTED % len(diff))

    def assertListEqual(self, list1, list2, msg=None):
        """A list-specific equality assertion.

        Args:
            list1: The first list to compare.
            list2: The second list to compare.
            msg: Optional message to use on failure instead of a list of
                    differences.

        """
        self.assertSequenceEqual(list1, list2, msg, seq_type=list)

    def assertTupleEqual(self, tuple1, tuple2, msg=None):
        """A tuple-specific equality assertion.

        Args:
            tuple1: The first tuple to compare.
            tuple2: The second tuple to compare.
            msg: Optional message to use on failure instead of a list of
                    differences.
        """
        self.assertSequenceEqual(tuple1, tuple2, msg, seq_type=tuple)

    def assertSetEqual(self, set1, set2, msg=None):
        """A set-specific equality assertion.

        Args:
            set1: The first set to compare.
            set2: The second set to compare.
            msg: Optional message to use on failure instead of a list of
                    differences.

        assertSetEqual uses ducktyping to support different types of sets, and
        is optimized for sets specifically (parameters must support a
        difference method).
        """
        try:
            difference1 = set1.difference(set2)
        except TypeError, e:
            self.fail('invalid type when attempting set difference: %s' % e)
        except AttributeError, e:
            self.fail('first argument does not support set difference: %s' % e)

        try:
            difference2 = set2.difference(set1)
        except TypeError, e:
            self.fail('invalid type when attempting set difference: %s' % e)
        except AttributeError, e:
            self.fail('second argument does not support set difference: %s' % e)

        if not (difference1 or difference2):
            return

        lines = []
        if difference1:
            lines.append('Items in the first set but not the second:')
            for item in difference1:
                lines.append(repr(item))
        if difference2:
            lines.append('Items in the second set but not the first:')
            for item in difference2:
                lines.append(repr(item))

        standardMsg = '\n'.join(lines)
        self.fail(self._formatMessage(msg, standardMsg))

    def assertIn(self, member, container, msg=None):
        """Just like self.assertTrue(a in b), but with a nicer default message."""
        if member not in container:
            standardMsg = '%s not found in %s' % (safe_repr(member),
                                                  safe_repr(container))
            self.fail(self._formatMessage(msg, standardMsg))

    def assertNotIn(self, member, container, msg=None):
        """Just like self.assertTrue(a not in b), but with a nicer default message."""
        if member in container:
            standardMsg = '%s unexpectedly found in %s' % (safe_repr(member),
                                                        safe_repr(container))
            self.fail(self._formatMessage(msg, standardMsg))

    def assertIs(self, expr1, expr2, msg=None):
        """Just like self.assertTrue(a is b), but with a nicer default message."""
        if expr1 is not expr2:
            standardMsg = '%s is not %s' % (safe_repr(expr1),
                                             safe_repr(expr2))
            self.fail(self._formatMessage(msg, standardMsg))

    def assertIsNot(self, expr1, expr2, msg=None):
        """Just like self.assertTrue(a is not b), but with a nicer default message."""
        if expr1 is expr2:
            standardMsg = 'unexpectedly identical: %s' % (safe_repr(expr1),)
            self.fail(self._formatMessage(msg, standardMsg))

    def assertDictEqual(self, d1, d2, msg=None):
        self.assertIsInstance(d1, dict, 'First argument is not a dictionary')
        self.assertIsInstance(d2, dict, 'Second argument is not a dictionary')

        if d1 != d2:
            standardMsg = '%s != %s' % (safe_repr(d1, True), safe_repr(d2, True))
            diff = ('\n' + '\n'.join(difflib.ndiff(
                           pprint.pformat(d1).splitlines(),
                           pprint.pformat(d2).splitlines())))
            standardMsg = self._truncateMessage(standardMsg, diff)
            self.fail(self._formatMessage(msg, standardMsg))

    def assertDictContainsSubset(self, expected, actual, msg=None):
        """Checks whether actual is a superset of expected."""
        missing = []
        mismatched = []
        for key, value in expected.iteritems():
            if key not in actual:
                missing.append(key)
            elif value != actual[key]:
                mismatched.append('%s, expected: %s, actual: %s' %
                                  (safe_repr(key), safe_repr(value),
                                   safe_repr(actual[key])))

        if not (missing or mismatched):
            return

        standardMsg = ''
        if missing:
            standardMsg = 'Missing: %s' % ','.join(safe_repr(m) for m in
                                                    missing)
        if mismatched:
            if standardMsg:
                standardMsg += '; '
            standardMsg += 'Mismatched values: %s' % ','.join(mismatched)

        self.fail(self._formatMessage(msg, standardMsg))

    def assertItemsEqual(self, expected_seq, actual_seq, msg=None):
        """An unordered sequence specific comparison. It asserts that
        actual_seq and expected_seq have the same element counts.
        Equivalent to::

            self.assertEqual(Counter(iter(actual_seq)),
                             Counter(iter(expected_seq)))

        Asserts that each element has the same count in both sequences.
        Example:
            - [0, 1, 1] and [1, 0, 1] compare equal.
            - [0, 0, 1] and [0, 1] compare unequal.
        """
        first_seq, second_seq = list(expected_seq), list(actual_seq)
        with warnings.catch_warnings():
            if sys.py3kwarning:
                # Silence Py3k warning raised during the sorting
                for _msg in ["(code|dict|type) inequality comparisons",
                             "builtin_function_or_method order comparisons",
                             "comparing unequal types"]:
                    warnings.filterwarnings("ignore", _msg, DeprecationWarning)
            try:
                first = collections.Counter(first_seq)
                second = collections.Counter(second_seq)
            except TypeError:
                # Handle case with unhashable elements
                differences = _count_diff_all_purpose(first_seq, second_seq)
            else:
                if first == second:
                    return
                differences = _count_diff_hashable(first_seq, second_seq)

        if differences:
            standardMsg = 'Element counts were not equal:\n'
            lines = ['First has %d, Second has %d:  %r' % diff for diff in differences]
            diffMsg = '\n'.join(lines)
            standardMsg = self._truncateMessage(standardMsg, diffMsg)
            msg = self._formatMessage(msg, standardMsg)
            self.fail(msg)

    def assertMultiLineEqual(self, first, second, msg=None):
        """Assert that two multi-line strings are equal."""
        self.assertIsInstance(first, basestring,
                'First argument is not a string')
        self.assertIsInstance(second, basestring,
                'Second argument is not a string')

        if first != second:
            # don't use difflib if the strings are too long
            if (len(first) > self._diffThreshold or
                len(second) > self._diffThreshold):
                self._baseAssertEqual(first, second, msg)
            firstlines = first.splitlines(True)
            secondlines = second.splitlines(True)
            if len(firstlines) == 1 and first.strip('\r\n') == first:
                firstlines = [first + '\n']
                secondlines = [second + '\n']
            standardMsg = '%s != %s' % (safe_repr(first, True),
                                        safe_repr(second, True))
            diff = '\n' + ''.join(difflib.ndiff(firstlines, secondlines))
            standardMsg = self._truncateMessage(standardMsg, diff)
            self.fail(self._formatMessage(msg, standardMsg))

    def assertLess(self, a, b, msg=None):
        """Just like self.assertTrue(a < b), but with a nicer default message."""
        if not a < b:
            standardMsg = '%s not less than %s' % (safe_repr(a), safe_repr(b))
            self.fail(self._formatMessage(msg, standardMsg))

    def assertLessEqual(self, a, b, msg=None):
        """Just like self.assertTrue(a <= b), but with a nicer default message."""
        if not a <= b:
            standardMsg = '%s not less than or equal to %s' % (safe_repr(a), safe_repr(b))
            self.fail(self._formatMessage(msg, standardMsg))

    def assertGreater(self, a, b, msg=None):
        """Just like self.assertTrue(a > b), but with a nicer default message."""
        if not a > b:
            standardMsg = '%s not greater than %s' % (safe_repr(a), safe_repr(b))
            self.fail(self._formatMessage(msg, standardMsg))

    def assertGreaterEqual(self, a, b, msg=None):
        """Just like self.assertTrue(a >= b), but with a nicer default message."""
        if not a >= b:
            standardMsg = '%s not greater than or equal to %s' % (safe_repr(a), safe_repr(b))
            self.fail(self._formatMessage(msg, standardMsg))

    def assertIsNone(self, obj, msg=None):
        """Same as self.assertTrue(obj is None), with a nicer default message."""
        if obj is not None:
            standardMsg = '%s is not None' % (safe_repr(obj),)
            self.fail(self._formatMessage(msg, standardMsg))

    def assertIsNotNone(self, obj, msg=None):
        """Included for symmetry with assertIsNone."""
        if obj is None:
            standardMsg = 'unexpectedly None'
            self.fail(self._formatMessage(msg, standardMsg))

    def assertIsInstance(self, obj, cls, msg=None):
        """Same as self.assertTrue(isinstance(obj, cls)), with a nicer
        default message."""
        if not isinstance(obj, cls):
            standardMsg = '%s is not an instance of %r' % (safe_repr(obj), cls)
            self.fail(self._formatMessage(msg, standardMsg))

    def assertNotIsInstance(self, obj, cls, msg=None):
        """Included for symmetry with assertIsInstance."""
        if isinstance(obj, cls):
            standardMsg = '%s is an instance of %r' % (safe_repr(obj), cls)
            self.fail(self._formatMessage(msg, standardMsg))

    def assertRaisesRegexp(self, expected_exception, expected_regexp,
                           callable_obj=None, *args, **kwargs):
        """Asserts that the message in a raised exception matches a regexp.

        Args:
            expected_exception: Exception class expected to be raised.
            expected_regexp: Regexp (re pattern object or string) expected
                    to be found in error message.
            callable_obj: Function to be called.
            args: Extra args.
            kwargs: Extra kwargs.
        """
        if expected_regexp is not None:
            expected_regexp = re.compile(expected_regexp)
        context = _AssertRaisesContext(expected_exception, self, expected_regexp)
        if callable_obj is None:
            return context
        with context:
            callable_obj(*args, **kwargs)

    def assertRegexpMatches(self, text, expected_regexp, msg=None):
        """Fail the test unless the text matches the regular expression."""
        if isinstance(expected_regexp, basestring):
            expected_regexp = re.compile(expected_regexp)
        if not expected_regexp.search(text):
            msg = msg or "Regexp didn't match"
            msg = '%s: %r not found in %r' % (msg, expected_regexp.pattern, text)
            raise self.failureException(msg)

    def assertNotRegexpMatches(self, text, unexpected_regexp, msg=None):
        """Fail the test if the text matches the regular expression."""
        if isinstance(unexpected_regexp, basestring):
            unexpected_regexp = re.compile(unexpected_regexp)
        match = unexpected_regexp.search(text)
        if match:
            msg = msg or "Regexp matched"
            msg = '%s: %r matches %r in %r' % (msg,
                                               text[match.start():match.end()],
                                               unexpected_regexp.pattern,
                                               text)
            raise self.failureException(msg)


class FunctionTestCase(TestCase):
    """A test case that wraps a test function.

    This is useful for slipping pre-existing test functions into the
    unittest framework. Optionally, set-up and tidy-up functions can be
    supplied. As with TestCase, the tidy-up ('tearDown') function will
    always be called if the set-up ('setUp') function ran successfully.
    """

    def __init__(self, testFunc, setUp=None, tearDown=None, description=None):
        super(FunctionTestCase, self).__init__()
        self._setUpFunc = setUp
        self._tearDownFunc = tearDown
        self._testFunc = testFunc
        self._description = description

    def setUp(self):
        if self._setUpFunc is not None:
            self._setUpFunc()

    def tearDown(self):
        if self._tearDownFunc is not None:
            self._tearDownFunc()

    def runTest(self):
        self._testFunc()

    def id(self):
        return self._testFunc.__name__

    def __eq__(self, other):
        if not isinstance(other, self.__class__):
            return NotImplemented

        return self._setUpFunc == other._setUpFunc and \
               self._tearDownFunc == other._tearDownFunc and \
               self._testFunc == other._testFunc and \
               self._description == other._description

    def __ne__(self, other):
        return not self == other

    def __hash__(self):
        return hash((type(self), self._setUpFunc, self._tearDownFunc,
                     self._testFunc, self._description))

    def __str__(self):
        return "%s (%s)" % (strclass(self.__class__),
                            self._testFunc.__name__)

    def __repr__(self):
        return "<%s tec=%s>" % (strclass(self.__class__),
                                     self._testFunc)

    def shortDescription(self):
        if self._description is not None:
            return self._description
        doc = self._testFunc.__doc__
        return doc and doc.split("\n")[0].strip() or None
