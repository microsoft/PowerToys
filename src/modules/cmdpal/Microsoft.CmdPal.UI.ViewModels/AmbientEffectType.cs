// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Specifies the GPU-accelerated ambient background effect rendered behind content.
/// </summary>
public enum AmbientEffectType
{
    /// <summary>
    /// No ambient effect (default).
    /// </summary>
    Off,

    /// <summary>
    /// Slowly drifting radial gradient blobs with soft merge blur.
    /// </summary>
    LavaLamp,

    /// <summary>
    /// Red light sweeping back and forth (Knight Rider style).
    /// </summary>
    KittScanner,

    /// <summary>
    /// Northern-lights-style flowing color bands.
    /// </summary>
    Aurora,

    /// <summary>
    /// Gentle breathing radial glow.
    /// </summary>
    PulseGlow,

    /// <summary>
    /// Soft roaming spotlight tracing a Lissajous curve.
    /// </summary>
    Spotlight,

    /// <summary>
    /// WMP-inspired bouncing equalizer bars along the bottom edge.
    /// </summary>
    Bars,

    /// <summary>
    /// WMP "Alchemy" inspired rotating starburst rays from center.
    /// </summary>
    Alchemy,

    /// <summary>
    /// WMP "Geiss" inspired swirling psychedelic plasma field.
    /// </summary>
    Plasma,

    /// <summary>
    /// Retro 80s synthwave neon grid with scanlines and glowing horizon.
    /// </summary>
    RetroGrid,

    /// <summary>
    /// Audio-reactive equalizer bars driven by real-time system audio via WASAPI loopback.
    /// </summary>
    BarsLive,

    /// <summary>
    /// Multi-color audio-reactive glow along the top edge — each zone reacts to a frequency band.
    /// </summary>
    AudioGlow,

    /// <summary>
    /// WMP "Ambience" inspired — dreamy flowing organic shapes with audio-reactive morphing and slow color cycling.
    /// </summary>
    Ambience,
}
