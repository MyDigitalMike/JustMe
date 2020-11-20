using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Advertisements;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public GameObject PanelSettings;
    public GameObject PrincipalMenu;
    // Start is called before the first frame update
    void Start()
    {
        Advertisement.Banner.Hide();
        ClosePanelSettings();
        OpenMainPanel();
    }
    /// <summary>
    /// Open the settings menu.
    /// </summary>
    public void OpenPanelSettings()
    {
        PanelSettings.SetActive(true);
        CloseMainPanel();
    }
    /// <summary>
    /// Close the settings menu
    /// </summary>
    public void ClosePanelSettings()
    {
        PanelSettings.SetActive(false);
        OpenMainPanel();
    }
    /// <summary>
    /// Open the main menu
    /// </summary>
    public void OpenMainPanel()
    {
        PrincipalMenu.SetActive(true);
    }
    /// <summary>
    /// Close the main menu
    /// </summary>
    public void CloseMainPanel()
    {
        PrincipalMenu.SetActive(false);
    }
    public void Creditos()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Creditos");

    }
    public void Quit()
    {
#if UNITY_EDITOR
        // Application.Quit() does not work in the editor so
        // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
        UnityEditor.EditorApplication.isPlaying = false;
#else
         Application.Quit();
#endif
    }
    public void QuitMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
    // Update is called once per frame
    void Update()
    {

    }
}
