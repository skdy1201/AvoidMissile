using UnityEngine;

public class SimpleBTTest : MonoBehaviour
{
    [SerializeField] private BehaviorTree behaviorTree;

    void Update()
    {
        if (behaviorTree == null)
        {
            Debug.LogError("[SimpleBTTest] behaviorTree is NULL!");
            return;
        }

        if (behaviorTree.RootNode == null)
        {
            Debug.LogError("[SimpleBTTest] RootNode is NULL!");
            return;
        }

        behaviorTree.RootNode.Execute(gameObject);
    }
}
