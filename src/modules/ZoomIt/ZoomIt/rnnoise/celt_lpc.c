/* Copyright (c) 2009-2010 Xiph.Org Foundation
   Written by Jean-Marc Valin */
/*
   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

#ifdef HAVE_CONFIG_H
#include "config.h"
#endif

#include "celt_lpc.h"
#include "arch.h"
#include "common.h"
#include "pitch.h"
#include "denoise.h"

void rnn_lpc(
      opus_val16       *_lpc, /* out: [0...p-1] LPC coefficients      */
const opus_val32 *ac,  /* in:  [0...p] autocorrelation values  */
int          p
)
{
   int i, j;
   opus_val32 r;
   opus_val32 error = ac[0];
#ifdef FIXED_POINT
   opus_val32 lpc[LPC_ORDER];
#else
   float *lpc = _lpc;
#endif

   RNN_CLEAR(lpc, p);
   if (ac[0] != 0)
   {
      for (i = 0; i < p; i++) {
         /* Sum up this iteration's reflection coefficient */
         opus_val32 rr = 0;
         for (j = 0; j < i; j++)
            rr += MULT32_32_Q31(lpc[j],ac[i - j]);
         rr += SHR32(ac[i + 1],3);
         r = -SHL32(rr,3)/error;
         /*  Update LPC coefficients and total error */
         lpc[i] = SHR32(r,3);
         for (j = 0; j < (i+1)>>1; j++)
         {
            opus_val32 tmp1, tmp2;
            tmp1 = lpc[j];
            tmp2 = lpc[i-1-j];
            lpc[j]     = tmp1 + MULT32_32_Q31(r,tmp2);
            lpc[i-1-j] = tmp2 + MULT32_32_Q31(r,tmp1);
         }

         error = error - MULT32_32_Q31(MULT32_32_Q31(r,r),error);
         /* Bail out once we get 30 dB gain */
#ifdef FIXED_POINT
         if (error<SHR32(ac[0],10))
            break;
#else
         if (error<.001f*ac[0])
            break;
#endif
      }
   }
#ifdef FIXED_POINT
   for (i=0;i<p;i++)
      _lpc[i] = ROUND16(lpc[i],16);
#endif
}


int rnn_autocorr(
                   const opus_val16 *x,   /*  in: [0...n-1] samples x   */
                   opus_val32       *ac,  /* out: [0...lag-1] ac values */
                   const opus_val16       *window,
                   int          overlap,
                   int          lag,
                   int          n)
{
   opus_val32 d;
   int i, k;
   int fastN=n-lag;
   int shift;
   const opus_val16 *xptr;
   opus_val16 xx[PITCH_BUF_SIZE/2];
   celt_assert(n>0);
   celt_assert(n<=PITCH_BUF_SIZE/2)
   celt_assert(overlap>=0);
   if (overlap == 0)
   {
      xptr = x;
   } else {
      for (i=0;i<n;i++)
         xx[i] = x[i];
      for (i=0;i<overlap;i++)
      {
         xx[i] = MULT16_16_Q15(x[i],window[i]);
         xx[n-i-1] = MULT16_16_Q15(x[n-i-1],window[i]);
      }
      xptr = xx;
   }
   shift=0;
#ifdef FIXED_POINT
   {
      opus_val32 ac0;
      ac0 = 1+(n<<7);
      if (n&1) ac0 += SHR32(MULT16_16(xptr[0],xptr[0]),9);
      for(i=(n&1);i<n;i+=2)
      {
         ac0 += SHR32(MULT16_16(xptr[i],xptr[i]),9);
         ac0 += SHR32(MULT16_16(xptr[i+1],xptr[i+1]),9);
      }

      shift = celt_ilog2(ac0)-30+10;
      shift = (shift)/2;
      if (shift>0)
      {
         for(i=0;i<n;i++)
            xx[i] = PSHR32(xptr[i], shift);
         xptr = xx;
      } else
         shift = 0;
   }
#endif
   rnn_pitch_xcorr(xptr, xptr, ac, fastN, lag+1);
   for (k=0;k<=lag;k++)
   {
      for (i = k+fastN, d = 0; i < n; i++)
         d = MAC16_16(d, xptr[i], xptr[i-k]);
      ac[k] += d;
   }
#ifdef FIXED_POINT
   shift = 2*shift;
   if (shift<=0)
      ac[0] += SHL32((opus_int32)1, -shift);
   if (ac[0] < 268435456)
   {
      int shift2 = 29 - EC_ILOG(ac[0]);
      for (i=0;i<=lag;i++)
         ac[i] = SHL32(ac[i], shift2);
      shift -= shift2;
   } else if (ac[0] >= 536870912)
   {
      int shift2=1;
      if (ac[0] >= 1073741824)
         shift2++;
      for (i=0;i<=lag;i++)
         ac[i] = SHR32(ac[i], shift2);
      shift += shift2;
   }
#endif

   return shift;
}
