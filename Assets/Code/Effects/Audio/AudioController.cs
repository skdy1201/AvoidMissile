using GoogleMobileAds.Api;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public enum SoundType
{
    TitleBgm,
    PlayBgm,
    BombSound,
}

public enum PlayerEffectSFX
{
    Slide,
    Jump,
    Respawn,
}

public enum ItemEffectSFX
{
    Buff,
    Nerf,
    Reverse,
    Lock,
    Revive,
}

// TODO : 사운드 감소 연결 안해둠.

/// <summary>
/// 게임의 사운드를 관리해주는 컨트롤러
/// </summary>
/// <remarks>
/// BGM, Player Sound 등 여러 소리들을 담당
/// </remarks>
public class AudioController : Singleton<AudioController>
{
    #region Serialized Fields

    [Header("Audio Object")]
    [SerializeField] private GameObject effectAudio;


    [Header("BGM")]
    [SerializeField] private GameObject[] bgmSounds;
    
    [FormerlySerializedAs("curBGM")]
    [SerializeField] private SoundType currentBGM;

    [Header("Effect Sound")]
    [SerializeField] AudioClip[] boomEffect;
    [SerializeField] AudioClip[] playerEffect;
    [SerializeField] AudioClip[] itemEffect;

    #endregion

    #region Private/Protected Fields

    /// <summary>
    /// 사운드 목록을 담고 있는 실제 오브젝트 리스트
    /// </summary>
    private List<GameObject> gameSounds = new List<GameObject>();

