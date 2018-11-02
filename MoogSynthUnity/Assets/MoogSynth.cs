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
    public enum Parameters
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
    public enum Modulators
    {
        ENV1 = 0,
        LFO1,
        LFO2,
    };
    public enum FilterType
    {
        Schmid,
#if LAZZARINI_FILTER
        Lazzarini
#endif
    }

    MoogFilter filter1, filter2;
#if LAZZARINI_FILTER
    MoogFilter_Lazzarini filter1Laz, filter2Laz;
#endif

    // Parameters
    [Header("Filter")]
    public FilterType filterType = FilterType.Schmid;
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
    //[Range(0, 1)]
    //public float squareAmp = 0.4f;
    [Range(0, 1)]
    public float squareAmp = 0.0f;
    [Range(0, 1)]
    public float sawAmp = 0.0f;
    [Range(0, 1)]
    public float subSine = 0.4f;

    //[Range(0, 1)]
    //public float sawDPWAmp = 0.4f;
    //[Range(0, 1)]
    //public float sawAmp = 0.4f;

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

    [HideInInspector]
    public float[] modulationMatrix = null;

    // Synth state
    Phaser osc1, osc2, lfo;
    Phaser fenv;
    ADSR aenv;
    // when note_is_on = true, wait for current_delta samples and set note_is_playing = true
    //bool note_is_playing;

    // Current MIDI evt
    bool note_is_on;
    int current_note;
    int current_velocity;

    EventQueue queue;

    int sample_rate;

    float[] freqtab = new float[128];

    private const int QueueCapacity = 320;
    private float[] lastBuffer = new float[2048];
    private readonly object bufferMutex = new object();
    private bool debugBufferEnabled = false;

    private EventQueue.QueuedEvent nextEvent;
    private bool eventIsWaiting = false;


    /// Public interface
    public bool queue_event(EventQueue.EventType evtType, int data, Int64 time_smp)
    {
        //queueLock = true;
        bool result = queue.Enqueue(evtType, data, time_smp);
        //queueLock = false;
        return result;
    }
    public void ClearQueue()
    {
        queue.Clear();
    }
    public bool set_parameter(int param_id, float value)
    {
        switch (param_id)
        {
            case (int)Parameters.Cutoff: cutoffFrequency = value; break;
            case (int)Parameters.Resonance: resonance = value; break;
            case (int)Parameters.Decay: filterEnvDecay = value; break;
            case (int)Parameters.Filter_enabled: filterEnabled = value; break;
            case (int)Parameters.Square_amp: squareAmp = value; break;
            case (int)Parameters.Sub_amp: subSine = value; break;
            case (int)Parameters.PWM_str: pwmStrength = value; break;
            case (int)Parameters.PWM_freq: pwmFrequency = value; break;
            case (int)Parameters.AENV_attack: aenv_attack = value; break;
            case (int)Parameters.AENV_decay: aenv_decay = value; break;
            case (int)Parameters.AENV_sustain: aenv_sustain = value; break;
            case (int)Parameters.AENV_release: aenv_release = value; break;
        }
        return true;
    }
    public float get_parameter(int param_id)
    {
        switch (param_id)
        {
            case (int)Parameters.Cutoff: return cutoffFrequency;
            case (int)Parameters.Resonance: return resonance;
            case (int)Parameters.Decay: return filterEnvDecay;
            case (int)Parameters.Filter_enabled: return filterEnabled;
            case (int)Parameters.Square_amp: return squareAmp;
            case (int)Parameters.Sub_amp: return subSine;
            case (int)Parameters.PWM_str: return pwmStrength;
            case (int)Parameters.PWM_freq: return pwmFrequency;
            case (int)Parameters.AENV_attack: return aenv_attack;
            case (int)Parameters.AENV_decay: return aenv_decay;
            case (int)Parameters.AENV_sustain: return aenv_sustain;
            case (int)Parameters.AENV_release: return aenv_release;
            default: return -1.0f;
        }
    }

    // This should only be called from OnAudioFilterRead
    public void HandleEventNow(EventQueue.QueuedEvent currentEvent)
    {
        note_is_on = (currentEvent.eventType == EventQueue.EventType.Note_on);

        if (note_is_on)
        {
            current_note = currentEvent.data;
            osc1.phase = 0u;
            osc2.phase = 0u;
            fenv.restart();
            update_params();
        }

        aenv.gate(note_is_on);
    }

    public Int64 GetTime_smp()
    {
        //return masterClock_smp;
        return time_smp;
    }

    /// Debug
    public void SetDebugBufferEnabled(bool enabled)
    {
        this.debugBufferEnabled = enabled;
    }
    public float[] GetLastBuffer()
    {
        return lastBuffer;
    }
    public object GetBufferMutex()
    {
        return bufferMutex;
    }

    //public bool bufferLock = false;
    //public bool queueLock = false;

    /// Unity
    private void Start()
    {
        init(1, 48000);
    }

    //private static MoogSynth clockMaster = null;
    //private bool isClockMaster = false;
    //private Int64 masterClock_smp = 0;
    private Int64 time_smp = 0;

    private void OnAudioFilterRead(float[] data, int channels)
    {
        //if (clockMaster == null)
        //{
        //    clockMaster = this;
        //    isClockMaster = true;
        //    masterClock_smp = 0;
        //}
        //else if (isClockMaster) // not first frame, increment
        //{
        //    masterClock_smp += sampleFrames;
        //}

        if (channels == 2)
        {
            int sampleFrames = data.Length / 2;
            render_float32_stereo_interleaved(data, sampleFrames);

            if (debugBufferEnabled)
            {
                //bufferLock = true;
                lock (bufferMutex)
                {
                    Array.Copy(data, lastBuffer, data.Length);
                }
                //bufferLock = false;
            }
        }
    }

    /// Internal
    private void init(int queue_length, int sample_rate)
    {
        osc1 = new Phaser();
        osc2 = new Phaser();
        lfo = new Phaser();
        fenv = new Phaser();
        aenv = new ADSR();

        note_is_on = false;

        for (int i = 0; i < 128; i++)
        { // 128 midi notes
            freqtab[i] = midi2freq(i % 12, i / 12 - 2);
        }

        this.sample_rate = sample_rate;

        filter1 = new MoogFilter(sample_rate);
        filter2 = new MoogFilter(sample_rate);
#if LAZZARINI_FILTER
        filter1Laz = new MoogFilter_Lazzarini(sample_rate);
        filter2Laz = new MoogFilter_Lazzarini(sample_rate);
#endif

        queue = new EventQueue(QueueCapacity);

        update_params();

        Reset();
    }

    private void Reset()
    {
        osc1.phase = 0u;
        osc2.phase = 0u;
        lfo.phase = 0u;

        aenv.reset();
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

        if (filterType == FilterType.Schmid)
        {
            filter1.SetResonance(resonance);
            filter2.SetResonance(resonance);
            filter1.SetCutoff(cutoffFrequency * env01); // 0 Hz cutoff is bad
            filter2.SetCutoff(cutoffFrequency * env01);
            filter1.SetOversampling(oversampling);
            filter2.SetOversampling(oversampling);
        }
#if LAZZARINI_FILTER
        else if (filterType == FilterType.Lazzarini)
        {
            filter1Laz.SetResonance(resonance);
            filter2Laz.SetResonance(resonance);
            filter1Laz.SetCutoff(cutoffFrequency * env01);
            filter2Laz.SetCutoff(cutoffFrequency * env01);
        }
#endif

        aenv.setAttackRate(aenv_attack * sample_rate);
        aenv.setDecayRate(aenv_decay * sample_rate);
        aenv.setReleaseRate(aenv_release * sample_rate);
        aenv.setSustainLevel(aenv_sustain);
    }

    private void render_float32_stereo_interleaved(float[] buffer, int sample_frames)
    {
        int smp = 0;
        int buf_idx = 0;
        //int time_smp = masterClock_smp;

        update_params();

        // Cache this for the entire buffer, we don't need to check for
        // every sample if new events have been enqueued.
        // This assumes that no other metdods call GetFrontAndDequeue.
        int queueSize = queue.GetSize();

        // Render loop
        for (; smp < sample_frames; ++smp)
        {
            // Event handling
            // This is sample accurate event handling.
            // If it's too slow, we can decide to only handle 1 event per buffer and
            // move this code outside the loop.
            while(true)
            {
                if (eventIsWaiting == false && queueSize > 0)
                {
                    //queueLock = true;
                    if (queue.GetFrontAndDequeue(ref nextEvent))
                    {
                        eventIsWaiting = true;
                        queueSize--;
                    }
                    //queueLock = false;
                }

                if (eventIsWaiting)
                {
                    if (nextEvent.time_smp <= time_smp)
                    {
                        HandleEventNow(nextEvent);
                        eventIsWaiting = false;
                    }
                    else
                    {
                        // we assume that queued events are in order, so if it's not
                        // now, we stop getting events from the queue
                        break;
                    }
                }
                else
                {
                    // no more events
                    break;
                }
            }

            // Rendering
            if (note_is_on)
            {
                // Render sample
                float amp = aenv.process() * 0.5f;

                float lfo_val = lfo.sin() * 0.48f * pwmStrength + 0.5f;

                //float saw = osc1.saw() * sawAmp;
                //float square = osc1.square(lfo_val) * squareAmp;
                //float sawDPW = osc1.sawDPW() * sawDPWAmp;
                float sine = osc2.sin() * subSine;
                float sawPolyBLEP = osc1.sawPolyBLEP() * sawAmp;
                float squarePolyBLEP = osc1.squarePolyBLEP(lfo_val) * squareAmp;

                float sample = (sine + sawPolyBLEP + squarePolyBLEP)
                    * /*(current_velocity * 0.0079f) **/ amp;

                buffer[buf_idx++] = sample;
                buffer[buf_idx++] = sample;

                // Update oscillators
                osc1.update();
                osc2.update();
                lfo.update();
                fenv.update_oneshot();
            }
            else
            {
                buffer[buf_idx++] = 0.0f;
                buffer[buf_idx++] = 0.0f;
            }
            time_smp++;
        }

        // Filter entire buffer
        if (filterEnabled >= 0.5f)
        {
            if (filterType == FilterType.Schmid)
            {
                filter1.process_mono_stride(buffer, sample_frames, 0, 2);
                filter2.process_mono_stride(buffer, sample_frames, 1, 2);
            }
#if LAZZARINI_FILTER
            else if (filterType == FilterType.Lazzarini)
            {
                filter1Laz.process_mono_stride(buffer, sample_frames, 0, 2);
                filter2Laz.process_mono_stride(buffer, sample_frames, 1, 2);
            }
#endif
        }
    }

    /// Internals
    private float midi2freq(int note, int octave)
    {
        return 32.70319566257483f * Mathf.Pow(2.0f, note / 12.0f + octave);
    }
};
