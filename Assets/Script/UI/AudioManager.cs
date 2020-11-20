using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public static readonly string FirstPlay = "FirstPlay";
    public static readonly string BackgroundSoundPref = "BackgroundSoundPref";
    public static readonly string SoundEffectsPref = "SoundEffectsPref";
    private int FirstPlayInt;
    public Slider BackgroundSoundSlider, SoundEffectsSlider;
    private float BackgroundSoundFloat, SoundEffectsFloat;
    public AudioSource BackgroundSoundAudio;
    public AudioSource[] SoundEffectsAudio;
    void Start()
    {
        FirstPlayInt = PlayerPrefs.GetInt(FirstPlay);
        if(FirstPlayInt == 0)
        {
            BackgroundSoundFloat = .125f;
            SoundEffectsFloat = .35f;
            BackgroundSoundSlider.value = BackgroundSoundFloat;
            SoundEffectsSlider.value = SoundEffectsFloat;
            PlayerPrefs.SetFloat(BackgroundSoundPref, BackgroundSoundFloat);
            PlayerPrefs.SetFloat(SoundEffectsPref, SoundEffectsFloat);
            PlayerPrefs.SetInt(FirstPlay, -1);
        }
        else
        {
            BackgroundSoundFloat = PlayerPrefs.GetFloat(BackgroundSoundPref);
            BackgroundSoundSlider.value = BackgroundSoundFloat;
           
            SoundEffectsFloat = PlayerPrefs.GetFloat(SoundEffectsPref);
            SoundEffectsSlider.value = SoundEffectsFloat;
        }
    }
    public void SaveSoundSettings()
    {
        PlayerPrefs.SetFloat(BackgroundSoundPref, BackgroundSoundSlider.value);
        PlayerPrefs.SetFloat(SoundEffectsPref, SoundEffectsSlider.value);
    }
    private void OnApplicationFocus(bool inFocus)
    {
        if (!inFocus)
        {
            SaveSoundSettings();
        }
    }
    public void UpdateSound()
    {
        BackgroundSoundAudio.volume = BackgroundSoundSlider.value;
        for (int i = 0; i < SoundEffectsAudio.Length; i++)
        {
            SoundEffectsAudio[i].volume = SoundEffectsSlider.value;
        }
    }
}
