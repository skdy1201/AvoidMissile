using UnityEngine;

/// <summary>
/// BehaviorTree 에셋. 루트 노드를 가진 트리 구조
/// </summary>
[CreateAssetMenu(fileName = "BehaviorTree", menuName = "BehaviorTree/BehaviorTree")]
public class BehaviorTree : ScriptableObject
{
    #region Serialized Fields
    [SerializeField] private BehaviorNode rootNode;
    #endregion

    #region Properties
    /// <summary>
    /// 트리의 루트 노드
    /// </summary>
    public BehaviorNode RootNode => rootNode;
    #endregion
}
