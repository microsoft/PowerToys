namespace Mages.Core.Tests
{
    using Mages.Core.Source;
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class ScannerTests
    {
        [Test]
        public void ScannerWithEmptySourceOneAdvanced()
        {
            var scanner = new StringScanner(String.Empty);
            Assert.IsFalse(scanner.MoveNext());
            Assert.AreEqual(1, scanner.Position.Column);
            Assert.AreEqual(1, scanner.Position.Row);
            Assert.AreEqual(CharacterTable.End, scanner.Current);
        }

        [Test]
        public void ScannerWithEmptySourceTwoAdvanced()
        {
            var scanner = new StringScanner(String.Empty);
            Assert.IsFalse(scanner.MoveNext());
            Assert.IsFalse(scanner.MoveNext());
            Assert.AreEqual(1, scanner.Position.Column);
            Assert.AreEqual(1, scanner.Position.Row);
            Assert.AreEqual(CharacterTable.End, scanner.Current);
        }

        [Test]
        public void ScannerWithEmptySourceThreeAdvanced()
        {
            var scanner = new StringScanner(String.Empty);
            Assert.IsFalse(scanner.MoveNext());
            Assert.IsFalse(scanner.MoveNext());
            Assert.IsFalse(scanner.MoveNext());
            Assert.AreEqual(1, scanner.Position.Column);
            Assert.AreEqual(1, scanner.Position.Row);
            Assert.AreEqual(CharacterTable.End, scanner.Current);
        }

        [Test]
        public void ScannerWithEmptySourceNotAdvanced()
        {
            var scanner = new StringScanner(String.Empty);
            Assert.AreEqual(0, scanner.Position.Column);
            Assert.AreEqual(1, scanner.Position.Row);
            Assert.AreEqual(CharacterTable.NullPtr, scanner.Current);
        }

        [Test]
        public void ScannerWithSimpleSource()
        {
            var source = "abcdefgh";
            var scanner = new StringScanner(source);

            for (int i = 0; i < source.Length; i++)
            {
                Assert.IsTrue(scanner.MoveNext());
                Assert.AreEqual(1 + i, scanner.Position.Column);
                Assert.AreEqual(1, scanner.Position.Row);
                Assert.AreEqual((Int32)source[i], scanner.Current);
            }

            Assert.IsFalse(scanner.MoveNext());
            Assert.AreEqual(CharacterTable.End, scanner.Current);
        }

        [Test]
        public void ScannerWithLinebreakSource()
        {
            var source = new [] { "abcdefgh", "ijkl", "mnop" };
            var scanner = new StringScanner(String.Join(Environment.NewLine, source));

            for (int i = 0; i < source.Length; i++)
            {
                for (int j = 0; j < source[i].Length; j++)
                {
                    Assert.IsTrue(scanner.MoveNext());
                    Assert.AreEqual(1 + j, scanner.Position.Column);
                    Assert.AreEqual(1 + i, scanner.Position.Row);
                    Assert.AreEqual((Int32)source[i][j], scanner.Current);
                }

                if (i < source.Length - 1)
                {
                    Assert.IsTrue(scanner.MoveNext());
                    Assert.AreEqual((Int32)'\n', scanner.Current);
                }
            }

            Assert.IsFalse(scanner.MoveNext());
            Assert.AreEqual(CharacterTable.End, scanner.Current);
        }

        [Test]
        public void ScannerWithLinebreakEndedSource()
        {
            var source = new[] { "abcdefgh", "ijkl", "mnop", String.Empty };
            var scanner = new StringScanner(String.Join(Environment.NewLine, source));

            for (int i = 0; i < source.Length; i++)
            {
                for (int j = 0; j < source[i].Length; j++)
                {
                    Assert.IsTrue(scanner.MoveNext());
                    Assert.AreEqual(1 + j, scanner.Position.Column);
                    Assert.AreEqual(1 + i, scanner.Position.Row);
                    Assert.AreEqual((Int32)source[i][j], scanner.Current);
                }

                if (i < source.Length - 1)
                {
                    Assert.IsTrue(scanner.MoveNext());
                    Assert.AreEqual((Int32)'\n', scanner.Current);
                }
            }

            Assert.IsFalse(scanner.MoveNext());
            Assert.AreEqual(CharacterTable.End, scanner.Current);
        }

        [Test]
        public void ScannerWithOnlyLinebreaksSource()
        {
            var source = new[] { String.Empty, String.Empty, String.Empty, String.Empty };
            var scanner = new StringScanner(String.Join(Environment.NewLine, source));

            Assert.IsTrue(scanner.MoveNext());
            Assert.AreEqual(1, scanner.Position.Column);
            Assert.AreEqual(1, scanner.Position.Row);
            Assert.AreEqual(CharacterTable.LineFeed, scanner.Current);

            Assert.IsTrue(scanner.MoveNext());
            Assert.AreEqual(1, scanner.Position.Column);
            Assert.AreEqual(2, scanner.Position.Row);
            Assert.AreEqual(CharacterTable.LineFeed, scanner.Current);

            Assert.IsTrue(scanner.MoveNext());
            Assert.AreEqual(1, scanner.Position.Column);
            Assert.AreEqual(3, scanner.Position.Row);
            Assert.AreEqual(CharacterTable.LineFeed, scanner.Current);

            Assert.IsFalse(scanner.MoveNext());
            Assert.AreEqual(CharacterTable.End, scanner.Current);
        }

        [Test]
        public void ScannerWithUnicodeSource()
        {
            var source = "♳♡♤♛♏♖♚♲⚄⚔⚣⛄☁";
            var scanner = new StringScanner(source);

            for (int i = 0; i < source.Length; i++)
            {
                Assert.IsTrue(scanner.MoveNext());
                Assert.AreEqual(1 + i, scanner.Position.Column);
                Assert.AreEqual(1, scanner.Position.Row);
                Assert.AreEqual((Int32)source[i], scanner.Current);
            }

            Assert.IsFalse(scanner.MoveNext());
            Assert.AreEqual(CharacterTable.End, scanner.Current);
        }
    }
}
