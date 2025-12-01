using UnityEngine;
using UnityEngine.SceneManagement;

public class DevTestController : MonoBehaviour
{

    #region Unity Lifecycle
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKey(KeyCode.Escape))
        {
            Debug.Log("KeyDown ESC");

            // TODO : 정리 함수를 만들어 둬야 할듯?
            SceneController.Instance.ChangeScene();

        }
    }

    #endregion
}
