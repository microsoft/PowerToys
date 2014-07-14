import datetime

import unittest


class Test_Assertions(unittest.TestCase):
    def test_AlmostEqual(self):
        self.assertAlmostEqual(1.00000001, 1.0)
        self.assertNotAlmostEqual(1.0000001, 1.0)
        self.assertRaises(self.failureException,
                          self.assertAlmostEqual, 1.0000001, 1.0)
        self.assertRaises(self.failureException,
                          self.assertNotAlmostEqual, 1.00000001, 1.0)

        self.assertAlmostEqual(1.1, 1.0, places=0)
        self.assertRaises(self.failureException,
                          self.assertAlmostEqual, 1.1, 1.0, places=1)

        self.assertAlmostEqual(0, .1+.1j, places=0)
        self.assertNotAlmostEqual(0, .1+.1j, places=1)
        self.assertRaises(self.failureException,
                          self.assertAlmostEqual, 0, .1+.1j, places=1)
        self.assertRaises(self.failureException,
                          self.assertNotAlmostEqual, 0, .1+.1j, places=0)

        self.assertAlmostEqual(float('inf'), float('inf'))
        self.assertRaises(self.failureException, self.assertNotAlmostEqual,
                          float('inf'), float('inf'))

    def test_AmostEqualWithDelta(self):
        self.assertAlmostEqual(1.1, 1.0, delta=0.5)
        self.assertAlmostEqual(1.0, 1.1, delta=0.5)
        self.assertNotAlmostEqual(1.1, 1.0, delta=0.05)
        self.assertNotAlmostEqual(1.0, 1.1, delta=0.05)

        self.assertAlmostEqual(1.0, 1.0, delta=0.5)
        self.assertRaises(self.failureException, self.assertNotAlmostEqual,
                          1.0, 1.0, delta=0.5)

        self.assertRaises(self.failureException, self.assertAlmostEqual,
                          1.1, 1.0, delta=0.05)
        self.assertRaises(self.failureException, self.assertNotAlmostEqual,
                          1.1, 1.0, delta=0.5)

        self.assertRaises(TypeError, self.assertAlmostEqual,
                          1.1, 1.0, places=2, delta=2)
        self.assertRaises(TypeError, self.assertNotAlmostEqual,
                          1.1, 1.0, places=2, delta=2)

        first = datetime.datetime.now()
        second = first + datetime.timedelta(seconds=10)
        self.assertAlmostEqual(first, second,
                               delta=datetime.timedelta(seconds=20))
        self.assertNotAlmostEqual(first, second,
                                  delta=datetime.timedelta(seconds=5))

    def test_assertRaises(self):
        def _raise(e):
            raise e
        self.assertRaises(KeyError, _raise, KeyError)
        self.assertRaises(KeyError, _raise, KeyError("key"))
        try:
            self.assertRaises(KeyError, lambda: None)
        except self.failureException as e:
            self.assertIn("KeyError not raised", e.args)
        else:
            self.fail("assertRaises() didn't fail")
        try:
            self.assertRaises(KeyError, _raise, ValueError)
        except ValueError:
            pass
        else:
            self.fail("assertRaises() didn't let exception pass through")
        with self.assertRaises(KeyError) as cm:
            try:
                raise KeyError
            except Exception, e:
                raise
        self.assertIs(cm.exception, e)

        with self.assertRaises(KeyError):
            raise KeyError("key")
        try:
            with self.assertRaises(KeyError):
                pass
        except self.failureException as e:
            self.assertIn("KeyError not raised", e.args)
        else:
            self.fail("assertRaises() didn't fail")
        try:
            with self.assertRaises(KeyError):
                raise ValueError
        except ValueError:
            pass
        else:
            self.fail("assertRaises() didn't let exception pass through")

    def testAssertNotRegexpMatches(self):
        self.assertNotRegexpMatches('Ala ma kota', r'r+')
        try:
            self.assertNotRegexpMatches('Ala ma kota', r'k.t', 'Message')
        except self.failureException, e:
            self.assertIn("'kot'", e.args[0])
            self.assertIn('Message', e.args[0])
        else:
            self.fail('assertNotRegexpMatches should have failed.')


