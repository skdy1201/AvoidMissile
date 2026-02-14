using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

/// <summary>
/// 추후 추가될 여지가 있을 수 있음.
/// </summary>
public enum BoomParticle
{
    Normal,
    Grand,
}

// todo : Spawner의 OnPlayerDeath와 PlayerDeadonBoomSpawner
// 함수가 같은 용도지만, 다르게 쓰인다. Spawner의 함수를 잘 활용해야 한다.

/// <summary>
/// 파티클 오브젝트를 다루는 스포너
/// </summary>
public class BoomEffectSpawner : Spawner<BoomParticle>
{

    #region Serialized Fields

    /// <summary>
    /// 폭팔 이펙트 리스트
    /// </summary>
    [FormerlySerializedAs("BoomList")]
    [SerializeField] private List<GameObject> boomEffects = new List<GameObject>();

    #endregion


    #region Private/Protected Fields
    
    /// <summary>
    /// 현재 활성화 되어있는 파티클 오브젝트
    /// </summary>
    /// <remarks>
    /// 중간 삭제를 위한 링크드 리스트
    /// </remarks>>
    private LinkedList<GameObject> activeBoomEffects = new LinkedList<GameObject>();

    private int particleNumber = 0;

    #endregion

    #region Properties

    /// <summary>
    /// 열거형의 갯수로 스포너 리스트를 초기화 해야한다.
    /// </summary>
    public new static BoomEffectSpawner Instance
    {
        get { return Singleton<Spawner<BoomParticle>>.Instance as BoomEffectSpawner; }
    }

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 스포너에 파티클 오브젝트를 미리 만들어서 넣어둔다.
    /// 플레이어 사망 이벤트를 등록한다.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        int count = boomEffects.Count;

        for (int i = 0; i < count; ++i)
        {
            // 스포너마다 폭발 이펙트 넣어두기
            Queue<GameObject> currentSpawner = spawners[i];

            for (int j = 0; j < 100; ++j)
            {
                GameObject boomEffect = Instantiate(boomEffects[i]);
                boomEffect.transform.parent = this.gameObject.transform;

                boomEffect.name = "BoomEffect" + particleNumber.ToString();
                ++particleNumber;

                boomEffect.SetActive(false);
                currentSpawner.Enqueue(boomEffect);
            }
        }

        Player.OnPlayerDead.AddListener(() => PlayerDeadonBoomSpawner());

    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 파티클 오브젝트를 빌려온다
    /// </summary>
    /// <param name="type"> 폭발 파티클 타입 </param>
    /// <returns> 폭발 파티클 오브젝트</returns>
    public override GameObject RentSpawner(BoomParticle type)
    {
        GameObject gameObject = null;

        // 없으면 생성
        if (spawners[(int)type].Count <= 0)
        {
            gameObject = Instantiate(boomEffects[(int)type]);
            gameObject.transform.parent = this.gameObject.transform;
            gameObject.name = "BoomEffect" + particleNumber.ToString();
            ++particleNumber;
            return gameObject;
        }

        gameObject = spawners[(int)type].Dequeue();
        gameObject.GetComponent<ParticleController>().ResetParticleCheckBit();

        activeBoomEffects.AddLast(gameObject);

        return gameObject;
    }

    /// <summary>
    /// 스포너에 되돌려 놓기
    /// </summary>
    /// <param name="type"> 폭발 파티클 타입</param>
    /// <param name="gameObject"> 스포너에 집어넣을 폭팔 파티클 오브젝트</param>
    public override void ReturnSpawner(BoomParticle type, GameObject gameObject)
    {
        // 자식 오브젝트의 파티클 시스템 체크.
        ParticleSystem[] allParticles = gameObject.GetComponent<ParticleController>().Particles;
        
        // 있다면 전부 멈추기
        foreach (ParticleSystem particleSystem in allParticles)
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Clear(true);
            particleSystem.Simulate(0f, true, true);
        }
        
        gameObject.SetActive(false);
        spawners[(int)type].Enqueue(gameObject);

        gameObject.transform.parent = this.gameObject.transform;

        if (activeBoomEffects.Contains(gameObject))
            activeBoomEffects.Remove(gameObject);

    }

    /// <summary>
    /// 플레이어가 사망하면 모든 파티클을 멈추고, 
    /// 현재 활성화된 파티클을 다시 반납한다.
    /// </summary>
    public void PlayerDeadonBoomSpawner() // 코루틴 제거
    {
        var aliveBoom = activeBoomEffects.First;

        while (aliveBoom != null)
        {
            // 현재 파티클 멈추기
            GameObject currentObject = aliveBoom.Value;
            ParticleSystem particleSystem = currentObject.GetComponent<ParticleSystem>();
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            activeBoomEffects.Remove(aliveBoom);

            // 다시 풀에 되돌리기
            ReturnSpawner(BoomParticle.Normal, currentObject);
            aliveBoom = aliveBoom.Next;
        }

    }

    public void SelfEndProtocol()
    {
        EndProtocol();
        Debug.Log("Call EndProtocol");
    }

    #endregion

    #region Private/Protected Methods

    protected override void StartProtocol() { }

    /// <summary>
    /// 활성화된 폭발 이펙트를 다 멈추고, 
    /// </summary>
    protected override void EndProtocol()
    {
        // 모든 활성 파티클 즉시 정지
        foreach (GameObject boom in activeBoomEffects)
        {
            if (boom != null)
            {
                ParticleSystem[] allParticles = boom.GetComponent<ParticleController>().Particles;

                foreach (ParticleSystem ps in allParticles)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Clear(true);
                }

                boom.SetActive(false);
            }
        }

        // LinkedList 비우기
        activeBoomEffects.Clear();

        // 풀에 있는 것들도 정리
        foreach (var queue in spawners)
        {
            foreach (GameObject pooled in queue)
            {
                if (pooled != null)
                {
                    ParticleSystem[] allParticles = pooled.GetComponent<ParticleController>().Particles;
                    foreach (ParticleSystem ps in allParticles)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        ps.Clear(true);
                    }
                }
            }
        }
    }

    #endregion
}