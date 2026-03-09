// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * VNode → CmdPal SDK type translator.
 *
 * Converts VNode trees captured by the React reconciler into CmdPal-
 * compatible SDK objects. Produces concrete classes that implement the
 * same interfaces as the CmdPal TypeScript SDK.
 *
 * Mapping:
 *   List                → RaycastDynamicListPage  (IDynamicListPage)
 *   List.Item           → RaycastListItem         (IListItem)
 *   List.Section        → section grouping on items
 *   Detail              → RaycastContentPage      (IContentPage)
 *   Detail.Markdown     → RaycastMarkdownContent  (IMarkdownContent)
 *   Detail.Metadata.*   → IDetails / IDetailsElement
 *   ActionPanel/Action  → IContextItem on moreCommands
 *   Form                → RaycastContentPage with form description
 *   Unknown             → null (graceful fallback)
 */

// ══════════════════════════════════════════════════════════════════════════
// Types (compatible with CmdPal SDK generated types)
// ══════════════════════════════════════════════════════════════════════════

export interface IconData {
  icon?: string;
  data?: string;
}

export interface IconInfo {
  light?: IconData | string;
  dark?: IconData | string;
}

export interface Tag {
  text: string;
  icon?: IconInfo;
  toolTip?: string;
}

export interface ContextItem {
  title?: string;
  subtitle?: string;
  icon?: IconInfo;
  command?: unknown;
  moreCommands?: ContextItem[];
  isCritical?: boolean;
}

export interface DetailsElement {
  key: string;
  icon?: IconInfo;
  data?: unknown;
}

export interface Details {
  title?: string;
  body?: string;
  heroImage?: IconInfo;
  metadata?: DetailsElement[];
}

// ── Input type — accepts any VNode-shaped object ────────────────────────

interface VNodeInput {
  type: string;
  props: Record<string, unknown>;
  children: unknown[];
}

// ══════════════════════════════════════════════════════════════════════════
// Concrete CmdPal-compatible classes
// ══════════════════════════════════════════════════════════════════════════

/**
 * ListItem produced by the translator.
 * Implements the IListItem shape from CmdPal SDK.
 */
export class RaycastListItem {
  title: string = '';
  subtitle: string = '';
  icon?: IconInfo;
  command?: unknown;
  moreCommands: ContextItem[] = [];
  tags: Tag[] = [];
  details?: Details;
  section: string = '';
  textToSuggest: string = '';
}

/**
 * DynamicListPage produced by translating a Raycast List VNode.
 * Implements the IDynamicListPage shape from CmdPal SDK.
 */
export class RaycastDynamicListPage {
  readonly _type = 'dynamicListPage';
  id: string = '';
  name: string = '';
  icon?: IconInfo;
  placeholderText: string = '';
  searchText: string = '';
  showDetails: boolean = false;
  hasMoreItems: boolean = false;
  gridProperties?: { layout: string; showTitle: boolean; showSubtitle: boolean };

  PropChanged?: (args: unknown) => void;
  ItemsChanged?: (args: unknown) => void;

  private _items: RaycastListItem[];
  private _onSearchTextChange?: (text: string) => void;

  constructor(
    items: RaycastListItem[] = [],
    onSearchTextChange?: (text: string) => void,
  ) {
    this._items = items;
    this._onSearchTextChange = onSearchTextChange;
  }

  getItems(): RaycastListItem[] {
    return this._items;
  }

  updateSearchText(oldSearch: string, newSearch: string): void {
    this._onSearchTextChange?.(newSearch);
  }

  setSearchText(searchText: string): void {
    const oldSearch = this.searchText;
    this.searchText = searchText;
    this.updateSearchText(oldSearch, searchText);
  }

  loadMore(): void {}
}

/**
 * MarkdownContent produced by translating Raycast Detail markdown.
 * Implements the IMarkdownContent shape from CmdPal SDK.
 */
export class RaycastMarkdownContent {
  type: string = 'markdown';
  id: string = '';
  body: string = '';

  PropChanged?: (args: unknown) => void;

  constructor(body?: string) {
    if (body !== undefined) this.body = body;
  }
}

