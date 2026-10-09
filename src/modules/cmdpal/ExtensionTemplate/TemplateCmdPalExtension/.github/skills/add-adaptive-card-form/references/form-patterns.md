# Common Adaptive Card Form Patterns

Reusable template JSON and handler code for the most common form types in Command Palette extensions.

---

## Simple Text Input Form

A basic form with one or two text fields and a submit button.

### TemplateJson

```json
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.6",
    "body": [
        {
            "type": "Input.Text",
            "id": "FirstName",
            "label": "First Name",
            "placeholder": "Enter your first name",
            "isRequired": true,
            "errorMessage": "First name is required"
        },
        {
            "type": "Input.Text",
            "id": "Email",
            "label": "Email Address",
            "placeholder": "user@example.com",
            "style": "Email",
            "isRequired": true,
            "errorMessage": "A valid email is required"
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": "Submit"
        }
    ]
}
```

### SubmitForm Handler

```csharp
public override CommandResult SubmitForm(string payload)
{
    var input = JsonNode.Parse(payload)?.AsObject();
    if (input == null) return CommandResult.GoHome();

    var firstName = input["FirstName"]?.ToString() ?? "";
    var email = input["Email"]?.ToString() ?? "";

    return CommandResult.ShowToast($"Registered {firstName} ({email})");
}
```

---

## Toggle/Checkbox Form

Use `Input.Toggle` for boolean on/off settings. Combine with `DataJson` for dynamic defaults.

### TemplateJson

```json
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.6",
    "body": [
        {
            "type": "TextBlock",
            "text": "Preferences",
            "weight": "Bolder",
            "size": "Medium"
        },
        {
            "type": "Input.Toggle",
            "id": "AcceptsTerms",
            "title": "I accept the terms and conditions",
            "valueOn": "true",
            "valueOff": "false",
            "value": "false"
        },
        {
            "type": "Input.Toggle",
            "id": "EnableNotifications",
            "title": "Enable notifications",
            "valueOn": "true",
            "valueOff": "false",
            "value": "${notificationsDefault}"
        },
        {
            "type": "Input.Toggle",
            "id": "DarkMode",
            "title": "Use dark mode",
            "valueOn": "true",
            "valueOff": "false",
            "value": "${darkModeDefault}"
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": "Save Preferences"
        }
    ]
}
```

### DataJson (Dynamic Defaults)

```csharp
DataJson = """
{
    "notificationsDefault": "true",
    "darkModeDefault": "false"
}
""";
```

### SubmitForm Handler

```csharp
public override CommandResult SubmitForm(string payload)
{
    var input = JsonNode.Parse(payload)?.AsObject();
    if (input == null) return CommandResult.GoHome();

    var accepted = input["AcceptsTerms"]?.ToString() == "true";
    var notifications = input["EnableNotifications"]?.ToString() == "true";
    var darkMode = input["DarkMode"]?.ToString() == "true";

    if (!accepted)
    {
        return CommandResult.ShowToast("You must accept the terms to continue.");
    }

    // Save preferences...
    return CommandResult.ShowToast("Preferences saved!");
}
```

---

## Choice Set (Dropdown/Radio) Form

Use `Input.ChoiceSet` for single-select dropdowns or radio buttons.

### Compact Style (Dropdown)

```json
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.6",
    "body": [
        {
            "type": "Input.ChoiceSet",
            "id": "Priority",
            "label": "Priority Level",
            "style": "compact",
            "value": "medium",
            "choices": [
                { "title": "Low", "value": "low" },
                { "title": "Medium", "value": "medium" },
                { "title": "High", "value": "high" },
                { "title": "Critical", "value": "critical" }
            ]
        },
        {
            "type": "Input.ChoiceSet",
            "id": "Category",
            "label": "Category",
            "style": "compact",
            "choices": [
                { "title": "Bug Report", "value": "bug" },
                { "title": "Feature Request", "value": "feature" },
                { "title": "Documentation", "value": "docs" },
                { "title": "Question", "value": "question" }
            ]
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": "Create Issue"
        }
    ]
}
```

### Expanded Style (Radio Buttons)

```json
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.6",
    "body": [
        {
            "type": "Input.ChoiceSet",
            "id": "Theme",
            "label": "Select a theme",
            "style": "expanded",
            "value": "system",
            "choices": [
                { "title": "Light", "value": "light" },
                { "title": "Dark", "value": "dark" },
                { "title": "System Default", "value": "system" }
            ]
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": "Apply"
        }
    ]
}
```

---

## Multi-Section Form

Combine multiple input types with TextBlock headers to create organized, multi-section forms. Use `Action.ShowCard` for progressive disclosure of optional sections.

### TemplateJson

```json
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.6",
    "body": [
        {
            "type": "TextBlock",
            "text": "Personal Information",
            "weight": "Bolder",
            "size": "Medium",
            "separator": true
        },
        {
            "type": "Input.Text",
            "id": "FullName",
            "label": "Full Name",
            "placeholder": "Enter your full name",
            "isRequired": true,
            "errorMessage": "Name is required"
        },
        {
            "type": "Input.Text",
            "id": "Email",
            "label": "Email",
            "placeholder": "user@example.com",
            "style": "Email"
        },
        {
            "type": "TextBlock",
            "text": "Preferences",
            "weight": "Bolder",
            "size": "Medium",
            "separator": true,
            "spacing": "Large"
        },
        {
            "type": "Input.ChoiceSet",
            "id": "Language",
            "label": "Preferred Language",
            "style": "compact",
            "value": "en",
            "choices": [
                { "title": "English", "value": "en" },
                { "title": "Spanish", "value": "es" },
                { "title": "French", "value": "fr" },
                { "title": "German", "value": "de" }
            ]
        },
        {
            "type": "Input.Toggle",
            "id": "Newsletter",
            "title": "Subscribe to newsletter",
            "valueOn": "true",
            "valueOff": "false",
            "value": "true"
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": "Save Profile"
        },
        {
            "type": "Action.ShowCard",
            "title": "Advanced Options",
            "card": {
                "type": "AdaptiveCard",
                "body": [
                    {
                        "type": "Input.Text",
                        "id": "ApiKey",
                        "label": "API Key (optional)",
                        "placeholder": "Enter your API key"
                    },
                    {
                        "type": "Input.Toggle",
                        "id": "DebugMode",
                        "title": "Enable debug mode",
                        "valueOn": "true",
                        "valueOff": "false",
                        "value": "false"
                    }
                ],
                "actions": [
                    {
                        "type": "Action.Submit",
                        "title": "Save All"
                    }
                ]
            }
        }
    ]
}
```

