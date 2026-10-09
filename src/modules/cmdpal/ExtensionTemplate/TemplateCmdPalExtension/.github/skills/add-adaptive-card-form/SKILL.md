---
name: add-adaptive-card-form
description: >-
  Create form-based UI for your Command Palette extension using Adaptive Cards.
  Use when asked to add forms, user input fields, toggle switches, text inputs,
  dropdown menus, data entry, surveys, configuration dialogs, or interactive
  content pages. Supports the Adaptive Cards Designer for visual form building.
---

# Add Forms with Adaptive Cards

Create interactive forms in your Command Palette extension using Adaptive Cards. Forms allow you to collect user input through text fields, toggles, dropdowns, and other controls.

## When to Use This Skill

- Adding a form to collect user input (name, settings, feedback)
- Creating interactive configuration dialogs
- Building data entry interfaces
- Adding toggle switches or dropdown menus
- Displaying complex layouts beyond simple lists

## Prerequisites

- Familiarity with [Adaptive Cards](https://adaptivecards.io/)
- Optional: Use the [Adaptive Card Designer](https://adaptivecards.io/designer/) to visually build your form

## Quick Start

### Step 1: Create a ContentPage with FormContent

Create a new file in your `Pages/` directory:

```csharp
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System.Text.Json.Nodes;

namespace YourExtension;

internal sealed partial class MyFormPage : ContentPage
{
    private readonly MyForm _form = new();

    public MyFormPage()
    {
        Name = "Open";
        Title = "My Form";
        Icon = new IconInfo("\uECA5");
    }

    public override IContent[] GetContent() => [_form];
}

internal sealed partial class MyForm : FormContent
{
    public MyForm()
    {
        TemplateJson = """
        {
            "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
            "type": "AdaptiveCard",
            "version": "1.6",
            "body": [
                {
                    "type": "Input.Text",
                    "label": "Name",
                    "id": "Name",
                    "isRequired": true,
                    "errorMessage": "Name is required",
                    "placeholder": "Enter your name"
                }
            ],
            "actions": [
                {
                    "type": "Action.Submit",
                    "title": "Submit"
                }
            ]
        }
        """;
    }

    public override CommandResult SubmitForm(string payload)
    {
        var formInput = JsonNode.Parse(payload)?.AsObject();
        if (formInput == null)
        {
            return CommandResult.GoHome();
        }

        var name = formInput["Name"]?.ToString() ?? "Unknown";
        return CommandResult.ShowToast($"Hello, {name}!");
    }
}
```

### Step 2: Register the Page

In your `CommandsProvider`, add the form page:

```csharp
_commands = [
    new CommandItem(new MyFormPage()) { Title = "My Form" },
];
```

### Step 3: Deploy and Test

1. Deploy your extension
2. In Command Palette, run `Reload`
3. Navigate to your form and submit it

## Key Concepts

### TemplateJson
The JSON layout of your form (from Adaptive Cards schema). Design it at https://adaptivecards.io/designer/

### DataJson (Optional)
Dynamic data binding using `${...}` placeholders in your TemplateJson:
```csharp
TemplateJson = """{ "body": [{ "type": "TextBlock", "text": "${title}" }] }""";
DataJson = """{ "title": "Dynamic Title" }""";
```

### SubmitForm
Called when the user submits. Parse `payload` as JSON to read input values by their `id`.

### Mixing Content Types
You can combine forms with markdown on the same page:
```csharp
public override IContent[] GetContent() => [
    new MarkdownContent("# Instructions\nFill out the form below."),
    _form,
];
```

## Common Form Patterns

See [form-patterns.md](references/form-patterns.md) for template JSON for common form types.

## Documentation

- [Get user input with forms](https://learn.microsoft.com/windows/powertoys/command-palette/using-form-pages)
- [Adaptive Card Designer](https://adaptivecards.io/designer/)
- [Adaptive Cards Schema](https://adaptivecards.io/explorer/)
