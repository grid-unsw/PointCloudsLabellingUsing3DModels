using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Manager))]
public class ManagerEditor : Editor
{
    private Manager _manager;

    private void OnEnable()
    {
        _manager = (Manager) target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        /*
        if (GUILayout.Button("Classify points"))
        {
           
        }
        */
    }
}
