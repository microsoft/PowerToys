// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.ValueGenerator.UnitTests
{
    [TestClass]
    public class InputParserTests
    {
        [DataTestMethod]
        [DataRow("md5 abc", typeof(Hashing.HashRequest))]
        [DataRow("sha1 abc", typeof(Hashing.HashRequest))]
        [DataRow("sha256 abc", typeof(Hashing.HashRequest))]
        [DataRow("sha384 abc", typeof(Hashing.HashRequest))]
        [DataRow("sha512 abc", typeof(Hashing.HashRequest))]
        [DataRow("sha1111 abc", null)]
        [DataRow("uuid", typeof(GUID.GUIDRequest))]
        [DataRow("guidv3 ns:DNS abc", typeof(GUID.GUIDRequest))]
        [DataRow("guidv2 ns:DNS abc", null)]
        [DataRow("uUiD5 ns:URL abc", typeof(GUID.GUIDRequest))]
        [DataRow("Guidvv ns:DNS abc", null)]
        [DataRow("guidv4", typeof(GUID.GUIDRequest))]
        [DataRow("base64 abc", typeof(Base64.Base64Request))]
        [DataRow("base99 abc", null)]
        [DataRow("base64s abc", null)]
        [DataRow("base64d abc=", typeof(Base64.Base64DecodeRequest))]
        public void ParserTest(string input, Type? expectedRequestType)
        {
            var parser = new InputParser();
            var query = new Query(input);

            var expectException = false;
            string? command = null;
            if (query.Terms.Count == 0)
            {
                expectException = true;
            }
            else
            {
                command = query.Terms[0];
            }

            if (command != null && !CommandIsKnown(command))
            {
                expectException = true;
            }

            try
            {
                IComputeRequest request = parser.ParseInput(query);
                if (expectException)
                {
                    Assert.Fail("Parser should have thrown an exception");
                }

                Assert.IsNotNull(request);

                Assert.AreEqual(expectedRequestType, request.GetType());
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

        private static bool CommandIsKnown(string command)
        {
            string[] hashes = new string[] { "md5", "sha1", "sha256", "sha384", "sha512", "base64", "base64d" };

            if (hashes.Contains(command.ToLowerInvariant()))
            {
                return true;
            }

            Regex regex = new Regex("^(guid|uuid)([1345]{0,1}|v[1345]{1})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            return regex.IsMatch(command);
        }
    }
}
