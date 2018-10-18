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

[RequireComponent(typeof(AudioSource))]
public class MoogSynth : MonoBehaviour
{
    enum Parameters
    {
    	Cutoff = 0,
    	Resonance,
    	Decay,
    	Filter_enabled,

    	Square_amp,
    	Sub_amp,

    	PWM_str,
    	PWM_freq,

    	AENV_attack,
    	AENV_decay,
    	AENV_sustain,
    	AENV_release,
    };
    public enum Event_type
    {
        None = 0,
        Note_on = 1,
        Note_off = 2,
    };

    MoogFilter filter1, filter2;

    // Parameters
    [Header("Filter")]
    [Range(10, 24000)]
    public float cutoffFrequency;
    [Range(0, 1)]
    public float resonance;
    [Range(0, 1)]
    public float filterEnvDecay;
    [Range(0, 1)]
    public float filterEnabled;
    [Range(1, 4)]
    public int oversampling = 1;

    [Header("Amplitude")]
    [Range(0, 1)]
    public float squareAmp = 0.4f;
    [Range(0, 1)]
    public float subSine = 0.4f;

    [Header("Pulse-width Modulation")]
    [Range(0, 1)]
    public float pwmStrength;
    [Range(0, 1)]
    public float pwmFrequency;

    [Header("AENV")]
    [Range(0, 1)]
    public float aenv_attack;
    [Range(0, 1)]
    public float aenv_decay;
    [Range(0, 1)]
    public float aenv_sustain;
    [Range(0, 1)]
    public float aenv_release;

    // Synth state
    Phaser osc1, osc2, lfo;
    Phaser fenv;
    ADSR aenv;
    // when note_is_on = true, wait for current_delta samples and set note_is_playing = true
    bool note_is_playing;

    // Current MIDI evt
    bool note_is_on;
    int current_note;
    int current_velocity;

    int sample_rate;

    float[] freqtab = new float[128];


    /// Unity
    private void Start()
    {
        init(1, 48000);
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (channels == 2)
        {
            render_float32_stereo_interleaved(data, data.Length / 2);
        }
    }

    public void init(int queue_length, int sample_rate)
    {
        osc1 = new Phaser();
        osc2 = new Phaser();
        lfo = new Phaser();
        fenv = new Phaser();
        aenv = new ADSR();

        osc1.phase = 0u;
        osc2.phase = 0u;
        lfo.phase = 0u;

        aenv.reset();

        note_is_on = false;

        for (int i = 0; i < 128; i++)
        { // 128 midi notes
            freqtab[i] = midi2freq(i % 12, i / 12 - 2);
        }

        this.sample_rate = sample_rate;

        filter1 = new MoogFilter(sample_rate);
        filter2 = new MoogFilter(sample_rate);

        update_params();
    }

    private void update_params()
    {
        // Set synth params
        float freq = freqtab[current_note & 0x7f];
        osc1.set_freq(freq, sample_rate);
        osc2.set_freq(freq * 0.5f, sample_rate);
        fenv.set_freq(1.0f / filterEnvDecay, sample_rate);
        lfo.set_freq(pwmFrequency * 2.3f, sample_rate);

        float env01 = fenv.quad_down01();

        filter1.SetResonance(resonance);
        filter2.SetResonance(resonance);
        filter1.SetCutoff(cutoffFrequency * env01); // 0 Hz cutoff is bad
        filter2.SetCutoff(cutoffFrequency * env01);
        filter1.SetOversampling(oversampling);
        filter2.SetOversampling(oversampling);

        aenv.setAttackRate(aenv_attack * sample_rate);
        aenv.setDecayRate(aenv_decay * sample_rate);
        aenv.setReleaseRate(aenv_release * sample_rate);
        aenv.setSustainLevel(aenv_sustain);
    }

