using UnityEngine;
using UnityEngine.Advertisements;
using UnityEngine.SceneManagement;

public class Publicidad : MonoBehaviour, IUnityAdsListener
{
    public string gameId = "3570454";
    public string myPlacementId = "GameOver";
    public bool testMode = false;

    void Start()
    {
        Advertisement.AddListener(this);
        Advertisement.Initialize(gameId, testMode);
    }
    public void ShowRewardedVideo()
    {
        // Check if UnityAds ready before calling Show method:
        if (Advertisement.IsReady(myPlacementId))
        {
            Advertisement.Show(myPlacementId);
        }
        else
        {
            Debug.Log("Rewarded video is not ready at the moment! Please try again later!");
        }
    }
    // Implement IUnityAdsListener interface methods:
    /// <summary>
    /// The actions for after of show the Advertising
    /// </summary>
    /// <param name="placementId">Id of Unity Advertising</param>
    /// <param name="showResult">The show the result expected</param>
    public void OnUnityAdsDidFinish(string placementId, ShowResult showResult)
    {
        // Define conditional logic for each ad completion status:
        if (showResult == ShowResult.Finished)
        {
            SceneManager.LoadScene("JustMe");
        }
        else if (showResult == ShowResult.Skipped)
        {
            SceneManager.LoadScene("MainMenu");
        }
        else if (showResult == ShowResult.Failed)
        {
            Debug.LogWarning("The ad did not finish due to an error.");
        }
    }
    public void OnUnityAdsReady(string placementId)
    {
        // If the ready Placement is rewarded, show the ad:
        if (placementId == myPlacementId)
        {
            // Optional actions to take when the placement becomes ready(For example, enable the rewarded ads button)
        }
    }

    public void OnUnityAdsDidError(string message)
    {
        // Log the error.
    }

    public void OnUnityAdsDidStart(string placementId)
    {
        // Optional actions to take when the end-users triggers an ad.
    }

    // When the object that subscribes to ad events is destroyed, remove the listener:
    public void OnDestroy()
    {
        Advertisement.RemoveListener(this);
    }
}
