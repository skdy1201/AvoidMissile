using UnityEngine;

/// <summary>
/// 모든 아이템의 기본 동작을 정의하는 추상 클래스
/// </summary>
/// <remarks>
/// 상속받는 아이템은 EffectItem()을 반드시 구현해야 하며,
/// SyncData()를 통해 외부 데이터와 동기화
/// </remarks>
public abstract class Item : MonoBehaviour
{

    #region Serialized Fields

    [SerializeField] protected ItemType category;

    [SerializeField] protected float value;

    [SerializeField] protected float time;

    #endregion

    #region Public Methods

    /// <summary>
    /// 아이템이 가진 효과를 동작시키는 가상함수
    /// 각각의 아이템들이 직접 구현
    /// </summary>
    abstract public void EffectItem();

    /// <summary>
    /// csv 파일로 바꾼 데이터와 아이템 정보를 매칭
    /// </summary>
    /// <param name="itemName"> 오브젝트 이름 </param>
    /// <param name="itemType"> 아이템 타입 </param>
    /// <param name="itemValue"> 아이템 스탯 </param>
    /// <param name="itemTime"> 아이템 동작 시간 </param>
    public void SyncData(string itemName, string itemType, string itemValue, string itemTime)
    {
        this.gameObject.name = itemName;

        if (System.Enum.TryParse(itemType, out ItemType parsedType))
            category = parsedType;
        else
        {
            Debug.Log("Wrong Item Type");
        }

        value = float.Parse(itemValue);
        time = float.Parse(itemTime);


    }

    #endregion

}
