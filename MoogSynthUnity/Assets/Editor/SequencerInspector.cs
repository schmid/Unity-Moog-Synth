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

[CustomEditor(typeof(Sequencer)), CanEditMultipleObjects]
public class SequencerInspector : Editor
{
    /// Static config
    const int matrixMaxX = 32;
    const int matrixMaxY = 25;

    const int gridSize = 5;
    const int matrixScale = 3;
    const int texturePadding = 2;

    const int textureSizeX = matrixMaxX * gridSize;
    const int textureSizeY = matrixMaxY * gridSize;

    const int matrixGuiSizeX = textureSizeX * matrixScale;
    const int matrixGuiSizeY = textureSizeY * matrixScale;

    /// State
    private bool editMatrix = false;

    /// Cache
    [System.NonSerialized]
    static Texture2D matrixTexture = null;
    [System.NonSerialized]
    static GUIStyle boxStyle = null;


    public override void OnInspectorGUI()
    {
        Sequencer parent = target as Sequencer;

        if (parent.pitch == null || parent.pitch.Length != Sequencer.maxLength)
        {
            System.Array.Resize(ref parent.pitch, Sequencer.maxLength);
        }

        EnsureInit();
        DrawDefaultInspector();
        editMatrix = EditorGUILayout.Toggle("Edit matrix", editMatrix);

        var rect = GUILayoutUtility.GetRect (matrixGuiSizeX, matrixGuiSizeY, GUILayout.ExpandWidth(false));

        Vector2 mousePosRelative = Event.current.mousePosition - rect.min;
        float mouseX = mousePosRelative.x;
        float mouseY = matrixGuiSizeY - mousePosRelative.y;
        int length = parent.length;

        if (Event.current.type == EventType.Repaint)
        {
            UpdateMatrix(parent, new Vector2(mouseX, mouseY));
        }
        if(editMatrix && (Event.current.type == EventType.MouseDown))
        {
            int mouseCellX = (int)mouseX / gridSize / matrixScale;
            int mouseCellY = (int)mouseY / gridSize / matrixScale;
            if (mouseCellX >= 0 && mouseCellX < length)
            {
                int pitchCurrent = parent.pitch[mouseCellX];

                Undo.RecordObject(parent, "modified note");
                if (pitchCurrent != mouseCellY)
                {
                    parent.pitch[mouseCellX] = mouseCellY;
                }
                else
                {
                    parent.pitch[mouseCellX] = Sequencer.restPitch;
                }
            }
        }
        boxStyle.normal.background = matrixTexture;
        GUI.Box(rect, GUIContent.none, boxStyle);

        if (GUILayout.Button("Reset cache"))
        {
            ResetCache();
        }
    }

    private void UpdateMatrix(Sequencer parent, Vector2 cursorPos)
    {
        int columns = parent.length;
        int seqIdx = -1;
        if (Application.isPlaying)
        {
            seqIdx = parent.getCurrentSeqIdx();
        }

        // mouse cell coords in matrix
        int mouseCellX = (int)cursorPos.x / gridSize / matrixScale;
        int mouseCellY = (int)cursorPos.y / gridSize / matrixScale;

        Color color;
        for (int y = 0; y < 25 * gridSize; ++y)
        {
            for (int x = 0; x < matrixMaxX * gridSize; ++x)
            {
                int mx = x / gridSize; // cell coords in matrix
                int my = y / gridSize;
                int cx = x % gridSize; // pixel coords in cell
                int cy = y % gridSize;

                bool isMouseInCell = (mouseCellX == mx && mouseCellY == my);

                int sn = my % 12; // scale note
                bool isBlackKey = (sn == 1 || sn == 3 || sn == 6 || sn == 8 || sn == 10);
                bool isInsideMatrix = mx < columns;
                bool isNote = isInsideMatrix && (parent.pitch[mx] == my);
                bool isCurrentSeqIdx = mx == seqIdx;

                Color shade = Color.HSVToRGB(0.0f, 0.0f, ((float)(y % gridSize)) / gridSize * 0.2f);

                if (isBlackKey)
                {
                    color = new Color(0.4f, 0.4f, 0.4f);
                }
                else
                {
                    color = new Color(0.8f, 0.8f, 0.8f);
                }

                if (cx == 0 || cy == 0)
                {
                    color = new Color(0.2f, 0.2f, 0.2f);
                }
                else
                {
                    color += shade;
                }


                if (isInsideMatrix)
                {
                    if (isNote && cx != 0 && cy != 0)
                    {
                        if (editMatrix)
                        {
                            color = Color.Lerp(color, Color.red, 0.7f);
                        }
                        else
                        {
                            color = Color.Lerp(color, new Color(0.7f, 0.2f, 1.0f), 0.7f);
                        }
                    }
                    if (isMouseInCell && editMatrix)
                    {
                        color = Color.Lerp(color, Color.blue, 0.2f);
                    }
                    if (isCurrentSeqIdx)
                    {
                        color = Color.Lerp(color, Color.green, 0.2f);
                    }
                }
                else
                {
                    color = Color.Lerp(color, Color.black, 0.8f);
                }

                matrixTexture.SetPixel(x + texturePadding, y + texturePadding, color);
            }
        }

        matrixTexture.Apply();
    }

    private void EnsureInit()
    {
        if (matrixTexture == null)
        {
            matrixTexture = new Texture2D(textureSizeX + texturePadding * 2, textureSizeY + texturePadding * 2);
            matrixTexture.filterMode = FilterMode.Point;
        }
        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.padding = new RectOffset(0, 0, 0, 0);
        }
    }

    private void ResetCache()
    {
        matrixTexture = null;
        boxStyle = null;
    }

    public override bool RequiresConstantRepaint()
    {
        return false;
    }
}
