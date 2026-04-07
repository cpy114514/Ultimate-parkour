using UnityEngine;
using UnityEngine.UI;

public class LoadSceneWithWhiteFade : MonoBehaviour
{
    [Header("Scene")]
    public string sceneName;

    [Header("Legacy Fade")]
    public Image fadeImage;
    public float fadeDuration = 1f;

    bool isLoading;

    void Start()
    {
        if (fadeImage != null)
        {
            Color color = fadeImage.color;
            color.a = 0f;
            fadeImage.color = color;

            // Legacy fade images in the Start scene are full-screen children of buttons.
            // Keep them non-interactive so clicks only hit the actual button rect.
            fadeImage.raycastTarget = false;
            fadeImage.gameObject.SetActive(false);
        }
    }

    public void LoadScene()
    {
        if (isLoading)
        {
            return;
        }

        if (SceneTransitionController.TryLoadScene(sceneName))
        {
            isLoading = true;
        }
    }
}
