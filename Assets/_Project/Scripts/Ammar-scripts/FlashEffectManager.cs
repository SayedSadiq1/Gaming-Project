using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FlashEffectManager : MonoBehaviour
{
    public static FlashEffectManager Instance;

    private Image flashImage;
    private CanvasGroup canvasGroup;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        CreateUI();
    }

    void CreateUI()
    {
        GameObject canvasGO = new GameObject("FlashCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject imageGO = new GameObject("FlashImage");
        imageGO.transform.SetParent(canvasGO.transform);
        flashImage = imageGO.AddComponent<Image>();
        flashImage.color = Color.white;
        
        RectTransform rt = imageGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        DontDestroyOnLoad(canvasGO);
    }

    public void TriggerFlash(float intensity, float holdDuration, float fadeDuration)
    {
        StopAllCoroutines();
        StartCoroutine(FlashRoutine(intensity, holdDuration, fadeDuration));
    }

    private IEnumerator FlashRoutine(float intensity, float holdDuration, float fadeDuration)
    {
        canvasGroup.alpha = intensity;
        
        // Hold the flash
        yield return new WaitForSeconds(holdDuration);

        // Fade out
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(intensity, 0f, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }
}
