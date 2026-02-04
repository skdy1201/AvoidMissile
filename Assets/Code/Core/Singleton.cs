using System.Collections;
using UnityEngine;

/// <summary>
/// 싱글톤 패턴을 구현하는 추상 베이스 클래스
/// </summary>
/// <typeparam name="T"> MonoBehavior를 상속받는 싱글톤 타입 </typeparam>
///<remarks>
/// DontDestroyOnLoad를 통해 씬 전환 시에도 인스턴스 유지.
/// 중복 생성시 자동 제거
///</remarks>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{

    #region Abstract Methods

    /// <summary>
    /// 씬 시작 시 호출되는 초기화 작업
    /// </summary>
    abstract protected void StartProtocol();

    /// <summary>
    /// 씬 종료 시 호출되는 정리 작업
    /// </summary>
    abstract protected void EndProtocol();

    #endregion

    #region Private/Protected Fields

    private static T instance;

    #endregion


    #region Properties

    /// <summary>
    /// 싱글톤 인스턴스에 접근. 없으면 씬에서 찾고, 그래도 없으면 자동 생성
    /// </summary>
    /// <returns> 싱글톤 인스턴스 </returns>
    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                // 씬에 이미 존재하는 인스턴스 먼저 찾기
                instance = FindAnyObjectByType<T>();

                // 씬에도 없으면 새로 생성
                if (instance == null)
                {
                    GameObject gameObject = new GameObject(typeof(T).Name);
                    instance = gameObject.AddComponent<T>();
                }
            }

            return instance;
        }
    }

    #endregion


    #region Unity Lifecycle

    /// <summary>
    /// 싱글톤 인스턴스 초기화 및 중복 생성 방지
    /// </summary>
    protected virtual void Awake()
    {
        if (instance == null)
        {
            instance = this.GetComponent<T>();
            DontDestroyOnLoad(this.gameObject);
        }
        else if (instance == this)
        {
            // Instance getter에서 FindObjectOfType으로 먼저 찾은 경우
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            // 이미 다른 인스턴스가 존재하면, 중복 오브젝트 제거
            Destroy(this.gameObject);
        }
    }

    #endregion


}
