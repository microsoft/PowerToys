// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Settings;

public partial class InternalPage
{
    internal static class SampleData
    {
        internal static string ExceptionMessageWithPii { get; } =
            $"""
             Test exception with personal information; thrown from the UI thread

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
    }
}