/**
 * ContentPage produced by translating Raycast Detail or Form VNodes.
 * Implements the IContentPage shape from CmdPal SDK.
 */
export class RaycastContentPage {
  readonly _type = 'contentPage';
  id: string = '';
  name: string = '';
  icon?: IconInfo;
  details?: Details;
  commands: ContextItem[] = [];

  PropChanged?: (args: unknown) => void;
  ItemsChanged?: (args: unknown) => void;

  private _content: RaycastMarkdownContent[];

  constructor(content: RaycastMarkdownContent[] = []) {
    this._content = content;
  }

  getContent(): RaycastMarkdownContent[] {
    return this._content;
  }
}

// ══════════════════════════════════════════════════════════════════════════
// Internal helpers
// ══════════════════════════════════════════════════════════════════════════

/** Type guard: is this child a valid VNode (not null, not text)? */
function isVNode(node: unknown): node is VNodeInput {
  return (
    node != null &&
    typeof node === 'object' &&
    'type' in node &&
    typeof (node as any).type === 'string' &&
    (node as any).type !== '#text'
  );
}

/** Convert a Raycast icon prop to CmdPal IIconInfo format. */
function toIconInfo(raw: unknown): IconInfo | undefined {
  if (raw == null) return undefined;

  if (typeof raw === 'string') {
    const data: IconData = { icon: raw };
    return { light: data, dark: data };
  }

  if (typeof raw === 'object') {
    const obj = raw as Record<string, unknown>;
    // Already IIconInfo-shaped
    if ('light' in obj || 'dark' in obj) return raw as IconInfo;
    // Raycast Icon enum object with source property
    if ('source' in obj && typeof obj.source === 'string') {
      const data: IconData = { icon: obj.source };
      return { light: data, dark: data };
    }
  }

  return undefined;
}

interface RaycastAccessory {
  text?: string;
  icon?: unknown;
  tooltip?: string;
  tag?: string | { value: string; color?: unknown };
  date?: Date;
}

/** Convert Raycast accessories to CmdPal ITag array. */
function accessoriesToTags(accessories: RaycastAccessory[]): Tag[] {
  const tags: Tag[] = [];
  for (const acc of accessories) {
    if (acc.text) {
      tags.push({
        text: acc.text,
        toolTip: acc.tooltip,
        icon: toIconInfo(acc.icon),
      });
    } else if (acc.tag) {
      const text = typeof acc.tag === 'string' ? acc.tag : acc.tag.value;
      tags.push({ text });
    } else if (acc.icon) {
      tags.push({ text: '', icon: toIconInfo(acc.icon) });
    } else if (acc.date) {
      tags.push({ text: acc.date.toLocaleDateString() });
    }
  }
  return tags;
}

/** Convert a single Action VNode to a CmdPal IContextItem. */
function vnodeToContextItem(node: VNodeInput): ContextItem {
  return {
    title: (node.props.title as string) ?? node.type,
    icon: toIconInfo(node.props.icon),
  };
}

/** Walk an ActionPanel subtree and extract all actions as IContextItem[]. */
function extractActions(node: VNodeInput): ContextItem[] {
  const actions: ContextItem[] = [];

  for (const child of node.children) {
    if (!isVNode(child)) continue;

    if (child.type === 'ActionPanel') {
      for (const actionChild of child.children) {
        if (!isVNode(actionChild)) continue;

        if (actionChild.type === 'ActionPanel.Section') {
          for (const sectionChild of actionChild.children) {
            if (isVNode(sectionChild)) {
              actions.push(vnodeToContextItem(sectionChild));
            }
          }
        } else {
          actions.push(vnodeToContextItem(actionChild));
        }
      }
    }
  }

  return actions;
}

// ══════════════════════════════════════════════════════════════════════════
// List translation
// ══════════════════════════════════════════════════════════════════════════

