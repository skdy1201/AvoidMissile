using UnityEngine;
using UnityEditor;

public class BehaviorTreeTracker : EditorWindow
{

    [InitializeOnLoadMethod]
    static void Init()
    {
        Selection.selectionChanged += SelectedBT; // BT 선택 감지
        Debug.Log("editor init");
    }

    static void SelectedBT()
    {
        Object selectedObject = Selection.activeObject;

        if (selectedObject is BehaviorTree tree)
        {
            Debug.Log($"BehaviorTree 에셋 선택: {tree.name}");
            return;
        }

        GameObject selectedGameObjcet = Selection.activeGameObject;

        if(selectedGameObjcet != null)
        {
            var runner = selectedGameObjcet.GetComponent<BehaviorTreeRunner>();
            if (runner != null && runner.targetTree != null)
            {
                Debug.Log($"Runner의 트리 선택: {runner.targetTree.name}");
            }
        }
    }
}
