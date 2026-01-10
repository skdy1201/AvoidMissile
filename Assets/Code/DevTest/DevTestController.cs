using UnityEngine;
using UnityEngine.SceneManagement;

public class DevTestController : MonoBehaviour
{

    // DevTestScene에서 테스트 할 때, 한 번만 동작시키기 위한 변수
    private bool DevTestActive = false;

    #region Unity Lifecycle

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
       // if(Input.GetKey(KeyCode.Escape))
       // {
       //     Debug.Log("KeyDown ESC");
       //
       //     // TODO : 정리 함수를 만들어 둬야 할듯?
       //     SceneController.Instance.ChangeScene();
       //
       // }


       if(DevTestActive == false)
       {
           DevTestActive = true;
           ItemSpawner.Instance.StartCoroutine("ItemSpawnLoop");
       }

        //if(DevTestActive == false)
        //{
        //    DevTestActive = true;
        //    ItemSpawner.Instance.TestPercent();
        //}
        

        // DevTest Scene에서 테스트
        //#if UNITY_EDITOR
        //        if (Input.GetKey(KeyCode.I) && DevTestActive == false)
        //        {
        //            DevTestActive = true;
        //            ItemSpawner.Instance.StartCoroutine("ItemSpawnLoop");
        //        }
        //#endif

    }

    #endregion
}
