# PowerOCR AI OCR Enhancement – Fixing Spec

## 1. Problem Statement
PowerOCR currently performs on‑device OCR using the existing Windows OCR APIs (classic text extraction path). While this works for many scenarios, recognition quality degrades for:
- Low‑contrast or stylized text
- Multilingual mixed‑script content
- Complex layouts (columns, tables, code blocks)
- Handwritten or partially handwritten text

Modern AI (Foundry) based text recognition models can materially improve accuracy, language coverage, and layout fidelity. Users increasingly expect higher accuracy with minimal configuration. The absence of an AI option reduces competitiveness and user satisfaction, especially for accessibility and productivity workflows. We need to add an optional (auto‑enabled when supported) AI recognition path without regressing performance or privacy expectations.

## 2. Root Cause Analysis (Why is this an issue?)
Not a defect in existing code; rather a capability gap:
- The module was built before Windows AI Foundry / updated Text Recognition APIs became broadly consumable.
- No abstraction layer exists today to plug in alternative recognizers.
- Settings model lacks a flag to select recognition backend.
- No capability detection logic to decide when an AI path is viable.

## 3. Proposed Solution
Introduce a dual‑path OCR pipeline with automatic capability detection and a new user setting `UseAITextRecognition` (visible with explanation + telemetry). Default behavior: enable AI path by default only when (a) OS / SDK exposes the AI API, (b) the AI model is loadable locally OR network policy allows remote model acquisition, and (c) device resources (basic heuristic: sufficient RAM + optional NPU/GPU availability if the API reports acceleration). Otherwise fall back seamlessly to the legacy path.

### 3.1 Affected Modules / Files
- `src/modules/PowerOCR/Settings/UserSettings.cs` – add boolean setting property `UseAITextRecognition`.
- `src/modules/PowerOCR/PowerOCR.csproj` – add SDK / package references for AI Foundry Text Recognition sample-derived API (e.g., `WindowsAppSDK`, potential `Microsoft.Windows.AIFoundry` NuGet once confirmed; placeholder until package name finalized).
- New helper: `src/modules/PowerOCR/Helpers/AiTextRecognizer.cs` – wraps AI API usage (model load, infer, cancellation, error mapping).
- Possibly new `Telemetry/PowerOCRAI*Event.cs` for AI-specific invocation & error telemetry.
- `OCROverlay.xaml.cs` (or whichever orchestrates capture) – inject backend selection logic.
- (Optional) `LanguageHelper.cs` – extend to supply multi‑language list supported by AI backend if broader than legacy.

### 3.2 Setting Semantics
Property: `bool UseAITextRecognition`.
Initialization logic:
1. On first run after upgrade: If setting absent → run `AiOcrCapabilityProbe()`.
2. If probe returns `SupportedAndRecommended` → persist `true`; else `false`.
3. User can override in Settings UI (toggle with tooltip: “Uses advanced AI-based text recognition for improved accuracy. Falls back automatically if unavailable.”).

### 3.3 Capability Probe (`AiOcrCapabilityProbe`)
Steps:
- Check OS version / contracts for AI Foundry namespace presence (reflection or `ApiInformation.IsTypePresent`).
- Attempt lightweight model metadata load (do not fully allocate full model unless required – e.g., call async load with cancellation token and early metadata query if API supports). If load fails with “not found” and network acquisition is allowed → queue background download prompt OR mark as `TemporarilyUnavailable`.
- Collect resource heuristics (e.g., memory >= 4GB free, CPU load < threshold) – informational only. Do not block enablement unless below a hard fail scenario.
- Return enum: `Unsupported`, `Supported`, `SupportedAndRecommended` (e.g., presence of NPU / GPU acceleration capability flags).

### 3.4 Recognition Flow Changes
Current Flow (simplified): Screen capture → preprocess (crop, scale) → Windows OCR API → results mapped to `ResultTable` / display overlay.

New Flow Decision:
```
if (settings.UseAITextRecognition && AiTextRecognizer.IsUsable)
    backend = AiTextRecognizer
else
    backend = LegacyOcrEngine
```

AI Path Pseudocode (derived conceptually from sample `TextRecognizerViewModel` in Windows AI Foundry examples):
```
var recognizer = await AiTextRecognizer.GetOrCreateAsync();
var result = await recognizer.RecognizeAsync(bitmap, languageCodes, cancellationToken);
// result should include text lines, bounding boxes, confidence, maybe block structure
MapToResultTable(result);
```

### 3.5 Mapping & Data Model
Extend `ResultRow` / `ResultColumn` only if necessary (e.g., to store per-line confidence). If added:
- Add nullable `double? Confidence` property (non-breaking for serialization if omitted when null).
- Keep legacy path populating `null` or approximate (if original API returns confidence per word or not at all, leave null).
Avoid altering existing public contract consumed by other modules; additions only.

### 3.6 Error Handling & Fallback
Errors (model load failure, inference exception, timeout) → log (`Logger.LogError`), emit telemetry event with error code bucketized, then fallback to legacy OCR automatically within the same user invocation (surface a subtle toast “AI recognition unavailable – used standard OCR”). Do not show a modal.

