// This extension's default export throws during render to simulate a broken extension.
export default function Command() {
  throw new Error("Simulated broken extension: missing dependency");
}
