// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls.AmbientEffects.Effects;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.Controls.AmbientEffects;

internal static class BackgroundEffectFactory
{
    public static IBackgroundEffect? Create(AmbientEffectType type, DockSide? dockSide = null)
    {
        return type switch
        {
            AmbientEffectType.LavaLamp => new LavaLampEffect(),
            AmbientEffectType.KittScanner => new KittScannerEffect(dockSide),
            AmbientEffectType.Aurora => new AuroraEffect(),
            AmbientEffectType.PulseGlow => new PulseGlowEffect(),
            AmbientEffectType.Spotlight => new SpotlightEffect(),
            AmbientEffectType.Bars => new BarsEffect(),
            AmbientEffectType.Alchemy => new AlchemyEffect(),
            AmbientEffectType.Plasma => new PlasmaEffect(),
            AmbientEffectType.RetroGrid => new RetroGridEffect(),
            AmbientEffectType.BarsLive => new LiveBarsEffect(),
            AmbientEffectType.AudioGlow => new AudioGlowEffect(),
            AmbientEffectType.Ambience => new AmbienceEffect(),
            _ => null,
        };
    }
}
