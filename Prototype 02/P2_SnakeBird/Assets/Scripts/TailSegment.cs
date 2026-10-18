using UnityEngine;

/// <summary>
/// SNAKE BIRD MOD: one baby chick in the tail. Touching a pipe or the ground ends the run.
/// </summary>
public class TailSegment : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float graceTimer;
    private float bobOffset;

    public void Init(SpriteRenderer sr, float grace, int index)
    {
        spriteRenderer = sr;
        graceTimer = grace;
        bobOffset = index * 0.7f;
        transform.localScale = Vector3.zero; // pops in
    }

    private void Update()
    {
        // pop-in + gentle squishy bounce
        float target = 1f + Mathf.Sin(Time.time * 10f + bobOffset) * 0.06f;
        float s = Mathf.MoveTowards(transform.localScale.x, target, Time.deltaTime * 6f);
        transform.localScale = new Vector3(s, s, 1f);

        if (graceTimer > 0f)
        {
            graceTimer -= Time.deltaTime;
            // blink while safe
            spriteRenderer.enabled = graceTimer <= 0f || Mathf.Repeat(graceTimer, 0.16f) > 0.08f;
        }
    }

    private void OnTriggerEnter2D(Collider2D other) => CheckHit(other);
    private void OnTriggerStay2D(Collider2D other) => CheckHit(other);

    private void CheckHit(Collider2D other)
    {
        if (graceTimer > 0f) return;
        if (other.CompareTag("Obstacle")) {
            GameManager.Instance.GameOver();
        }
    }
}
