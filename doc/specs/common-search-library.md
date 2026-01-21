# Common.Search Library Specification

## Overview

本文档描述 `Common.Search` 库的重构设计，目标是提供一个通用的、可插拔的搜索框架，支持多种搜索引擎实现（Fuzzy Match、Semantic Search 等）。

## Goals

1. **解耦** - 搜索引擎与数据源完全解耦
2. **可插拔** - 支持替换不同的搜索引擎实现
3. **泛型** - 不绑定特定业务类型（如 SettingEntry）
4. **可组合** - 支持多引擎组合（即时 Fuzzy + 延迟 Semantic）
5. **可复用** - 可被 PowerToys 多个模块使用

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Consumer (e.g., Settings.UI)               │
├─────────────────────────────────────────────────────────────────┤
│  SettingsDataProvider        ← 业务特定的数据加载               │
│  SettingsSearchService       ← 业务特定的搜索服务               │
│  SettingEntry : ISearchable  ← 业务实体实现搜索契约             │
└─────────────────────────────────────────────────────────────────┘
                              │ uses
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Common.Search (Library)                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                    Core Abstractions                     │   │
│  ├─────────────────────────────────────────────────────────┤   │
│  │  ISearchable              ← 可搜索内容契约               │   │
│  │  ISearchEngine<T>         ← 搜索引擎接口                 │   │
│  │  SearchResult<T>          ← 统一结果模型                 │   │
│  │  SearchOptions            ← 搜索选项                     │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                    Implementations                       │   │
│  ├─────────────────────────────────────────────────────────┤   │
│  │  FuzzSearch/                                             │   │
│  │  ├── FuzzSearchEngine<T>  ← 内存 Fuzzy 搜索              │   │
│  │  ├── StringMatcher        ← 现有的模糊匹配算法           │   │
│  │  └── MatchResult          ← Fuzzy 匹配结果               │   │
│  │                                                          │   │
│  │  SemanticSearch/                                         │   │
│  │  ├── SemanticSearchEngine ← Windows AI Search 封装       │   │
│  │  └── SemanticSearchCapabilities                          │   │
│  │                                                          │   │
│  │  CompositeSearch/                                        │   │
│  │  └── CompositeSearchEngine<T> ← 多引擎组合               │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Core Interfaces

### ISearchable

定义可搜索内容的最小契约。

```csharp
namespace Common.Search;

/// <summary>
/// Defines a searchable item that can be indexed and searched.
/// </summary>
public interface ISearchable
{
    /// <summary>
    /// Gets the unique identifier for this item.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the primary searchable text (e.g., title, header).
    /// </summary>
    string SearchableText { get; }

    /// <summary>
    /// Gets optional secondary searchable text (e.g., description).
    /// Returns null if not available.
    /// </summary>
    string? SecondarySearchableText { get; }
}
```

### ISearchEngine&lt;T&gt;

搜索引擎核心接口。

```csharp
namespace Common.Search;

/// <summary>
/// Defines a pluggable search engine that can index and search items.
/// </summary>
/// <typeparam name="T">The type of items to search, must implement ISearchable.</typeparam>
public interface ISearchEngine<T> : IDisposable
    where T : ISearchable
{
    /// <summary>
    /// Gets a value indicating whether the engine is ready to search.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Gets the engine capabilities.
    /// </summary>
    SearchEngineCapabilities Capabilities { get; }

    /// <summary>
    /// Initializes the search engine.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes a single item.
    /// </summary>
    Task IndexAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes multiple items in batch.
    /// </summary>
    Task IndexBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an item from the index by its ID.
    /// </summary>
    Task RemoveAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all indexed items.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for items matching the query.
    /// </summary>
    Task<IReadOnlyList<SearchResult<T>>> SearchAsync(
        string query,
        SearchOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

### SearchResult&lt;T&gt;

统一的搜索结果模型。

```csharp
namespace Common.Search;

/// <summary>
/// Represents a search result with the matched item and scoring information.
/// </summary>
public sealed class SearchResult<T>
    where T : ISearchable
{
    /// <summary>
    /// Gets the matched item.
    /// </summary>
    public required T Item { get; init; }

    /// <summary>
    /// Gets the relevance score (higher is more relevant).
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// Gets the type of match that produced this result.
    /// </summary>
    public required SearchMatchKind MatchKind { get; init; }

    /// <summary>
    /// Gets the match details for highlighting (optional).
    /// </summary>
    public IReadOnlyList<MatchSpan>? MatchSpans { get; init; }
}

