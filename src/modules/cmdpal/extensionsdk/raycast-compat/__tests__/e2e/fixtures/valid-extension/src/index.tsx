import { List, ActionPanel, Action, showToast, Toast, Icon } from "@raycast/api";
import { useState } from "react";

export default function Command() {
  const [searchText, setSearchText] = useState("");
  const [items] = useState([
    { id: "1", title: "Hello World", subtitle: "First item", icon: Icon.Star },
    { id: "2", title: "Goodbye World", subtitle: "Second item", icon: Icon.Globe },
  ]);

  const filtered = items.filter(i =>
    i.title.toLowerCase().includes(searchText.toLowerCase())
  );

  return (
    <List onSearchTextChange={setSearchText} searchBarPlaceholder="Search items...">
      {filtered.map(item => (
        <List.Item
          key={item.id}
          title={item.title}
          subtitle={item.subtitle}
          icon={item.icon}
          actions={
            <ActionPanel>
              <Action.CopyToClipboard content={item.title} />
              <Action.OpenInBrowser url={`https://example.com/${item.id}`} />
            </ActionPanel>
          }
        />
      ))}
    </List>
  );
}
