// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ManagedCommon
{
    public sealed class HttpServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Dictionary<string, IHttpRequestHandler> _requestHandlers;
        private readonly JsonSerializerOptions _fallbackJsonOptions;
        private Task? _listenerTask;
        private bool _disposed;

        public HttpServer(string prefix = "http://localhost:8080/")
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _cancellationTokenSource = new CancellationTokenSource();
            _requestHandlers = new Dictionary<string, IHttpRequestHandler>(StringComparer.OrdinalIgnoreCase);

            // Cached fallback options for generic deserialization
            _fallbackJsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            Logger.LogInfo($"HTTP server configured to listen on: {prefix}");
        }

        /// <summary>
        /// Register a request handler for a specific module.
        /// </summary>
        /// <param name="handler">The request handler to register.</param>
        public void RegisterHandler(IHttpRequestHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);

            if (string.IsNullOrWhiteSpace(handler.ModuleName))
            {
                throw new ArgumentException("Module name cannot be null or empty", nameof(handler));
            }

            _requestHandlers[handler.ModuleName] = handler;
            Logger.LogInfo($"Registered HTTP handler for module: {handler.ModuleName}");
        }

        /// <summary>
        /// Unregister a request handler for a specific module.
        /// </summary>
        /// <param name="moduleName">The name of the module to unregister.</param>
        public void UnregisterHandler(string moduleName)
        {
            if (_requestHandlers.Remove(moduleName))
            {
                Logger.LogInfo($"Unregistered HTTP handler for module: {moduleName}");
            }
        }

        public void Start()
        {
            try
            {
                _listener.Start();
                Logger.LogInfo("HTTP server started successfully");

                _listenerTask = Task.Run(async () => await ListenAsync(_cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to start HTTP server: {ex.Message}");
                throw;
            }
        }

        public void Stop()
        {
            try
            {
                _cancellationTokenSource.Cancel();
                _listener.Stop();
                _listenerTask?.Wait(TimeSpan.FromSeconds(5));
                Logger.LogInfo("HTTP server stopped");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error stopping HTTP server: {ex.Message}");
            }
        }

        /// <summary>
        /// Utility method for modules to write JSON responses.
        /// </summary>
        /// <param name="response">The HTTP response to write to.</param>
        /// <param name="data">The object to serialize as JSON.</param>
        public async Task WriteJsonResponseAsync(HttpListenerResponse response, object data)
        {
            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;

            string json = data switch
            {
                ServerStatusResponse serverStatus => JsonSerializer.Serialize(serverStatus, HttpServerJsonContext.Default.ServerStatusResponse),
                GlobalStatusResponse globalStatus => JsonSerializer.Serialize(globalStatus, HttpServerJsonContext.Default.GlobalStatusResponse),
                ErrorResponse error => JsonSerializer.Serialize(error, HttpServerJsonContext.Default.ErrorResponse),
                _ => JsonSerializer.Serialize(data, HttpServerJsonContext.Default.Object),
            };

            var buffer = Encoding.UTF8.GetBytes(json);

            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer.AsMemory(), _cancellationTokenSource.Token);
            response.Close();
        }

        /// <summary>
        /// Utility method for modules to read JSON request bodies as string.
        /// Modules should handle their own deserialization to maintain AOT compatibility.
        /// </summary>
        /// <param name="request">The HTTP request to read from.</param>
        /// <returns>The JSON string, or null if no body.</returns>
        public async Task<string?> ReadJsonRequestBodyAsync(HttpListenerRequest request)
        {
            if (!request.HasEntityBody)
            {
                return null;
            }

            using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            return string.IsNullOrWhiteSpace(body) ? null : body;
        }

        /// <summary>
        /// Legacy utility method for modules to read JSON request bodies.
        /// Warning: This method uses reflection-based deserialization and is not AOT-compatible.
        /// Consider using ReadJsonRequestBodyAsync and handling deserialization in your module.
        /// </summary>
        /// <typeparam name="T">The type to deserialize to.</typeparam>
        /// <param name="request">The HTTP request to read from.</param>
        /// <returns>The deserialized object, or null if no body or invalid JSON.</returns>
        [Obsolete("Use ReadJsonRequestBodyAsync and handle deserialization in your module for AOT compatibility")]
        public async Task<T?> ReadJsonRequestAsync<T>(HttpListenerRequest request)
            where T : class
        {
            var body = await ReadJsonRequestBodyAsync(request);
            if (body == null)
            {
                return null;
            }

            try
            {
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling
                return JsonSerializer.Deserialize<T>(body, _fallbackJsonOptions);
#pragma warning restore IL3050
#pragma warning restore IL2026
            }
            catch (JsonException ex)
            {
                Logger.LogError($"Error deserializing request body: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _cancellationTokenSource?.Dispose();
                _listener?.Close();
                _disposed = true;
            }
        }

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var contextTask = _listener.GetContextAsync();
                    var context = await contextTask.ConfigureAwait(false);

                    // Handle request asynchronously without blocking the listener
                    _ = Task.Run(async () => await HandleRequestAsync(context), cancellationToken);
                }
                catch (ObjectDisposedException)
                {
                    // Expected when listener is stopped
                    break;
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 995)
                {
                    // Expected when listener is stopped
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error in HTTP listener: {ex.Message}");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                Logger.LogInfo($"HTTP Request: {request.HttpMethod} {request.Url?.AbsolutePath}");

                // Set CORS headers
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                var path = request.Url?.AbsolutePath?.TrimStart('/');

                if (string.IsNullOrEmpty(path))
                {
                    await HandleRootRequestAsync(response);
                    return;
                }

                // Parse the path to extract module name and sub-path
                var segments = path.Split('/', 2);
                var moduleName = segments[0];
                var subPath = segments.Length > 1 ? segments[1] : string.Empty;

                // Check for global endpoints
                if (string.Equals(moduleName, "status", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleGlobalStatusAsync(response);
                    return;
                }

                // Route to module-specific handler
                if (_requestHandlers.TryGetValue(moduleName, out var handler))
                {
                    try
                    {
                        await handler.HandleRequestAsync(context, subPath);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error in module handler for {moduleName}: {ex.Message}");
                        await HandleErrorAsync(response, 500, $"Internal server error in {moduleName} module: {ex.Message}");
                    }
                }
                else
                {
                    await HandleNotFoundAsync(response, moduleName);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unhandled error in HTTP request handler: {ex.Message}");
            }
        }

        private async Task HandleRootRequestAsync(HttpListenerResponse response)
        {
            var rootInfo = new ServerStatusResponse
            {
                Application = "PowerToys HTTP Server",
                Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                Status = "Running",
                RegisteredModules = _requestHandlers.Keys,
                AvailableEndpoints = [
                    "GET /status - Get server status",
                    "GET /{module}/... - Module-specific endpoints",
                ],
                Timestamp = DateTimeOffset.Now,
            };

            await WriteJsonResponseAsync(response, rootInfo);
        }

        private async Task HandleGlobalStatusAsync(HttpListenerResponse response)
        {
            var moduleStatuses = new Dictionary<string, ModuleStatusResponse>();

            foreach (var kvp in _requestHandlers)
            {
                moduleStatuses[kvp.Key] = new ModuleStatusResponse
                {
                    ModuleName = kvp.Value.ModuleName,
                    AvailableEndpoints = kvp.Value.GetAvailableEndpoints(),
                };
            }

            var status = new GlobalStatusResponse
            {
                Application = "PowerToys HTTP Server",
                Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                Status = "Running",
                RegisteredModules = _requestHandlers.Count,
                Modules = moduleStatuses,
                Timestamp = DateTimeOffset.Now,
            };

            await WriteJsonResponseAsync(response, status);
        }

        private async Task HandleNotFoundAsync(HttpListenerResponse response, string requestedModule)
        {
            var errorResponse = new ErrorResponse
            {
                Error = $"Module '{requestedModule}' not found",
                RegisteredModules = _requestHandlers.Keys,
                AvailableEndpoints = [
                    "GET /status - Get server status and available modules",
                ],
                Timestamp = DateTimeOffset.Now,
            };

            response.StatusCode = 404;
            await WriteJsonResponseAsync(response, errorResponse);
        }

        private async Task HandleErrorAsync(HttpListenerResponse response, int statusCode, string message)
        {
            response.StatusCode = statusCode;
            var errorResponse = new ErrorResponse
            {
                Error = message,
                StatusCode = statusCode,
                Timestamp = DateTimeOffset.Now,
            };

            await WriteJsonResponseAsync(response, errorResponse);
        }
    }
}
