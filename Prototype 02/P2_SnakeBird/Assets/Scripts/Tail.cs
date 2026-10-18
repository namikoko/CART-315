using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SNAKE BIRD MOD: a parade of baby chicks that follows the bird, like a snake's tail.
/// Every treat you eat adds a chick. The chicks lag behind the bird a little, so
/// big flaps whip the tail around, and if any chick touches a pipe it's game over.
/// </summary>
public class Tail : MonoBehaviour
{
    [Tooltip("Horizontal distance between chicks")]
    public float spacing = 0.42f;

    [Tooltip("How quickly each chick catches up vertically. Lower = floppier tail = harder")]
    public float followSharpness = 13f;

    [Tooltip("Max number of chicks in the parade")]
    public int maxLength = 15;

    [Tooltip("Seconds a new chick is safe (blinks) before it can hit pipes")]
    public float spawnGrace = 0.6f;

    private readonly List<TailSegment> segments = new List<TailSegment>();
    private Sprite[] chickSprites;

    public int Count => segments.Count;
    public bool IsFull => segments.Count >= maxLength;

    private void Awake()
    {
        chickSprites = Resources.LoadAll<Sprite>("Chicks");
        System.Array.Sort(chickSprites, (a, b) => string.CompareOrdinal(a.name, b.name)); // pink first
    }

    public void Grow()
    {
        if (IsFull) return;

        Vector3 spawnPos = segments.Count > 0
            ? segments[segments.Count - 1].transform.position
            : transform.position;
        spawnPos.z = transform.position.z + 0.1f; // just behind the bird

        GameObject go = new GameObject("Baby Chick " + (segments.Count + 1));
        go.transform.position = spawnPos;
        go.transform.localScale = Vector3.one;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        if (chickSprites != null && chickSprites.Length > 0) {
            sr.sprite = chickSprites[segments.Count % chickSprites.Length];
        }
        sr.sortingOrder = 1;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.17f; // a little forgiving

        TailSegment seg = go.AddComponent<TailSegment>();
        seg.Init(sr, spawnGrace, segments.Count);
        segments.Add(seg);
    }

    public void Clear()
    {
        for (int i = 0; i < segments.Count; i++) {
            if (segments[i] != null) Destroy(segments[i].gameObject);
        }
        segments.Clear();
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // paused

        float t = 1f - Mathf.Exp(-followSharpness * dt);
        Vector3 leader = transform.position;

        for (int i = 0; i < segments.Count; i++)
        {
            Transform seg = segments[i].transform;
            Vector3 p = seg.position;
            float oldY = p.y;

            p.x = leader.x - spacing;
            p.y = Mathf.Lerp(p.y, leader.y, t);
            p.z = transform.position.z + 0.1f + i * 0.01f;
            seg.position = p;

            // tilt each chick with its own vertical motion, like the bird does
            float vy = (p.y - oldY) / dt;
            seg.rotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(vy * 4f, -35f, 35f));

            leader = p;
        }
    }
}