function translateListItem(node: VNodeInput, section?: string): RaycastListItem {
  const item = new RaycastListItem();
  item.title = (node.props.title as string) ?? '';
  item.subtitle = (node.props.subtitle as string) ?? '';
  item.icon = toIconInfo(node.props.icon);
  item.section = section ?? '';

  const accessories = node.props.accessories as RaycastAccessory[] | undefined;
  if (accessories?.length) {
    item.tags = accessoriesToTags(accessories);
  }

  const keywords = node.props.keywords as string[] | undefined;
  if (keywords?.length) {
    item.textToSuggest = keywords.join(' ');
  }

  item.moreCommands = extractActions(node);

  return item;
}

function translateList(node: VNodeInput): RaycastDynamicListPage {
  const items: RaycastListItem[] = [];

  for (const child of node.children) {
    if (!isVNode(child)) continue;

    if (child.type === 'List.Item') {
      items.push(translateListItem(child));
    } else if (child.type === 'List.Section') {
      const sectionTitle = (child.props.title as string) ?? '';
      for (const sectionChild of child.children) {
        if (isVNode(sectionChild) && sectionChild.type === 'List.Item') {
          items.push(translateListItem(sectionChild, sectionTitle));
        }
      }
    }
    // Unknown child types silently skipped
  }

  const page = new RaycastDynamicListPage(
    items,
    node.props.onSearchTextChange as ((text: string) => void) | undefined,
  );
  page.name = (node.props.navigationTitle as string) ?? '';
  page.placeholderText = (node.props.searchBarPlaceholder as string) ?? '';

  return page;
}

// ══════════════════════════════════════════════════════════════════════════
// Grid translation (Grid → DynamicListPage, Grid.Item → ListItem)
// ══════════════════════════════════════════════════════════════════════════

function translateGridItem(node: VNodeInput, section?: string): RaycastListItem {
  const item = new RaycastListItem();
  item.title = (node.props.title as string) ?? '';
  item.subtitle = (node.props.subtitle as string) ?? '';

  // Grid.Item uses content.source for images; map to icon
  const content = node.props.content as { source?: string; tooltip?: string } | undefined;
  if (content?.source) {
    const data: IconData = { icon: content.source };
    item.icon = { light: data, dark: data };
  } else {
    item.icon = toIconInfo(node.props.icon);
  }

  item.section = section ?? '';

  const keywords = node.props.keywords as string[] | undefined;
  if (keywords?.length) {
    item.textToSuggest = keywords.join(' ');
  }

  item.moreCommands = extractActions(node);

  return item;
}

/**
 * Infer the CmdPal grid layout preset from Raycast Grid props.
 *
 * Raycast uses flexible `columns` (number); CmdPal has 3 presets:
 *   - "small"   → icon-only tiles (many columns)
 *   - "medium"  → tiles + title (moderate columns)
 *   - "gallery" → large tiles + title + subtitle (few columns)
 */
function inferGridLayout(columns?: number): { layout: string; showTitle: boolean; showSubtitle: boolean } {
  if (columns !== undefined && columns >= 7) {
    return { layout: 'small', showTitle: false, showSubtitle: false };
  }
  if (columns !== undefined && columns <= 3) {
    return { layout: 'gallery', showTitle: true, showSubtitle: true };
  }
  // Default: medium (4–6 columns or unspecified)
  return { layout: 'medium', showTitle: true, showSubtitle: false };
}

function translateGrid(node: VNodeInput): RaycastDynamicListPage {
  const items: RaycastListItem[] = [];

  for (const child of node.children) {
    if (!isVNode(child)) continue;

    if (child.type === 'Grid.Item') {
      items.push(translateGridItem(child));
    } else if (child.type === 'Grid.Section') {
      const sectionTitle = (child.props.title as string) ?? '';
      for (const sectionChild of child.children) {
        if (isVNode(sectionChild) && sectionChild.type === 'Grid.Item') {
          items.push(translateGridItem(sectionChild, sectionTitle));
        }
      }
    }
    // Unknown child types (Grid.EmptyView, etc.) silently skipped
  }

  const page = new RaycastDynamicListPage(
    items,
    node.props.onSearchTextChange as ((text: string) => void) | undefined,
  );
  page.name = (node.props.navigationTitle as string) ?? '';
  page.placeholderText = (node.props.searchBarPlaceholder as string) ?? '';
  page.gridProperties = inferGridLayout(node.props.columns as number | undefined);

  return page;
}

