using UnityEngine;

public class CPUController : MonoBehaviour
{
    public Ball ball;
    public Paddle paddle;

    public bool neverMiss = true;   // Paddle follows the ball exactly
    public float courtLimitY = 3.68f;  // Keeps the paddle between the top and bottom walls

    private Rigidbody2D _paddleRigidBody;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _paddleRigidBody = paddle.GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        if (neverMiss) return;

        Vector2 ballPos = ball.transform.position;
        Vector2 paddlePos = paddle.transform.position;

        paddle.direction = new Vector2(0.0f, (ballPos - paddlePos).y);
    }

    // FixedUpdate is called once per physics update
    void FixedUpdate()
    {
        if (!neverMiss) return;

        paddle.direction = Vector2.zero;

        float targetY = Mathf.Clamp(ball.transform.position.y, -courtLimitY, courtLimitY);
        _paddleRigidBody.linearVelocity = Vector2.zero;
        _paddleRigidBody.MovePosition(new Vector2(_paddleRigidBody.position.x, targetY));
    }
}
