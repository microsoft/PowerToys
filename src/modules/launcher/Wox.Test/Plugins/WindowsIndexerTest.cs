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
        public void ModifySearchQuery_ShouldNotAddAsterisk_ForAllSpaceSeparatedStrings()
        {
            // Arrange
            string query1 = "*";
            string query2 = " *";
            string query3 = "* ";
            string query4 = "**";
            string query5 = "*  ** *** ";

            // Act
            query1 = _api.ModifySearchQuery(query1);
            query2 = _api.ModifySearchQuery(query2);
            query3 = _api.ModifySearchQuery(query3);
            query4 = _api.ModifySearchQuery(query4);
            query5 = _api.ModifySearchQuery(query5);

            // Assert
            Assert.AreEqual(query1, "*");
            Assert.AreEqual(query2, "*");
            Assert.AreEqual(query3, "*");
            Assert.AreEqual(query4, "*");
            Assert.AreEqual(query5, "*");

        }

        [Test]
        public void ModifySearchQuery_ShouldAddAsterisk_ForAllSpaceSeparatedStrings()
        {
            // Arrange
            string query1 = "test";
            string query2 = "*test";
            string query3 = " **test";
            string query4 = " *test** *";
            string query5 = "* ** test* ** ";

            // Act
            query1 = _api.ModifySearchQuery(query1);
            query2 = _api.ModifySearchQuery(query2);
            query3 = _api.ModifySearchQuery(query3);
            query4 = _api.ModifySearchQuery(query4);
            query5 = _api.ModifySearchQuery(query5);

            // Assert
            Assert.AreEqual(query1, "*test");
            Assert.AreEqual(query2, "*test");
            Assert.AreEqual(query3, "*test");
            Assert.AreEqual(query4, "*test");
            Assert.AreEqual(query5, "*test");
        }

        [Test]
        public void ModifySearchQuery_ShouldAddAsterisk_MultipleKeywords()
        {
            // Arrange
            string query1 = "test1 test2";
            string query2 = "*test1 * test2";
            string query3 = "test1 test2 test3 test4 ";

            // Act
            query1 = _api.ModifySearchQuery(query1);
            query2 = _api.ModifySearchQuery(query2);
            query3 = _api.ModifySearchQuery(query3);

            // Assert
            Assert.AreEqual(query1, "*test1 *test2");
            Assert.AreEqual(query2, "*test1 *test2");
            Assert.AreEqual(query3, "*test1 *test2 *test3 *test4");
        }

        [Test]
        public void ExecuteQuery_ShouldDisposeAllConnections_AfterFunctionCall()
        {
            // Arrange
            ISearchQueryHelper queryHelper;
            _api.InitQueryHelper(out queryHelper, 10);
            string keyword = "test";
            keyword = _api.ModifySearchQuery(keyword);
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
