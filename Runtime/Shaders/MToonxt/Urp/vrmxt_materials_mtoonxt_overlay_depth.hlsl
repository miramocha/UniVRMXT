#ifndef VRMC_MATERIALS_MTOONXT_OVERLAY_DEPTH_INCLUDED
#define VRMC_MATERIALS_MTOONXT_OVERLAY_DEPTH_INCLUDED

// Color overlay passes define MTOONXT_OVERLAY_DEPTH_PASS and hardcode ZTest Always /
// ZWrite Off. Other color passes omit that define. Skip when the matching keyword
// disagrees so Unity does not run both LightMode copies. Outline uses a separate
// keyword so outline overlay does not skip body forward.
#if defined(MTOON_PASS_OUTLINE)
    #if defined(MTOONXT_OVERLAY_DEPTH_PASS)
        #if !defined(_MTOONXT_OUTLINE_OVERLAY_DEPTH)
            #define MTOONXT_SKIP_THIS_OVERLAY_DEPTH_PASS
        #endif
    #elif defined(_MTOONXT_OUTLINE_OVERLAY_DEPTH)
        #define MTOONXT_SKIP_THIS_OVERLAY_DEPTH_PASS
    #endif
#else
    #if defined(MTOONXT_OVERLAY_DEPTH_PASS)
        #if !defined(_MTOONXT_OVERLAY_DEPTH)
            #define MTOONXT_SKIP_THIS_OVERLAY_DEPTH_PASS
        #endif
    #elif defined(_MTOONXT_OVERLAY_DEPTH)
        #define MTOONXT_SKIP_THIS_OVERLAY_DEPTH_PASS
    #endif
#endif

#endif