/// <summary>
/// Represents a span of matched text for highlighting.
/// </summary>
public readonly record struct MatchSpan(int Start, int Length);

/// <summary>
/// Specifies the kind of match that produced a search result.
/// </summary>
public enum SearchMatchKind
{
    /// <summary>Exact text match.</summary>
    Exact,

    /// <summary>Fuzzy/approximate text match.</summary>
    Fuzzy,

    /// <summary>Semantic/AI-based match.</summary>
    Semantic,

    /// <summary>Combined match from multiple engines.</summary>
    Composite,
}
```

### SearchOptions

搜索配置选项。

```csharp
namespace Common.Search;

/// <summary>
/// Options for configuring search behavior.
/// </summary>
public sealed class SearchOptions
{
    /// <summary>
    /// Gets or sets the maximum number of results to return.
    /// Default is 20.
    /// </summary>
    public int MaxResults { get; set; } = 20;

    /// <summary>
    /// Gets or sets the minimum score threshold (0.0 to 1.0).
    /// Results below this score are filtered out.
    /// Default is 0.0 (no filtering).
    /// </summary>
    public double MinScore { get; set; } = 0.0;

    /// <summary>
    /// Gets or sets the language hint for the search (e.g., "en-US").
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include match spans for highlighting.
    /// Default is false.
    /// </summary>
    public bool IncludeMatchSpans { get; set; } = false;
}
```

### SearchEngineCapabilities

引擎能力描述。

```csharp
namespace Common.Search;

/// <summary>
/// Describes the capabilities of a search engine.
/// </summary>
public sealed class SearchEngineCapabilities
{
    /// <summary>
    /// Gets a value indicating whether the engine supports fuzzy matching.
    /// </summary>
    public bool SupportsFuzzyMatch { get; init; }

    /// <summary>
    /// Gets a value indicating whether the engine supports semantic search.
    /// </summary>
    public bool SupportsSemanticSearch { get; init; }

    /// <summary>
    /// Gets a value indicating whether the engine persists the index to disk.
    /// </summary>
    public bool PersistsIndex { get; init; }

    /// <summary>
    /// Gets a value indicating whether the engine supports incremental indexing.
    /// </summary>
    public bool SupportsIncrementalIndex { get; init; }

    /// <summary>
    /// Gets a value indicating whether the engine supports match span highlighting.
    /// </summary>
    public bool SupportsMatchSpans { get; init; }
}
```

## Implementations

### FuzzSearchEngine&lt;T&gt;

基于现有 StringMatcher 的内存搜索引擎。

**特点：**
- 纯内存，无持久化
- 即时响应（毫秒级）
- 支持 match spans 高亮
- 基于字符的模糊匹配

**Capabilities：**
```csharp
new SearchEngineCapabilities
{
    SupportsFuzzyMatch = true,
    SupportsSemanticSearch = false,
    PersistsIndex = false,
    SupportsIncrementalIndex = true,
    SupportsMatchSpans = true,
}
```

### SemanticSearchEngine

基于 Windows App SDK AI Search API 的语义搜索引擎。

**特点：**
- 系统管理的持久化索引
- AI 驱动的语义理解
- 需要模型初始化（可能较慢）
- 可能不可用（依赖系统支持）

**Capabilities：**
```csharp
new SearchEngineCapabilities
{
    SupportsFuzzyMatch = true,  // API 同时提供 lexical + semantic
    SupportsSemanticSearch = true,
    PersistsIndex = true,
    SupportsIncrementalIndex = true,
    SupportsMatchSpans = false,  // API 不返回详细位置
}
```

**注意：** SemanticSearchEngine 不是泛型的，因为它需要将内容转换为字符串存入系统索引。实现时通过 `ISearchable` 接口提取文本。

### CompositeSearchEngine&lt;T&gt;

组合多个搜索引擎，支持 fallback 和结果合并。

```csharp
namespace Common.Search;

