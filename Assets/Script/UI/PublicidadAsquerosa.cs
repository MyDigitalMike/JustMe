using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Advertisements;

public class PublicidadAsquerosa : MonoBehaviour
{
    public string gameId = "3570454";
    public string placementId = "GameOver";
    public bool testMode = true;

    void Start()
    {
        Advertisement.Initialize(gameId, testMode);
        StartCoroutine(ShowBannerWhenReady());
    }
    IEnumerator ShowBannerWhenReady()
    {
        while (!Advertisement.IsReady(placementId))
        {
            yield return new WaitForSeconds(0.5f);
            Advertisement.Show(placementId);
            //Advertisement.Banner.SetPosition(BannerPosition.BOTTOM_CENTER);
        }
        //Advertisement.Banner.Show(placementId);
    }
}