    /// <summary>
    /// 이펙트 사운드를 재생할 인스턴스
    /// </summary>
    private GameObject effectAudioInstance;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 오디오 컨트롤러 초기 세팅
    /// </summary>
    /// <remarks>
    /// 씬 변경 이벤트들 등록
    /// 실제 오디오를 재생시킬 인스턴스를 캐싱
    /// </remarks>
    protected override void Awake()
    {
        base.Awake();

        // 이벤트 등록
        GameProgress.StartScene.AddListener(() => StartProtocol());
        GameProgress.EndScene.AddListener(() => EndProtocol());

        // 프리팹을 인스턴스화 해서 저장
        for (int i = 0; i < bgmSounds.Length; ++i)
        {
            GameObject gameObject = Instantiate(bgmSounds[i]);
            gameObject.SetActive(false);
            gameObject.transform.parent = this.gameObject.transform;
            gameSounds.Add(gameObject);
        }

        // BGM 재생
        PlayBGM(SoundType.TitleBgm);

        // 이펙트 오브젝트 프리팹 인스턴스
        effectAudioInstance = Instantiate(effectAudio);
        effectAudioInstance.transform.parent = this.gameObject.transform;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 재생하고 있던, BGM과 재생하려는 BGM을 교체
    /// </summary>
    /// <param name="type"> 재생하려는 BGM 타입 </param>
    /// <param name="loop"> 반복 재생 여부 </param>
    public void PlayBGM(SoundType type, bool loop = true)
    {
        // 재생하려는 타입의 BGM이 꺼져 있다면, 루프여부를 확인하고 재생
        if (gameSounds[(int)type].activeSelf == false)
        {
            gameSounds[(int)type].SetActive(true);
            gameSounds[(int)type].GetComponent<AudioSource>().volume = GameData.Instance.GetSettingValue(OptionType.Bgm);

            gameSounds[(int)type].GetComponent<AudioSource>().Play();

            gameSounds[(int)type].GetComponent<AudioSource>().loop = loop;
        }

        // 현재 재생하려는 타입이 아닌 BGM이 켜져 있다면, 정지
        if (type == SoundType.TitleBgm && gameSounds[(int)SoundType.PlayBgm].activeSelf)
        {
            gameSounds[(int)SoundType.PlayBgm].GetComponent<AudioSource>().Stop();
            gameSounds[(int)SoundType.PlayBgm].SetActive(false);
        }
        else if (type == SoundType.PlayBgm && gameSounds[(int)SoundType.TitleBgm].activeSelf)
        {
            gameSounds[(int)SoundType.TitleBgm].GetComponent<AudioSource>().Stop();
            gameSounds[(int)SoundType.TitleBgm].SetActive(false);
        }

    }

    /// <summary>
    /// 일반 폭발 이펙트 사운드 랜덤 재생 (index 0, 1만 사용)
    /// </summary>
    public void PlayNormalBoomSound()
    {
        int randomIndex = Random.Range(0, 2);

        effectAudioInstance.GetComponent<AudioSource>().PlayOneShot(boomEffect[randomIndex], GameData.Instance.GetSettingValue(OptionType.EffectSound));

    }

    /// <summary>
    /// 지정 인덱스의 폭발 이펙트 사운드 재생
    /// </summary>
    /// <param name="index"> boomEffect 배열 인덱스 </param>
    public void PlayBoomSound(int index)
    {
        effectAudioInstance.GetComponent<AudioSource>().PlayOneShot(boomEffect[index], GameData.Instance.GetSettingValue(OptionType.EffectSound));
    }

    /// <summary>
    /// 아이템 이펙트 사운드 재생
    /// </summary>
    /// <param name="effectSoundidx"></param>
    public void PlayItemSound(int effectSoundidx)
    {
        effectAudioInstance.GetComponent<AudioSource>().PlayOneShot(itemEffect[effectSoundidx], GameData.Instance.GetSettingValue(OptionType.EffectSound));
    }

    /// <summary>
    /// BGM 볼륨 설정
    /// </summary>
    /// <param name="value"> 볼륨 값 </param>
    public void SetBGMvolume(float value)
    {
        gameSounds[(int)currentBGM].GetComponent<AudioSource>().volume = value;
    }

    /// <summary>
    /// 플레이어의 이펙트 사운드 재생
    /// </summary>
    /// <param name="effectNumber"> 플레이어 이펙트 열거형 </param>
    public void PlayPlayerEffect(int effectNumber)
    {
        effectAudioInstance.GetComponent<AudioSource>().PlayOneShot(playerEffect[effectNumber], GameData.Instance.GetSettingValue(OptionType.EffectSound));
    }

    public void PauseBGM()
    {
        gameSounds[(int)currentBGM].GetComponent<AudioSource>().Pause();
    }

    public void PlayCurBGM()
    {
        gameSounds[(int)currentBGM].GetComponent<AudioSource>().Play();
    }

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 씬에 따라서, BGM을 재생한다.
    /// </summary>
    protected override void StartProtocol()
    {
        if (SceneManager.GetActiveScene().name == GlobalData.Instance.TitleScene)
        {
            PlayBGM(SoundType.TitleBgm, true);
            currentBGM = SoundType.TitleBgm;
        }
        else
        {
            PlayBGM(SoundType.PlayBgm, true);
            currentBGM = SoundType.PlayBgm;
        }

    }

    /// <summary>
    /// 레벨 종료 시 호출되는 정리 작업
    /// </summary>
    /// <remarks>
    /// 현재는 별도 정리 작업이 없음
    /// </remarks>
    protected override void EndProtocol()
    {
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// 주어진 시간 내에 점진적으로 음량 감소
    /// </summary>
    /// <param name="time"> 시간 </param>
    /// <param name="targetSound"> 줄이려는 사운드 열거형 </param>
    /// <returns></returns>
    private IEnumerator FadeOutSound(float time, SoundType targetSound)
    {
        float currentTime = time;

        while (currentTime > 0)
        {
            currentTime -= Time.unscaledTime;

            float ratio = Mathf.Floor((currentTime / Time.unscaledTime) * 100) / 100;
            gameSounds[(int)targetSound].GetComponent<AudioSource>().volume = ratio;
            yield return null;
        }
    }

    #endregion
}
