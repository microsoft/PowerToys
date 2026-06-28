/* Copyright (c) 2007-2008 CSIRO
   Copyright (c) 2007-2009 Xiph.Org Foundation
   Written by Jean-Marc Valin */
/**
   @file pitch.c
   @brief Pitch analysis
 */

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

#include "pitch.h"
#include "common.h"
#include "denoise.h"
#include "celt_lpc.h"
#include "math.h"

static void find_best_pitch(opus_val32 *xcorr, opus_val16 *y, int len,
                            int max_pitch, int *best_pitch
#ifdef FIXED_POINT
                            , int yshift, opus_val32 maxcorr
#endif
                            )
{
   int i, j;
   opus_val32 Syy=1;
   opus_val16 best_num[2];
   opus_val32 best_den[2];
#ifdef FIXED_POINT
   int xshift;

   xshift = celt_ilog2(maxcorr)-14;
#endif

   best_num[0] = -1;
   best_num[1] = -1;
   best_den[0] = 0;
   best_den[1] = 0;
   best_pitch[0] = 0;
   best_pitch[1] = 1;
   for (j=0;j<len;j++)
      Syy = ADD32(Syy, SHR32(MULT16_16(y[j],y[j]), yshift));
   for (i=0;i<max_pitch;i++)
   {
      if (xcorr[i]>0)
      {
         opus_val16 num;
         opus_val32 xcorr16;
         xcorr16 = EXTRACT16(VSHR32(xcorr[i], xshift));
#ifndef FIXED_POINT
         /* Considering the range of xcorr16, this should avoid both underflows
            and overflows (inf) when squaring xcorr16 */
         xcorr16 *= 1e-12f;
#endif
         num = MULT16_16_Q15(xcorr16,xcorr16);
         if (MULT16_32_Q15(num,best_den[1]) > MULT16_32_Q15(best_num[1],Syy))
         {
            if (MULT16_32_Q15(num,best_den[0]) > MULT16_32_Q15(best_num[0],Syy))
            {
               best_num[1] = best_num[0];
               best_den[1] = best_den[0];
               best_pitch[1] = best_pitch[0];
               best_num[0] = num;
               best_den[0] = Syy;
               best_pitch[0] = i;
            } else {
               best_num[1] = num;
               best_den[1] = Syy;
               best_pitch[1] = i;
            }
         }
      }
      Syy += SHR32(MULT16_16(y[i+len],y[i+len]),yshift) - SHR32(MULT16_16(y[i],y[i]),yshift);
      Syy = MAX32(1, Syy);
   }
}

static void celt_fir5(const opus_val16 *x,
         const opus_val16 *num,
         opus_val16 *y,
         int N,
         opus_val16 *mem)
{
   int i;
   opus_val16 num0, num1, num2, num3, num4;
   opus_val32 mem0, mem1, mem2, mem3, mem4;
   num0=num[0];
   num1=num[1];
   num2=num[2];
   num3=num[3];
   num4=num[4];
   mem0=mem[0];
   mem1=mem[1];
   mem2=mem[2];
   mem3=mem[3];
   mem4=mem[4];
   for (i=0;i<N;i++)
   {
      opus_val32 sum = SHL32(EXTEND32(x[i]), SIG_SHIFT);
      sum = MAC16_16(sum,num0,mem0);
      sum = MAC16_16(sum,num1,mem1);
      sum = MAC16_16(sum,num2,mem2);
      sum = MAC16_16(sum,num3,mem3);
      sum = MAC16_16(sum,num4,mem4);
      mem4 = mem3;
      mem3 = mem2;
      mem2 = mem1;
      mem1 = mem0;
      mem0 = x[i];
      y[i] = ROUND16(sum, SIG_SHIFT);
   }
   mem[0]=mem0;
   mem[1]=mem1;
   mem[2]=mem2;
   mem[3]=mem3;
   mem[4]=mem4;
}


