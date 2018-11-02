# MoogSynth

This is an example of a real-time Unity synthesizer with a Moog filter simulation.

It has a monophonic pulse-width modulated square wave and a saw wave (using polyBLEP for band-limiting), with a sub sine wave (one octave lower), and a resonant Moog-simulation filter based on the Huovilainen model. The filter frequency is controlled by a simple ramp envelope, and the amplitude is controlled by an ADSR envelope.

The synthesizer is controlled using a thread safe event queue, and note events can be sent from normal Unity MonoBehaviours.
Note events can be note on and off, have a pitch, and a time stamp, which is measured in samples.

`Sequencer.cs` is a step sequencer component that demonstrates how to implement sequencing and how to convert BPM to sample ticks.

# Sample-accurate Sequencing

Here is an example of how to compute time stamps corresponding to a given tempo (in BPM):

    MoogSynth synth;
    const Int64 bufferTime = 2048; // experiment to find the lowest value that works on everything (in samples)
    float tempo_bpm = 120;
    Int64 tempo_smpPerNote;
    Int64 nextNoteTime; // (in samples)

    void Start()
    {
        tempo_smpPerNote = (Int64)(60 * AudioSettings.outputSampleRate / tempo_bpm);

        // First note is played at a reasonable point in the near future
        nextNoteTime = synth.GetTime_smp() + buffertime; // as soon as possible
    }

    void Update()
    {
        Int64 time = synth.GetTime_smp();

        // Time to queue a new note?
        while(time + bufferTime >= nextNoteTime)
        {
            int pitch = UnityEngine.Random.Range(30,90);
            synth.queue_event(EventQueue.EventType.Note_on, pitch, nextNoteTime);

            // From here on, all notes are sample accurate in relation to the first note
            noteOnTime += tempo_smpPerNote;
        }
    }


