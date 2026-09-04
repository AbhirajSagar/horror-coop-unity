#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CaveGeneration.Editor
{
    [CustomEditor(typeof(CaveMeshGenerator3D))]
    public class CaveGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CaveMeshGenerator3D generator = (CaveMeshGenerator3D)target;

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate Height-Varied 3D Rooms", GUILayout.Height(30)))
            {
                generator.Generate3DNetwork();
                EditorUtility.SetDirty(generator);
            }

            if (GUILayout.Button("Randomize Seed & Generate", GUILayout.Height(30)))
            {
                generator.GenerateRandomSeed();
                EditorUtility.SetDirty(generator);
            }

            if (GUILayout.Button("Place Player in Random Room", GUILayout.Height(30)))
            {
                generator.PlacePlayerInRandomRoom();
            }
        }
    }
}
#endif
