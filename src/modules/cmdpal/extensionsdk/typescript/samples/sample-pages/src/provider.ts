// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  CommandProvider,
  CommandItem,
  ExtensionServer,
  ICommandItem,
  ICommand,
  IFallbackCommandItem,
  InvokableCommand,
  CommandResult,
  ICommandResult,
  IconInfo,
} from '@cmdpal/sdk';
import { SamplesHubPage } from './pages/samples-hub';
import { BasicListPage } from './pages/basic-list';
import { DynamicSearchPage } from './pages/dynamic-list';
import { MarkdownDemoPage } from './pages/markdown-page';
import { FormDemoPage } from './pages/form-page';
import { DetailsListPage } from './pages/details-list';
import { IconShowcasePage } from './pages/icon-showcase';
import { SectionsPage } from './pages/sections-list';
import { MultiMarkdownPage } from './pages/multi-markdown';
import { MarkdownDetailsPage } from './pages/markdown-details';
import { TreeContentPage } from './pages/tree-content';
import { NavigationCommandsPage } from './pages/navigation-commands';
import { StatusMessagesPage } from './pages/status-messages';
import { GridShowcasePage } from './pages/grid-page';
import { ToastDemoCommand, ConfirmDemoCommand } from './commands/demo-commands';

// ---------------------------------------------------------------------------
// Fallback command: appears in global search results
// ---------------------------------------------------------------------------

class FallbackSearchCommand extends InvokableCommand {
  id = 'fallback-search';
  name = 'Search with TypeScript Sample';

  invoke(): ICommandResult {
    return CommandResult.showToast('🔍 Fallback search triggered — no results for this query');
  }
}

/**
 * Main provider for the Sample Pages TypeScript extension.
 * Demonstrates various Command Palette features using the TypeScript SDK.
 */
export class SamplePagesProvider extends CommandProvider {
  // Existing pages
  private readonly hubPage: SamplesHubPage;
  private readonly basicListPage: BasicListPage;
  private readonly dynamicSearchPage: DynamicSearchPage;
  private readonly markdownPage: MarkdownDemoPage;
  private readonly formPage: FormDemoPage;

  // New pages
  private readonly detailsListPage: DetailsListPage;
  private readonly iconShowcasePage: IconShowcasePage;
  private readonly sectionsPage: SectionsPage;
  private readonly multiMarkdownPage: MultiMarkdownPage;
  private readonly markdownDetailsPage: MarkdownDetailsPage;
  private readonly treeContentPage: TreeContentPage;
  private readonly navigationCommandsPage: NavigationCommandsPage;
  private readonly statusMessagesPage: StatusMessagesPage;
  private readonly gridShowcasePage: GridShowcasePage;

  // Commands
  private readonly toastDemo: ToastDemoCommand;
  private readonly confirmDemo: ConfirmDemoCommand;
  private readonly fallbackSearch: FallbackSearchCommand;

