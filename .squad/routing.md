# Routing Rules — Command Palette

## Domain Routing

| Signal / Keywords | Route To | Why |
|-------------------|----------|-----|
| XAML, UI, layout, styling, pages, controls, views, theme, animation, accessibility | Dallas | WinUI 3 / XAML specialist |
| ViewModel, binding, MVVM, command, property changed, observable | Dallas | MVVM is UI-layer concern |
| AOT, trimming, binary size, PublishAot, LINQ removal | Dallas + Ripley | AOT affects UI project primarily; Lead reviews |
| Extension, plugin, SDK, WinRT, IDynamicListPage, ICommandProvider | Parker | Extension SDK & WinRT interop |
| C++, native, keyboard hook, module interface, COM | Parker | C++ native code |
| Toolkit, extension helpers, ExtensionObject, MarkdownHelper | Parker | Extension toolkit layer |
| Test, unit test, coverage, mock, assert, edge case | Lambert | Testing specialist |
| Architecture, design, scope, trade-off, review, refactor strategy | Ripley | Lead makes structural calls |
| Performance, memory, profiling, hot path | Ripley + Parker | Lead + Core for perf work |
| New extension / new plugin | Parker (implement) + Lambert (tests) | Parallel: build + test |
| Bug fix (UI) | Dallas (fix) + Lambert (verify) | Parallel: fix + regression test |
| Bug fix (core/SDK) | Parker (fix) + Lambert (verify) | Parallel: fix + regression test |

## Boundary Enforcement

All agents MUST refuse work that touches files outside `src/modules/cmdpal/CommandPalette.slnf`.
If a task requires out-of-scope changes, escalate to Michael.

## Default

If unclear, route to Ripley (Lead). Ripley will delegate or handle.
