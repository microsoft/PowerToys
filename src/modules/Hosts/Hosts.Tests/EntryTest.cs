// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using HostsUILib.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hosts.Tests
{
    [TestClass]
    public class EntryTest
    {
        [DataTestMethod]
        [DataRow("\t\t10.1.1.1\t\thost\t\t", "10.1.1.1", "host", "", true)]
        [DataRow("  10.1.1.1  host  ", "10.1.1.1", "host", "", true)]
        [DataRow("10.1.1.1 host", "10.1.1.1", "host", "", true)]
        [DataRow("\t\t#\t\t10.1.1.1\thost\t\t", "10.1.1.1", "host", "", false)]
        [DataRow("  #  10.1.1.1  host  ", "10.1.1.1", "host", "", false)]
        [DataRow("#10.1.1.1 host", "10.1.1.1", "host", "", false)]
        [DataRow("\t\t10.1.1.1\t\thost\t\t#\t\tcomment\t\t", "10.1.1.1", "host", "comment", true)]
        [DataRow("  10.1.1.1  host  #  comment  ", "10.1.1.1", "host", "comment", true)]
        [DataRow("10.1.1.1 host#comment", "10.1.1.1", "host", "comment", true)]
        [DataRow("\t\t#\t\t10.1.1.1\thost\t\t#\t\tcomment\t\t", "10.1.1.1", "host", "comment", false)]
        [DataRow("  #  10.1.1.1  host  #  comment  ", "10.1.1.1", "host", "comment", false)]
        [DataRow("#10.1.1.1 host#comment", "10.1.1.1", "host", "comment", false)]
        [DataRow("# #10.1.1.1 host#comment", "10.1.1.1", "host", "comment", false)]
        [DataRow("# #\t10.1.1.1 host#comment", "10.1.1.1", "host", "comment", false)]
        [DataRow("# # \t10.1.1.1 host#comment", "10.1.1.1", "host", "comment", false)]
        public void Valid_Entry_SingleHost(string line, string address, string host, string comment, bool active)
        {
            var entry = new Entry(0, line);

            Assert.AreEqual(entry.Address, address);
            Assert.AreEqual(entry.Hosts, host);
            Assert.AreEqual(entry.Comment, comment);
            Assert.AreEqual(entry.Active, active);
            Assert.IsTrue(entry.Valid);
        }

        [DataTestMethod]
        [DataRow("\t\t10.1.1.1\t\thost host.local\t\t", "10.1.1.1", "host host.local", "", true)]
        [DataRow("  10.1.1.1  host  host.local  ", "10.1.1.1", "host host.local", "", true)]
        [DataRow("10.1.1.1 host host.local", "10.1.1.1", "host host.local", "", true)]
        [DataRow("\t\t#\t\t10.1.1.1\thost\t\thost.local\t\t", "10.1.1.1", "host host.local", "", false)]
        [DataRow("  #  10.1.1.1  host  host.local  ", "10.1.1.1", "host host.local", "", false)]
        [DataRow("#10.1.1.1 host host.local", "10.1.1.1", "host host.local", "", false)]
        [DataRow("\t\t10.1.1.1\t\thost\t\thost.local\t\t#\t\tcomment\t\t", "10.1.1.1", "host host.local", "comment", true)]
        [DataRow("  10.1.1.1  host  host.local  #  comment  ", "10.1.1.1", "host host.local", "comment", true)]
        [DataRow("10.1.1.1 host host.local#comment", "10.1.1.1", "host host.local", "comment", true)]
        [DataRow("\t\t#\t\t10.1.1.1\thost\t\thost.local\t\t#\t\tcomment\t\t", "10.1.1.1", "host host.local", "comment", false)]
        [DataRow("  #  10.1.1.1  host  host.local  #  comment  ", "10.1.1.1", "host host.local", "comment", false)]
        [DataRow("#10.1.1.1 host host.local#comment", "10.1.1.1", "host host.local", "comment", false)]
        public void Valid_Entry_MultipleHosts(string line, string address, string host, string comment, bool active)
        {
            var entry = new Entry(0, line);

            Assert.AreEqual(entry.Address, address);
            Assert.AreEqual(entry.Hosts, host);
            Assert.AreEqual(entry.Comment, comment);
            Assert.AreEqual(entry.Active, active);
            Assert.IsTrue(entry.Valid);
        }

        [DataTestMethod]
        [DataRow("\t\t10.1.1.1\t\t")]
        [DataRow("  10.1.1.1  ")]
        [DataRow("10.1.1.1")]
        [DataRow("\t\thost\t\t")]
        [DataRow("  host  ")]
        [DataRow("host")]
        [DataRow("\t\t10\t\thost")]
        [DataRow("  10  host  ")]
        [DataRow("10 host")]
        [DataRow("\t\thost\t\t10.1.1.1")]
        [DataRow("  host  10.1.1.1")]
        [DataRow("host 10.1.1.1")]
        [DataRow("# comment 10.1.1.1 host # comment")]
        [DataRow("10.1.1.1 host01 host02 host03 host04 host05 host06 host07 host08 host09 host10")]
        [DataRow("102.54.94.97 rhino.acme.com # source server")]
        [DataRow("38.25.63.10 x.acme.com # x client host")]
        public void Not_Valid_Entry(string line)
        {
            var entry = new Entry(0, line);
            Assert.IsFalse(entry.Valid);
        }
    }
}
