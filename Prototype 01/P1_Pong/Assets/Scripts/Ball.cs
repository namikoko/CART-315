using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class Ball : MonoBehaviour
{
    private Rigidbody2D _rigidBody;
    private Vector2 _lastVelocity;
    private float _currentSpeed;
    private bool _inPlay;

    public float startSpeed = 7.0f;            // World units per second when a round starts
    public float speedIncreasePerHit = 0.35f;  // Added on every paddle hit, like Piano Tiles getting faster
    public float maxSpeed = 22.0f;
    public float maxBounceAngle = 55.0f;       // Angle when the ball hits the very edge of a paddle

    public event Action<Paddle> PaddleHit;

    private void Awake()
    {
        _rigidBody = GetComponent<Rigidbody2D>();
        _rigidBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    // FixedUpdate is called once per physics update
    private void FixedUpdate()
    {
        if (!_inPlay) return;

        Vector2 direction = _rigidBody.linearVelocity.normalized;
        if (direction == Vector2.zero) direction = _lastVelocity.normalized;

        // Never let the ball go nearly straight up and down
        if (Mathf.Abs(direction.x) < 0.3f)
        {
            direction.x = (direction.x < 0 ? -1.0f : 1.0f) * 0.3f;
            direction = direction.normalized;
        }

        _rigidBody.linearVelocity = direction * _currentSpeed;
        _lastVelocity = _rigidBody.linearVelocity;
    }

    public void ResetBall()
    {
        _inPlay = false;
        _rigidBody.linearVelocity = Vector2.zero;
        _rigidBody.angularVelocity = 0;
        _lastVelocity = Vector2.zero;
        _rigidBody.position = Vector2.zero;
        transform.position = Vector3.zero;
    }

    // Serve the ball. xDirection = -1 sends it left (towards the player), 1 sends it right.
    public void Launch(float xDirection)
    {
        float y = (Random.value < 0.5f ? -1.0f : 1.0f) * Random.Range(0.3f, 0.6f);
        Vector2 direction = new Vector2(xDirection, y).normalized;

        _currentSpeed = startSpeed;
        _rigidBody.linearVelocity = direction * _currentSpeed;
        _lastVelocity = _rigidBody.linearVelocity;
        _inPlay = true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_inPlay) return;

        Paddle paddle = collision.gameObject.GetComponent<Paddle>();
        if (paddle != null)
        {
            _currentSpeed = Mathf.Min(_currentSpeed + speedIncreasePerHit, maxSpeed);

            // Where on the paddle did we hit? -1 = bottom edge, 0 = middle, 1 = top edge
            float paddleHalfHeight = collision.collider.bounds.extents.y;
            float offset = (transform.position.y - paddle.transform.position.y) / paddleHalfHeight;
            offset = Mathf.Clamp(offset, -1.0f, 1.0f);

            // Send the ball back the other way, angled by where it hit
            float angle = offset * maxBounceAngle * Mathf.Deg2Rad;
            float xDirection = paddle.transform.position.x < transform.position.x ? 1.0f : -1.0f;
            Vector2 direction = new Vector2(xDirection * Mathf.Cos(angle), Mathf.Sin(angle));

            _rigidBody.linearVelocity = direction * _currentSpeed;
            _lastVelocity = _rigidBody.linearVelocity;

            PaddleHit?.Invoke(paddle);
        }
        else
        {
            // Walls: bounce off cleanly without losing speed
            Vector2 normal = collision.GetContact(0).normal;
            _rigidBody.linearVelocity = Vector2.Reflect(_lastVelocity, normal).normalized * _currentSpeed;
            _lastVelocity = _rigidBody.linearVelocity;
        }
    }
}
