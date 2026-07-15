// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Serialization of commands, command items, list items, and content into the
 * wire shapes described in `03-jsonrpc-protocol.md`. Page commands carry a
 * `pageType` discriminator; separators serialize with `_isSeparator: true`.
 */

import type {
  Content,
  ContextItem,
  Details,
  ICommand,
  ICommandItem,
  IContentPage,
  IFallbackCommandItem,
  IListItem,
  IListPage,
} from '../types.js';
import { serializeCommandResult, type WireCommandResult } from './commandResult.js';

function hasFunction(value: object, key: string): boolean {
  return key in value && typeof (value as Record<string, unknown>)[key] === 'function';
}

function isListPage(command: ICommand): command is IListPage {
  return hasFunction(command, 'getItems');
}

function isDynamicListPage(command: ICommand): boolean {
  return hasFunction(command, 'getItems') && hasFunction(command, 'setSearchText');
}

function isContentPage(command: ICommand): command is IContentPage {
  return hasFunction(command, 'getContent');
}

function isSeparator(item: IListItem): boolean {
  const marker = item as { isSeparator?: boolean; _isSeparator?: boolean };
  return marker.isSeparator === true || marker._isSeparator === true;
}

function assign(target: Record<string, unknown>, key: string, value: unknown): void {
  if (value !== undefined) {
    target[key] = value;
  }
}

/**
 * Serializes host-facing values while registering every command it encounters
 * so the runtime can resolve later `command/invoke` and `provider/getCommand`
 * requests.
 */
export class WireSerializer {
  private readonly register: (command: ICommand) => void;

  constructor(register: (command: ICommand) => void = () => {}) {
    this.register = register;
  }

  command(command: ICommand): Record<string, unknown> {
    this.register(command);

    const result: Record<string, unknown> = {
      id: command.id,
      name: command.name,
      displayName: command.name,
    };
    assign(result, 'icon', command.icon ?? undefined);

    if (isContentPage(command) && !isListPage(command)) {
      result.pageType = 'contentPage';
      this.applyContentPageProps(result, command);
    } else if (isListPage(command)) {
      result.pageType = isDynamicListPage(command) ? 'dynamicListPage' : 'listPage';
      this.applyListPageProps(result, command);
    }

    return result;
  }

  commandItem(item: ICommandItem): Record<string, unknown> {
    const result: Record<string, unknown> = {
      id: item.command.id,
      title: item.title,
      displayName: item.title,
    };
    assign(result, 'subtitle', item.subtitle);
    assign(result, 'icon', item.icon ?? undefined);
    result.command = this.command(item.command);

    const displayTitle = (item as IFallbackCommandItem).displayTitle;
    assign(result, 'displayTitle', displayTitle);

    if (item.moreCommands && item.moreCommands.length > 0) {
      result.moreCommands = this.contextItems(item.moreCommands);
    }
    return result;
  }

  commandItems(items: ICommandItem[]): Record<string, unknown>[] {
    return items.map((item) => this.commandItem(item));
  }

  listItem(item: IListItem): Record<string, unknown> {
    if (isSeparator(item)) {
      return {
        _isSeparator: true,
        title: item.title,
        section: item.section,
        command: null,
      };
    }

    const result: Record<string, unknown> = {
      id: item.command.id,
      title: item.title,
      displayName: item.title,
    };
    assign(result, 'subtitle', item.subtitle);
    assign(result, 'section', item.section);
    assign(result, 'tags', item.tags);
    assign(result, 'textToSuggest', item.textToSuggest);
    assign(result, 'icon', item.icon ?? undefined);
    result.command = this.command(item.command);

    if (item.details) {
      result.details = this.details(item.details);
    }
    if (item.moreCommands && item.moreCommands.length > 0) {
      result.moreCommands = this.contextItems(item.moreCommands);
    }
    return result;
  }

  listItems(items: IListItem[]): Record<string, unknown>[] {
    return items.map((item) => this.listItem(item));
  }

  contextItems(items: ContextItem[]): Record<string, unknown>[] {
    return items.map((item) => {
      const result: Record<string, unknown> = {
        command: this.command(item.command),
        title: item.title,
      };
      assign(result, 'subtitle', item.subtitle);
      assign(result, 'icon', item.icon ?? undefined);
      assign(result, 'isCritical', item.isCritical);
      assign(result, 'requestedShortcut', item.requestedShortcut);
      return result;
    });
  }

  details(details: Details): Record<string, unknown> {
    const result: Record<string, unknown> = {};
    assign(result, 'heroImage', details.heroImage ?? undefined);
    assign(result, 'title', details.title);
    assign(result, 'body', details.body);

    if (details.metadata) {
      result.metadata = details.metadata.map((element) => {
        const data = element.data;
        if (data.type === 'commands') {
          return {
            key: element.key,
            data: { type: 'commands', commands: data.commands.map((c) => this.command(c)) },
          };
        }
        return { key: element.key, data };
      });
    }
    return result;
  }

  async content(content: Content): Promise<Record<string, unknown>> {
    switch (content.type) {
      case 'markdown':
        return { type: 'markdown', body: content.body };
      case 'plainText': {
        const result: Record<string, unknown> = { type: 'plainText', text: content.text };
        assign(result, 'fontFamily', content.fontFamily);
        assign(result, 'wrapWords', content.wrapWords);
        return result;
      }
      case 'image': {
        const result: Record<string, unknown> = { type: 'image', image: content.image };
        assign(result, 'maxWidth', content.maxWidth);
        assign(result, 'maxHeight', content.maxHeight);
        return result;
      }
      case 'form': {
        const result: Record<string, unknown> = {
          type: 'form',
          templateJson: content.templateJson,
          dataJson: content.dataJson,
        };
        assign(result, 'stateJson', content.stateJson);
        return result;
      }
      case 'tree': {
        const children = await content.getChildren();
        return {
          type: 'tree',
          rootContent: await this.content(content.rootContent),
          children: await Promise.all(children.map((child) => this.content(child))),
        };
      }
    }
  }

  commandResult(result: CommandResultInput): WireCommandResult {
    return serializeCommandResult(result, (command) => this.command(command));
  }

  private applyListPageProps(target: Record<string, unknown>, page: IListPage): void {
    assign(target, 'title', page.title);
    assign(target, 'isLoading', page.isLoading);
    assign(target, 'accentColor', page.accentColor ?? undefined);
    assign(target, 'placeholderText', page.placeholderText);
    assign(target, 'searchText', page.searchText);
    assign(target, 'showDetails', page.showDetails);
    assign(target, 'filters', page.filters ?? undefined);
    assign(target, 'gridProperties', page.gridProperties ?? undefined);
    assign(target, 'hasMoreItems', page.hasMoreItems);
    if (page.emptyContent) {
      target.emptyContent = this.commandItem(page.emptyContent);
    }
  }

  private applyContentPageProps(target: Record<string, unknown>, page: IContentPage): void {
    assign(target, 'title', page.title);
    assign(target, 'isLoading', page.isLoading);
    assign(target, 'accentColor', page.accentColor ?? undefined);
    if (page.details) {
      target.details = this.details(page.details);
    }
    if (page.commands && page.commands.length > 0) {
      target.commands = this.contextItems(page.commands);
    }
  }
}

type CommandResultInput = Parameters<typeof serializeCommandResult>[0];