void rnn_pitch_downsample(celt_sig *x[], opus_val16 *x_lp,
      int len, int C)
{
   int i;
   opus_val32 ac[5];
   opus_val16 tmp=Q15ONE;
   opus_val16 lpc[4], mem[5]={0,0,0,0,0};
   opus_val16 lpc2[5];
   opus_val16 c1 = QCONST16(.8f,15);
#ifdef FIXED_POINT
   int shift;
   opus_val32 maxabs = celt_maxabs32(x[0], len);
   if (C==2)
   {
      opus_val32 maxabs_1 = celt_maxabs32(x[1], len);
      maxabs = MAX32(maxabs, maxabs_1);
   }
   if (maxabs<1)
      maxabs=1;
   shift = celt_ilog2(maxabs)-10;
   if (shift<0)
      shift=0;
   if (C==2)
      shift++;
#endif
   for (i=1;i<len>>1;i++)
      x_lp[i] = SHR32(HALF32(HALF32(x[0][(2*i-1)]+x[0][(2*i+1)])+x[0][2*i]), shift);
   x_lp[0] = SHR32(HALF32(HALF32(x[0][1])+x[0][0]), shift);
   if (C==2)
   {
      for (i=1;i<len>>1;i++)
         x_lp[i] += SHR32(HALF32(HALF32(x[1][(2*i-1)]+x[1][(2*i+1)])+x[1][2*i]), shift);
      x_lp[0] += SHR32(HALF32(HALF32(x[1][1])+x[1][0]), shift);
   }

   rnn_autocorr(x_lp, ac, NULL, 0,
                  4, len>>1);

   /* Noise floor -40 dB */
#ifdef FIXED_POINT
   ac[0] += SHR32(ac[0],13);
#else
   ac[0] *= 1.0001f;
#endif
   /* Lag windowing */
   for (i=1;i<=4;i++)
   {
      /*ac[i] *= exp(-.5*(2*M_PI*.002*i)*(2*M_PI*.002*i));*/
#ifdef FIXED_POINT
      ac[i] -= MULT16_32_Q15(2*i*i, ac[i]);
#else
      ac[i] -= ac[i]*(.008f*i)*(.008f*i);
#endif
   }

   rnn_lpc(lpc, ac, 4);
   for (i=0;i<4;i++)
   {
      tmp = MULT16_16_Q15(QCONST16(.9f,15), tmp);
      lpc[i] = MULT16_16_Q15(lpc[i], tmp);
   }
   /* Add a zero */
   lpc2[0] = lpc[0] + QCONST16(.8f,SIG_SHIFT);
   lpc2[1] = lpc[1] + MULT16_16_Q15(c1,lpc[0]);
   lpc2[2] = lpc[2] + MULT16_16_Q15(c1,lpc[1]);
   lpc2[3] = lpc[3] + MULT16_16_Q15(c1,lpc[2]);
   lpc2[4] = MULT16_16_Q15(c1,lpc[3]);
   celt_fir5(x_lp, lpc2, x_lp, len>>1, mem);
}

void rnn_pitch_xcorr(const opus_val16 *_x, const opus_val16 *_y,
      opus_val32 *xcorr, int len, int max_pitch)
{

#if 0 /* This is a simple version of the pitch correlation that should work
         well on DSPs like Blackfin and TI C5x/C6x */
   int i, j;
#ifdef FIXED_POINT
   opus_val32 maxcorr=1;
#endif
   for (i=0;i<max_pitch;i++)
   {
      opus_val32 sum = 0;
      for (j=0;j<len;j++)
         sum = MAC16_16(sum, _x[j], _y[i+j]);
      xcorr[i] = sum;
#ifdef FIXED_POINT
      maxcorr = MAX32(maxcorr, sum);
#endif
   }
#ifdef FIXED_POINT
   return maxcorr;
#endif

#else /* Unrolled version of the pitch correlation -- runs faster on x86 and ARM */
   int i;
   /*The EDSP version requires that max_pitch is at least 1, and that _x is
      32-bit aligned.
     Since it's hard to put asserts in assembly, put them here.*/
#ifdef FIXED_POINT
   opus_val32 maxcorr=1;
#endif
   celt_assert(max_pitch>0);
   celt_assert((((unsigned char *)_x-(unsigned char *)NULL)&3)==0);
   for (i=0;i<max_pitch-3;i+=4)
   {
      opus_val32 sum[4]={0,0,0,0};
      xcorr_kernel(_x, _y+i, sum, len);
      xcorr[i]=sum[0];
      xcorr[i+1]=sum[1];
      xcorr[i+2]=sum[2];
      xcorr[i+3]=sum[3];
#ifdef FIXED_POINT
      sum[0] = MAX32(sum[0], sum[1]);
      sum[2] = MAX32(sum[2], sum[3]);
      sum[0] = MAX32(sum[0], sum[2]);
      maxcorr = MAX32(maxcorr, sum[0]);
#endif
   }
   /* In case max_pitch isn't a multiple of 4, do non-unrolled version. */
   for (;i<max_pitch;i++)
   {
      opus_val32 sum;
      sum = celt_inner_prod(_x, _y+i, len);
      xcorr[i] = sum;
#ifdef FIXED_POINT
      maxcorr = MAX32(maxcorr, sum);
#endif
   }
#ifdef FIXED_POINT
   return maxcorr;
#endif
#endif
}

