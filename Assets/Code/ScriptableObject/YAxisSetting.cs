using UnityEngine;

[CreateAssetMenu(fileName = "YAxisSetting", menuName = "Scriptable Objects/YAxisSetting")]
public class YAxisSetting : ScriptableObject
{
    [SerializeField] public int LimitMissileCount = 50;
    [SerializeField] public int MinMissileCount = 1;
    [SerializeField] public int LimitMinMissileCount = 20;
    [SerializeField] public int CurMaxMissileCount = 0;

    [SerializeField] public float MaxMissileTimer = 2f;
    [SerializeField] public float MissileCycle = 3f;

    [SerializeField] public float baseMissileSpeed = 4f;


}
