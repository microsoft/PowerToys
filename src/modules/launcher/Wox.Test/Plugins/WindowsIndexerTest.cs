using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using Microsoft.Search.Interop;
using Microsoft.Plugin.Indexer.SearchHelper;

namespace Wox.Test.Plugins
{

    [TestFixture]
    public class WindowsIndexerTest
    {
        private WindowsSearchAPI _api = new WindowsSearchAPI();

        [Test]
        public void InitQueryHelper_ShouldInitialize_WhenFunctionIsCalled()
        {
            // Arrange
            int maxCount = 10;
            ISearchQueryHelper queryHelper = null;

            // Act
            _api.InitQueryHelper(out queryHelper, maxCount);

            // Assert
            Assert.IsNotNull(queryHelper);
            Assert.AreEqual(queryHelper.QueryMaxResults, maxCount);
        }

        [Test]
        public void ModifyQueryHelper_ShouldSetQueryHelper_WhenPatternIsAsterisk()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            String pattern = "*";
            _api.InitQueryHelper(out queryHelper, 10);

            // Act
            _api.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("LIKE"));
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("Contains"));
        }

        [Test]
        public void ModifyQueryHelper_ShouldSetQueryHelper_WhenPatternContainsAsterisk()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            String pattern = "tt*^&)";
            _api.InitQueryHelper(out queryHelper, 10);

            // Act
            _api.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("LIKE"));
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("Contains"));
        }

        [Test]
        public void ModifyQueryHelper_ShouldSetQueryHelper_WhenPatternContainsPercent()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            String pattern = "tt%^&)";
            _api.InitQueryHelper(out queryHelper, 10);

            // Act
            _api.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("LIKE"));
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("Contains"));
        }

        [Test]
        public void ModifyQueryHelper_ShouldSetQueryHelper_WhenPatternContainsUnderScore()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            String pattern = "tt_^&)";
            _api.InitQueryHelper(out queryHelper, 10);

            // Act
            _api.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("LIKE"));
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("Contains"));
        }

        [Test]
        public void ModifyQueryHelper_ShouldSetQueryHelper_WhenPatternContainsQuestionMark()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            String pattern = "tt?^&)";
            _api.InitQueryHelper(out queryHelper, 10);

            // Act
            _api.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("LIKE"));
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("Contains"));
        }

        [Test]
        public void ModifyQueryHelper_ShouldSetQueryHelper_WhenPatternDoesNotContainSplSymbols()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            String pattern = "tt^&)bc";
            _api.InitQueryHelper(out queryHelper, 10);

            // Act
            _api.ModifyQueryHelper(ref queryHelper, pattern);

            // Assert
            Assert.IsFalse(queryHelper.QueryWhereRestrictions.Contains("LIKE"));
            Assert.IsTrue(queryHelper.QueryWhereRestrictions.Contains("Contains"));
        }

        [Test]
        public void ExecuteQuery_ShouldDisposeAllConnections_AfterFunctionCall()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            _api.InitQueryHelper(out queryHelper, 10);
            _api.ModifyQueryHelper(ref queryHelper, "*");
            string keyword = "test";
            bool commandDisposed = false;
            bool resultDisposed = false;

            // Act
            _api.ExecuteQuery(queryHelper, keyword);
            try
            {
                _api.command.ExecuteReader();
            }
            catch(InvalidOperationException)
            {
                commandDisposed = true;
            }

            try
            {
                _api.WDSResults.Read();
            }
            catch(InvalidOperationException)
            {
                resultDisposed = true;
            }

            // Assert
            Assert.IsTrue(_api.conn.State == System.Data.ConnectionState.Closed);
            Assert.IsTrue(commandDisposed);
            Assert.IsTrue(resultDisposed);
        }
    }
}