/// <summary>
/// A search engine that combines results from multiple engines.
/// </summary>
public sealed class CompositeSearchEngine<T> : ISearchEngine<T>
    where T : ISearchable
{
    /// <summary>
    /// Strategy for combining results from multiple engines.
    /// </summary>
    public enum CombineStrategy
    {
        /// <summary>Use first ready engine only.</summary>
        FirstReady,

        /// <summary>Merge results from all ready engines.</summary>
        MergeAll,

        /// <summary>Use primary, fallback to secondary if primary not ready.</summary>
        PrimaryWithFallback,
    }
}
```

**典型用法：** Fuzzy 作为即时响应，Semantic 准备好后增强结果。

## Directory Structure

```
src/common/Common.Search/
├── Common.Search.csproj
├── GlobalSuppressions.cs
├── ISearchable.cs
├── ISearchEngine.cs
├── SearchResult.cs
├── SearchOptions.cs
├── SearchEngineCapabilities.cs
├── SearchMatchKind.cs
├── MatchSpan.cs
│
├── FuzzSearch/
│   ├── FuzzSearchEngine.cs
│   ├── StringMatcher.cs         (existing)
│   ├── MatchOption.cs           (existing)
│   ├── MatchResult.cs           (existing)
│   └── SearchPrecisionScore.cs  (existing)
│
├── SemanticSearch/
│   ├── SemanticSearchEngine.cs
│   ├── SemanticSearchCapabilities.cs
│   └── SemanticSearchAdapter.cs  (adapts ISearchable to Windows API)
│
└── CompositeSearch/
    └── CompositeSearchEngine.cs
```

## Consumer Usage (Settings.UI)

### SettingEntry 实现 ISearchable

```csharp
// Settings.UI.Library/SettingEntry.cs
public struct SettingEntry : ISearchable
{
    // Existing properties...

    // ISearchable implementation
    public string Id => ElementUid ?? $"{PageTypeName}|{ElementName}";
    public string SearchableText => Header ?? string.Empty;
    public string? SecondarySearchableText => Description;
}
```

### SettingsSearchService

```csharp
// Settings.UI/Services/SettingsSearchService.cs
public sealed class SettingsSearchService : IDisposable
{
    private readonly ISearchEngine<SettingEntry> _engine;

    public SettingsSearchService(ISearchEngine<SettingEntry> engine)
    {
        _engine = engine;
    }

    public async Task InitializeAsync(IEnumerable<SettingEntry> entries)
    {
        await _engine.InitializeAsync();
        await _engine.IndexBatchAsync(entries);
    }

    public async Task<List<SettingEntry>> SearchAsync(string query, CancellationToken ct = default)
    {
        var results = await _engine.SearchAsync(query, cancellationToken: ct);
        return results.Select(r => r.Item).ToList();
    }
}
```

### Startup Configuration

```csharp
// Option 1: Fuzzy only (default, immediate)
var engine = new FuzzSearchEngine<SettingEntry>();

// Option 2: Semantic only (requires Windows AI)
var engine = new SemanticSearchAdapter<SettingEntry>("PowerToysSettings");

// Option 3: Composite (best of both worlds)
var engine = new CompositeSearchEngine<SettingEntry>(
    primary: new SemanticSearchAdapter<SettingEntry>("PowerToysSettings"),
    fallback: new FuzzSearchEngine<SettingEntry>(),
    strategy: CombineStrategy.PrimaryWithFallback
);

var searchService = new SettingsSearchService(engine);
await searchService.InitializeAsync(settingEntries);
```

## Migration Plan

### Phase 1: Core Abstractions
1. 创建 `ISearchable`, `ISearchEngine<T>`, `SearchResult<T>` 等核心接口
2. 保持现有 FuzzSearch 代码不变

### Phase 2: FuzzSearchEngine&lt;T&gt;
1. 创建泛型 `FuzzSearchEngine<T>` 实现
2. 内部复用现有 `StringMatcher`

### Phase 3: SemanticSearchEngine
1. 完善现有 `SemanticSearchEngine` 实现
2. 创建 `SemanticSearchAdapter<T>` 桥接泛型接口

### Phase 4: Settings.UI Migration
1. `SettingEntry` 实现 `ISearchable`
2. 创建 `SettingsSearchService`
3. 迁移 `SearchIndexService` 到新架构
4. 保持 API 兼容，逐步废弃旧方法

### Phase 5: CompositeSearchEngine (Optional)
1. 实现组合引擎
2. 支持 Fuzzy + Semantic 混合搜索

## Open Questions

1. **是否需要支持图片搜索？** 当前 SemanticSearchEngine 支持 `IndexImage`，但 `ISearchable` 只有文本。如果需要图片，可能需要 `IImageSearchable` 扩展。

2. **结果去重策略？** CompositeEngine 合并结果时，同一个 Item 可能被多个引擎匹配，如何去重和合并分数？

3. **异步 vs 同步？** FuzzSearch 完全可以同步执行，但接口统一用 `Task` 是否合适？考虑提供同步重载？

4. **索引更新策略？** 当 Settings 内容变化时（例如用户切换语言），如何高效更新索引？

---

*Document Version: 1.0*
*Last Updated: 2026-01-21*
