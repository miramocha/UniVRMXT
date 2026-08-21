#ifndef VRMC_MATERIALS_MTOONXT_OVERLAY_DEPTH_INCLUDED
#define VRMC_MATERIALS_MTOONXT_OVERLAY_DEPTH_INCLUDED

// Color overlay passes define MTOONXT_OVERLAY_DEPTH_PASS and hardcode ZTest Always /
// ZWrite Off. Other passes omit that define. Skip when the material keyword disagrees
// so Unity does not run both LightMode copies.
#if defined(MTOONXT_OVERLAY_DEPTH_PASS)
    #if !defined(_MTOONXT_OVERLAY_DEPTH)
        #define MTOONXT_SKIP_THIS_OVERLAY_DEPTH_PASS
    #endif
#elif defined(_MTOONXT_OVERLAY_DEPTH)
    #define MTOONXT_SKIP_THIS_OVERLAY_DEPTH_PASS
#endif

#endif
