using UnityEngine;

/// <summary>
/// SNAKE BIRD MOD: a floating snack (strawberry, heart candy, star cookie, macaron).
/// Eat it to add a baby chick to your tail and raise your score multiplier.
/// It rides along with the pipe it was spawned with, so it scrolls and cleans up for free.
/// </summary>
public class Treat : MonoBehaviour
{
    public float bobHeight = 0.18f;
    public float bobSpeed = 3f;

    private Vector3 basePos;
    private float phase;

    private void Start()
    {
        basePos = transform.localPosition;
        phase = Random.value * 10f;
    }

    private void Update()
    {
        transform.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
        float pulse = 1f + Mathf.Sin(Time.time * 6f + phase) * 0.08f;
        transform.localScale = new Vector3(pulse, pulse, 1f);
    }

    /// <summary>Builds a treat GameObject in code, so no prefab setup is needed in the editor.</summary>
    public static Treat Create(Sprite sprite, Transform parent, Vector3 localPos)
    {
        GameObject go = new GameObject("Treat");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 1;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.3f; // generous so it feels good to grab

        return go.AddComponent<Treat>();
    }
}
