using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GrassBladeTest))]
public class GrassBladeTestEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GrassBladeTest script = (GrassBladeTest)target;

        if (GUILayout.Button("Reset Points"))
        {
            script.ResetPoints();
        }
    }
}
