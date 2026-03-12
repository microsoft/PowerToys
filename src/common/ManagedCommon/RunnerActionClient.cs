// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using PowerToys.Interop;

namespace ManagedCommon
{
    public sealed class RunnerActionClient
    {
        public IReadOnlyList<RunnerActionDescriptor> ListActions()
        {
            var response = SendRequest("list_actions", string.Empty, "{}");
            return response.Success && response.Actions.Count > 0 ? response.Actions : Array.Empty<RunnerActionDescriptor>();
        }

        public RunnerActionInvokeResult InvokeAction(string actionId, string serializedArguments = "{}")
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                return new RunnerActionInvokeResult
                {
                    Success = false,
                    ErrorCode = "invalid_action_id",
                    Message = "Action id is required.",
                };
            }

            var response = SendRequest("invoke_action", actionId, string.IsNullOrWhiteSpace(serializedArguments) ? "{}" : serializedArguments);
            return new RunnerActionInvokeResult
            {
                Success = response.Success,
                ErrorCode = response.ErrorCode,
                Message = response.Message,
            };
        }

        private static RunnerActionResponse SendRequest(string requestType, string actionId, string arguments)
        {
            var pipeName = Path.GetFileName(Constants.PowerToysActionsPipe());
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.None);
            pipe.Connect(2000);

            var payload = BuildRequestPayload(requestType, actionId, arguments);
            var lengthBuffer = BitConverter.GetBytes(payload.Length);
            pipe.Write(lengthBuffer, 0, lengthBuffer.Length);
            pipe.Write(payload, 0, payload.Length);
            pipe.Flush();

            var responseLengthBuffer = ReadExact(pipe, sizeof(int));
            var responseLength = BitConverter.ToInt32(responseLengthBuffer, 0);
            var responsePayload = responseLength == 0 ? Array.Empty<byte>() : ReadExact(pipe, responseLength);
            return ParseResponse(responsePayload);
        }

        private static byte[] BuildRequestPayload(string requestType, string actionId, string arguments)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("type", requestType);
                if (!string.IsNullOrWhiteSpace(actionId))
                {
                    writer.WriteString("action_id", actionId);
                }

                writer.WriteString("arguments", arguments);
                writer.WriteEndObject();
            }

            return stream.ToArray();
        }

        private static RunnerActionResponse ParseResponse(byte[] payload)
        {
            if (payload.Length == 0)
            {
                return RunnerActionResponse.CreateError("empty_response", "Runner returned an empty response.");
            }

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var response = new RunnerActionResponse();

            if (root.TryGetProperty("success", out var successElement) &&
                (successElement.ValueKind == JsonValueKind.True || successElement.ValueKind == JsonValueKind.False))
            {
                response.Success = successElement.GetBoolean();
            }

            if (root.TryGetProperty("error_code", out var errorCodeElement) && errorCodeElement.ValueKind == JsonValueKind.String)
            {
                response.ErrorCode = errorCodeElement.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String)
            {
                response.Message = messageElement.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("actions", out var actionsElement) && actionsElement.ValueKind == JsonValueKind.Array)
            {
                response.Actions = ParseActions(actionsElement);
            }

            return response;
        }

        private static List<RunnerActionDescriptor> ParseActions(JsonElement actionsElement)
        {
            var actions = new List<RunnerActionDescriptor>();
            foreach (var element in actionsElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                actions.Add(new RunnerActionDescriptor
                {
                    ActionId = GetStringProperty(element, "action_id"),
                    ModuleKey = GetStringProperty(element, "module_key"),
                    DisplayName = GetStringProperty(element, "display_name"),
                    Description = GetStringProperty(element, "description"),
                    Category = GetStringProperty(element, "category"),
                    Available = GetBoolProperty(element, "available"),
                });
            }

            return actions;
        }

        private static string GetStringProperty(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
        }

        private static bool GetBoolProperty(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) &&
                (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False) &&
                property.GetBoolean();
        }

        private static byte[] ReadExact(Stream stream, int length)
        {
            var buffer = new byte[length];
            var offset = 0;
            while (offset < length)
            {
                var bytesRead = stream.Read(buffer, offset, length - offset);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException("Unexpected end of stream while reading runner action response.");
                }

                offset += bytesRead;
            }

            return buffer;
        }

        private sealed class RunnerActionResponse
        {
            public bool Success { get; set; }

            public string ErrorCode { get; set; } = string.Empty;

            public string Message { get; set; } = string.Empty;

            public List<RunnerActionDescriptor> Actions { get; set; } = new();

            public static RunnerActionResponse CreateError(string errorCode, string message)
            {
                return new RunnerActionResponse
                {
                    Success = false,
                    ErrorCode = errorCode,
                    Message = message,
                };
            }
        }
    }
}
