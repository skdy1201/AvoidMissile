using UnityEngine;

/// <summary>
/// BehaviorTree를 실행하는 컴포넌트
/// </summary>
public class BehaviorTreeRunner : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private BehaviorTree targetTree;
    #endregion

    #region Properties
    /// <summary>
    /// 실행할 BehaviorTree
    /// </summary>
    public BehaviorTree TargetTree => targetTree;
    #endregion

    #region Public Methods
    /// <summary>
    /// BehaviorTree 실행
    /// </summary>
    public void RunBT()
    {
        if (targetTree == null || targetTree.RootNode == null)
            return;

        targetTree.RootNode.ResetExecutionFlag();
        targetTree.RootNode.Execute(gameObject);
    }
    #endregion
}
