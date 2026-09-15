// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using WorkspacesLauncherUI.Data;
using WorkspacesLauncherUI.Models;

namespace WorkspacesLauncherUI.IPC
{
    internal sealed class LauncherMessage
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal string Type { get; private init; }

        internal SignatureWarningRequest Warning { get; private init; }

        internal Guid RequestId { get; private init; }

        internal AppLaunchData.AppLaunchDataWrapper LaunchStatus { get; private init; }

        internal static LauncherMessage Parse(byte[] body, int launcherProcessId)
        {
            var json = StrictUtf8.GetString(body);
            ValidateString(json);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            ValidateElement(root);
            if (RequiredInteger(root, "protocolVersion") != 1)
            {
                throw new InvalidDataException("Unsupported launcher protocol version.");
            }

            var type = RequiredString(root, "type");
            return type switch
            {
                "launch-status" => new LauncherMessage { Type = type, LaunchStatus = ParseLaunchStatus(root, launcherProcessId) },
                "elevation-warning" => new LauncherMessage
                {
                    Type = type,
                    RequestId = RequiredRequestId(root),
                    Warning = new SignatureWarningRequest
                    {
                        RequestId = RequiredString(root, "requestId"),
                        AppName = RequiredString(root, "appName"),
                        Path = RequiredString(root, "path"),
                        Arguments = RequiredString(root, "arguments"),
                        Reason = RequiredString(root, "reason"),
                        Status = RequiredString(root, "status"),
                    },
                },
                "dismiss-warning" => new LauncherMessage { Type = type, RequestId = RequiredRequestId(root) },
                "shutdown" => new LauncherMessage { Type = type },
                _ => throw new InvalidDataException("Unsupported launcher message type."),
            };
        }

        private static AppLaunchData.AppLaunchDataWrapper ParseLaunchStatus(JsonElement root, int launcherProcessId)
        {
            if (RequiredInteger(root, "processId") != launcherProcessId)
            {
                throw new InvalidDataException("The launch status process identity is invalid.");
            }

            var apps = Required(root, "apps", JsonValueKind.Object);
            var entries = Required(apps, "appLaunchInfos", JsonValueKind.Array);
            var list = new List<AppLaunchInfoData.AppLaunchInfoWrapper>();
            foreach (var entry in entries.EnumerateArray())
            {
                var state = RequiredInteger(entry, "state");
                if (!Enum.IsDefined(typeof(LaunchingState), state))
                {
                    throw new InvalidDataException("Invalid application launch state.");
                }

                var app = Required(entry, "application", JsonValueKind.Object);
                var position = Required(app, "position", JsonValueKind.Object);
                list.Add(new AppLaunchInfoData.AppLaunchInfoWrapper
                {
                    State = (LaunchingState)state,
                    Application = new ApplicationWrapper
                    {
                        Application = RequiredString(app, "application"),
                        ApplicationPath = RequiredString(app, "application-path"),
                        Title = RequiredString(app, "title"),
                        PackageFullName = RequiredString(app, "package-full-name"),
                        AppUserModelId = RequiredString(app, "app-user-model-id"),
                        PwaAppId = RequiredString(app, "pwa-app-id"),
                        CommandLineArguments = RequiredString(app, "command-line-arguments"),
                        IsElevated = RequiredBoolean(app, "is-elevated"),
                        CanLaunchElevated = RequiredBoolean(app, "can-launch-elevated"),
                        Minimized = RequiredBoolean(app, "minimized"),
                        Maximized = RequiredBoolean(app, "maximized"),
                        Monitor = RequiredInteger(app, "monitor"),
                        Position = new PositionWrapper
                        {
                            X = RequiredInteger(position, "X"),
                            Y = RequiredInteger(position, "Y"),
                            Width = RequiredInteger(position, "width"),
                            Height = RequiredInteger(position, "height"),
                        },
                    },
                });
            }

            return new AppLaunchData.AppLaunchDataWrapper
            {
                LauncherProcessID = launcherProcessId,
                AppLaunchInfos = new AppLaunchInfosData.AppLaunchInfoListWrapper { AppLaunchInfoList = list },
            };
        }

        private static Guid RequiredRequestId(JsonElement root)
        {
            if (!Guid.TryParse(RequiredString(root, "requestId"), out var requestId))
            {
                throw new InvalidDataException("Invalid launcher request ID.");
            }

            return requestId;
        }

        private static JsonElement Required(JsonElement element, string name, JsonValueKind kind)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(name, out var value) ||
                value.ValueKind != kind)
            {
                throw new InvalidDataException("Missing or incorrectly typed launcher message field.");
            }

            return value;
        }

        private static string RequiredString(JsonElement element, string name)
        {
            return Required(element, name, JsonValueKind.String).GetString();
        }

        private static int RequiredInteger(JsonElement element, string name)
        {
            var value = Required(element, name, JsonValueKind.Number);
            if (!value.TryGetInt32(out var result))
            {
                throw new InvalidDataException("Invalid integer launcher message field.");
            }

            return result;
        }

        private static bool RequiredBoolean(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(name, out var value) ||
                (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False))
            {
                throw new InvalidDataException("Missing or incorrectly typed boolean launcher message field.");
            }

            return value.GetBoolean();
        }

        private static void ValidateElement(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    ValidateString(property.Name);
                    if (!names.Add(property.Name))
                    {
                        throw new InvalidDataException("Duplicate launcher message field.");
                    }

                    ValidateElement(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    ValidateElement(item);
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                ValidateString(element.GetString());
            }
        }

        private static void ValidateString(string value)
        {
            if (value.Contains('\0'))
            {
                throw new InvalidDataException("NUL is not allowed in launcher messages.");
            }

            // Validate decoded JSON escapes as well as the original UTF-8 bytes.
            _ = StrictUtf8.GetByteCount(value);
        }
    }
}
