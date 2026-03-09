// Sample Raycast extension for testing the CmdPal bundler.
// This file imports from @raycast/api exactly as a real Raycast extension would.
// The bundler should alias this import to our compat layer.

import { List, Icon, showToast, Toast, Action, ActionPanel } from "@raycast/api";

interface Item {
  id: string;
  title: string;
  subtitle: string;
}

const ITEMS: Item[] = [
  { id: "1", title: "First Item", subtitle: "This is the first item" },
  { id: "2", title: "Second Item", subtitle: "This is the second item" },
  { id: "3", title: "Third Item", subtitle: "This is the third item" },
];

export default function Command() {
  return (
    <List searchBarPlaceholder="Search items...">
      {ITEMS.map((item) => (
        <List.Item
          key={item.id}
          title={item.title}
          subtitle={item.subtitle}
          icon={Icon.Star}
          actions={
            <ActionPanel>
              <Action
                title="Show Toast"
                onAction={async () => {
                  await showToast({
                    style: Toast.Style.Success,
                    title: `Selected: ${item.title}`,
                  });
                }}
              />
            </ActionPanel>
          }
        />
      ))}
    </List>
  );
}
