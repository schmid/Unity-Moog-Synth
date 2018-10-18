# MoogSynth

This is an example of a real-time Unity synthesizer with a Moog filter simulation.

It is a monophonic pulse-width modulated square wave with a sub sine wave (one octave lower), with a resonant Moog-simulation filter based on the Huovilainen model. The filter frequency is controlled by a LFO-based envelope, and the amplitude is controlled by an ADSR envelope.

# Limitations

All note events are quantized to audio buffer boundaries. This means that your tempo will be rounded to a number of ticks, e.g.:

    int tempo_BPM;
    float beat_duration = 60.0f / tempo_BPM;
    float beat_16th_duration = beat_duration / 4; // 4 16th notes per beat
    float tick_duration = AudioSetttings.GetDSPBufferSize() / AudioSettings.outputSampleRate;
    int ticksPer16thNote = Mathf.RoundToInt(beat_16th_duration / tick_duration);

(Note: this code is currently untested).

# Usage

Note events **must** be sent from an `OnAudioFilterRead` method to avoid timing errors. `Sequencer.cs` is provided as an example, if you want to use this code, you should probably write your own sequencer based on that.
