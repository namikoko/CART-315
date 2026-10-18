using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-1)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private Player player;
    [SerializeField] private Spawner spawner;
    [SerializeField] private Text scoreText;
    [SerializeField] private GameObject playButton;
    [SerializeField] private GameObject gameOver;

    [Header("Snake Bird")]
    [Tooltip("Points for eating a treat (pipes are worth 1 x multiplier)")]
    [SerializeField] private int treatPoints = 1;
    [SerializeField] private Color multiplierColor = new Color32(240, 132, 166, 255);

    public int score { get; private set; } = 0;
    public int bestScore { get; private set; } = 0;

    /// <summary>1 + number of baby chicks in your tail.</summary>
    public int Multiplier => 1 + (player != null && player.Tail != null ? player.Tail.Count : 0);

    private Text infoText;   // shows "x3" while playing, "best 42" on game over
    private bool isGameOver;

    // UI layout (canvas units, measured from the screen center)
    private const float GameOverY = 110f;
    private const float InfoCenterY = 25f;
    private const float PlayButtonY = -95f;
    private Vector2 infoTopPos;
    private Vector3 infoTopScale;

    private void Awake()
    {
        if (Instance != null) {
            DestroyImmediate(gameObject);
        } else {
            Instance = this;
        }
    }

    // Unity wipes static fields if scripts recompile during Play mode; this re-links us.
    private void OnEnable()
    {
        if (Instance == null) {
            Instance = this;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) {
            Instance = null;
        }
    }

    private void Start()
    {
        bestScore = PlayerPrefs.GetInt("SnakeBirdBest", 0);
        if (GetComponent<SoundManager>() == null) {
            gameObject.AddComponent<SoundManager>();
        }

        CreateInfoText();
        CenterMenuUI();
        ShowInfoInCenter(bestScore > 0 ? "best " + bestScore : "eat treats!");
        Pause();
    }

    public void Pause()
    {
        Time.timeScale = 0f;
        player.enabled = false;
    }

    public void Play()
    {
        score = 0;
        scoreText.text = score.ToString();
        isGameOver = false;

        playButton.SetActive(false);
        gameOver.SetActive(false);

        Time.timeScale = 1f;
        player.enabled = true;
        player.Tail.Clear();
        ShowInfoUnderScore();
        UpdateInfoText();

        SoundManager.Play("Click");
        SoundManager.StartMusic();

        Pipes[] pipes = FindObjectsByType<Pipes>(FindObjectsSortMode.None);

        for (int i = 0; i < pipes.Length; i++) {
            Destroy(pipes[i].gameObject);
        }
    }

    public void GameOver()
    {
        if (isGameOver) return; // the bird and several chicks can all hit on the same frame
        isGameOver = true;

        if (score > bestScore) {
            bestScore = score;
            PlayerPrefs.SetInt("SnakeBirdBest", bestScore);
            PlayerPrefs.Save();
            ShowInfoInCenter("new best!");
        } else {
            ShowInfoInCenter("best " + bestScore);
        }

        SoundManager.Play("GameOver");
        SoundManager.StopMusic();

        playButton.SetActive(true);
        gameOver.SetActive(true);

        Pause();
    }

    public void IncreaseScore()
    {
        // SNAKE BIRD MOD: every pipe is worth more the longer your tail is
        score += Multiplier;
        scoreText.text = score.ToString();
        SoundManager.Play("Point", 0.6f);
    }

    public void EatTreat()
    {
        if (isGameOver) return;

        score += treatPoints;
        SoundManager.Play("Eat");
        if (!player.Tail.IsFull) {
            player.Tail.Grow();
            SoundManager.Play("Chick", 0.7f);
        }
        scoreText.text = score.ToString();
        UpdateInfoText();
    }

    private void UpdateInfoText()
    {
        infoText.text = "x" + Multiplier;
    }

    // Copies the score label to make a smaller pink label under it, so no scene editing is needed.
    private void CreateInfoText()
    {
        scoreText.color = Color.white;

        infoText = Instantiate(scoreText, scoreText.transform.parent);
        infoText.name = "Multiplier";
        infoText.color = multiplierColor;

        RectTransform src = scoreText.rectTransform;
        RectTransform rt = infoText.rectTransform;
        rt.localScale = src.localScale * 0.45f;
        rt.anchoredPosition = src.anchoredPosition + new Vector2(0f, -85f);

        infoTopPos = rt.anchoredPosition;
        infoTopScale = rt.localScale;
    }

    // SNAKE BIRD MOD: stack GAME OVER / best score / PLAY in the middle of the screen
    private void CenterMenuUI()
    {
        CenterOn(gameOver.GetComponent<RectTransform>(), GameOverY);
        CenterOn(playButton.GetComponent<RectTransform>(), PlayButtonY);
    }

    private static void CenterOn(RectTransform rt, float y)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
    }

    private void ShowInfoInCenter(string message)
    {
        RectTransform rt = infoText.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, InfoCenterY);
        rt.localScale = infoTopScale * 1.2f;
        infoText.text = message;
    }

    private void ShowInfoUnderScore()
    {
        RectTransform rt = infoText.rectTransform;
        RectTransform src = scoreText.rectTransform;
        rt.anchorMin = src.anchorMin;
        rt.anchorMax = src.anchorMax;
        rt.anchoredPosition = infoTopPos;
        rt.localScale = infoTopScale;
    }

}
