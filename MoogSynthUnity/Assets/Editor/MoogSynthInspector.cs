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

using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MoogSynth)), CanEditMultipleObjects]
public class MoogSynthInspector : Editor
{
    private enum OscilloscopeMode
    {
        None = 1,
        Small = 2,
        Large = 3
    }

    Texture2D tex = null;
    int t = 0;
    const int bufSize = 1024;
    private float[] testBuf = null;
    float[] bufCopy = null;
    string[] sourceNames = null;
    string[] targetNames = null;
    //float[] testMatrix = new float[8 * 8];

    /// Static Cache
    private static OscilloscopeMode oscilloscopeMode = OscilloscopeMode.None;


    public override void OnInspectorGUI()
    {
        MoogSynth parent = target as MoogSynth;

        //serializedObject.Update();

        DrawDefaultInspector();

        GUILayout.Space(8);
        GUILayout.Label("Visualization", EditorStyles.boldLabel);
        int modeInt = EditorPrefs.GetInt("MoogSynth:oscMode");
        oscilloscopeMode = (OscilloscopeMode) EditorGUILayout.EnumPopup("Oscilloscope", (OscilloscopeMode)modeInt);
        if (((int)oscilloscopeMode) != modeInt)
        {
            EditorPrefs.SetInt("MoogSynth:oscMode", (int)oscilloscopeMode);
        }
        parent.SetDebugBufferEnabled(oscilloscopeMode != OscilloscopeMode.None);

        if (oscilloscopeMode != OscilloscopeMode.None)
        {
            if (Event.current.type == EventType.Repaint)
            {
                int oscHeight = 256;
                int oscWidth = 512;
                if (oscilloscopeMode == OscilloscopeMode.Small)
                {
                    oscHeight = 64;
                }

                float[] buf = null;
                if (Application.isPlaying)
                {
                    if (bufCopy == null)
                    {
                        bufCopy = new float[bufSize];
                    }
                    lock (parent.GetBufferMutex())
                    {
                        System.Array.Copy(parent.GetLastBuffer(), bufCopy, bufSize);
                    }
                    buf = bufCopy;
                }
                else
                {
                    if (testBuf == null || testBuf.Length < bufSize)
                    {
                        testBuf = new float[bufSize];
                        for (int x = 0; x < bufSize; ++x)
                        {
                            testBuf[x] = 0.0f; // Mathf.Sin(((float)x) / oscWidth * Mathf.PI * 2.0f);
                        }
                    }
                    buf = testBuf;
                }

                RenderBuffer(buf, ref tex, oscWidth, oscHeight, 1);
            }

            GUILayout.Box(tex);
        }

        if (targetNames == null)
        {
            targetNames = System.Enum.GetNames(typeof(MoogSynth.Parameters));
            sourceNames = System.Enum.GetNames(typeof(MoogSynth.Modulators));
        }
        int matrixsize = targetNames.Length * sourceNames.Length;
        if (parent.modulationMatrix == null)
        {
            parent.modulationMatrix = new float[matrixsize];
        }
        else if (parent.modulationMatrix.Length < matrixsize)
        {
            System.Array.Resize(ref parent.modulationMatrix, matrixsize);
        }
        //ModulationMatrix(parent.modulationMatrix, sourceNames, targetNames);

        if (GUILayout.Button("Reset Cache"))
        {
            tex = null;
        }
    }

    private void RenderBuffer(float[] buf, ref Texture2D tex, int width, int height, int stride)
    {
        if (tex == null || tex.width != width || tex.height != height)
        {
            tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
        }

        // Check zero crossing
        float valueOld = 0.0f;
        int offset = 0;
        for (int i = 0; i < bufSize; ++i)
        {
            float valueNew = buf[i*stride];
            if (valueOld < 0 && valueNew > 0)
            {
                offset = i;
                break;
            }
            valueOld = valueNew;
        }

        Color col = Color.green;
        float yScale = 1.0f / height;
        float lineFocus = height * 0.3f;
        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                float yNorm = y * yScale * 2.0f - 1.0f; // [-1;+1]
                float oscValue = -1.0f;
                if ((x + offset) < bufSize)
                {
                    oscValue = buf[(x+offset)*stride]; // stereo interleaved
                }
                float intensity = Mathf.Pow(1.0f - Mathf.Abs(oscValue - yNorm), lineFocus);
                col = new Color(intensity, intensity, intensity);
                tex.SetPixel(x, y, col);
            }
            t++;
        }
        tex.Apply(false);
    }

    // Sources are vertical, targets are horizontal
    private void ModulationMatrix(float[] matrix, string[] sources, string[] targets)
    {
        const int guiWidth = 34;
        int width = targets.Length;
        int height = sources.Length;
        Debug.Assert(matrix.Length >= width * height);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("src/tgt", GUILayout.Width(guiWidth));
        for (int x = 0; x < width; ++x)
        {
            GUILayout.Label(targets[x], GUILayout.Width(guiWidth));
        }
        EditorGUILayout.EndHorizontal();
        for (int y = 0; y < height; ++y)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(sources[y], GUILayout.Width(guiWidth));
            for (int x = 0; x < width; ++x)
            {
                matrix[y*width+x] = EditorGUILayout.FloatField(matrix[y*width+x], GUILayout.Width(guiWidth));
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    public override bool RequiresConstantRepaint()
    {
        return oscilloscopeMode != OscilloscopeMode.None;
    }
}
