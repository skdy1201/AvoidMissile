using UnityEngine;
using System.Collections.Generic;

public class BlackBoard : MonoBehaviour
{
   Dictionary<string, object> datas = new Dictionary<string, object>();

    public void InsertData(string key, object value)
    {
        datas[key] = value; 
    }    

    public T GetData<T>(string key)
    {
        return (T)datas[key];
    }

}
