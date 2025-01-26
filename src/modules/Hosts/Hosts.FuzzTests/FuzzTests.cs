// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using HostsUILib.Helpers;

namespace Hosts.FuzzTests
{
    public class FuzzTests
    {
        // Case1： Fuzzing method for ValidIPv4
        public static void FuzzValidIPv4(ReadOnlySpan<byte> input)
        {
            try
            {
                string address = System.Text.Encoding.UTF8.GetString(input);
                bool isValid = ValidationHelper.ValidIPv4(address);

                // Console.WriteLine($"Input:{address}, ValidIPv4:{isValid}");
            }
            catch (Exception ex) when (ex is OutOfMemoryException)
            {
                // This is an example. It's important to filter out any *expected* exceptions from our code here.
                // However, catching all exceptions is considered an anti-pattern because it may suppress legitimate
                // issues, such as a NullReferenceException thrown by our code. In this case, we still re-throw
                // the exception, as the ToJsonFromXmlOrCsvAsync method is not expected to throw any exceptions.
                throw;
            }
        }

        // Case2: fuzzing method for ValidIPv6
        public static void FuzzValidIPv6(ReadOnlySpan<byte> input)
        {
            try
            {
                string address = System.Text.Encoding.UTF8.GetString(input);
                bool isValid = ValidationHelper.ValidIPv6(address);

                // Console.WriteLine($"Input:{address}, ValidIPv6:{isValid}");
            }
            catch (Exception ex) when (ex is OutOfMemoryException)
            {
                // This is an example. It's important to filter out any *expected* exceptions from our code here.
                // However, catching all exceptions is considered an anti-pattern because it may suppress legitimate
                // issues, such as a NullReferenceException thrown by our code. In this case, we still re-throw
                // the exception, as the ToJsonFromXmlOrCsvAsync method is not expected to throw any exceptions.
                throw;
            }
        }

        // Case3: fuzzing method for ValidHosts
        public static void FuzzValidHosts(ReadOnlySpan<byte> input)
        {
            try
            {
                string hosts = System.Text.Encoding.UTF8.GetString(input);
                bool isValid = ValidationHelper.ValidHosts(hosts, true);

                // Console.WriteLine($"Input:{hosts}, ValidHosts:{isValid}");
            }
            catch (Exception ex) when (ex is OutOfMemoryException)
            {
                // This is an example. It's important to filter out any *expected* exceptions from our code here.
                // However, catching all exceptions is considered an anti-pattern because it may suppress legitimate
                // issues, such as a NullReferenceException thrown by our code. In this case, we still re-throw
                // the exception, as the ToJsonFromXmlOrCsvAsync method is not expected to throw any exceptions.
                throw;
            }
        }

        // public static void FuzzWriteAsync(ReadOnlySpan<byte> input)
        // {
        //     var additionalLines = System.Text.Encoding.UTF8.GetString(input);
        //     var entries = new List<Entry>();
        //     entries.Add(new Entry(0, System.Text.Encoding.UTF8.GetString(input)));
        //     HostsService.WriteAsync(additionalLines, entries).Wait();
        //     }
    }
}
