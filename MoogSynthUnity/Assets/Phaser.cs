// Copyright (c) 2018 Jakob Schmid
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
//  
//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.
//  
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE."

using System;
using UnityEngine;

// Originally, this was designed with a float phase that ran between 0 and 1.
// The code was simplified and made more robust by using a 16-bit integer.
// It turns out that 16-bit integers are too small for slow LFOs:
//
// A 0.3 Hz LFO:
//   freq       = 0.3 [per/s] / 48000 [smp/s] = 0.000006 [per/smp]
//   freq_int16 = 0.00006 [per/smp] * (1<<16) ~ 0.41
//   ^ which is rounded down to 0
//
// In comparison, the same LFO using 32-bit integers:
//   freq_int32 = 0.00006 [per/smp] * (1<<32) ~ 26843.5 [per/smp]
//   which is rounded down to 26843
class Phaser
{
    public UInt32 phase = 0u; // using an integer type automatically ensures limits
                              // phase is in [0 ; 2^(32-1)]

    const float PHASE_MAX = 4294967296;
    float amp = 1.0f;
    UInt32 freq__ph_p_smp = 0u;
    bool is_active = true;

    public Phaser(float amp = 1.0f)
    {
        this.amp = amp;
    }

    public void restart()
    {
        phase = 0u;
        is_active = true;
    }
    public void update()
    {
        phase += freq__ph_p_smp;
    }
    public void update_oneshot() // envelope-like behaviour
    {
        UInt32 phase_old = phase;
        phase += freq__ph_p_smp;

        // Stop
        if (phase < phase_old)
        {
            is_active = false;
            phase = 0u;
        }
    }
    public void set_freq(float freq__hz, int sample_rate = 48000)
    {
        float freq__ppsmp = freq__hz / sample_rate; // periods per sample
        freq__ph_p_smp = (uint)(freq__ppsmp * PHASE_MAX);
    }
    public float sin()
    {
        if (is_active == false) return 0.0f;
        float ph01 = phase / PHASE_MAX;
        return Mathf.Sin(ph01 * 6.28318530717959f) * amp;
    }
    public float square(float pulse_width)
    {
        float ph01 = phase / PHASE_MAX;
        return ph01 > pulse_width ? amp : -amp;
    }
    // (1-x)^2
    // s=2: parabolic
    public float quad_down01()
    {
        if (is_active == false) return 0.0f;
        float ph01 = phase / PHASE_MAX;
        float x = 1.0f - ph01;
        return x * x;
    }
};
