// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  DynamicListPage,
  ListItem,
  InvokableCommand,
  CommandResult,
  IListItem,
  IFilters,
  IFilterItem,
  ICommandResult,
  IconInfo,
  tag,
} from '@cmdpal/sdk';

// ---------------------------------------------------------------------------
// Sample data
// ---------------------------------------------------------------------------

interface Fruit {
  name: string;
  emoji: string;
  category: 'berry' | 'citrus' | 'tropical' | 'stone';
  color: string;
}

const FRUITS: Fruit[] = [
  { name: 'Strawberry', emoji: '🍓', category: 'berry', color: 'Red' },
  { name: 'Blueberry', emoji: '🫐', category: 'berry', color: 'Blue' },
  { name: 'Raspberry', emoji: '🍇', category: 'berry', color: 'Red' },
  { name: 'Orange', emoji: '🍊', category: 'citrus', color: 'Orange' },
  { name: 'Lemon', emoji: '🍋', category: 'citrus', color: 'Yellow' },
  { name: 'Grapefruit', emoji: '🍊', category: 'citrus', color: 'Pink' },
  { name: 'Lime', emoji: '🍈', category: 'citrus', color: 'Green' },
  { name: 'Mango', emoji: '🥭', category: 'tropical', color: 'Orange' },
  { name: 'Pineapple', emoji: '🍍', category: 'tropical', color: 'Yellow' },
  { name: 'Coconut', emoji: '🥥', category: 'tropical', color: 'White' },
  { name: 'Banana', emoji: '🍌', category: 'tropical', color: 'Yellow' },
  { name: 'Peach', emoji: '🍑', category: 'stone', color: 'Orange' },
  { name: 'Cherry', emoji: '🍒', category: 'stone', color: 'Red' },
  { name: 'Plum', emoji: '🫐', category: 'stone', color: 'Purple' },
  { name: 'Apricot', emoji: '🍑', category: 'stone', color: 'Orange' },
];

// ---------------------------------------------------------------------------
// Filters
// ---------------------------------------------------------------------------

class FruitFilters implements IFilters {
  currentFilterId: string = 'all';

  getFilters(): IFilterItem[] {
    return [
      { id: 'all', name: 'All Fruits', icon: IconInfo.fromGlyph('🍎') },
      { id: 'berry', name: 'Berries', icon: IconInfo.fromGlyph('🍓') },
      { id: 'citrus', name: 'Citrus', icon: IconInfo.fromGlyph('🍊') },
      { id: 'tropical', name: 'Tropical', icon: IconInfo.fromGlyph('🥭') },
      { id: 'stone', name: 'Stone Fruit', icon: IconInfo.fromGlyph('🍑') },
    ];
  }
}

// ---------------------------------------------------------------------------
// Select command (shows a toast)
// ---------------------------------------------------------------------------

class SelectFruitCommand extends InvokableCommand {
  constructor(fruit: Fruit) {
    super();
    this.id = `select-${fruit.name.toLowerCase()}`;
    this.name = fruit.name;
  }

  invoke(): ICommandResult {
    return CommandResult.showToast(`You picked: ${this.name}`);
  }
}

// ---------------------------------------------------------------------------
// Dynamic search page
// ---------------------------------------------------------------------------

/**
 * Demonstrates a dynamic list page with:
 * - Live search filtering as the user types
 * - Category filter tabs (All, Berries, Citrus, Tropical, Stone)
 * - Section grouping by category
 * - Tags showing color and fruit category
 */
export class DynamicSearchPage extends DynamicListPage {
  id = 'dynamic-search';
  name = 'Fruit Finder';
  placeholderText = 'Search fruits...';
  showDetails = false;
  filters: FruitFilters = new FruitFilters();

  updateSearchText(oldSearch: string, newSearch: string): void {
    // searchText is already set by the base class setSearchText()
    this.notifyItemsChanged();
  }

  getItems(): IListItem[] {
    const filterId = this.filters.currentFilterId;
    const currentSearch = this.searchText.toLowerCase();
    const items: IListItem[] = [];

    for (const fruit of FRUITS) {
      // Apply category filter
      if (filterId !== 'all' && fruit.category !== filterId) {
        continue;
      }

      // Apply text search
      if (
        currentSearch &&
        !fruit.name.toLowerCase().includes(currentSearch) &&
        !fruit.color.toLowerCase().includes(currentSearch) &&
        !fruit.category.toLowerCase().includes(currentSearch)
      ) {
        continue;
      }

      items.push(
        new ListItem({
          title: `${fruit.emoji} ${fruit.name}`,
          subtitle: `${fruit.color} ${fruit.category} fruit`,
          icon: IconInfo.fromGlyph(fruit.emoji),
          command: new SelectFruitCommand(fruit),
          section: fruit.category.charAt(0).toUpperCase() + fruit.category.slice(1),
          tags: [
            tag({ text: fruit.color, toolTip: `Color: ${fruit.color}` }),
            tag({ text: fruit.category, toolTip: `Category: ${fruit.category}` }),
          ],
        }),
      );
    }

    if (items.length === 0) {
      items.push(
        new ListItem({
          title: 'No fruits found',
          subtitle: `No matches for "${currentSearch}"`,
          icon: IconInfo.fromGlyph('🔍'),
          command: new class extends InvokableCommand {
            id = 'no-results';
            name = 'No results';
            invoke() {
              return CommandResult.keepOpen();
            }
          }(),
        }),
      );
    }

    return items;
  }
}
