// Example sequencer code copyright Jakob Schmid 2018.
// This code is free to use and modify by anyone.

using UnityEngine;

public class Sequencer : MonoBehaviour
{
    /// Config
    public MoogSynth synth;

    [Header("Note: modifying sequence length while running can lead to errors")]
    public int[] sequence = null;
    public int speed = 10;
    [Range(0, 256)]
    public int pitch = 32;

    // LFOs
    [Header("LFO 1")]
    [Range(0, 6)]
    public int lfo1Param;
    [Range(0, 10000)]
    public float lfo1Strength;
    [Range(0, 10)]
    public float lfo1Freq = 1;

    [Header("LFO 2")]
    [Range(0, 6)]
    public int lfo2Param;
    [Range(0, 10000)]
    public float lfo2Strength;
    [Range(0, 10)]
    public float lfo2Freq = 1;

    /// State
    private float lfo1BaseValue;
    private float lfo2BaseValue;
    private int sequenceIdx = 0;
    private int audioFrameCount = 0;

    /// Components
    private Phaser lfo1;
    private Phaser lfo2;


    private void Awake()
    {
        lfo1 = new Phaser();
        lfo2 = new Phaser();
        lfo1BaseValue = synth.get_parameter(lfo1Param);
        lfo2BaseValue = synth.get_parameter(lfo2Param);
    }

    // No audio is rendered in this method.
    // This code is only here to get updates synchronized in audio frame time.
    private void OnAudioFilterRead(float[] data, int channels)
    {
        bool playNote = (audioFrameCount % speed) == 0;
        if (playNote)
        {
            int note = sequence[sequenceIdx++];
            sequenceIdx %= sequence.Length;
            if (note != -1)
            {
                note += pitch;

                synth.queue_event(MoogSynth.Event_type.Note_on, note);
            }
        }

        lfo1.set_freq(lfo1Freq * 1024);
        lfo2.set_freq(lfo2Freq * 1024);
        lfo1.update();
        lfo2.update();
        synth.set_parameter(lfo1Param, lfo1BaseValue + lfo1.sin() * lfo1Strength);
        synth.set_parameter(lfo2Param, lfo2BaseValue + lfo2.sin() * lfo2Strength);

        audioFrameCount++;
    }
}
