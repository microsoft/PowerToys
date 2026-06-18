# 05 — Getting Started: Build Your First CmdPal Extension

This guide walks you through building a CmdPal JavaScript extension from scratch. By the end, you'll have a working extension with a searchable list page, a content page, and settings.

---

## Prerequisites

- **Node.js 18+** installed and on your PATH
- **PowerToys** with Command Palette enabled
- A text editor (VS Code recommended)

---

## Step 1: Scaffold the Project

```bash
mkdir my-first-extension && cd my-first-extension
npm init -y
npm install @microsoft/cmdpal-sdk
npm install --save-dev typescript
```

Create `tsconfig.json`:

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "commonjs",
    "outDir": "./dist",
    "rootDir": "./src",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "moduleResolution": "node"
  },
  "include": ["src/**/*"]
}
```

Add the `cmdpal` section to your `package.json`:

```json
{
  "name": "my-first-extension",
  "version": "1.0.0",
  "description": "A tutorial CmdPal extension",
  "main": "dist/index.js",
  "cmdpal": {
    "displayName": "My First Extension",
    "debug": true
  },
  "dependencies": {
    "@microsoft/cmdpal-sdk": "^1.0.0"
  },
  "devDependencies": {
    "typescript": "^5.0.0"
  }
}
```

---

## Step 2: Create a Simple Command

Create `src/index.ts`:

```typescript
import {
  CommandProvider,
  Command,
  ListPage,
  ListItem,
  CommandResultKind,
  run,
  iconFromGlyph,
} from "@microsoft/cmdpal-sdk";

// Define a simple command that shows a toast
class GreetCommand extends Command {
  constructor() {
    super({
      id: "greet",
      name: "Say Hello",
      icon: iconFromGlyph("\uE76E"), // Checkmark icon
    });
  }

  async invoke(): Promise<{ kind: CommandResultKind }> {
    return {
      kind: CommandResultKind.ShowToast,
      args: { Message: "Hello from my extension!" },
    };
  }
}

// Define the main list page
class MainPage extends ListPage {
  constructor() {
    super({
      id: "main-page",
      name: "My First Extension",
      icon: iconFromGlyph("\uE8A5"),
    });
  }

  async getItems(): Promise<ListItem[]> {
    return [
      new ListItem({
        title: "Say Hello",
        subtitle: "Shows a greeting toast",
        command: new GreetCommand(),
        icon: iconFromGlyph("\uE76E"),
      }),
    ];
  }
}

// Define the provider
class MyProvider extends CommandProvider {
  constructor() {
    super("my-first-extension", "My First Extension");
  }

  getTopLevelCommands() {
    return [
      new ListItem({
        title: "My First Extension",
        subtitle: "A tutorial extension",
        command: new MainPage(),
        icon: iconFromGlyph("\uE8A5"),
      }),
    ];
  }
}

// Start the extension
run(new MyProvider());
```

---

## Step 3: Build and Install

```bash
# Build
npx tsc

# Install (junction link for development)
$extensionsDir = "$env:LOCALAPPDATA\Microsoft\PowerToys\CommandPalette\JSExtensions"
New-Item -ItemType Junction -Path "$extensionsDir\my-first-extension" -Target (Resolve-Path .)
```

Open CmdPal — you should see "My First Extension" in the list. Click it to see your command, then click "Say Hello" to see the toast!

---

## Step 4: Add a Dynamic List Page (Search)

Let's add a searchable list that filters items as you type:

```typescript
import {
  DynamicListPage,
  // ... other imports
} from "@microsoft/cmdpal-sdk";

class SearchablePage extends DynamicListPage {
  private allItems = [
    { title: "Apple", emoji: "🍎" },
    { title: "Banana", emoji: "🍌" },
    { title: "Cherry", emoji: "🍒" },
    { title: "Dragon Fruit", emoji: "🐉" },
    { title: "Elderberry", emoji: "🫐" },
  ];

  constructor() {
    super({
      id: "searchable-page",
      name: "Fruit Search",
      icon: iconFromGlyph("\uE721"), // Search icon
      placeholderText: "Search fruits...",
    });
  }

  async getItems(): Promise<ListItem[]> {
    const query = this.searchText?.toLowerCase() ?? "";
    return this.allItems
      .filter((item) => item.title.toLowerCase().includes(query))
      .map(
        (item) =>
          new ListItem({
            title: `${item.emoji} ${item.title}`,
            subtitle: "A delicious fruit",
            command: new ToastCommand(item.title),
          })
      );
  }
}

class ToastCommand extends Command {
  private fruit: string;

  constructor(fruit: string) {
    super({ id: `toast-${fruit}`, name: `Select ${fruit}` });
    this.fruit = fruit;
  }

  async invoke() {
    return {
      kind: CommandResultKind.ShowToast,
      args: { Message: `You selected ${this.fruit}!` },
    };
  }
}
```

Add the searchable page to your `MainPage.getItems()`:

```typescript
new ListItem({
  title: "Fruit Search",
  subtitle: "Search through a list of fruits",
  command: new SearchablePage(),
  icon: iconFromGlyph("\uE721"),
}),
```

---

## Step 5: Add a Content Page

Content pages display rich content — markdown, images, and forms:

```typescript
import {
  ContentPage,
  MarkdownContent,
  ImageContent,
  FormContent,
  iconFromUrl,
  // ... other imports
} from "@microsoft/cmdpal-sdk";