    public void render_float32_stereo_interleaved(float[] buffer, int sample_frames)
    {
        int smp = 0;
        int buf_idx = 0;
        if (note_is_on)
        {

            // Wait for current_delta
            //if (note_is_playing == false)
            //{
            //	for (smp = 0; smp < sample_frames && smp < current_delta; ++smp)
            //	{
            //		(*buf_ptr++) = 0.0f;
            //		(*buf_ptr++) = 0.0f;
            //	}
            //	note_is_playing = true;
            //}

            update_params();

            // Render loop
            for (; smp < sample_frames; ++smp)
            {

                // Render sample
                float amp = aenv.process() * 0.5f * squareAmp;
                float lfo_val = lfo.sin() * 0.48f * pwmStrength + 0.5f;
                float sample = (osc1.square(lfo_val) + osc2.sin() * subSine)
                                * (current_velocity * 0.0079f) * amp;

                buffer[buf_idx++] = sample;
                buffer[buf_idx++] = sample;

                // Update oscillators
                osc1.update();
                osc2.update();
                lfo.update();
                fenv.update_oneshot();
            }

            // Filter entire buffer
            if (filterEnabled >= 0.5f)
            {
            	filter1.process_mono_stride(buffer, sample_frames, 0, 2);
            	filter2.process_mono_stride(buffer, sample_frames, 1, 2);
            }
        }
        else
        {
            for (; smp < sample_frames; ++smp)
            {
                buffer[buf_idx++] = 0.0f;
                buffer[buf_idx++] = 0.0f;
            }
        }
    }

    public bool queue_event(Event_type evt_type, int data)
    {
        note_is_on = (evt_type == Event_type.Note_on);
        if (note_is_on)
        {
            current_note = data;
        }
        current_velocity = 127;

        note_is_playing = note_is_on;
        if (note_is_on)
        {
            osc1.phase = 0u;
            osc2.phase = 0u;
            fenv.restart();
        }
        aenv.gate(note_is_on);

        note_is_on = true;

        return true;
    }

    public bool set_parameter(int param_id, float value)
    {
        switch (param_id)
        {
            case (int)Parameters.Cutoff        : cutoffFrequency = value; break;
            case (int)Parameters.Resonance     : resonance = value;       break;
            case (int)Parameters.Decay         : filterEnvDecay = value;  break;
            case (int)Parameters.Filter_enabled: filterEnabled = value;   break;
            case (int)Parameters.Square_amp    : squareAmp = value;       break;
            case (int)Parameters.Sub_amp       : subSine = value;         break;
            case (int)Parameters.PWM_str       : pwmStrength = value;     break;
            case (int)Parameters.PWM_freq      : pwmFrequency = value;    break;
            case (int)Parameters.AENV_attack   : aenv_attack = value;     break;
            case (int)Parameters.AENV_decay    : aenv_decay = value;      break;
            case (int)Parameters.AENV_sustain  : aenv_sustain = value;    break;
            case (int)Parameters.AENV_release  : aenv_release = value;    break;
        }
        return true;
    }

    public float get_parameter(int param_id)
    {
        switch (param_id)
        {
            case (int)Parameters.Cutoff        : return cutoffFrequency;
            case (int)Parameters.Resonance     : return resonance;
            case (int)Parameters.Decay         : return filterEnvDecay;
            case (int)Parameters.Filter_enabled: return filterEnabled;
            case (int)Parameters.Square_amp    : return squareAmp;
            case (int)Parameters.Sub_amp       : return subSine;
            case (int)Parameters.PWM_str       : return pwmStrength;
            case (int)Parameters.PWM_freq      : return pwmFrequency;
            case (int)Parameters.AENV_attack   : return aenv_attack;
            case (int)Parameters.AENV_decay    : return aenv_decay;
            case (int)Parameters.AENV_sustain  : return aenv_sustain;
            case (int)Parameters.AENV_release  : return aenv_release;
            default: return -1.0f;
        }
    }

    /// Internals
    private float midi2freq(int note, int octave)
    {
        return 32.70319566257483f * Mathf.Pow(2.0f, note / 12.0f + octave);
    }
};
