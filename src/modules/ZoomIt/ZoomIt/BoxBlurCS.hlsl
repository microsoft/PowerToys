//==============================================================================
//
// BoxBlurCS.hlsl
//
// D3D11 compute shader for separable box blur.  Each dispatch performs
// either a horizontal or a vertical pass controlled by the Direction
// constant.  Run four dispatches (H→V→H→V) to approximate a Gaussian.
//
// Uses group-shared memory so each texel is loaded once per thread
// group, then reused across the sliding window.
//
// Entry point:
//   CSMain  (cs_5_0)
//
// Recompile with:
//   fxc /T cs_5_0 /E CSMain /Fh BoxBlurCS.h /Vn g_BoxBlurCS BoxBlurCS.hlsl
//
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
//==============================================================================

// Input texture (read-only).
Texture2D<float4>   InputTex  : register(t0);

// Output texture (write-only).
RWTexture2D<float4> OutputTex : register(u0);

cbuffer BlurConstants : register(b0)
{
    uint  Direction;    // 0 = horizontal, 1 = vertical
    int   Radius;       // Box blur radius in pixels
    uint  Width;        // Image width
    uint  Height;       // Image height
};

// Thread group: 256 threads along the blur axis.
#define GROUP_SIZE 256

// Max radius we support.  Shared memory = (GROUP_SIZE + 2*MAX_RADIUS) * 4 floats * 4 bytes
// = (256 + 64) * 16 = ~5 KB, well within the 32 KB limit.
#define MAX_RADIUS 32

// Shared memory tile: enough for the group + apron on both sides.
groupshared float4 tile[GROUP_SIZE + 2 * MAX_RADIUS];

[numthreads(GROUP_SIZE, 1, 1)]
void CSMain( uint3 groupId    : SV_GroupID,
             uint3 groupTid   : SV_GroupThreadID,
             uint3 dispatchId : SV_DispatchThreadID )
{
    int r = min( Radius, MAX_RADIUS );
    int tileSize = GROUP_SIZE + 2 * r;
    int tid = (int)groupTid.x;

    if( Direction == 0 )
    {
        // ── Horizontal pass ────────────────────────────────────────
        int row = (int)groupId.y;
        if( (uint)row >= Height )
            return;

        int groupStart = (int)groupId.x * GROUP_SIZE;

        // Load tile: each thread loads its primary texel + apron.
        for( int i = tid; i < tileSize; i += GROUP_SIZE )
        {
            int srcX = clamp( groupStart + i - r, 0, (int)Width - 1 );
            tile[i] = InputTex[int2( srcX, row )];
        }
        GroupMemoryBarrierWithGroupSync();

        int outX = groupStart + tid;
        if( (uint)outX >= Width )
            return;

        // Sum the window from shared memory.
        float4 sum = (float4)0;
        int windowStart = tid;        // tile index = tid + r - r
        for( int k = 0; k <= 2 * r; k++ )
        {
            sum += tile[windowStart + k];
        }
        OutputTex[int2( outX, row )] = sum / (float)( 2 * r + 1 );
    }
    else
    {
        // ── Vertical pass ──────────────────────────────────────────
        int col = (int)groupId.y;
        if( (uint)col >= Width )
            return;

        int groupStart = (int)groupId.x * GROUP_SIZE;

        // Load tile.
        for( int i = tid; i < tileSize; i += GROUP_SIZE )
        {
            int srcY = clamp( groupStart + i - r, 0, (int)Height - 1 );
            tile[i] = InputTex[int2( col, srcY )];
        }
        GroupMemoryBarrierWithGroupSync();

        int outY = groupStart + tid;
        if( (uint)outY >= Height )
            return;

        float4 sum = (float4)0;
        int windowStart = tid;
        for( int k = 0; k <= 2 * r; k++ )
        {
            sum += tile[windowStart + k];
        }
        OutputTex[int2( col, outY )] = sum / (float)( 2 * r + 1 );
    }
}