---

## Feedback Form

A common pattern for collecting user feedback with a multiline text area and a rating.

### TemplateJson

```json
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.6",
    "body": [
        {
            "type": "TextBlock",
            "text": "We'd love your feedback!",
            "weight": "Bolder",
            "size": "Medium"
        },
        {
            "type": "TextBlock",
            "text": "Tell us what you think and how we can improve.",
            "wrap": true,
            "spacing": "Small"
        },
        {
            "type": "Input.ChoiceSet",
            "id": "Rating",
            "label": "How would you rate your experience?",
            "style": "expanded",
            "isRequired": true,
            "errorMessage": "Please select a rating",
            "choices": [
                { "title": "⭐ Poor", "value": "1" },
                { "title": "⭐⭐ Fair", "value": "2" },
                { "title": "⭐⭐⭐ Good", "value": "3" },
                { "title": "⭐⭐⭐⭐ Great", "value": "4" },
                { "title": "⭐⭐⭐⭐⭐ Excellent", "value": "5" }
            ]
        },
        {
            "type": "Input.Text",
            "id": "Comments",
            "label": "Comments",
            "placeholder": "Share your thoughts...",
            "isMultiline": true,
            "maxLength": 500
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": "Send Feedback"
        }
    ]
}
```

### SubmitForm Handler with Confirmation Dialog

```csharp
public override CommandResult SubmitForm(string payload)
{
    var input = JsonNode.Parse(payload)?.AsObject();
    if (input == null) return CommandResult.GoHome();

    var rating = input["Rating"]?.ToString() ?? "0";
    var comments = input["Comments"]?.ToString() ?? "";

    return CommandResult.Confirm(new ConfirmationArgs
    {
        Title = "Submit feedback?",
        Description = $"Rating: {rating}/5\n\n{(string.IsNullOrEmpty(comments) ? "No comments" : comments)}",
        PrimaryCommand = new AnonymousCommand(() =>
        {
            // Process and store feedback
            new ToastStatusMessage("Thank you for your feedback!").Show();
        })
        {
            Name = "Submit",
            Result = CommandResult.Dismiss(),
        },
    });
}
```

---

## Tree Content with Forms (Comment/Reply Pattern)

Use `TreeContent` to create nested, threaded discussions where each node can contain a form for replies.

### Post Content (Tree Node)

```csharp
internal sealed partial class PostContent : TreeContent
{
    private readonly string _author;
    private readonly string _body;
    private readonly PostReplyForm _replyForm;
    private readonly List<PostContent> _childPosts = [];

    public PostContent(string author, string body)
    {
        _author = author;
        _body = body;
        _replyForm = new PostReplyForm(this);

        TemplateJson = """
        {
            "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
            "type": "AdaptiveCard",
            "version": "1.6",
            "body": [
                {
                    "type": "TextBlock",
                    "text": "${author}",
                    "weight": "Bolder"
                },
                {
                    "type": "TextBlock",
                    "text": "${body}",
                    "wrap": true
                }
            ]
        }
        """;
        DataJson = $$"""{ "author": "{{_author}}", "body": "{{_body}}" }""";
    }

    public override IContent[] GetChildren() => [_replyForm, .. _childPosts];

    public void AddReply(PostContent reply) => _childPosts.Add(reply);
}
```

### Reply Form (Child of Tree Node)

```csharp
internal sealed partial class PostReplyForm : FormContent
{
    private readonly PostContent _parent;

    public PostReplyForm(PostContent parent)
    {
        _parent = parent;
        TemplateJson = """
        {
            "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
            "type": "AdaptiveCard",
            "version": "1.6",
            "body": [
                {
                    "type": "Input.Text",
                    "id": "ReplyText",
                    "placeholder": "Write a reply...",
                    "isMultiline": true
                }
            ],
            "actions": [
                {
                    "type": "Action.Submit",
                    "title": "Reply"
                }
            ]
        }
        """;
    }

    public override CommandResult SubmitForm(string payload)
    {
        var input = JsonNode.Parse(payload)?.AsObject();
        if (input == null) return CommandResult.GoHome();

        var replyText = input["ReplyText"]?.ToString();
        if (!string.IsNullOrWhiteSpace(replyText))
        {
            _parent.AddReply(new PostContent("You", replyText));
        }

        return CommandResult.KeepOpen();
    }
}
```

### Hosting the Thread on a ContentPage

```csharp
internal sealed partial class ThreadPage : ContentPage
{
    private readonly PostContent _rootPost;

    public ThreadPage()
    {
        Name = "Discussion";
        Title = "Discussion Thread";
        Icon = new IconInfo("\uE90A");

        _rootPost = new PostContent("Alice", "Has anyone tried the new API?");
        _rootPost.AddReply(new PostContent("Bob", "Yes! It works great."));
    }

    public override IContent[] GetContent() => [_rootPost];
}
```