  constructor() {
    super();

    // Existing pages
    this.hubPage = new SamplesHubPage();
    this.basicListPage = new BasicListPage();
    this.dynamicSearchPage = new DynamicSearchPage();
    this.markdownPage = new MarkdownDemoPage();
    this.formPage = new FormDemoPage();

    // New pages
    this.detailsListPage = new DetailsListPage();
    this.iconShowcasePage = new IconShowcasePage();
    this.sectionsPage = new SectionsPage();
    this.multiMarkdownPage = new MultiMarkdownPage();
    this.markdownDetailsPage = new MarkdownDetailsPage();
    this.treeContentPage = new TreeContentPage();
    this.navigationCommandsPage = new NavigationCommandsPage();
    this.statusMessagesPage = new StatusMessagesPage(this);
    this.gridShowcasePage = new GridShowcasePage();

    // Commands
    this.toastDemo = new ToastDemoCommand();
    this.confirmDemo = new ConfirmDemoCommand();
    this.fallbackSearch = new FallbackSearchCommand();

    // Wire hub page entries
    this.hubPage.addPage('Basic List Page', 'List items with tags, details, and context menus', '\uE8FD', this.basicListPage, 'ListPage');
    this.hubPage.addPage('Dynamic Search Page', 'Filtered list with live search and filter tabs', '\uE721', this.dynamicSearchPage, 'DynamicListPage');
    this.hubPage.addPage('Details Panel Samples', 'Detail sizes, hero images, metadata, colored tags', '\uE8A1', this.detailsListPage, 'ListPage');
    this.hubPage.addPage('Icon Showcase', 'Emojis, Segoe Fluent icons, complex emoji sequences', '\uE734', this.iconShowcasePage, 'ListPage');
    this.hubPage.addPage('Sectioned List', 'Items grouped by section headers', '\uE8A8', this.sectionsPage, 'ListPage');
    this.hubPage.addPage('Markdown Content Page', 'Rich markdown rendering with multiple sections', '\uE70B', this.markdownPage, 'ContentPage');
    this.hubPage.addPage('Multiple Markdown Bodies', '3-4 separate MarkdownContent blocks on one page', '\uE8A4', this.multiMarkdownPage, 'ContentPage');
    this.hubPage.addPage('Markdown + Details', 'ContentPage with markdown body + details panel', '\uE8A3', this.markdownDetailsPage, 'ContentPage');
    this.hubPage.addPage('Form & Adaptive Cards', 'Input forms with text, toggles, and choice sets', '\uE9D5', this.formPage, 'FormContent');
    this.hubPage.addPage('Tree Content', 'Nested TreeContent with markdown and forms', '\uE8D5', this.treeContentPage, 'ContentPage');
    this.hubPage.addPage('Navigation Commands', 'All CommandResult types (Dismiss, GoBack, Toast, etc.)', '\uE700', this.navigationCommandsPage, 'ListPage');
    this.hubPage.addPage('Status Messages', 'Info, Success, Warning, Error status messages', '\uE7E7', this.statusMessagesPage, 'ListPage');
    this.hubPage.addPage('Grid & Gallery Layouts', 'Small, medium, and gallery grid tile views', '\uE80A', this.gridShowcasePage, 'ListPage');

    // Register all pages so the server can route requests to them
    const allPages = [
      this.hubPage,
      this.basicListPage,
      this.dynamicSearchPage,
      this.markdownPage,
      this.formPage,
      this.detailsListPage,
      this.iconShowcasePage,
      this.sectionsPage,
      this.multiMarkdownPage,
      this.markdownDetailsPage,
      this.treeContentPage,
      this.navigationCommandsPage,
      this.statusMessagesPage,
      this.gridShowcasePage,
      this.gridShowcasePage.galleryFull,
      this.gridShowcasePage.galleryTitleOnly,
      this.gridShowcasePage.galleryNoText,
      this.gridShowcasePage.mediumGrid,
      this.gridShowcasePage.mediumGridNoTitle,
      this.gridShowcasePage.smallGrid,
    ];
    for (const page of allPages) {
      ExtensionServer._registerPage(page);
    }
  }

  get id(): string {
    return 'sample-pages-ts';
  }

  get displayName(): string {
    return 'Sample Pages (TypeScript)';
  }

  get icon() {
    return IconInfo.fromGlyph('\uE82D');
  }

  topLevelCommands(): ICommandItem[] {
    // The command IS the page — matching WinRT extension pattern.
    // CmdPal checks `command is IPage` and navigates directly.
    return [
      new CommandItem({
        title: 'Sample Pages (TypeScript)',
        subtitle: 'Explore TypeScript SDK features',
        icon: this.icon,
        command: this.hubPage,
      }),
    ];
  }

  fallbackCommands(): IFallbackCommandItem[] {
    return [
      new CommandItem({
        title: 'Search with TypeScript Sample',
        subtitle: 'Fallback command from the TS sample extension',
        icon: this.icon,
        command: this.fallbackSearch,
      }) as unknown as IFallbackCommandItem,
    ];
  }

  getCommand(id: string): ICommand | undefined {
    // Check all pages
    const allPages: any[] = [
      this.hubPage,
      this.basicListPage,
      this.dynamicSearchPage,
      this.markdownPage,
      this.formPage,
      this.detailsListPage,
      this.iconShowcasePage,
      this.sectionsPage,
      this.multiMarkdownPage,
      this.markdownDetailsPage,
      this.treeContentPage,
      this.navigationCommandsPage,
      this.statusMessagesPage,
      this.gridShowcasePage,
      this.gridShowcasePage.galleryFull,
      this.gridShowcasePage.galleryTitleOnly,
      this.gridShowcasePage.galleryNoText,
      this.gridShowcasePage.mediumGrid,
      this.gridShowcasePage.mediumGridNoTitle,
      this.gridShowcasePage.smallGrid,
    ];
    for (const page of allPages) {
      if (page.id === id) {
        return page;
      }
    }

    // Check standalone commands
    switch (id) {
      case 'toast-demo':
        return this.toastDemo;
      case 'confirm-demo':
        return this.confirmDemo;
      case 'fallback-search':
        return this.fallbackSearch;
      default:
        return undefined;
    }
  }
}
