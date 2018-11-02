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

public class Sequencer : MonoBehaviour
{
    /// Static config
    private const Int64 queueBufferTime = 4096; // queue this amount of samples ahead

    public MoogSynth synth;
    [Range(1, 2000)]
    public float tempo = 60;
    [Range(1, 32)]
    public int tempoSubdivision = 1;
    public int[] pitch;
    [Range(0,120)]
    public int transpose = 48;
    [Range(0,120)]
    public int pitchRandomize = 0;
    private Int64 nextNoteTime = 0;

    private float tempoOld = 60;

    private int seqIdx = 0;

    private void Start()
    {
        if (synth == null)
            synth = GetComponent<MoogSynth>();
        nextNoteTime = synth.GetTime_smp() + queueBufferTime;
    }

    private void Update()
    {
        int sampleRate = AudioSettings.outputSampleRate;
        tempo = Mathf.Clamp(tempo, 1, 2000);
        // sample rate: Fs = x smp/s
        // tempo      : x beat/m = x/60 beat/s = 60/x s/beat = 60 * Fs / x smp/beat
        Int64 tempo_smpPerNote = (Int64)(60 * sampleRate / tempo / tempoSubdivision);

        if (tempo != tempoOld)
        {
            nextNoteTime = synth.GetTime_smp() + queueBufferTime;
            synth.ClearQueue();
            tempoOld = tempo;
        }

        Int64 time = synth.GetTime_smp();
        bool queueSuccess = false; 
        while(time + queueBufferTime >= nextNoteTime)
        {
            int seqLength = pitch.Length;
            if (seqIdx >= seqLength)
            {
                seqIdx = 0;
                Debug.Log("seqIdx out of range, resetting");
            }
            int notePitch = pitch[seqIdx] + transpose + UnityEngine.Random.Range(-pitchRandomize, pitchRandomize);
            seqIdx = (seqIdx + 1) % seqLength;

            Int64 noteOnTime = nextNoteTime;
            Int64 noteOffTime = nextNoteTime + (Int64)(tempo_smpPerNote * 0.75f);
            queueSuccess = synth.queue_event(EventQueue.EventType.Note_on, notePitch, noteOnTime);
            //queueSuccess &= synth.queue_event(EventQueue.EventType.Note_off, 0, noteOffTime);
            nextNoteTime += tempo_smpPerNote;
            if (queueSuccess == false)
            {
                Debug.LogError("Event enqueue failed", this);
                break;
            }
        }
    }
}