void rnn_pitch_search(const opus_val16 *x_lp, opus_val16 *y,
                  int len, int max_pitch, int *pitch)
{
   int i, j;
   int lag;
   int best_pitch[2]={0,0};
#ifdef FIXED_POINT
   opus_val32 maxcorr;
   opus_val32 xmax, ymax;
   int shift=0;
#endif
   int offset;
   opus_val16 x_lp4[PITCH_FRAME_SIZE>>2];
   opus_val16 y_lp4[(PITCH_FRAME_SIZE+PITCH_MAX_PERIOD)>>2];
   opus_val32 xcorr[PITCH_MAX_PERIOD>>1];

   celt_assert(len <= PITCH_FRAME_SIZE);
   celt_assert(max_pitch <= PITCH_MAX_PERIOD);
   celt_assert(len>0);
   celt_assert(max_pitch>0);
   lag = len+max_pitch;


   /* Downsample by 2 again */
   for (j=0;j<len>>2;j++)
      x_lp4[j] = x_lp[2*j];
   for (j=0;j<lag>>2;j++)
      y_lp4[j] = y[2*j];

#ifdef FIXED_POINT
   xmax = celt_maxabs16(x_lp4, len>>2);
   ymax = celt_maxabs16(y_lp4, lag>>2);
   shift = celt_ilog2(MAX32(1, MAX32(xmax, ymax)))-11;
   if (shift>0)
   {
      for (j=0;j<len>>2;j++)
         x_lp4[j] = SHR16(x_lp4[j], shift);
      for (j=0;j<lag>>2;j++)
         y_lp4[j] = SHR16(y_lp4[j], shift);
      /* Use double the shift for a MAC */
      shift *= 2;
   } else {
      shift = 0;
   }
#endif

   /* Coarse search with 4x decimation */

#ifdef FIXED_POINT
   maxcorr =
#endif
   rnn_pitch_xcorr(x_lp4, y_lp4, xcorr, len>>2, max_pitch>>2);

   find_best_pitch(xcorr, y_lp4, len>>2, max_pitch>>2, best_pitch
#ifdef FIXED_POINT
                   , 0, maxcorr
#endif
                   );

   /* Finer search with 2x decimation */
#ifdef FIXED_POINT
   maxcorr=1;
#endif
   for (i=0;i<max_pitch>>1;i++)
   {
      opus_val32 sum;
      xcorr[i] = 0;
      if (abs(i-2*best_pitch[0])>2 && abs(i-2*best_pitch[1])>2)
         continue;
#ifdef FIXED_POINT
      sum = 0;
      for (j=0;j<len>>1;j++)
         sum += SHR32(MULT16_16(x_lp[j],y[i+j]), shift);
#else
      sum = celt_inner_prod(x_lp, y+i, len>>1);
#endif
      xcorr[i] = MAX32(-1, sum);
#ifdef FIXED_POINT
      maxcorr = MAX32(maxcorr, sum);
#endif
   }
   find_best_pitch(xcorr, y, len>>1, max_pitch>>1, best_pitch
#ifdef FIXED_POINT
                   , shift+1, maxcorr
#endif
                   );

   /* Refine by pseudo-interpolation */
   if (best_pitch[0]>0 && best_pitch[0]<(max_pitch>>1)-1)
   {
      opus_val32 a, b, c;
      a = xcorr[best_pitch[0]-1];
      b = xcorr[best_pitch[0]];
      c = xcorr[best_pitch[0]+1];
      if ((c-a) > MULT16_32_Q15(QCONST16(.7f,15),b-a))
         offset = 1;
      else if ((a-c) > MULT16_32_Q15(QCONST16(.7f,15),b-c))
         offset = -1;
      else
         offset = 0;
   } else {
      offset = 0;
   }
   *pitch = 2*best_pitch[0]-offset;
}

#ifdef FIXED_POINT
static opus_val16 compute_pitch_gain(opus_val32 xy, opus_val32 xx, opus_val32 yy)
{
   opus_val32 x2y2;
   int sx, sy, shift;
   opus_val32 g;
   opus_val16 den;
   if (xy == 0 || xx == 0 || yy == 0)
      return 0;
   sx = celt_ilog2(xx)-14;
   sy = celt_ilog2(yy)-14;
   shift = sx + sy;
   x2y2 = SHR32(MULT16_16(VSHR32(xx, sx), VSHR32(yy, sy)), 14);
   if (shift & 1) {
      if (x2y2 < 32768)
      {
         x2y2 <<= 1;
         shift--;
      } else {
         x2y2 >>= 1;
         shift++;
      }
   }
   den = celt_rsqrt_norm(x2y2);
   g = MULT16_32_Q15(den, xy);
   g = VSHR32(g, (shift>>1)-1);
   return EXTRACT16(MIN32(g, Q15ONE));
}
#else
static opus_val16 compute_pitch_gain(opus_val32 xy, opus_val32 xx, opus_val32 yy)
{
   return xy/sqrt(1+xx*yy);
}
#endif

