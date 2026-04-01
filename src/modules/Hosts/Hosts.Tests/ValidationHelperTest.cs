// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;

using HostsUILib;
using HostsUILib.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hosts.Tests
{
    [TestClass]
    public class ValidationHelperTest
    {
        [DataTestMethod]
        [DataRow("0.0.0.0")]
        [DataRow("127.0.0.1")]
        [DataRow("192.168.1.1")]
        [DataRow("255.255.255.255")]
        [DataRow("10.0.0.1")]
        [DataRow("172.16.0.1")]
        [DataRow("1.2.3.4")]
        [DataRow("01.01.01.01")]
        [DataRow("0.0.0.1")]
        public void ValidIPv4_ValidAddresses_ReturnsTrue(string address)
        {
            Assert.IsTrue(ValidationHelper.ValidIPv4(address));
        }

        [DataTestMethod]
        [DataRow("256.0.0.0")]
        [DataRow("0.256.0.0")]
        [DataRow("0.0.256.0")]
        [DataRow("0.0.0.256")]
        [DataRow("999.999.999.999")]
        [DataRow("1.2.3")]
        [DataRow("1.2.3.4.5")]
        [DataRow("1.2.3.")]
        [DataRow(".1.2.3")]
        [DataRow("1..2.3")]
        [DataRow("abc.def.ghi.jkl")]
        [DataRow("192.168.1.1/24")]
        [DataRow("192.168.1.1:80")]
        [DataRow("192.168.1")]
        [DataRow("-1.0.0.0")]
        public void ValidIPv4_InvalidAddresses_ReturnsFalse(string address)
        {
            Assert.IsFalse(ValidationHelper.ValidIPv4(address));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("\t")]
        [DataRow("\n")]
        public void ValidIPv4_NullOrWhitespace_ReturnsFalse(string address)
        {
            Assert.IsFalse(ValidationHelper.ValidIPv4(address));
        }

        [DataTestMethod]
        [DataRow("::1")]
        [DataRow("::")]
        [DataRow("2001:0db8:85a3:0000:0000:8a2e:0370:7334")]
        [DataRow("2001:db8:85a3:0:0:8a2e:370:7334")]
        [DataRow("2001:db8:85a3::8a2e:370:7334")]
        [DataRow("fe80::1")]
        [DataRow("ff02::1")]
        [DataRow("2001:db8::1")]
        [DataRow("::ffff:192.168.1.1")]
        [DataRow("fe80::1%eth0")]
        public void ValidIPv6_ValidAddresses_ReturnsTrue(string address)
        {
            Assert.IsTrue(ValidationHelper.ValidIPv6(address));
        }

        [DataTestMethod]
        [DataRow("2001:db8:85a3:0:0:8a2e:370:7334:extra")]
        [DataRow("gggg::1")]
        [DataRow("12345::1")]
        [DataRow("192.168.1.1")]
        [DataRow("::ffff:999.999.999.999")]
        [DataRow("hello")]
        [DataRow("2001:db8:85a3::8a2e:370:7334:1234:5678")]
        public void ValidIPv6_InvalidAddresses_ReturnsFalse(string address)
        {
            Assert.IsFalse(ValidationHelper.ValidIPv6(address));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("\t")]
        public void ValidIPv6_NullOrWhitespace_ReturnsFalse(string address)
        {
            Assert.IsFalse(ValidationHelper.ValidIPv6(address));
        }

        [DataTestMethod]
        [DataRow("localhost")]
        [DataRow("example.com")]
        [DataRow("sub.domain.example.com")]
        [DataRow("my-host")]
        [DataRow("host1 host2")]
        [DataRow("host1 host2 host3")]
        [DataRow("example.com www.example.com")]
        public void ValidHosts_ValidHostnames_ReturnsTrue(string hosts)
        {
            Assert.IsTrue(ValidationHelper.ValidHosts(hosts, validateHostsLength: false));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("\t")]
        public void ValidHosts_NullOrWhitespace_ReturnsFalse(string hosts)
        {
            Assert.IsFalse(ValidationHelper.ValidHosts(hosts, validateHostsLength: false));
        }

        [TestMethod]
        public void ValidHosts_WithLengthValidation_ExceedsMaxCount_ReturnsFalse()
        {
            // Create a host string with one more than MaxHostsCount hosts
            var hosts = string.Join(" ", Enumerable.Range(1, Consts.MaxHostsCount + 1).Select(i => $"h{i}"));
            Assert.IsFalse(ValidationHelper.ValidHosts(hosts, validateHostsLength: true));
        }

        [TestMethod]
        public void ValidHosts_WithLengthValidation_AtMaxCount_ReturnsTrue()
        {
            // Create a host string with exactly MaxHostsCount hosts
            var hosts = string.Join(" ", Enumerable.Range(1, Consts.MaxHostsCount).Select(i => $"h{i}"));
            Assert.IsTrue(ValidationHelper.ValidHosts(hosts, validateHostsLength: true));
        }

        [TestMethod]
        public void ValidHosts_WithLengthValidation_BelowMaxCount_ReturnsTrue()
        {
            string hosts = "host1 host2 host3";
            Assert.IsTrue(ValidationHelper.ValidHosts(hosts, validateHostsLength: true));
        }

        [TestMethod]
        public void ValidHosts_WithoutLengthValidation_ExceedsMaxCount_ReturnsTrue()
        {
            // When validateHostsLength is false, exceeding max count should still return true
            var hosts = string.Join(" ", Enumerable.Range(1, Consts.MaxHostsCount + 1).Select(i => $"h{i}"));
            Assert.IsTrue(ValidationHelper.ValidHosts(hosts, validateHostsLength: false));
        }

        [TestMethod]
        public void ValidHosts_SingleHost_ReturnsTrue()
        {
            Assert.IsTrue(ValidationHelper.ValidHosts("localhost", validateHostsLength: true));
        }

        [TestMethod]
        public void ValidHosts_InvalidHostname_ReturnsFalse()
        {
            Assert.IsFalse(ValidationHelper.ValidHosts("host_with!invalid@chars", validateHostsLength: false));
        }

        [TestMethod]
        public void ValidHosts_HostWithSubdomains_ReturnsTrue()
        {
            Assert.IsTrue(ValidationHelper.ValidHosts("sub.domain.example.com", validateHostsLength: true));
        }

        [TestMethod]
        public void ValidHosts_MultipleValidHosts_WithLengthValidation_ReturnsTrue()
        {
            Assert.IsTrue(ValidationHelper.ValidHosts("example.com www.example.com api.example.com", validateHostsLength: true));
        }
    }
}
