using UnityEngine;
using UnityEngine.SceneManagement;

public class BackgroundMusicController : MonoBehaviour
{
    const string StartStoryMusicGroup = "StartStory";

    static BackgroundMusicController instance;

    AudioSource musicSource;
    string currentSceneName;
    string currentMusicGroup;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    static BackgroundMusicController EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<BackgroundMusicController>();
        if (instance != null)
        {
            instance.EnsureAudioSource();
            DontDestroyOnLoad(instance.gameObject);
            return instance;
        }

        GameObject root = new GameObject("BackgroundMusicController");
        instance = root.AddComponent<BackgroundMusicController>();
        DontDestroyOnLoad(root);
        instance.EnsureAudioSource();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureAudioSource();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SyncSceneMusic(scene);
    }

    void EnsureAudioSource()
    {
        if (musicSource != null)
        {
            return;
        }

        musicSource = GetComponent<AudioSource>();
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
    }

    void SyncSceneMusic(Scene scene)
    {
        EnsureAudioSource();

        AudioSource sceneMusic = FindSceneMusicSource(scene);
        string nextGroup = GetMusicGroup(scene.name);

        if (sceneMusic == null)
        {
            if (ShouldKeepExistingMusic(nextGroup))
            {
                currentSceneName = scene.name;
                return;
            }

            musicSource.Stop();
            musicSource.clip = null;
            currentSceneName = scene.name;
            currentMusicGroup = nextGroup;
            return;
        }

        AudioClip nextClip = sceneMusic.clip;
        float nextVolume = sceneMusic.volume;
        float nextPitch = sceneMusic.pitch;
        StopSceneMusicSource(sceneMusic);

        bool canContinue =
            musicSource.clip == nextClip &&
            musicSource.isPlaying &&
            !string.IsNullOrEmpty(currentMusicGroup) &&
            currentMusicGroup == nextGroup &&
            ShouldContinueMusicGroup(nextGroup);

        if (!canContinue)
        {
            musicSource.Stop();
            musicSource.clip = nextClip;
            musicSource.time = 0f;
            musicSource.Play();
        }

        musicSource.volume = nextVolume;
        musicSource.pitch = nextPitch;
        musicSource.loop = true;
        currentSceneName = scene.name;
        currentMusicGroup = nextGroup;
    }

    AudioSource FindSceneMusicSource(Scene scene)
    {
        AudioSource[] sources = FindObjectsOfType<AudioSource>();
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null || source == musicSource || source.transform.IsChildOf(transform))
            {
                continue;
            }

            if (source.gameObject.scene != scene || source.clip == null || !source.loop)
            {
                continue;
            }

            return source;
        }

        return null;
    }

    void StopSceneMusicSource(AudioSource sceneMusic)
    {
        sceneMusic.Stop();
        sceneMusic.enabled = false;
    }

    bool ShouldKeepExistingMusic(string nextGroup)
    {
        return ShouldContinueMusicGroup(nextGroup) &&
               currentMusicGroup == nextGroup &&
               musicSource.clip != null &&
               musicSource.isPlaying;
    }

    bool ShouldContinueMusicGroup(string group)
    {
        return group == StartStoryMusicGroup;
    }

    string GetMusicGroup(string sceneName)
    {
        if (sceneName == "Start" || sceneName == "Story_Start" || sceneName == "Story1")
        {
            return StartStoryMusicGroup;
        }

        return sceneName;
    }
}
