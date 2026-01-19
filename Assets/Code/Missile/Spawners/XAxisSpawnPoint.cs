using Unity.VisualScripting;
using UnityEngine;

public class XAxisSpawnPoint : MonoBehaviour
{

    #region Unity Lifecycle

    /// <summary>
    /// 씬이 시작하면 자신의 위치를 미사일 스포너에 등록한다.
    /// </summary>
    void Start()
    {
        MissileSpawner.Instance.AddXSpawnPoint(this.gameObject);
    }

    #endregion

}
