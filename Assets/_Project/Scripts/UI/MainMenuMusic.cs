using UnityEngine;
using System.Collections;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Main Menu Music
//  Fades in a looping ambient track. Drop a clip into the AudioSource in
//  the inspector, or assign one in 'clip' and Start() will set it up.
// ─────────────────────────────────────────────────────────────────────────────
public class MainMenuMusic : MonoBehaviour
{
    public AudioSource source;
    public AudioClip   clip;
    public float       fadeIn       = 2f;
    public float       targetVolume = 0.7f;

    void Start()
    {
        if (source == null) source = GetComponent<AudioSource>();
        if (source == null) return;

        // Apply saved music volume if present
        targetVolume = PlayerPrefs.GetFloat(SettingsMenu.K_MUSIC, targetVolume);

        if (clip != null) source.clip = clip;
        if (source.clip == null) return;          // no clip assigned — silent menu, that's fine

        source.loop   = true;
        source.volume = 0f;
        source.Play();
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(0f, targetVolume, t / fadeIn);
            yield return null;
        }
        source.volume = targetVolume;
    }
}
