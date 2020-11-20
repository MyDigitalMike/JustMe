using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuBuy : MonoBehaviour
{
    public static bool EnPausa = false;
    public GameObject buyMenuUI; public GameObject MenuUI;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    public void BuyMenu()
    {
        if (EnPausa)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }
    public void Resume()
    {
        buyMenuUI.SetActive(false);
        MenuUI.SetActive(true);
        Time.timeScale = 1f;
        EnPausa = false;
    }
    public void Pause()
    {
        buyMenuUI.SetActive(true);
        MenuUI.SetActive(false);
        Time.timeScale = 0f;
        EnPausa = true;
    }
}
