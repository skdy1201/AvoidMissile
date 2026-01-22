using UnityEngine;

[CreateAssetMenu(fileName = "BehaviorTree", menuName = "BehaviorTree/BehaviorTree")]

public class BehaviorTree : ScriptableObject
{
    [SerializeField] private BehaviorNode rootNode;

    public BehaviorNode RootNode
    {
        get { return rootNode; } 
    }
}
