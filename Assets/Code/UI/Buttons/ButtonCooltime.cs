using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using System.Collections;
using UnityEngine.Serialization;


/// <summary>
/// 버튼의 쿨타임을 관리하는 컴포넌트
/// </summary>
public class ButtonCooltime : MonoBehaviour
{
    #region Serialized Fields

    [FormerlySerializedAs("targetButton")]
    [SerializeField] private Button button;

    [SerializeField] private float cooldown = 3f;

    [FormerlySerializedAs("cooldownGray")]
    [SerializeField] private Image grayImage;

    #endregion

    #region Private/Protected Fields
    #endregion

    #region Properties

    public Button GetButtonObject => button;

    public float GetCooldown => cooldown;

    public Image GetCooldownImage => grayImage;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 버튼 스크립트를 캐싱하고, 쿨타임 이미지를 비활성화 시켜둔 뒤, 쿨타임을 설정해둔다.
    /// </summary>
    private void Awake()
    {

        if (gameObject.transform.parent != null)
            button = gameObject.transform.parent.GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError("Not Connect Button");
            Debug.Break();
        }

        grayImage = GetComponent<Image>();

        if(grayImage == null)
        {
            Debug.LogError("CooldownImage component not found");
            Debug.Break();
        }    

        grayImage.enabled = false;

        ButtonFunction.Instance.SetButtonCooltime(this);
    }

    #endregion

}
