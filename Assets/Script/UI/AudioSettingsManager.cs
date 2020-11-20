using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioSettingsManager : MonoBehaviour
{
    public static readonly string BackgroundSoundPref = "BackgroundSoundPref";
    public static readonly string SoundEffectsPref = "SoundEffectsPref";
    private float BackgroundSoundFloat, SoundEffectsFloat;
    public AudioSource BackgroundSoundAudio;
    public AudioSource[] SoundEffectsAudio;
    public void Awake()
    {
        ContinueSettings();
    }
    private void ContinueSettings()
    {
        BackgroundSoundFloat = PlayerPrefs.GetFloat(BackgroundSoundPref);
        SoundEffectsFloat = PlayerPrefs.GetFloat(SoundEffectsPref);
        BackgroundSoundAudio.volume = BackgroundSoundFloat;
        for (int i = 0; i < SoundEffectsAudio.Length; i++)
        {
            SoundEffectsAudio[i].volume = SoundEffectsFloat;
        }
    }
}
