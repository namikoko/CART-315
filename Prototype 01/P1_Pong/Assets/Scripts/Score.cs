using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Piano Tiles style score: how much of the song you played.
public class Score : MonoBehaviour
{
    public TextMeshProUGUI songTitleText;
    public TextMeshProUGUI noteCounterText;
    public TextMeshProUGUI percentText;
    public TextMeshProUGUI messageText;

    public RectTransform progressFill;  // Stretched from the left edge to show progress
    public Image[] stars;               // 3 stars: first hit, halfway, finished
    public Image[] missPips;            // One per allowed miss; fades out when used

    public Color starOnColor = new Color(1.0f, 0.714f, 0.812f);   // Nadeshiko Pink
    public Color starOffColor = new Color(1.0f, 0.863f, 0.910f);  // Pig Pink

    public void ShowSong(string title, int totalNotes)
    {
        songTitleText.text = title;
        SetProgress(0, totalNotes);
        ShowMessage("");
    }

    public void SetProgress(int notesPlayed, int totalNotes)
    {
        float progress = totalNotes > 0 ? (float)notesPlayed / totalNotes : 0.0f;

        progressFill.anchorMax = new Vector2(progress, 1.0f);
        noteCounterText.text = notesPlayed + " / " + totalNotes;
        percentText.text = Mathf.FloorToInt(progress * 100) + "%";

        int starCount = StarsFor(notesPlayed, totalNotes);
        for (int i = 0; i < stars.Length; i++)
            stars[i].color = i < starCount ? starOnColor : starOffColor;
    }

    public void SetMisses(int misses, int maxMisses)
    {
        int missesLeft = maxMisses - misses;
        for (int i = 0; i < missPips.Length; i++)
            missPips[i].color = i < missesLeft ? starOnColor : starOffColor;
    }

    public void ShowMessage(string message)
    {
        messageText.text = message;
    }

    public static int StarsFor(int notesPlayed, int totalNotes)
    {
        if (totalNotes <= 0) return 0;
        if (notesPlayed >= totalNotes) return 3;  // Finished the song
        if (notesPlayed * 2 >= totalNotes) return 2;  // Halfway
        if (notesPlayed >= 1) return 1;  // Hit at least once
        return 0;
    }
}
