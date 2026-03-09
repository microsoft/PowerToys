/**
 * Raycast Color enum compatibility stub.
 *
 * Maps Raycast's named color constants to hex color strings.
 * These colors approximate Raycast's design tokens. When rendered in
 * CmdPal's UI layer, they'll be used as-is or mapped to the closest
 * CmdPal theme color.
 */
export declare const Color: {
    readonly Blue: "#007AFF";
    readonly Green: "#28CD41";
    readonly Magenta: "#FF2D55";
    readonly Orange: "#FF9500";
    readonly Purple: "#AF52DE";
    readonly Red: "#FF3B30";
    readonly Yellow: "#FFCC00";
    readonly PrimaryText: "#000000";
    readonly SecondaryText: "#8E8E93";
};
export type ColorEnum = typeof Color;
export type ColorKey = keyof ColorEnum;
/**
 * Raycast's Color.Dynamic — adapts to light/dark appearance.
 * For the spike, we return the light value.
 */
export declare function ColorDynamic(light: string, dark: string): string;
/**
 * Resolve a Raycast color reference to a hex string.
 * Handles Color enum values, hex strings, and Color.Dynamic objects.
 */
export declare function resolveColor(color: unknown): string | undefined;
//# sourceMappingURL=colors.d.ts.map