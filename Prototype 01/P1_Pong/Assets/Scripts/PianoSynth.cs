using System.Collections.Generic;
using UnityEngine;

// Makes piano-like sounds in code, so no audio files are needed.
[RequireComponent(typeof(AudioSource))]
public class PianoSynth : MonoBehaviour
{
    public float volume = 0.5f;
    public float noteLength = 1.6f;  // Seconds each note rings for

    // Loudness of each harmonic (1st = the note itself, then the overtones that make it sound like a piano)
    private static readonly float[] HarmonicLevels = { 1.0f, 0.6f, 0.35f, 0.2f, 0.12f, 0.07f };

    private AudioSource _source;
    private int _sampleRate;
    private readonly Dictionary<int, AudioClip> _clips = new Dictionary<int, AudioClip>();

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _sampleRate = AudioSettings.outputSampleRate;
    }

    public void PlayNote(string noteName)
    {
        int midi = NoteToMidi(noteName);
        if (midi < 0)
        {
            Debug.LogWarning("Unknown note: " + noteName);
            return;
        }

        _source.PlayOneShot(GetClip(midi), volume);
    }

    // Slam a handful of random low keys at once
    public void PlayWham()
    {
        for (int i = 0; i < 6; i++)
        {
            int midi = Random.Range(28, 52);  // Roughly E1 to E3
            _source.PlayOneShot(GetClip(midi), volume * 0.6f);
        }
    }

    // "C#4" -> 61. Returns -1 if the note can't be read.
    public static int NoteToMidi(string note)
    {
        if (string.IsNullOrEmpty(note)) return -1;

        int semitone;
        switch (char.ToUpper(note[0]))
        {
            case 'C': semitone = 0; break;
            case 'D': semitone = 2; break;
            case 'E': semitone = 4; break;
            case 'F': semitone = 5; break;
            case 'G': semitone = 7; break;
            case 'A': semitone = 9; break;
            case 'B': semitone = 11; break;
            default: return -1;
        }

        int i = 1;
        if (i < note.Length && note[i] == '#') { semitone++; i++; }
        else if (i < note.Length && note[i] == 'b') { semitone--; i++; }

        if (!int.TryParse(note.Substring(i), out int octave)) return -1;

        return 12 * (octave + 1) + semitone;
    }

    private AudioClip GetClip(int midi)
    {
        if (!_clips.TryGetValue(midi, out AudioClip clip))
        {
            clip = CreatePianoClip(midi);
            _clips[midi] = clip;
        }
        return clip;
    }

    private AudioClip CreatePianoClip(int midi)
    {
        float frequency = 440.0f * Mathf.Pow(2.0f, (midi - 69) / 12.0f);
        int sampleCount = Mathf.CeilToInt(noteLength * _sampleRate);
        float[] samples = new float[sampleCount];

        // Higher notes fade faster, like a real piano
        float baseDecay = 2.5f + frequency / 400.0f;
        float peak = 0.0001f;

        for (int s = 0; s < sampleCount; s++)
        {
            float t = (float)s / _sampleRate;
            float attack = Mathf.Min(1.0f, t / 0.004f);  // Sharp 4ms hammer strike
            float value = 0.0f;

            for (int h = 0; h < HarmonicLevels.Length; h++)
            {
                int n = h + 1;
                float harmonicFrequency = frequency * n * (1.0f + 0.0004f * n * n);  // Slightly stretched overtones, like piano strings
                if (harmonicFrequency > _sampleRate / 2.0f) break;

                float envelope = Mathf.Exp(-t * baseDecay * (1.0f + 0.6f * h));  // Overtones die away before the main note
                value += HarmonicLevels[h] * envelope * Mathf.Sin(2.0f * Mathf.PI * harmonicFrequency * t);
            }

            samples[s] = value * attack;
            peak = Mathf.Max(peak, Mathf.Abs(samples[s]));
        }

        // Make every note the same loudness
        for (int s = 0; s < sampleCount; s++)
            samples[s] *= 0.9f / peak;

        AudioClip clip = AudioClip.Create("Piano_" + midi, sampleCount, 1, _sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
