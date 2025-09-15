// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Awake.Core
{
    /// <summary>
    /// HTTP request handler for Awake module functionality.
    /// </summary>
    internal sealed class AwakeHttpHandler : IHttpRequestHandler
    {
        public string ModuleName => "awake";

        public string[] GetAvailableEndpoints()
        {
            return new[]
            {
                "GET /awake/status - Get current Awake status",
                "POST /awake/indefinite - Set indefinite keep-awake (body: {\"keepDisplayOn\": true, \"processId\": 0})",
                "POST /awake/timed - Set timed keep-awake (body: {\"seconds\": 3600, \"keepDisplayOn\": true})",
                "POST /awake/expirable - Set expirable keep-awake (body: {\"expireAt\": \"2024-12-31T23:59:59Z\", \"keepDisplayOn\": true})",
                "POST /awake/passive - Set passive mode (no keep-awake)",
                "POST /awake/display/toggle - Toggle display setting",
                "GET /awake/settings - Get current PowerToys settings",
            };
        }

        public async Task HandleRequestAsync(HttpListenerContext context, string path)
        {
            var request = context.Request;
            var response = context.Response;
            var method = request.HttpMethod.ToUpperInvariant();
            var pathLower = path.ToLowerInvariant();

            try
            {
                switch ((method, pathLower))
                {
                    case ("GET", "status"):
                        await HandleGetStatusAsync(response);
                        break;

                    case ("POST", "indefinite"):
                        await HandleSetIndefiniteAwakeAsync(request, response);
                        break;

                    case ("POST", "timed"):
                        await HandleSetTimedAwakeAsync(request, response);
                        break;

                    case ("POST", "expirable"):
                        await HandleSetExpirableAwakeAsync(request, response);
                        break;

                    case ("POST", "passive"):
                        await HandleSetPassiveAwakeAsync(response);
                        break;

                    case ("POST", "display/toggle"):
                        await HandleToggleDisplayAsync(response);
                        break;

                    case ("GET", "settings"):
                        await HandleGetSettingsAsync(response);
                        break;

                    default:
                        await HandleNotFoundAsync(response, path);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error handling Awake request {method} /{path}: {ex.Message}");
                await HandleErrorAsync(response, 500, $"Internal server error: {ex.Message}");
            }
        }

        private async Task HandleGetStatusAsync(HttpListenerResponse response)
        {
            var status = new
            {
                Module = "Awake",
                Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                Status = "Running",
                UsingPowerToysConfig = Manager.IsUsingPowerToysConfig,
                Timestamp = DateTimeOffset.Now,
            };

            await WriteJsonResponseAsync(response, status);
        }

        private async Task HandleSetIndefiniteAwakeAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var requestData = await ReadJsonRequestAsync<IndefiniteAwakeRequest>(request);

            Manager.SetIndefiniteKeepAwake(
                requestData?.KeepDisplayOn ?? true,
                requestData?.ProcessId ?? 0,
                "HttpServer");

            var result = new
            {
                Success = true,
                Mode = "Indefinite",
                KeepDisplayOn = requestData?.KeepDisplayOn ?? true,
                ProcessId = requestData?.ProcessId ?? 0,
            };
            await WriteJsonResponseAsync(response, result);
        }

        private async Task HandleSetTimedAwakeAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var requestData = await ReadJsonRequestAsync<TimedAwakeRequest>(request);

            if (requestData?.Seconds == null || requestData.Seconds <= 0)
            {
                await HandleErrorAsync(response, 400, "Invalid or missing 'seconds' parameter. Must be a positive integer.");
                return;
            }

            Manager.SetTimedKeepAwake(
                requestData.Seconds,
                requestData.KeepDisplayOn ?? true,
                "HttpServer");

            var result = new
            {
                Success = true,
                Mode = "Timed",
                Seconds = requestData.Seconds,
                KeepDisplayOn = requestData.KeepDisplayOn ?? true,
            };
            await WriteJsonResponseAsync(response, result);
        }

        private async Task HandleSetExpirableAwakeAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var requestData = await ReadJsonRequestAsync<ExpirableAwakeRequest>(request);

            if (requestData?.ExpireAt == null)
            {
                await HandleErrorAsync(response, 400, "Missing 'expireAt' parameter. Expected ISO 8601 format (e.g., '2024-12-31T23:59:59Z').");
                return;
            }

            if (requestData.ExpireAt <= DateTimeOffset.Now)
            {
                await HandleErrorAsync(response, 400, "Expiration time must be in the future.");
                return;
            }

            Manager.SetExpirableKeepAwake(
                requestData.ExpireAt,
                requestData.KeepDisplayOn ?? true,
                "HttpServer");

            var result = new
            {
                Success = true,
                Mode = "Expirable",
                ExpireAt = requestData.ExpireAt,
                KeepDisplayOn = requestData.KeepDisplayOn ?? true,
            };
            await WriteJsonResponseAsync(response, result);
        }

        private async Task HandleSetPassiveAwakeAsync(HttpListenerResponse response)
        {
            Manager.SetPassiveKeepAwake(true, "HttpServer");

            var result = new { Success = true, Mode = "Passive" };
            await WriteJsonResponseAsync(response, result);
        }

        private async Task HandleToggleDisplayAsync(HttpListenerResponse response)
        {
            Manager.SetDisplay("HttpServer");

            var result = new { Success = true, Action = "Display setting toggled" };
            await WriteJsonResponseAsync(response, result);
        }

        private async Task HandleGetSettingsAsync(HttpListenerResponse response)
        {
            try
            {
                if (Manager.ModuleSettings != null)
                {
                    var settings = Manager.ModuleSettings.GetSettings<AwakeSettings>(Constants.AppName);
                    await WriteJsonResponseAsync(response, settings);
                }
                else
                {
                    await HandleErrorAsync(response, 500, "Settings module not available");
                }
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(response, 500, $"Error retrieving settings: {ex.Message}");
            }
        }

        private async Task HandleNotFoundAsync(HttpListenerResponse response, string path)
        {
            var errorResponse = new
            {
                Error = $"Awake endpoint '/{path}' not found",
                AvailableEndpoints = GetAvailableEndpoints(),
                Module = ModuleName,
                Timestamp = DateTimeOffset.Now,
            };

            response.StatusCode = 404;
            await WriteJsonResponseAsync(response, errorResponse);
        }

        private async Task HandleErrorAsync(HttpListenerResponse response, int statusCode, string message)
        {
            response.StatusCode = statusCode;
            var errorResponse = new
            {
                Error = message,
                StatusCode = statusCode,
                Module = ModuleName,
                Timestamp = DateTimeOffset.Now,
            };
            await WriteJsonResponseAsync(response, errorResponse);
        }

        private async Task WriteJsonResponseAsync(HttpListenerResponse response, object data)
        {
            // Use HttpServer utility method via static access or DI - for now, replicate the functionality
            response.ContentType = "application/json";
            response.ContentEncoding = System.Text.Encoding.UTF8;

#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
            var json = System.Text.Json.JsonSerializer.Serialize(
                data, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            });
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances
            var buffer = System.Text.Encoding.UTF8.GetBytes(json);

            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer);
            response.Close();
        }

        private async Task<T?> ReadJsonRequestAsync<T>(HttpListenerRequest request)
            where T : class
        {
            if (!request.HasEntityBody)
            {
                return null;
            }

            using var reader = new System.IO.StreamReader(request.InputStream, System.Text.Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            try
            {
#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
                return System.Text.Json.JsonSerializer.Deserialize<T>(body, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                });
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances
            }
            catch (System.Text.Json.JsonException ex)
            {
                Logger.LogError($"Error deserializing request body: {ex.Message}");
                return null;
            }
        }

        // Request DTOs
        private sealed class IndefiniteAwakeRequest
        {
            public bool? KeepDisplayOn { get; set; }

            public int? ProcessId { get; set; }
        }

        private sealed class TimedAwakeRequest
        {
            public uint Seconds { get; set; }

            public bool? KeepDisplayOn { get; set; }
        }

        private sealed class ExpirableAwakeRequest
        {
            public DateTimeOffset ExpireAt { get; set; }

            public bool? KeepDisplayOn { get; set; }
        }
    }
}
