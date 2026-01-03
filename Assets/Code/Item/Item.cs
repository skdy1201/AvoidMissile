using System.Collections;
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

    [SerializeField] protected int percent;

    [SerializeField] protected float value;

    [SerializeField] protected float time;

    // ItemSpawner의 TIle Check를 위한, 배정 타일 변수
    [SerializeField]protected int spawnTile = -1;

    #endregion

    #region Private/Protected Fields

    protected Collider itemCollider = null;

    protected Material itemMaterial;

    private int cumulativePercent;

    #endregion

    #region Properties

    public ItemType Category
    {
        get { return category; }
    }

    public int SpawnTile
    {
        get { return spawnTile; }
        set {  spawnTile = value; }
    }

    public int Percent
    {
        get { return percent; }
        set { percent = value; }
    }

    public int CumulativePercent
    {
        get { return cumulativePercent; }
        set { cumulativePercent = value; }
    }

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 콜라이더를 미리 캐싱
    /// </summary>
    protected virtual void Awake()
    {
        itemCollider = GetComponent<Collider>();

        if (itemCollider == null)
            Debug.LogError("item doesn't have collider");

        itemMaterial = gameObject.GetComponent<MeshRenderer>().material;

    }

    private void OnEnable()
    {
        StartCoroutine("ItemTimer");
    }

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
            Debug.LogError("Wrong Item Type");
        }

        if(!float.TryParse(itemValue, out float parsedValue))
        {
            Debug.LogError("Wrong Value");
            return;
        }

        value = float.Parse(itemValue);


        if (!float.TryParse(itemTime, out float parsedTime))
        {
            Debug.LogError("Wrong Value");
            return;
        }
        time = float.Parse(itemTime);


    }

    public void SetValue(float  dataValue) => value = dataValue;
    public void SetTime(float dataTime) => time = dataTime;

    #endregion

    #region Private/Protected Methods

    /// <summary>
    /// 플레이어와 충돌한다면, 아이템의 효과를 발동시키고, 스포너에 반환한다.
    /// </summary>
    /// <param name="otherCollider"> 충돌한 다른 오브젝트 </param>
    private void OnCollisionEnter(Collision otherCollider)
    {
        if (otherCollider.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            EffectItem();

            ItemSpawner.Instance.ReturnSpawner(category, this.gameObject);
        }
    }

    /// <summary>
    /// 아이템을 ReturnSpawner 시킬때, 변한 Alpha값을 원상복구
    /// </summary>
    public void ResetItemAlpha()
    {
        Color itemColor = itemMaterial.color;
        itemColor.a = 1f;
        itemMaterial.color = itemColor;
    }

    #endregion

    #region

    IEnumerator ItemTimer()
    {
        Color itemColor = itemMaterial.color;

        yield return new WaitForSecondsRealtime(3.5f);

        float Timer = 2.5f;

        while(Timer >= 0f)
        {
            float itemAlpha = Mathf.PingPong(Time.time * 5f, 1f);

            itemColor.a = itemAlpha;

            itemMaterial.color = itemColor;

            Timer -= Time.deltaTime;

            yield return null;
        }

        ResetItemAlpha();

        ItemSpawner.Instance.ReturnSpawner(category, gameObject);
    }

    #endregion

}
