using UnityEngine;
using Google.Play.AppUpdate;
using Google.Play.Common;
using System.Collections;

public class AppUpdateController : MonoBehaviour
{
    AppUpdateManager appUpdateManager = new AppUpdateManager();

    AppUpdateInfo appUpdateInfo = null;

    AppUpdateOptions appUpdateOptions = null;

    IEnumerator CheckForUpdate()
    {
        PlayAsyncOperation<AppUpdateInfo, AppUpdateErrorCode> appUpdateInfoOperation;
        appUpdateInfoOperation = appUpdateManager.GetAppUpdateInfo();

        // Wait until the asynchronous operation completes.
        yield return appUpdateInfoOperation;

        if (appUpdateInfoOperation.IsSuccessful)
        {
            appUpdateInfo = appUpdateInfoOperation.GetResult();

            var availAblity = appUpdateInfo.UpdateAvailability; 

            // IsUpdateTypeAllowed(), ... and decide whether to ask the user
            if(availAblity == UpdateAvailability.UpdateAvailable)
            {
                // Creates an AppUpdateOptions defining an immediate in-app
                // update flow and its parameters.
                appUpdateOptions = AppUpdateOptions.ImmediateAppUpdateOptions();


            }

            // to start an in-app update.
        }
        else
        {
            // Log appUpdateInfoOperation.Error.
        }
    }

    IEnumerator StartImmediateUpdate()
    {
        if (appUpdateInfo == null || appUpdateOptions == null)
            yield break;

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
}
