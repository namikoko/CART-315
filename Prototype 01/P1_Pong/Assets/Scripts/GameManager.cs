using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public Score score;
    public Ball ball;
    public GameStates gameState;
    public PianoSynth piano;

    public int songIndex = 0;              // Which song from Songs.All to start with
    public float serveDelay = 1.0f;        // Pause before the ball is served
    public int maxMisses = 3;              // Misses allowed before game over
    public float missPause = 1.5f;         // Pause after a miss before the ball is served again
    public float gameOverPause = 3.0f;     // How long the "game over" result shows
    public float songCompletePause = 3.0f; // How long the "song complete" message shows

    private Song _song;
    private int _notesPlayed;
    private int _misses;

    private void Start()
    {
        ball.PaddleHit += OnPaddleHit;
        StartSong(songIndex);
    }

    private void Update()
    {
        // Number keys 1-9 pick a song (until there's a song select screen)
        for (int i = 0; i < Songs.All.Length && i < 9; i++)
        {
            if (Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
                StartSong(i);
        }
    }

    public void StartSong(int index)
    {
        songIndex = Mathf.Clamp(index, 0, Songs.All.Length - 1);
        _song = Songs.All[songIndex];
        _notesPlayed = 0;
        _misses = 0;

        score.ShowSong((songIndex + 1) + ". " + _song.title, _song.notes.Length);
        score.SetMisses(_misses, maxMisses);
        StartRound();
    }

    public void StartRound()
    {
        StopAllCoroutines();
        ball.ResetBall();
        StartCoroutine(ServeAfter(serveDelay));
    }

    private IEnumerator ServeAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        ball.Launch(-1.0f);  // Serve towards the player
    }

    private void OnPaddleHit(Paddle paddle)
    {
        if (_notesPlayed >= _song.notes.Length) return;

        piano.PlayNote(_song.notes[_notesPlayed]);
        _notesPlayed++;
        score.SetProgress(_notesPlayed, _song.notes.Length);

        if (_notesPlayed >= _song.notes.Length)
            StartCoroutine(SongComplete());
    }

    private IEnumerator SongComplete()
    {
        ball.ResetBall();
        score.ShowMessage("Song complete!");
        yield return new WaitForSeconds(songCompletePause);
        StartSong((songIndex + 1) % Songs.All.Length);
    }

    // Called by the goals. courtId 0 = the player's (left) goal.
    public void CourtTriggered(int courtId)
    {
        if (courtId != 0) return;  // The partner never misses; ignore the right goal

        StopAllCoroutines();
        ball.ResetBall();
        piano.PlayWham();

        _misses++;
        score.SetMisses(_misses, maxMisses);

        if (_misses >= maxMisses)
        {
            // Out of misses: show how far they got, then start the song over
            int percent = Mathf.FloorToInt(100.0f * _notesPlayed / _song.notes.Length);
            score.ShowMessage("Game over! " + percent + "%");
            StartCoroutine(RestartSongAfter(gameOverPause));
        }
        else
        {
            // Keep going: the song carries on from the same note
            int missesLeft = maxMisses - _misses;
            score.ShowMessage("Miss! " + missesLeft + (missesLeft == 1 ? " miss left" : " misses left"));
            StartCoroutine(ServeAgainAfter(missPause));
        }
    }

    private IEnumerator ServeAgainAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        score.ShowMessage("");
        ball.Launch(-1.0f);
    }

    private IEnumerator RestartSongAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        StartSong(songIndex);
    }
}