class AboutPage extends ContentPage {
  constructor() {
    super({
      id: "about-page",
      name: "About",
      icon: iconFromGlyph("\uE946"), // Info icon
    });
  }

  async getContent() {
    return [
      new MarkdownContent({
        body: `# My First Extension

This extension was built with the CmdPal TypeScript SDK.

## Features
- **Simple commands** with toast notifications
- **Searchable lists** with dynamic filtering
- **Rich content** with markdown and images
`,
      }),
    ];
  }
}
```

---

## Step 6: Add Settings

Extensions can have a settings page using Adaptive Cards:

```typescript
import {
  FormPage,
  // ... other imports
} from "@microsoft/cmdpal-sdk";

class SettingsPage extends FormPage {
  constructor() {
    super({
      id: "settings",
      name: "Settings",
      icon: iconFromGlyph("\uE713"),
    });
  }

  async getContent() {
    return [
      new FormContent({
        templateJson: JSON.stringify({
          type: "AdaptiveCard",
          body: [
            {
              type: "Input.Text",
              id: "greeting",
              label: "Custom Greeting",
              placeholder: "Enter a greeting...",
              value: "Hello",
            },
            {
              type: "Input.Toggle",
              id: "showEmoji",
              title: "Show emoji in results",
              value: "true",
            },
          ],
          actions: [
            {
              type: "Action.Submit",
              title: "Save",
            },
          ],
          $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
          version: "1.5",
        }),
        dataJson: JSON.stringify({
          greeting: "Hello",
          showEmoji: "true",
        }),
      }),
    ];
  }

  async onFormSubmit(inputs: Record<string, string>) {
    // Save settings (e.g., to a file or state)
    console.log("Settings saved:", inputs);
    return {
      kind: CommandResultKind.ShowToast,
      args: { Message: "Settings saved!" },
    };
  }
}
```

Register the settings page in your provider:

```typescript
class MyProvider extends CommandProvider {
  constructor() {
    super("my-first-extension", "My First Extension");
  }

  getSettings() {
    return new SettingsPage();
  }

  // ... getTopLevelCommands
}
```

---

## Step 7: Add Context Commands and Details

List items can have context menu commands and a details panel:

```typescript
import {
  CopyTextCommand,
  OpenUrlCommand,
  Details,
  DetailsMetadata,
  TagsData,
  Tag,
  LinkData,
  // ... other imports
} from "@microsoft/cmdpal-sdk";

// In your getItems():
new ListItem({
  title: "GitHub",
  subtitle: "Open GitHub in your browser",
  command: new OpenUrlCommand({
    id: "open-github",
    name: "Open GitHub",
    url: "https://github.com",
  }),
  icon: iconFromGlyph("\uE774"),
  tags: [
    new Tag({ text: "Web", foreground: { r: 100, g: 200, b: 255, a: 255 } }),
  ],
  details: new Details({
    title: "GitHub",
    body: "The world's leading software development platform.",
    heroImage: iconFromGlyph("\uE774"),
    metadata: [
      new DetailsMetadata({
        key: "URL",
        data: new LinkData({
          link: "https://github.com",
          text: "github.com",
        }),
      }),
      new DetailsMetadata({
        key: "Tags",
        data: new TagsData({
          tags: [new Tag({ text: "Development" }), new Tag({ text: "Git" })],
        }),
      }),
    ],
  }),
  moreCommands: [
    {
      title: "Copy URL",
      command: new CopyTextCommand({
        id: "copy-github-url",
        name: "Copy URL",
        text: "https://github.com",
      }),
      icon: iconFromGlyph("\uE8C8"),
    },
  ],
});
```

---

## Step 8: Rebuild and Test

```bash
# Rebuild after changes
npx tsc
```

CmdPal watches for `*.js` file changes and hot-reloads automatically. After running `tsc`, your extension will reload within ~500ms.

---

## Debugging Tips

### Enable Debug Mode

Set `"debug": true` in the `cmdpal` section of your `package.json`. The Node.js process starts with `--inspect`, allowing you to attach a debugger.

### Attach VS Code Debugger

Add to `.vscode/launch.json`:

```json
{
  "type": "node",
  "request": "attach",
  "name": "Attach to CmdPal Extension",
  "port": 9229,
  "restart": true,
  "skipFiles": ["<node_internals>/**"]
}
```

### View Logs

Use `host.log()` in your extension to send messages to the CmdPal log:

```typescript
// Inside any command or page method:
host.log("Fetching items...", MessageState.Info);
host.log("Error occurred!", MessageState.Error);
```

### Common Issues

| Issue | Solution |
|-------|----------|
| Extension not showing | Check `package.json` has a `cmdpal` section and valid `name` + `main` |
| Blank page | Check `getItems()` or `getContent()` is returning data |
| Command does nothing | Ensure `invoke()` returns a valid `CommandResultKind` |
| Images not loading | Use `iconFromUrl()` or `iconFromBase64()` helpers |
| Settings crash | Ensure form template JSON is valid Adaptive Card schema |

---

## Next Steps

- Read the [TypeScript SDK Reference](./02-typescript-sdk.md) for the full API
- Read the [JSON-RPC Protocol Specification](./03-jsonrpc-protocol.md) to understand the wire format
- Check out the [Sample Extension](../src/modules/cmdpal/ext/SampleJSExtension/) for a comprehensive example
- Read the [Architecture Overview](./01-architecture.md) for how it all fits together
