using UnityEngine;

public class BehaviorTreeRunner : MonoBehaviour
{
    [SerializeField] public BehaviorTree targetTree;
    
    public void RunBT()
    {
        Debug.Log("RUN BT");
        targetTree.RootNode.Execute(gameObject);
    }
}