// ══════════════════════════════════════════════════════════════════════════
// Detail translation
// ══════════════════════════════════════════════════════════════════════════

function translateDetailMetadata(node: VNodeInput): DetailsElement[] {
  const elements: DetailsElement[] = [];

  for (const child of node.children) {
    if (!isVNode(child)) continue;

    switch (child.type) {
      case 'Detail.Metadata.Label':
        elements.push({
          key: (child.props.title as string) ?? '',
          data: { text: child.props.text ?? '' },
          icon: toIconInfo(child.props.icon),
        });
        break;

      case 'Detail.Metadata.Link':
        elements.push({
          key: (child.props.title as string) ?? '',
          data: {
            _type: 'link',
            text: (child.props.text as string) ?? '',
            url: (child.props.target as string) ?? '',
          },
        });
        break;

      case 'Detail.Metadata.Separator':
        elements.push({
          key: '__separator__',
          data: { _type: 'separator' },
        });
        break;

      case 'Detail.Metadata.TagList': {
        const tags: Tag[] = [];
        for (const tagChild of child.children) {
          if (isVNode(tagChild) && tagChild.type === 'Detail.Metadata.TagList.Item') {
            tags.push({ text: (tagChild.props.text as string) ?? '' });
          }
        }
        elements.push({
          key: (child.props.title as string) ?? '',
          data: { _type: 'tags', tags },
        });
        break;
      }

      default:
        // Unknown metadata type — skip gracefully
        break;
    }
  }

  return elements;
}

function translateDetail(node: VNodeInput): RaycastContentPage {
  const content: RaycastMarkdownContent[] = [];
  let detailsMetadata: DetailsElement[] | undefined;

  for (const child of node.children) {
    if (!isVNode(child)) continue;

    if (child.type === 'Detail.Markdown') {
      content.push(
        new RaycastMarkdownContent((child.props.content as string) ?? ''),
      );
    } else if (child.type === 'Detail.Metadata') {
      detailsMetadata = translateDetailMetadata(child);
    }
  }

  // Fallback: check props.markdown (older Raycast pattern)
  if (content.length === 0 && typeof node.props.markdown === 'string') {
    content.push(new RaycastMarkdownContent(node.props.markdown));
  }

  const page = new RaycastContentPage(content);
  page.name = (node.props.navigationTitle as string) ?? '';

  if (detailsMetadata?.length) {
    page.details = { metadata: detailsMetadata };
  }

  return page;
}

// ══════════════════════════════════════════════════════════════════════════
// Form translation (stub — form fields as markdown description)
// ══════════════════════════════════════════════════════════════════════════

function translateForm(node: VNodeInput): RaycastContentPage {
  // Represent form fields as a markdown description for now.
  // Full FormContent integration will land with the bridge layer.
  const fields: string[] = [];

  for (const child of node.children) {
    if (!isVNode(child)) continue;
    const fieldTitle =
      (child.props.title as string) ??
      (child.props.id as string) ??
      child.type;
    fields.push(`- **${fieldTitle}** (${child.type})`);
  }

  const md =
    fields.length > 0 ? `# Form\n\n${fields.join('\n')}` : '# Form';

  const page = new RaycastContentPage([new RaycastMarkdownContent(md)]);
  page.name = (node.props.navigationTitle as string) ?? '';

  return page;
}

// ══════════════════════════════════════════════════════════════════════════
// Public API
// ══════════════════════════════════════════════════════════════════════════

/**
 * Translate a single VNode into a CmdPal-compatible SDK object.
 *
 * @param vnode - The root VNode from the reconciler tree
 * @returns A CmdPal page object, or null for unknown/unsupported types
 */
export function translateVNode(
  vnode: { type: string; props: Record<string, unknown>; children: unknown[] },
): RaycastDynamicListPage | RaycastContentPage | null {
  if (!vnode?.type) return null;

  switch (vnode.type) {
    case 'List':
      return translateList(vnode);
    case 'Grid':
      return translateGrid(vnode);
    case 'Detail':
      return translateDetail(vnode);
    case 'Form':
      return translateForm(vnode);
    default:
      return null;
  }
}
