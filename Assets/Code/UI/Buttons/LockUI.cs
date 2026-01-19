using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스킬 잠금을 표시해주는 UI애니메이션
/// </summary>
/// <remarks>
/// Canvas에서는 SpriteRender를 사용하지 못해 하나씩 직접 변경
/// </remarks>
public class LockUI : MonoBehaviour
{

    #region Serialized Fields

    /// <summary>
    /// 애니메이션을 재생할 스프라이트 
    /// </summary>
    [SerializeField] private List<Sprite> Sprites = new List<Sprite>();

    #endregion

    #region Private/Protected Fields

    // 현재 UI가 출력하는 이미지
    private Image image;

    private Button parentButton;

    // 에니메이션 관련 변수
    private bool animFinish = false;
    private int animIndex = 0;

    #endregion

    #region Properties
    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// private 변수 연결 및 잠금시킬 UI와 연결
    /// </summary>
    private void Awake()
    {
        image = GetComponent<Image>();

        parentButton = this.transform.parent.GetComponent<Button>();

        SkillButton parent = this.transform.parent.GetComponent<SkillButton>();

        if (parent != null)
        {
           parent.RegisterLock(this.gameObject);
        }

        this.gameObject.SetActive(false);

    }

    /// <summary>
    /// 스킬 사용이 금지 될동안, 상호작용 및 버튼 색 변경을 방지
    /// </summary>
    private void OnEnable()
    {
        parentButton.interactable = false;
        parentButton.transition = Selectable.Transition.None;
        StartCoroutine(LockAnimation());
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 연결한 버튼에서 잠금 애니메이션을 활성화 시키기 위한 public 함수
    /// </summary>
    public void OnLockUI()
    {
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 잠금 애니메이션이 끝나고, 설정을 되돌리기
    /// </summary>
    public void OffLockUI()
    {
        // 버튼 초기 설정으로 복구
        parentButton.interactable = true;
        parentButton.transition = Selectable.Transition.ColorTint;

        // 애니메이션 초기화
        animIndex = 0;
        image.sprite = Sprites[animIndex];
        animFinish = false;


        gameObject.SetActive(false);
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// 잠금 애니메이션 코루틴
    /// </summary>
    IEnumerator LockAnimation()
    {
        while (!animFinish)
        {
            yield return new WaitForSeconds(0.05f);
            animIndex++;

            if (animIndex <= 36)
                image.sprite = Sprites[animIndex];

            if (animIndex == 37)
                animFinish = true;
        }
    }

    #endregion

}
