using UnityEngine;


public class BehaviorTree : MonoBehaviour
{
    private BehaviorNode rootNode = new BehaviorNode();

    public void TestFunc()
    {
        BehaviorNode node = new BehaviorNode();

        rootNode.AddNode(node);
    }
    
}
