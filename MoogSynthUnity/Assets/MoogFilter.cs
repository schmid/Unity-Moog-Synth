using System;

// Based on https://github.com/ddiakopoulos/MoogLadders/blob/master/src/HuovilainenModel.h
//
// Based on implementation in CSound5 (LGPLv2.1)
// https://github.com/csound/csound/blob/develop/COPYING
//
// Huovilainen developed an improved and physically correct model of the Moog
// Ladder filter that builds upon the work done by Smith and Stilson. This model
// inserts nonlinearities inside each of the 4 one-pole sections on account of the
// smoothly saturating function of analog transistors. The base-emitter voltages of
// the transistors are considered with an experimental value of 1.22070313 which
// maintains the characteristic sound of the analog Moog. This model also permits
// self-oscillation for resonances greater than 1. The model depends on five
// hyperbolic tangent functions (tanh) for each sample, and an oversampling factor
// of two (preferably higher, if possible). Although a more faithful
// representation of the Moog ladder, these dependencies increase the processing
// time of the filter significantly. Lastly, a half-sample delay is introduced for 
// phase compensation at the final stage of the filter. 
//
// References: Huovilainen (2004), Huovilainen (2010), DAFX - Zolzer (ed) (2nd ed)
// Original implementation: Victor Lazzarini for CSound5
//
// Considerations for oversampling: 
// http://music.columbia.edu/pipermail/music-dsp/2005-February/062778.html
// http://www.synthmaker.co.uk/dokuwiki/doku.php?id=tutorials:oversampling
public class MoogFilter
{
    double[] stage = new double[4];
    double[] stageTanh = new double[3];
    double[] delay = new double[6];

    double thermal;
    double tune;
    double acr;
    double resQuad;

    float cutoff;
    float resonance;
    float sampleRate;

    public MoogFilter(float sampleRate)
    {
        thermal = 0.000025;
        Array.Clear(stage, 0, stage.Length);
        Array.Clear(delay, 0, delay.Length);
        Array.Clear(stageTanh, 0, stageTanh.Length);
        SetCutoff(1000.0f);
        SetResonance(0.10f);
        this.sampleRate = sampleRate;
    }

    void process_mono(float[] samples, uint n)
    {
        for (int s = 0; s < n; ++s)
        {
            // Oversample
            for (int j = 0; j < 2; j++)
            {
                float input = samples[s] - (float)(resQuad * delay[5]);
                delay[0] = stage[0] = delay[0] + tune * (Math.Tanh(input * thermal) - stageTanh[0]);
                for (int k = 1; k < 4; k++)
                {
                    input = (float)stage[k - 1];
                    stage[k] = delay[k] + tune * ((stageTanh[k - 1] = Math.Tanh(input * thermal)) - (k != 3 ? stageTanh[k] : Math.Tanh(delay[k] * thermal)));
                    delay[k] = stage[k];
                }
                // 0.5 sample delay for phase compensation
                delay[5] = (stage[3] + delay[4]) * 0.5;
                delay[4] = stage[3];
            }
            samples[s] = (float)delay[5];
        }

    }

    // Process samples[0], samples[0+stride*1], samples[0+stride*2], etc.
    public void process_mono_stride(float[] samples, int sample_count, int offset, int stride)
    {
        for (int s = 0; s < sample_count; ++s)
        {
            // Oversample
            for (int j = 0; j < 2; j++)
            {
                float input = samples[s * stride] - (float)(resQuad * delay[5]);
                delay[0] = stage[0] = delay[0] + tune * (Math.Tanh(input * thermal) - stageTanh[0]);
                for (int k = 1; k < 4; k++)
                {
                    input = (float)stage[k - 1];
                    stage[k] = delay[k] + tune * ((stageTanh[k - 1] = Math.Tanh(input * thermal))
                               - (k != 3 ? stageTanh[k] : Math.Tanh(delay[k] * thermal)));
                    delay[k] = stage[k];
                }
                // 0.5 sample delay for phase compensation
                delay[5] = (stage[3] + delay[4]) * 0.5;
                delay[4] = stage[3];
            }
            samples[s * stride + offset] = (float)delay[5];
        }
    }

    public void SetResonance(float r)
    {
        if (r > 0.9f) r = 0.9f;
        if (r < 0.0f) r = 0.0f;

        resonance = r;
        resQuad = 4.0 * resonance * acr;
    }

    public void SetCutoff(float c)
    {
        if (c < 0.01f) c = 0.01f;

        cutoff = c;

        double fc = cutoff / sampleRate;
        double f = fc * 0.5; // oversampled 
        double fc2 = fc * fc;
        double fc3 = fc * fc * fc;

        double fcr = 1.8730 * fc3 + 0.4955 * fc2 - 0.6490 * fc + 0.9988;
        acr = -3.9364 * fc2 + 1.8409 * fc + 0.9968;

        tune = (1.0 - Math.Exp(-((2 * Math.PI) * f * fcr))) / thermal;

        SetResonance(resonance);
    }
}
