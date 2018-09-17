# MoogSynth

This is an example of a real-time Unity synthesizer with a Moog filter simulation.

It is a monophonic pulse-width modulated square wave with a sub sine wave (one octave lower), with a resonant Moog-simulation filter based on the CSound implementation of the Huovilainen model. The filter frequency is controlled by a LFO-based envelope, and the amplitude is controlled by an ADSR envelope.

# Usage

Note events **must** be sent from an `OnAudioFilterRead` method to avoid timing errors. `Sequencer.cs` is provided as an example, if you want to use this code, you should probably write your own sequencer based on that.