static const int second_check[16] = {0, 0, 3, 2, 3, 2, 5, 2, 3, 2, 3, 2, 5, 2, 3, 2};
opus_val16 rnn_remove_doubling(opus_val16 *x, int maxperiod, int minperiod,
      int N, int *T0_, int prev_period, opus_val16 prev_gain)
{
   int k, i, T, T0;
   opus_val16 g, g0;
   opus_val16 pg;
   opus_val32 xy,xx,yy,xy2;
   opus_val32 xcorr[3];
   opus_val32 best_xy, best_yy;
   int offset;
   int minperiod0;
   opus_val32 yy_lookup[PITCH_MAX_PERIOD+1];
   
   celt_assert(maxperiod <= PITCH_MAX_PERIOD);

   minperiod0 = minperiod;
   maxperiod /= 2;
   minperiod /= 2;
   *T0_ /= 2;
   prev_period /= 2;
   N /= 2;
   x += maxperiod;
   if (*T0_>=maxperiod)
      *T0_=maxperiod-1;

   T = T0 = *T0_;
   dual_inner_prod(x, x, x-T0, N, &xx, &xy);
   yy_lookup[0] = xx;
   yy=xx;
   for (i=1;i<=maxperiod;i++)
   {
      yy = yy+MULT16_16(x[-i],x[-i])-MULT16_16(x[N-i],x[N-i]);
      yy_lookup[i] = MAX32(0, yy);
   }
   yy = yy_lookup[T0];
   best_xy = xy;
   best_yy = yy;
   g = g0 = compute_pitch_gain(xy, xx, yy);
   /* Look for any pitch at T/k */
   for (k=2;k<=15;k++)
   {
      int T1, T1b;
      opus_val16 g1;
      opus_val16 cont=0;
      opus_val16 thresh;
      T1 = (2*T0+k)/(2*k);
      if (T1 < minperiod)
         break;
      /* Look for another strong correlation at T1b */
      if (k==2)
      {
         if (T1+T0>maxperiod)
            T1b = T0;
         else
            T1b = T0+T1;
      } else
      {
         T1b = (2*second_check[k]*T0+k)/(2*k);
      }
      dual_inner_prod(x, &x[-T1], &x[-T1b], N, &xy, &xy2);
      xy = HALF32(xy + xy2);
      yy = HALF32(yy_lookup[T1] + yy_lookup[T1b]);
      g1 = compute_pitch_gain(xy, xx, yy);
      if (abs(T1-prev_period)<=1)
         cont = prev_gain;
      else if (abs(T1-prev_period)<=2 && 5*k*k < T0)
         cont = HALF16(prev_gain);
      else
         cont = 0;
      thresh = MAX16(QCONST16(.3f,15), MULT16_16_Q15(QCONST16(.7f,15),g0)-cont);
      /* Bias against very high pitch (very short period) to avoid false-positives
         due to short-term correlation */
      if (T1<3*minperiod)
         thresh = MAX16(QCONST16(.4f,15), MULT16_16_Q15(QCONST16(.85f,15),g0)-cont);
      else if (T1<2*minperiod)
         thresh = MAX16(QCONST16(.5f,15), MULT16_16_Q15(QCONST16(.9f,15),g0)-cont);
      if (g1 > thresh)
      {
         best_xy = xy;
         best_yy = yy;
         T = T1;
         g = g1;
      }
   }
   best_xy = MAX32(0, best_xy);
   if (best_yy <= best_xy)
      pg = Q15ONE;
   else
      pg = best_xy/(best_yy+1);

   for (k=0;k<3;k++)
      xcorr[k] = celt_inner_prod(x, x-(T+k-1), N);
   if ((xcorr[2]-xcorr[0]) > MULT16_32_Q15(QCONST16(.7f,15),xcorr[1]-xcorr[0]))
      offset = 1;
   else if ((xcorr[0]-xcorr[2]) > MULT16_32_Q15(QCONST16(.7f,15),xcorr[1]-xcorr[2]))
      offset = -1;
   else
      offset = 0;
   if (pg > g)
      pg = g;
   *T0_ = 2*T+offset;

   if (*T0_<minperiod0)
      *T0_=minperiod0;
   return pg;
}
