using UnityEngine;
using System.Collections.Generic;
using UnityEngine.ParticleSystemJobs;
using UnityEngine.Serialization;


/// <summary>
/// 파티클 오브젝트를 조정하기 위한 컨트롤러
/// </summary>
/// <remarks>
/// 비트마스킹을 통해 여러 파티클 시스템의 종료 상태 추적
/// 모든 파티클이 종료되면 자동으로 오브젝트 풀에 반환
/// </remarks>
public class ParticleController : MonoBehaviour
{
    #region Serialized Fields

    /// <summary>
    /// 인스펙터에서 파티클 시스템을 지정
    /// </summary>
    [Header("Particle Objects")]
    [SerializeField] private List<ParticleSystem> particleObjects = new List<ParticleSystem>();

    [Header("ParticleType")]
    [SerializeField] private BoomParticle boomParticleType;

    #endregion

    #region Private/Protected Fields

    /// <summary>
    /// 외부에서 GetComponent 호출을 피하기 위한 캐싱 배열
    /// </summary>
    private ParticleSystem[] cachedParticles;

    /// <summary>
    /// 모든 파티클의 종료 시간을 체크하는 변수
    /// </summary>
    /// <remarks> 
    /// 비트마스킹을 기록하는 변수
    /// </remarks>
    private int particleStatusBits = 0;

    /// <summary>
    /// 파티클 종료 체크를 위한 타겟 넘버
    /// </summary>
    /// <remarks>
    /// 비트 마스킹을 통한 체크용 변수
    /// </remarks>
    private int targetNumber = 0;

    #endregion

    #region Property

    /// <summary>
    /// 캐싱된 파티클 배열 반환 (GC Allocation 방지)
    /// </summary>
    public ParticleSystem[] Particles => cachedParticles;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 파티클 캐싱 및 타겟 넘버 세팅
    /// </summary>
    private void Awake()
    {
        // List를 Array로 변환하여 캐싱 (외부 접근 시 GC 방지)
        cachedParticles = particleObjects.ToArray();

        // 비트마스크 타겟 계산 (예: 3개면 0b111 = 7)
        for (int i = 0; i < particleObjects.Count; ++i)
            targetNumber += 1 << i;
    }

    /// <summary>
    /// 지정된 비트의 파티클이 종료된다면, 마킹
    /// </summary>
    /// <remarks>
    /// 타겟 넘버 마킹이 다 끝나면, BoomEffectSpawner로 다시 돌려놓는다.
    /// </remarks>
    private void Update()
    {

        // 각 파티클의 재생 완료 상태를 비트마스크로 추적
        // 모든 파티클이 종료되었는지 효율적으로 확인하기 위함
        for (int i = 0; i < particleObjects.Count; ++i)
        {
            if (particleObjects[i].isPlaying == false && (particleStatusBits & (1 << i)) == 0)
                particleStatusBits |= 1 << i;
        }

        // 모든 파티클 재생이 완료되면(비트마스크가 목표값과 일치) 오브젝트 풀로 반환
        if (particleStatusBits == targetNumber)
            BoomEffectSpawner.Instance.ReturnSpawner(boomParticleType, this.gameObject);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 파티클 상태 비트를 초기화하여 재사용 준비 상태로 전환
    /// </summary>
    /// <remarks>
    /// 오브젝트 풀에서 파티클을 재사용할 때 호출
    /// 이전 재생 상태를 초기화하여 새로운 파티클 재생을 준비
    /// </remarks>
    public void ResetParticleCheckBit() => particleStatusBits = 0;

    #endregion

}
