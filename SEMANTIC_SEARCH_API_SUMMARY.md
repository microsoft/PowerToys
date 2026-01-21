# Windows App SDK Semantic Search API 总结

## 1. 环境与依赖

| 项目 | 版本/值 |
|------|---------|
| **Windows App SDK** | `2.0.0-experimental3` |
| **.NET** | `net9.0-windows10.0.26100.0` |
| **AI Search NuGet** | `Microsoft.WindowsAppSDK.AI` (2.0.57-experimental) |
| **命名空间** | `Microsoft.Windows.AI.Search.Experimental.AppContentIndex` |
| **应用类型** | WinUI 3 MSIX 打包应用 |

---

## 2. 核心 API

### 2.1 索引管理
```csharp
// 创建/打开索引
var result = AppContentIndexer.GetOrCreateIndex("indexName");
if (result.Succeeded) {
    _indexer = result.Indexer;
    // result.Status: CreatedNew | OpenedExisting
}

// 等待索引能力就绪
await _indexer.WaitForIndexCapabilitiesAsync();

// 等待索引空闲（建索引完成）
await _indexer.WaitForIndexingIdleAsync(TimeSpan.FromSeconds(120));

// 清理
_indexer.RemoveAll();  // 删除所有索引
_indexer.Remove(id);   // 删除单个
_indexer.Dispose();
```

### 2.2 添加内容到索引
```csharp
// 索引文本 → 自动建立 TextLexical + TextSemantic 索引
IndexableAppContent textContent = AppManagedIndexableAppContent.CreateFromString(id, text);
_indexer.AddOrUpdate(textContent);

// 索引图片 → 自动建立 ImageSemantic + ImageOcr 索引
IndexableAppContent imageContent = AppManagedIndexableAppContent.CreateFromBitmap(id, softwareBitmap);
_indexer.AddOrUpdate(imageContent);
```

### 2.3 查询
```csharp
// 文本查询
TextQueryOptions options = new TextQueryOptions {
    Language = "en-US",                          // 可选
    MatchScope = QueryMatchScope.Unconstrained,  // 匹配范围
    TextMatchType = TextLexicalMatchType.Fuzzy   // Fuzzy | Exact
};
AppIndexTextQuery query = _indexer.CreateTextQuery(searchText, options);
IReadOnlyList<TextQueryMatch> matches = query.GetNextMatches(5);

// 图片查询
ImageQueryOptions imgOptions = new ImageQueryOptions {
    MatchScope = QueryMatchScope.Unconstrained,
    ImageOcrTextMatchType = TextLexicalMatchType.Fuzzy
};
AppIndexImageQuery imgQuery = _indexer.CreateImageQuery(searchText, imgOptions);
IReadOnlyList<ImageQueryMatch> imgMatches = imgQuery.GetNextMatches(5);
```

### 2.4 能力检查（只读）
```csharp
IndexCapabilities capabilities = _indexer.GetIndexCapabilities();

bool textLexicalOK = capabilities.GetCapabilityState(IndexCapability.TextLexical)
    .InitializationStatus == IndexCapabilityInitializationStatus.Initialized;
bool textSemanticOK = capabilities.GetCapabilityState(IndexCapability.TextSemantic)
    .InitializationStatus == IndexCapabilityInitializationStatus.Initialized;
bool imageSemanticOK = capabilities.GetCapabilityState(IndexCapability.ImageSemantic)
    .InitializationStatus == IndexCapabilityInitializationStatus.Initialized;
bool imageOcrOK = capabilities.GetCapabilityState(IndexCapability.ImageOcr)
    .InitializationStatus == IndexCapabilityInitializationStatus.Initialized;
```

---

## 3. 四种索引能力

| 能力 | 说明 | 触发方式 |
|------|------|----------|
| `TextLexical` | 词法/关键词搜索 | CreateFromString() 自动 |
| `TextSemantic` | AI 语义搜索 (Embedding) | CreateFromString() 自动 |
| `ImageSemantic` | 图像语义搜索 | CreateFromBitmap() 自动 |
| `ImageOcr` | 图片 OCR 文字搜索 | CreateFromBitmap() 自动 |

---

## 4. 可控选项（有限）

### TextQueryOptions
| 属性 | 类型 | 说明 |
|------|------|------|
| `Language` | string | 查询语言（可选，如 "en-US"）|
| `MatchScope` | QueryMatchScope | Unconstrained / Region / ContentItem |
| `TextMatchType` | TextLexicalMatchType | **Fuzzy** / Exact（仅影响 Lexical）|

