using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 많은 수의 오브젝트를 사용하기 위한 오브젝트 풀 스포너 추상 클래스
/// </summary>
/// <typeparam name="TEnum"> 오브젝트 타입을 구분하는 열거형 </typeparam>
public abstract class Spawner<TEnum> : Singleton<Spawner<TEnum>>
    where TEnum : System.Enum
{
    #region Private/Protected Fields
    /// <summary>
    /// 오브젝트 타입별 스포너 큐
    /// </summary
    protected List<Queue<GameObject>> spawners = new List<Queue<GameObject>>();

    #endregion


    #region Unity Lifecycle

    /// <summary>
    /// 이벤트 등록 및 열거형과 스포너 내부 컨테이너 동기화
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        // 이벤트 등록
        GameProgress.StartScene.AddListener(StartProtocol);
        GameProgress.EndScene.AddListener(EndProtocol);

        // 열거형 개수만큼 큐 초기화
        int queueCount = System.Enum.GetValues(typeof(TEnum)).Length;

        for (int i = 0; i < queueCount; ++i)
        {
            Queue<GameObject> Spawner = new Queue<GameObject>();
            spawners.Add(Spawner);
        }
    }

    #endregion

    #region Abstract Methods

    /// <summary>
    /// 스포너에서 오브젝트 대여
    /// </summary>
    /// <param name="type"> 대여할 오브젝트 타입 </param>
    /// <returns> 대여할 타입의 게임 오브젝트 </returns>
    abstract public GameObject RentSpawner(TEnum type);

    /// <summary>
    /// 사용한 오브젝트를 풀에 반납
    /// </summary>
    /// <param name="type"> 반납할 오브젝트의 타입 </param>
    /// <param name="gameObject"> 반납할 오브젝트 </param>
    abstract public void ReturnSpawner(TEnum type, GameObject gameObject);

    #endregion

    #region Public Methods

    /// <summary>
    /// 플레이어가 죽을 때, 스포너 정리 작업
    /// </summary>
    public virtual void OnPlayerDeath()
    {
        // 진행 중인 모든 코루틴 중지
        StopAllCoroutines();

        // 필요 시 추가 작업을 하위 클래스에서 구현
    }

    #endregion

}