### 3.7 Performance Considerations
- Cache AI recognizer instance (singleton with lazy async init) to avoid repeated model load cost.
- Introduce throttling via existing `ThrottledActionInvoker` if capturing rapid sequences.
- Add cancellation support for ESC key path already present – ensure AI inference respects `CancellationToken` and disposes session promptly.

### 3.8 Telemetry Additions
Events:
- `PowerOCRAIInvokedEvent` – record backend used, model id, duration, success.
- `PowerOCRAIFallbackEvent` – record reason for fallback.
Respect existing telemetry opt-out; no PII or raw text content captured (only counts, lengths, languages, duration, error class).

### 3.9 Security & Privacy
- All processing remains on-device if model local. If future remote model fetching occurs, add explicit user consent. For this iteration assume LOCAL inference only.
- No captured images leave the device.

### 3.10 Settings UI Integration
Add a toggle in Settings app under PowerOCR section (C# Settings UI project). Label + description. Gray out (read-only ON) with explanatory note if forced by policy (future group policy extension) – out of scope now.

## 4. Alternative Approaches Considered
1. Replace legacy OCR entirely with AI path.
   - Pros: Simpler code.
   - Cons: Risk of regression on low-end devices / unsupported OS; increased cold start latency.
2. Offer manual user opt-in only (disabled by default).
   - Pros: Minimal auto-detection logic.
   - Cons: Many users unaware; under-utilization of improvement.
3. Pluggable provider interface (strategy pattern) now vs. minimal conditional.
   - Pros: Future extensibility (e.g., remote service, handwriting specialized model).
   - Cons: Extra abstraction now; might be premature.

Chosen path: Hybrid – minimal provider abstraction (`ITextRecognizerBackend` internal) to keep AI vs Legacy clean without broad refactor.

## 5. Impact Analysis
- Performance: First AI model load adds latency (cold start). Mitigation: lazy load only after first explicit OCR invocation (or optional warmup idle task). Subsequent calls near parity or faster (GPU/NPU acceleration) depending on hardware.
- Memory: Model resident memory footprint increases working set; monitor with lightweight telemetry metric (size bucket only).
- UX: Higher accuracy; occasional initial delay—communicate via transient “Loading AI model…” overlay if >300ms.
- Compatibility: Falls back automatically; no OS/version crash risk if probe gating correct.
- Security/Privacy: Maintains local processing; low risk.
- Localization: Setting label & description require new resource strings (update `.resx`). AI path may support more languages—ensure language dropdown merges sets or indicates when AI extends coverage.

## 6. Testing Plan
### 6.1 Unit Tests
- `UserSettings` default logic when no prior value and probe mocked to each enum outcome.
- Capability probe logic (mock `ApiInformation` & model load result paths).
- Mapping function from AI result structure → `ResultTable` (including confidence optional).

### 6.2 Integration / Functional Tests
- End-to-end invocation using AI path (mock or stub model returning deterministic text) – validate overlay shows expected lines & fallback triggers on forced exception.
- Toggle setting OFF → always legacy backend.
- Simulated load failure mid-session → fallback event & legacy result produced.

### 6.3 Manual Scenarios
- Clean upgrade from previous version (no setting stored) on supported vs unsupported machine.
- Large multilingual sample (e.g., English + Japanese) – verify improved recognition & no crash.
- Rapid consecutive captures – ensure no resource leak / deadlock.
- Cancellation (ESC) during AI load & during inference.

### 6.4 Performance Measurements
- Log and compare cold and warm inference times vs legacy on sample images.

### 6.5 Telemetry Validation
- Confirm events fire with sanitized fields; no raw text captured.

### 6.6 Non-Regression
- Legacy path unchanged output for baseline image set when AI disabled.

## 7. Compatibility / Rollback Plan
Feature flag is the setting itself. Rollback steps:
1. Hotfix: Ship minor update that forces `UseAITextRecognition=false` default and hides toggle (if severe issue).
2. Full revert: Remove AI helper class, setting property, and package reference; retain migration code that safely ignores stale JSON field.
3. Data Compatibility: Additional JSON field in settings is ignored by older clients (they just won’t read it) – safe.
4. Guard: Keep legacy path fully functional and exercised by tests to ensure quick fallback.

## 8. Open Questions / Risks
- Exact NuGet / namespace for Windows AI Foundry Text Recognition at release time (placeholder names must be validated).
- Model size & load performance on low-end hardware – may need asynchronous warmup.
- Potential GPU driver issues causing sporadic failures; may need heuristic to disable AI after N consecutive failures.
- Localization of extended language list (how to surface languages not in existing enum?).
- Future remote or cloud augmentation – out of scope now but influences architecture (provider abstraction chosen mitigates risk).
- Legal review if future remote model download is integrated (not in this iteration).

---
Prepared: 2025-09-16
Author: (Auto-generated spec via assistant)
Branch: `issue/100-ai-on-textextract`