### ImageQueryOptions
| 属性 | 类型 | 说明 |
|------|------|------|
| `MatchScope` | QueryMatchScope | Unconstrained / Region / ContentItem |
| `ImageOcrTextMatchType` | TextLexicalMatchType | **Fuzzy** / Exact（仅影响 OCR）|

### 枚举值说明

**QueryMatchScope:**
- `Unconstrained` - 无约束，同时使用 Lexical + Semantic
- `Region` - 限制在特定区域
- `ContentItem` - 限制在单个内容项

**TextLexicalMatchType:**
- `Fuzzy` - 模糊匹配，允许拼写错误、近似词
- `Exact` - 精确匹配，必须完全一致

---

## 5. 关键限制 ⚠️

| 限制 | 说明 |
|------|------|
| **不能单独指定 Semantic/Lexical** | 系统自动同时使用所有可用能力 |
| **Fuzzy/Exact 只影响 Lexical** | 对 Semantic 搜索无效 |
| **能力检查是只读的** | `GetIndexCapabilities()` 只能查看，不能控制 |
| **无相似度阈值** | 不能设置 Semantic 匹配的阈值 |
| **无结果排序控制** | 无法指定按相关度或其他方式排序 |
| **语言需手动传** | 不会自动检测，需开发者指定 |
| **无相关度分数** | 查询结果不返回匹配分数 |

---

## 6. 典型使用流程

```
┌─────────────────────────────────────────────────────────┐
│                    App 启动时                           │
├─────────────────────────────────────────────────────────┤
│ 1. GetOrCreateIndex("name")     // 创建/打开索引        │
│ 2. WaitForIndexCapabilitiesAsync()  // 等待能力就绪     │
│ 3. GetIndexCapabilities()        // 检查可用能力        │
│ 4. IndexAll()                    // 索引所有数据        │
│    ├─ CreateFromString() × N                           │
│    └─ CreateFromBitmap() × N                           │
│ 5. WaitForIndexingIdleAsync()    // 等待索引完成        │
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│                    运行时查询                            │
├─────────────────────────────────────────────────────────┤
│ 1. CreateTextQuery(text, options)   // 创建查询         │
│ 2. query.GetNextMatches(N)          // 获取结果         │
│ 3. 处理 TextQueryMatch / ImageQueryMatch               │
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│                    App 退出时                           │
├─────────────────────────────────────────────────────────┤
│ 1. _indexer.RemoveAll()             // 清理索引         │
│ 2. _indexer.Dispose()               // 释放资源         │
└─────────────────────────────────────────────────────────┘
```

---

## 7. 查询结果处理

```csharp
// 文本查询结果
foreach (var match in textMatches)
{
    if (match.ContentKind == QueryMatchContentKind.AppManagedText)
    {
        AppManagedTextQueryMatch textResult = (AppManagedTextQueryMatch)match;
        string contentId = match.ContentId;      // 内容 ID
        int offset = textResult.TextOffset;      // 匹配文本偏移
        int length = textResult.TextLength;      // 匹配文本长度
    }
}

// 图片查询结果
foreach (var match in imageMatches)
{
    if (match.ContentKind == QueryMatchContentKind.AppManagedImage)
    {
        AppManagedImageQueryMatch imageResult = (AppManagedImageQueryMatch)match;
        string contentId = imageResult.ContentId;  // 图片 ID
    }
}
```

---

## 8. 能力变化监听

```csharp
// 监听索引能力变化
_indexer.Listener.IndexCapabilitiesChanged += (indexer, capabilities) =>
{
    // 重新检查能力状态，更新 UI
    LoadAppIndexCapabilities();
};
```

---

## 9. 结论

这是一个**高度封装的黑盒 API**：

### 优点 ✅
- 简单易用，几行代码即可实现搜索
- 自动处理 Lexical + Semantic
- 支持文本和图片多模态搜索
- 系统级集成，无需额外部署模型

### 缺点 ❌
- 无法精细控制搜索类型
- 不能只用 Semantic Search
- 选项有限，缺乏高级配置
- 实验性 API，可能变更

### 替代方案
**如果需要纯 Semantic Search（向量搜索）**，建议：
- 直接使用 Embedding 模型生成向量
- 配合向量数据库（Azure Cosmos DB、FAISS、Qdrant 等）

---

## 10. 相关 NuGet 包

```xml
<PackageReference Include="Microsoft.WindowsAppSDK" Version="2.0.0-experimental3" />
<PackageReference Include="Microsoft.WindowsAppSDK.AI" Version="2.0.57-experimental" />
```

---

*文档生成日期: 2026-01-21*
