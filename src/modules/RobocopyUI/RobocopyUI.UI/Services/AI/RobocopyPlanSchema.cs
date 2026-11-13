// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.Extensions.AI;

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// The JSON schema for the object a model must return to describe a robocopy plan. Providers hand
    /// this to the runtime so that structured-output-capable models are constrained to the exact shape
    /// <see cref="RobocopyCommandGenerator"/> parses, which removes a large class of run-to-run drift.
    /// </summary>
    public static class RobocopyPlanSchema
    {
        /// <summary>
        /// The schema name reported to the model runtime.
        /// </summary>
        public const string SchemaName = "robocopy_plan";

        /// <summary>
        /// A short description of what the schema represents.
        /// </summary>
        public const string SchemaDescription = "A validated robocopy command plan, or a request for one missing path.";

        private const string SchemaJson = """
            {
              "type": "object",
              "properties": {
                "needsFollowUp": {
                  "type": "boolean",
                  "description": "True only when the source or destination path is genuinely unknown."
                },
                "followUpQuestion": {
                  "type": "string",
                  "description": "The question to ask when needsFollowUp is true; otherwise an empty string."
                },
                "source": {
                  "type": "string",
                  "description": "The folder to copy from."
                },
                "destination": {
                  "type": "string",
                  "description": "The folder to copy to."
                },
                "options": {
                  "type": "array",
                  "description": "The robocopy switches to apply, in the order they should appear.",
                  "items": {
                    "type": "object",
                    "properties": {
                      "name": {
                        "type": "string",
                        "description": "The switch including its leading slash and with no colon or value, e.g. /E or /R."
                      },
                      "value": {
                        "type": "string",
                        "description": "The text after the colon, or an empty string for a plain flag."
                      }
                    },
                    "required": ["name", "value"],
                    "additionalProperties": false
                  }
                },
                "explanation": {
                  "type": "string",
                  "description": "A plain-language summary of what the command does."
                },
                "warnings": {
                  "type": "array",
                  "description": "Warnings for switches that delete or overwrite data at the destination.",
                  "items": { "type": "string" }
                }
              },
              "required": ["needsFollowUp", "followUpQuestion", "source", "destination", "options", "explanation", "warnings"],
              "additionalProperties": false
            }
            """;

        private static readonly JsonElement Schema = JsonDocument.Parse(SchemaJson).RootElement.Clone();

        /// <summary>
        /// Creates a response format that constrains the model to the plan schema.
        /// </summary>
        public static ChatResponseFormat CreateResponseFormat()
            => ChatResponseFormat.ForJsonSchema(Schema, SchemaName, SchemaDescription);
    }
}
