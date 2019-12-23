using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TilemapGenerator))]
public class TilemapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate Tilemaps"))
        {
            TilemapGenerator tilemapGenerator = (TilemapGenerator)target;
            tilemapGenerator.Generate();
        }
    }
}
