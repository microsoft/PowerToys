<#
.SYNOPSIS
    Converts an OOBE hero GIF into the H.264 MP4 format played by Settings' OOBEPageControl (HeroVideo).

.DESCRIPTION
    Settings plays OOBE hero animations with MediaPlayerElement, which needs H.264 MP4 so playback works on
    stock Windows without Store codec extensions. ffmpeg (with libx264) is not shipped with the repo.

    Encode settings: CRF 23, preset slow, tune animation, yuv420p limited range, +faststart, no audio.
    Pixels use the BT.601 matrix and are tagged as such (smpte170m, sRGB/BT.709 primaries). Media Foundation
    assumes BT.601 for untagged SD video, so colours stay correct even if a decoder ignores the tags.
    For GIFs with dithered backgrounds that grow at CRF 23, use -Denoise (hqdn3d) with -Crf 26.
    Existing assets used -Denoise -Crf 26 for ScreenRuler and QuickAccent; all others used the defaults.

.EXAMPLE
    .\tools\Convert-OobeGifToMp4.ps1 -GifPath .\FancyZones.gif -FfmpegPath C:\tools\ffmpeg.exe
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$GifPath,

    [string]$OutputPath = [System.IO.Path]::ChangeExtension($GifPath, '.mp4'),

    [string]$FfmpegPath = 'ffmpeg',

    [int]$Crf = 23,

    [switch]$Denoise
)

$ErrorActionPreference = 'Stop'

$filter = 'scale=trunc(iw/2)*2:trunc(ih/2)*2:out_color_matrix=bt601:out_range=tv,setparams=colorspace=smpte170m:color_primaries=bt709:color_trc=bt709:range=tv'
if ($Denoise) {
    $filter = "hqdn3d=4:3:6:4,$filter"
}

& $FfmpegPath -hide_banner -loglevel error -y -i $GifPath -fps_mode vfr -vf $filter `
    -c:v libx264 -crf $Crf -preset slow -tune animation -pix_fmt yuv420p `
    -colorspace smpte170m -color_primaries bt709 -color_trc bt709 -color_range tv `
    -movflags +faststart -an $OutputPath

if ($LASTEXITCODE -ne 0) {
    throw "ffmpeg failed with exit code $LASTEXITCODE"
}

Write-Host "$GifPath ($((Get-Item $GifPath).Length) bytes) -> $OutputPath ($((Get-Item $OutputPath).Length) bytes)"
