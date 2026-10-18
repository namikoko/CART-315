using UnityEngine;

public class Spawner : MonoBehaviour
{
    public Pipes prefab;
    public float spawnRate = 1f;
    public float minHeight = -1f;
    public float maxHeight = 2f;
    public float verticalGap = 3f;

    [Header("Snake Bird: treats")]
    [Range(0f, 1f)] public float treatChance = 0.8f;
    [Tooltip("Chance a treat sits right in the pipe gap instead of between pipes")]
    [Range(0f, 1f)] public float inGapChance = 0.3f;
    public float treatMinY = -2.4f;
    public float treatMaxY = 3.4f;

    private Sprite[] treatSprites;

    private void Awake()
    {
        treatSprites = Resources.LoadAll<Sprite>("Treats");
    }

    private void OnEnable()
    {
        InvokeRepeating(nameof(Spawn), spawnRate, spawnRate);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(Spawn));
    }

    private void Spawn()
    {
        Pipes pipes = Instantiate(prefab, transform.position, Quaternion.identity);
        pipes.transform.position += Vector3.up * Random.Range(minHeight, maxHeight);
        pipes.gap = verticalGap;

        SpawnTreat(pipes);
    }

    private void SpawnTreat(Pipes pipes)
    {
        if (treatSprites == null || treatSprites.Length == 0) return;
        if (Random.value > treatChance) return;

        Sprite sprite = treatSprites[Random.Range(0, treatSprites.Length)];
        Vector3 local;

        if (Random.value < inGapChance) {
            // right in the middle of the gap: easy to grab, but you have to commit
            local = Vector3.zero;
        } else {
            // halfway to the next pipe, at a random height: you have to swoop for it
            float halfway = pipes.speed * spawnRate * 0.5f;
            float worldY = Random.Range(treatMinY, treatMaxY);
            local = new Vector3(halfway, worldY - pipes.transform.position.y, 0f);
        }

        Treat.Create(sprite, pipes.transform, local);
    }

}
