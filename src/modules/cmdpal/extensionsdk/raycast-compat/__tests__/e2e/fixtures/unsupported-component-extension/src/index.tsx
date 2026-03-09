import { Grid } from "@raycast/api";

export default function Command() {
  return (
    <Grid>
      <Grid.Item title="Image 1" content="https://example.com/img1.png" />
      <Grid.Item title="Image 2" content="https://example.com/img2.png" />
    </Grid>
  );
}