class TestLongMessage(unittest.TestCase):
    """Test that the individual asserts honour longMessage.
    This actually tests all the message behaviour for
    asserts that use longMessage."""

    def setUp(self):
        class TestableTestFalse(unittest.TestCase):
            longMessage = False
            failureException = self.failureException

            def testTest(self):
                pass

        class TestableTestTrue(unittest.TestCase):
            longMessage = True
            failureException = self.failureException

            def testTest(self):
                pass

        self.testableTrue = TestableTestTrue('testTest')
        self.testableFalse = TestableTestFalse('testTest')

    def testDefault(self):
        self.assertFalse(unittest.TestCase.longMessage)

    def test_formatMsg(self):
        self.assertEqual(self.testableFalse._formatMessage(None, "foo"), "foo")
        self.assertEqual(self.testableFalse._formatMessage("foo", "bar"), "foo")

        self.assertEqual(self.testableTrue._formatMessage(None, "foo"), "foo")
        self.assertEqual(self.testableTrue._formatMessage("foo", "bar"), "bar : foo")

        # This blows up if _formatMessage uses string concatenation
        self.testableTrue._formatMessage(object(), 'foo')

    def test_formatMessage_unicode_error(self):
        one = ''.join(chr(i) for i in range(255))
        # this used to cause a UnicodeDecodeError constructing msg
        self.testableTrue._formatMessage(one, u'\uFFFD')

    def assertMessages(self, methodName, args, errors):
        def getMethod(i):
            useTestableFalse  = i < 2
            if useTestableFalse:
                test = self.testableFalse
            else:
                test = self.testableTrue
            return getattr(test, methodName)

        for i, expected_regexp in enumerate(errors):
            testMethod = getMethod(i)
            kwargs = {}
            withMsg = i % 2
            if withMsg:
                kwargs = {"msg": "oops"}

            with self.assertRaisesRegexp(self.failureException,
                                         expected_regexp=expected_regexp):
                testMethod(*args, **kwargs)

    def testAssertTrue(self):
        self.assertMessages('assertTrue', (False,),
                            ["^False is not true$", "^oops$", "^False is not true$",
                             "^False is not true : oops$"])

    def testAssertFalse(self):
        self.assertMessages('assertFalse', (True,),
                            ["^True is not false$", "^oops$", "^True is not false$",
                             "^True is not false : oops$"])

    def testNotEqual(self):
        self.assertMessages('assertNotEqual', (1, 1),
                            ["^1 == 1$", "^oops$", "^1 == 1$",
                             "^1 == 1 : oops$"])

    def testAlmostEqual(self):
        self.assertMessages('assertAlmostEqual', (1, 2),
                            ["^1 != 2 within 7 places$", "^oops$",
                             "^1 != 2 within 7 places$", "^1 != 2 within 7 places : oops$"])

    def testNotAlmostEqual(self):
        self.assertMessages('assertNotAlmostEqual', (1, 1),
                            ["^1 == 1 within 7 places$", "^oops$",
                             "^1 == 1 within 7 places$", "^1 == 1 within 7 places : oops$"])

    def test_baseAssertEqual(self):
        self.assertMessages('_baseAssertEqual', (1, 2),
                            ["^1 != 2$", "^oops$", "^1 != 2$", "^1 != 2 : oops$"])

    def testAssertSequenceEqual(self):
        # Error messages are multiline so not testing on full message
        # assertTupleEqual and assertListEqual delegate to this method
        self.assertMessages('assertSequenceEqual', ([], [None]),
                            ["\+ \[None\]$", "^oops$", r"\+ \[None\]$",
                             r"\+ \[None\] : oops$"])

    def testAssertSetEqual(self):
        self.assertMessages('assertSetEqual', (set(), set([None])),
                            ["None$", "^oops$", "None$",
                             "None : oops$"])

    def testAssertIn(self):
        self.assertMessages('assertIn', (None, []),
                            ['^None not found in \[\]$', "^oops$",
                             '^None not found in \[\]$',
                             '^None not found in \[\] : oops$'])

    def testAssertNotIn(self):
        self.assertMessages('assertNotIn', (None, [None]),
                            ['^None unexpectedly found in \[None\]$', "^oops$",
                             '^None unexpectedly found in \[None\]$',
                             '^None unexpectedly found in \[None\] : oops$'])

    def testAssertDictEqual(self):
        self.assertMessages('assertDictEqual', ({}, {'key': 'value'}),
                            [r"\+ \{'key': 'value'\}$", "^oops$",
                             "\+ \{'key': 'value'\}$",
                             "\+ \{'key': 'value'\} : oops$"])

    def testAssertDictContainsSubset(self):
        self.assertMessages('assertDictContainsSubset', ({'key': 'value'}, {}),
                            ["^Missing: 'key'$", "^oops$",
                             "^Missing: 'key'$",
                             "^Missing: 'key' : oops$"])

    def testAssertMultiLineEqual(self):
        self.assertMessages('assertMultiLineEqual', ("", "foo"),
                            [r"\+ foo$", "^oops$",
                             r"\+ foo$",
                             r"\+ foo : oops$"])

    def testAssertLess(self):
        self.assertMessages('assertLess', (2, 1),
                            ["^2 not less than 1$", "^oops$",
                             "^2 not less than 1$", "^2 not less than 1 : oops$"])

    def testAssertLessEqual(self):
        self.assertMessages('assertLessEqual', (2, 1),
                            ["^2 not less than or equal to 1$", "^oops$",
                             "^2 not less than or equal to 1$",
                             "^2 not less than or equal to 1 : oops$"])

    def testAssertGreater(self):
        self.assertMessages('assertGreater', (1, 2),
                            ["^1 not greater than 2$", "^oops$",
                             "^1 not greater than 2$",
                             "^1 not greater than 2 : oops$"])

    def testAssertGreaterEqual(self):
        self.assertMessages('assertGreaterEqual', (1, 2),
                            ["^1 not greater than or equal to 2$", "^oops$",
                             "^1 not greater than or equal to 2$",
                             "^1 not greater than or equal to 2 : oops$"])

    def testAssertIsNone(self):
        self.assertMessages('assertIsNone', ('not None',),
                            ["^'not None' is not None$", "^oops$",
                             "^'not None' is not None$",
                             "^'not None' is not None : oops$"])

    def testAssertIsNotNone(self):
        self.assertMessages('assertIsNotNone', (None,),
                            ["^unexpectedly None$", "^oops$",
                             "^unexpectedly None$",
                             "^unexpectedly None : oops$"])

    def testAssertIs(self):
        self.assertMessages('assertIs', (None, 'foo'),
                            ["^None is not 'foo'$", "^oops$",
                             "^None is not 'foo'$",
                             "^None is not 'foo' : oops$"])

    def testAssertIsNot(self):
        self.assertMessages('assertIsNot', (None, None),
                            ["^unexpectedly identical: None$", "^oops$",
                             "^unexpectedly identical: None$",
                             "^unexpectedly identical: None : oops$"])


if __name__ == '__main__':
    unittest.main()
