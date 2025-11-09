using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class SettingsMenu : MonoBehaviour
{
    [Header("UI Elements")]
    public Slider volumeSlider;       // 0..1
    public Toggle muteToggle;
    public Button closeButton;

    [Header("Optional")]
    public AudioMixer audioMixer;     // ถ้าใช้ AudioMixer ให้ผูกใน Inspector (parameter "MasterVolume")
    public string mixerVolumeParam = "MasterVolume";

    const string PREF_VOLUME = "master_volume";
    const string PREF_MUTE = "master_mute";

    void Start()
    {
        // load saved settings
        float vol = PlayerPrefs.GetFloat(PREF_VOLUME, 1f);
        bool muted = PlayerPrefs.GetInt(PREF_MUTE, 0) == 1;

        if (volumeSlider != null)
        {
            volumeSlider.value = vol;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        if (muteToggle != null)
        {
            muteToggle.isOn = muted;
            muteToggle.onValueChanged.AddListener(SetMute);
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        ApplyVolume(vol, muted);
    }

    public void SetVolume(float value)
    {
        // value in 0..1
        PlayerPrefs.SetFloat(PREF_VOLUME, value);
        ApplyVolume(value, muteToggle != null && muteToggle.isOn);
    }

    public void SetMute(bool isMuted)
    {
        PlayerPrefs.SetInt(PREF_MUTE, isMuted ? 1 : 0);
        float vol = volumeSlider != null ? volumeSlider.value : PlayerPrefs.GetFloat(PREF_VOLUME, 1f);
        ApplyVolume(vol, isMuted);
    }

    void ApplyVolume(float linearVolume, bool muted)
    {
        float used = muted ? 0f : linearVolume;
        // ถ้ามี AudioMixer ให้แปลงเป็น dB (ตัวอย่าง: -80 ... 0)
        if (audioMixer != null && !string.IsNullOrEmpty(mixerVolumeParam))
        {
            float dB = (used <= 0.0001f) ? -80f : Mathf.Lerp(-80f, 0f, used);
            audioMixer.SetFloat(mixerVolumeParam, dB);
        }
        else
        {
            // ถ้าไม่ใช้ mixer คุณอาจจะตั้งค่า AudioListener.volume (global)
            AudioListener.volume = used;
        }
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        // ล้าง listener เพื่อป้องกัน memory leak
        if (volumeSlider != null)
            volumeSlider.onValueChanged.RemoveListener(SetVolume);
        if (muteToggle != null)
            muteToggle.onValueChanged.RemoveListener(SetMute);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }
}