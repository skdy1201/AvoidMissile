using UnityEngine;

[CreateAssetMenu(fileName = "XAxisSetting", menuName = "Scriptable Objects/XAxisSetting")]
public class XAxisSetting : ScriptableObject
{
    [SerializeField] public int MaxLimitHP;

    [SerializeField] public int MaxLimitMoveSpeed;

    [SerializeField] public float MaxLimitRotateSpeed;

    [SerializeField] public float MaxMoveTime;

    [SerializeField] public float MinRotateTime;

}
