using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HexGridGenerator))]
public class HexGridGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("generate HexGrid"))
        {
            HexGridGenerator generator = (HexGridGenerator)target;
            generator.GenerateGrid();
        }
    }
}

