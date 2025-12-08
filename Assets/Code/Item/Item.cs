using UnityEngine;




public class Item : MonoBehaviour
{
    [SerializeField] protected ItemType category;
    [SerializeField] protected float value;
    [SerializeField] protected float time;

    /// <summary>
    /// csv 파일로 바꾼 데이터와 아이템 정보를 매칭
    /// </summary>
    /// <param name="itemName"> 오브젝트 이름 </param>
    /// <param name="itemType"> 아이템 타입 </param>
    /// <param name="itemValue"> 아이템 스탯 </param>
    /// <param name="itemTime"> 아이템 동작 시간 </param>
    public void SyncData(string itemName, string itemType, string itemValue, string itemTime)
    {
        this.gameObject.name = name;

        if (System.Enum.TryParse(itemType, out ItemType parsedType))
            category = parsedType;
        else
        {
            Debug.Log("Wrong Item Type");
        }

        value = float.Parse(itemValue);
        time = float.Parse(itemTime);

        Debug.Log($"cur ItemData's name is {gameObject.name} , type is {category}, value is {value}, time is {time}");

    }
}
