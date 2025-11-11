// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using Microsoft.PowerToys.Settings.UI.Library;
using Windows.AI.MachineLearning;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class LocalModelPasteProvider : IPasteAIProvider
    {
        private static readonly IReadOnlyCollection<AIServiceType> SupportedTypes = new[]
        {
            AIServiceType.Onnx,
            AIServiceType.ML,
        };

        public static PasteAIProviderRegistration Registration { get; } = new(SupportedTypes, config => new LocalModelPasteProvider(config));

        private readonly PasteAIConfig _config;

        private static readonly ConcurrentDictionary<string, Lazy<Task<LearningModel>>> ModelCache = new(StringComparer.OrdinalIgnoreCase);

        public LocalModelPasteProvider(PasteAIConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public async Task<string> ProcessPasteAsync(PasteAIRequest request, CancellationToken cancellationToken, IProgress<double> progress)
        {
            ArgumentNullException.ThrowIfNull(request);

            cancellationToken.ThrowIfCancellationRequested();

            var modelPath = _config.LocalModelPath ?? _config.ModelPath;
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                throw new PasteActionException(GetInferenceErrorMessage(), new InvalidOperationException("Local model path not provided."));
            }

            try
            {
                var model = await GetOrLoadModelAsync(modelPath).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                using LearningModelSession session = new(model);
                var binding = new LearningModelBinding(session);

                BindInputs(session.Model, binding, request);

                var correlationId = Guid.NewGuid().ToString();
                var evaluation = await session.EvaluateAsync(binding, correlationId).AsTask(cancellationToken).ConfigureAwait(false);

                var output = ExtractStringOutput(session.Model, evaluation);
                request.Usage = AIServiceUsage.None;
                progress?.Report(1.0);
                return output ?? string.Empty;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PasteActionException(GetInferenceErrorMessage(), ex);
            }
        }

        private static async Task<LearningModel> GetOrLoadModelAsync(string modelPath)
        {
            var loader = ModelCache.GetOrAdd(modelPath, path => new Lazy<Task<LearningModel>>(() => Task.Run(() => LearningModel.LoadFromFilePath(path))));
            return await loader.Value.ConfigureAwait(false);
        }

        private static void BindInputs(LearningModel model, LearningModelBinding binding, PasteAIRequest request)
        {
            var stringInputs = model.InputFeatures
                .OfType<TensorFeatureDescriptor>()
                .Where(descriptor => descriptor.TensorKind == TensorKind.String)
                .ToList();

            if (stringInputs.Count == 0)
            {
                throw new PasteActionException(GetInferenceErrorMessage(), new InvalidOperationException("Model does not expose string inputs."));
            }

            var prompt = request.Prompt ?? string.Empty;
            var input = request.InputText ?? string.Empty;
            var combined = string.IsNullOrWhiteSpace(prompt) ? input : string.Join(Environment.NewLine + Environment.NewLine, prompt, input);

            foreach (var inputDescriptor in stringInputs)
            {
                var value = ResolveInputValue(inputDescriptor.Name, prompt, input, combined);
                var tensor = TensorString.CreateFromArray(new long[] { 1 }, new[] { value });
                binding.Bind(inputDescriptor.Name, tensor);
            }
        }

        private static string ResolveInputValue(string featureName, string prompt, string input, string combined)
        {
            if (string.IsNullOrEmpty(featureName))
            {
                return combined;
            }

            switch (featureName.ToLowerInvariant())
            {
                case "prompt":
                case "instruction":
                case "instructions":
                    return prompt;
                case "input":
                case "text":
                case "input_text":
                case "inputtext":
                    return input;
                default:
                    return combined;
            }
        }

        private static string ExtractStringOutput(LearningModel model, LearningModelEvaluationResult evaluation)
        {
            foreach (var outputDescriptor in model.OutputFeatures.OfType<TensorFeatureDescriptor>().Where(descriptor => descriptor.TensorKind == TensorKind.String))
            {
                if (evaluation.Outputs.TryGetValue(outputDescriptor.Name, out var value) && value is TensorString tensor)
                {
                    var vector = tensor.GetAsVectorView();
                    if (vector.Count > 0)
                    {
                        return vector[0];
                    }
                }
            }

            throw new PasteActionException(GetInferenceErrorMessage(), new InvalidOperationException("Model did not return a string output."));
        }

        private static string GetInferenceErrorMessage()
        {
            var message = ResourceLoaderInstance.ResourceLoader.GetString("AdvancedPasteCustomModelInferenceError");
            return string.IsNullOrWhiteSpace(message) ? "The custom model failed to generate a response." : message;
        }
    }
}
