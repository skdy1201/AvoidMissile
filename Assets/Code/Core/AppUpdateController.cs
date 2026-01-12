using UnityEngine;
using Google.Play.AppUpdate;
using Google.Play.Common;
using System.Collections;

/// <summary>
/// 인 앱 업데이트를 담당하는 컨트롤러
/// </summary>
/// <remarks>
/// Google Play에서 제공해주는 매니저를 이용해
/// 업데이트 여부를 체크한 뒤, 상황에 따른 조치 수행
/// </remarks>
public class AppUpdateController : MonoBehaviour
{
    #region Private/Protected Fields
   
    private AppUpdateManager appUpdateManager = new AppUpdateManager();

    private AppUpdateInfo appUpdateInfo = null;

    private AppUpdateOptions appUpdateOptions = null;

    #endregion

    #region Coroutine

    IEnumerator CheckForUpdate()
    {
        PlayAsyncOperation<AppUpdateInfo, AppUpdateErrorCode> appUpdateInfoOperation;
        appUpdateInfoOperation = appUpdateManager.GetAppUpdateInfo();

        // Wait until the asynchronous operation completes.
        yield return appUpdateInfoOperation;

        if (appUpdateInfoOperation.IsSuccessful)
        {
            Debug.Log("possilbe Update");


            appUpdateInfo = appUpdateInfoOperation.GetResult();

            var availAblity = appUpdateInfo.UpdateAvailability;

            // IsUpdateTypeAllowed(), ... and decide whether to ask the user
            if (availAblity == UpdateAvailability.UpdateAvailable)
            {
                // Creates an AppUpdateOptions defining an immediate in-app
                // update flow and its parameters.
                appUpdateOptions = AppUpdateOptions.ImmediateAppUpdateOptions();
            }
            else if (availAblity == UpdateAvailability.UpdateNotAvailable)
            {
                Debug.Log("possible but, no Update");
            }
            // to start an in-app update.
            StartCoroutine(CheckForUpdate());
        }
        else
        {
            // Log appUpdateInfoOperation.Error.
            Debug.Log($"{appUpdateInfoOperation.Error}");
        }
    }

    IEnumerator StartImmediateUpdate()
    {
        if (appUpdateInfo == null || appUpdateOptions == null)
            yield break;

        Debug.Log("ImmediateUpdate Start");

        // Creates an AppUpdateRequest that can be used to monitor the
        // requested in-app update flow.
        var startUpdateRequest = appUpdateManager.StartUpdate(
          // The result returned by PlayAsyncOperation.GetResult().
          appUpdateInfo,
          // The AppUpdateOptions created defining the requested in-app update
          // and its parameters.
          appUpdateOptions);
        yield return startUpdateRequest;

        // If the update completes successfully, then the app restarts and this line
        // is never reached. If this line is reached, then handle the failure (for
        // example, by logging result.Error or by displaying a message to the user).
    }
    #endregion

}
