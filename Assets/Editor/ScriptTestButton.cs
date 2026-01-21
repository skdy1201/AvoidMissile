using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BehaviorTree))]
public class ScriptTestButton : Editor
{
    public override void OnInspectorGUI()
    {
       base.OnInspectorGUI();

        if (GUILayout.Button("테스트"))
        {
            BehaviorTree tree = (BehaviorTree)target;
            tree.TestFunc();  // "버튼 클릭됨!" 출력

        }
    }
}
