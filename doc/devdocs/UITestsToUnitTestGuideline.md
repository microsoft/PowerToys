# Guidelines: Moving UI Tests into Unit Tests (WinUI 3 / MVVM Focused)

## ✅ General Principles

* **UI tests are expensive** — reserve them for UI-specific behaviors only (e.g., layout, visual hierarchy, interaction with OS).
* **Push logic into ViewModels or services**, keeping Views as dumb as possible.
* **Validate data bindings and wiring once**; don’t repeatedly test binding correctness in every case.
* **Design with testability in mind** — favor DI, separation of concerns, and immutability.

---

## 🛠️ Best Practices \& Coding Guidelines

### a. ✔️ Verify Data Binding via Default vs Non-default Cases (Once)

* Ensure each UI Page has a **ViewModel that encapsulates state**.
* For each ViewModel:

  * Write unit tests that setting can be loaded and saved (e.g., default → custom).
  * Use 1–2 **UI integration tests** (can be screenshot or state probe) to validate:

    * Binding is correct
    * UI reflects the ViewModel state

```csharp
// Unit test example
\[Fact]
public void SettingEnabledFlag\_ShouldTriggerChange()
{
    var vm = new MySettingsViewModel();
    vm.IsFeatureEnabled = true;
    Assert.True(VerifyMySettings(SettingEnabledFlag.json));
}
```

> ✅ Don’t write multiple UI tests for each setting — use binding verification for confidence.

---

### b. 🧪 Business Logic Should Be Tested in ViewModel or Service

* Any logic that changes behavior based on inputs (e.g., "Action A should do X under Y setting") belongs in a **unit test**.
* Avoid verifying the same logic again via UI automation — instead:

  * Simulate inputs directly in the ViewModel
  * Assert the business result

```csharp
\[Fact]
public void Resize\_WithPercentSetting\_ShouldCalculateCorrectSize()
{
    var vm = new ResizeViewModel();
    vm.InputSize = new Size(100, 100);
    vm.DimensionUnit = ResizeUnit.Percent;
    vm.ResizeValue = 200;

    var result = vm.CalculateSize();
    Assert.Equal(new Size(200, 200), result);
}
```

> ✅ UI tests should NOT repeat all permutations — test logic in ViewModel.

---

### c. 🗱️ UI-Heavy Actions (e.g., Mouse, Focus) Should Be Stable and SOLID

* Abstract UI logic (e.g., pointer handling, UI effects) into **dedicated classes or handlers**.
* Follow **SOLID principles**:

  * Single Responsibility: one class = one behavior
  * Open/Closed: once stable, avoid modifying

* Write **manual test case or visual validation once**
* Avoid repeating UI automation if the logic or visual interaction never changes

> ✅ Document test cases if necessary, but don’t keep re-running UI tests once frozen and stable.

---

## 🔀 Patterns to Apply

* **Command-based actions** (`ICommand`) → Test logic behind the command, not the UI element
* **Observable ViewModels** (`INotifyPropertyChanged`) → Unit test property update logic and side effects
* **Service Abstractions** (`IFileService`, `IImageProcessor`) → Pure logic tests without UI

---

## ✅ Summary: How to Migrate UI Tests Effectively

| Strategy                          | Practice                                |
| --------------------------------- | --------------------------------------- |
| 🔄 Move logic to ViewModel        | Encapsulate all state \& decision-making |
| 🔬 Write unit tests for all logic | Validate behavior without UI            |
| 🧪 UI test only once for binding  | Default vs non-default case             |
| 🚫 Avoid repeating UI tests       | Don't verify same flow again via UI     |
| 🗱️ Design for stability          | Apply SOLID, document UI-only behaviors |

