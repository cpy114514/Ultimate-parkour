using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadSceneWithWhiteFade : MonoBehaviour
{
    [Header("Scene")]
    public string sceneName;

    [Header("Fade")]
    public Image fadeImage;          // 用来做白屏的 UI Image
    public float fadeDuration = 1f;  // 渐变时间

    private bool isLoading = false;

    private void Start()
    {
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
            fadeImage.gameObject.SetActive(true);
        }
    }

    public void LoadScene()
    {
        if (!isLoading)
        {
            StartCoroutine(FadeAndLoad());
        }
    }

    private IEnumerator FadeAndLoad()
    {
        isLoading = true;

        float time = 0f;
        Color c = fadeImage.color;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            c.a = Mathf.Clamp01(time / fadeDuration);
            fadeImage.color = c;
            yield return null;
        }

        SceneManager.LoadScene(sceneName);
    }
}