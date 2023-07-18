// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.PowerToys.Run.Plugin.ValueGenerator.UnitTests
{
    [TestClass]
    public class GUIDGeneratorTests
    {
        [TestMethod]
        public void GUIDv1Generator()
        {
            var guidRequest = new GUID.GUIDRequest(1);
            guidRequest.Compute();
            var guid = guidRequest.Result;

            Assert.IsNotNull(guid);
            Assert.AreEqual(0x1000, GetGUIDVersion(guid));
        }

        [TestMethod]
        public void GUIDv3Generator()
        {
            var guidRequest = new GUID.GUIDRequest(3, "ns:DNS", "abc");
            guidRequest.Compute();
            var guid = guidRequest.Result;

            Assert.IsNotNull(guid);
            Assert.AreEqual(0x3000, GetGUIDVersion(guid));
        }

        [TestMethod]
        public void GUIDv4Generator()
        {
            var guidRequest = new GUID.GUIDRequest(4);
            guidRequest.Compute();
            var guid = guidRequest.Result;

            Assert.IsNotNull(guid);
            Assert.AreEqual(0x4000, GetGUIDVersion(guid));
        }

        [TestMethod]
        public void GUIDv5Generator()
        {
            var guidRequest = new GUID.GUIDRequest(5, "ns:DNS", "abc");
            guidRequest.Compute();
            var guid = guidRequest.Result;

            Assert.IsNotNull(guid);
            Assert.AreEqual(0x5000, GetGUIDVersion(guid));
        }

        [DataTestMethod]
        [DataRow(3, "ns:DNS", "abc", "5bd670ce-29c8-3369-a8a1-10ce44c7259e")]
        [DataRow(3, "ns:URL", "abc", "874a8cb4-4e91-3055-a476-3d3e2ffe375f")]
        [DataRow(3, "ns:OID", "abc", "5557cd36-6b67-38ac-83fe-825f5905fc15")]
        [DataRow(3, "ns:X500", "abc", "589392cb-93e1-392c-a846-367c45ed1ecc")]
        [DataRow(3, "589392cb-93e1-392c-a846-367c45ed1ecc", "abc", "f55f77d2-feed-378e-aa47-2f716b07aaad")]
        [DataRow(3, "abc", "abc", null)]
        [DataRow(3, "", "abc", null)]
        [DataRow(3, null, "abc", null)]
        [DataRow(3, "abc", "", null)]
        [DataRow(3, "abc", null, null)]
        [DataRow(5, "ns:DNS", "abc", "6cb8e707-0fc5-5f55-88d4-d4fed43e64a8")]
        [DataRow(5, "ns:URL", "abc", "68661508-f3c4-55b4-945d-ae2b4dfe5db4")]
        [DataRow(5, "ns:OID", "abc", "7697a46f-b283-5da3-8e7c-62c11c03dd9e")]
        [DataRow(5, "ns:X500", "abc", "53e882a6-63b1-578b-8bf1-8f0878cfa6b7")]
        [DataRow(5, "589392cb-93e1-392c-a846-367c45ed1ecc", "abc", "396466f5-96f4-57e2-aea1-78e1bb1bacc6")]
        [DataRow(5, "abc", "abc", null)]
        [DataRow(5, "", "abc", null)]
        [DataRow(5, null, "abc", null)]
        [DataRow(5, "abc", "", null)]
        [DataRow(5, "abc", null, null)]
        public void GUIDv3Andv5(int version, string namespaceName, string name, string expectedResult)
        {
            var expectException = false;
            if (namespaceName == null)
            {
                expectException = true;
            }

            string[] predefinedNamespaces = new string[] { "ns:DNS", "ns:URL", "ns:OID", "ns:X500" };
            if (namespaceName != null &&
                !Guid.TryParse(namespaceName, out _) &&
                !predefinedNamespaces.Contains(namespaceName))
            {
                expectException = true;
            }

            if (name == null)
            {
                expectException = true;
            }

            try
            {
                var guidRequest = new GUID.GUIDRequest(version, namespaceName, name);

                guidRequest.Compute();

                if (expectException)
                {
                    Assert.Fail("GUID generator should have thrown an exception");
                }

                if (namespaceName != string.Empty)
                {
                    Assert.IsTrue(guidRequest.IsSuccessful);
                    Assert.AreEqual(expectedResult, guidRequest.ResultToString());
                }
                else
                {
                    Assert.IsFalse(guidRequest.IsSuccessful);
                }
            }
            catch (AssertFailedException)
            {
                throw;
            }
            catch
            {
                if (!expectException)
                {
                    throw;
                }
            }
        }

        private static short GetGUIDVersion(byte[] guid)
        {
            var time_hi_and_version = BitConverter.ToInt16(guid.AsSpan()[6..8]);
            return (short)(time_hi_and_version & 0xF000);
        }
    }
}
