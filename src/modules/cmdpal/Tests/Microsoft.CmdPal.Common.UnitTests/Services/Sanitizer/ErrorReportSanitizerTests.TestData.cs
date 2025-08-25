// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.UnitTests.Services.Sanitizer;

public partial class ErrorReportSanitizerTests
{
    private static class TestData
    {
        internal static string Input =>
             $"""
              HRESULT: 0x80004005
              HRESULT: -2147467259

              Here is e-mail address <jane.doe@contoso.com>
              IPv4 address: 192.168.100.1
              IPv4 loopback address: 127.0.0.1
              MAC address: 00-14-22-01-23-45
              IPv6 address: 2001:0db8:85a3:0000:0000:8a2e:0370:7334
              IPv6 loopback address: ::1
              Password: P@ssw0rd123!
              Password=secret
              Api key: 1234567890abcdef
              PostgreSQL connection string: Host=localhost;Username=postgres;Password=secret;Database=mydb
              InstrumentationKey=00000000-0000-0000-0000-000000000000;EndpointSuffix=ai.contoso.com;
              X-API-key: 1234567890abcdef
              Pet-Shop-Subscription-Key: 1234567890abcdef
              Here is a user name {Environment.UserName}
              And here is a profile path {Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\Pictures
              Here is a local app data path {Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\Microsoft\PowerToys\CmdPal
              Here is machine name {Environment.MachineName}
              JWT token: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiYWRtaW4iOnRydWUsImlhdCI6MTUxNjIzOTAyMn0.KMUFsIDTnFmyG3nMiGM6H9FNFUROf3wh7SmqJp-QV30
              User email john.doe@company.com failed validation
              File not found: {Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\secret.txt
              Connection string: Server=localhost;User ID=admin;Password=secret123;Database=test
              Phone number 555-123-4567 is invalid
              API key abc123def456ghi789jkl012mno345pqr678 expired
              Failed to connect to https://api.internal-company.com/users/12345?token=secret_abc123
              Error accessing file://C:/Users/john.doe/Documents/confidential.pdf
              JDBC connection failed: jdbc://database-server:5432/userdb?user=admin&password=secret
              FTP upload error: ftp://internal-server.company.com/uploads/user_data.csv
              Email service error: mailto:admin@internal-company.com?subject=Alert
              """;

        public const string Expected =
            $"""
             HRESULT: 0x80004005
             HRESULT: -2147467259

             Here is e-mail address <[EMAIL_REDACTED]>
             IPv4 address: [IP4_REDACTED]
             IPv4 loopback address: [IP4_REDACTED]
             MAC address: [MAC_ADDRESS_REDACTED]
             IPv6 address: [IP6_REDACTED]
             IPv6 loopback address: [IP6_REDACTED]
             Password: [REDACTED]
             Password= [REDACTED]
             Api key: [REDACTED]
             PostgreSQL connection string: [REDACTED]
             InstrumentationKey= [REDACTED]
             X-API-key: [REDACTED]
             Pet-Shop-Subscription-Key: [REDACTED]
             Here is a user name [USERNAME_REDACTED]
             And here is a profile path [USER_PROFILE_DIR]Pictures
             Here is a local app data path [LOCALAPPLICATIONDATA_DIR]Microsoft\PowerToys\CmdPal
             Here is machine name [MACHINE_NAME_REDACTED]
             JWT token: [REDACTED]
             User email [EMAIL_REDACTED] failed validation
             File not found: [MYDOCUMENTS_DIR]se****.txt
             Connection string: [REDACTED] ID=[REDACTED];Password= [REDACTED]
             Phone number [PHONE_REDACTED] is invalid
             API key [TOKEN_REDACTED] expired
             Failed to connect to [URL_REDACTED]
             Error accessing [URL_REDACTED]
             JDBC connection failed: [URL_REDACTED]
             FTP upload error: [URL_REDACTED]
             Email service error: mailto:[EMAIL_REDACTED]?subject=Alert
             """;
    }
}
