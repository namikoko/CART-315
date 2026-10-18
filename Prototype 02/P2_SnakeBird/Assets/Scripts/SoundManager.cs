using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SNAKE BIRD MOD: plays the music-box loop and the cute sound effects.
/// Clips live in Assets/Resources/Audio. Press M to mute or unmute.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Range(0f, 1f)] public float musicVolume = 0.22f;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    private AudioSource music;
    private AudioSource sfx;
    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private float musicTarget;
    private bool muted;

    private void Awake()
    {
        Instance = this;

        foreach (AudioClip clip in Resources.LoadAll<AudioClip>("Audio")) {
            clips[clip.name] = clip;
        }

        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.playOnAwake = false;
        music.volume = musicVolume;
        musicTarget = musicVolume;
        if (clips.TryGetValue("Music", out AudioClip song)) {
            music.clip = song;
            music.Play();
        }

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
    }

    private void OnEnable()
    {
        if (Instance == null) Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // unscaledDeltaTime so fades still work while the game is paused
        music.volume = Mathf.MoveTowards(music.volume, muted ? 0f : musicTarget, Time.unscaledDeltaTime * 0.8f);

        if (Input.GetKeyDown(KeyCode.M)) {
            muted = !muted;
        }
    }

    public static void Play(string name, float volume = 1f, float pitchJitter = 0f)
    {
        if (Instance == null || Instance.muted) return;
        if (!Instance.clips.TryGetValue(name, out AudioClip clip)) return;

        Instance.sfx.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        Instance.sfx.PlayOneShot(clip, volume * Instance.sfxVolume);
    }

    /// <summary>Stops the music when you lose.</summary>
    public static void StopMusic()
    {
        if (Instance == null) return;
        Instance.music.Stop();
    }

    /// <summary>Starts the song again from the top when you press PLAY.</summary>
    public static void StartMusic()
    {
        if (Instance == null || Instance.music.clip == null) return;
        Instance.musicTarget = Instance.musicVolume;
        Instance.music.Stop();
        Instance.music.Play();
    }
}
