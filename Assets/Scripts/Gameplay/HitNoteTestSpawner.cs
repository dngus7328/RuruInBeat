using UnityEngine;
using UnityEngine.InputSystem;

public class HitNoteTestSpawner : MonoBehaviour
{
    [Header("Slots")]
    [SerializeField] private Transform[] slots = new Transform[4];

    [Header("Spawn")]
    [SerializeField] private float spawnY = 4.5f;

    [SerializeField]
    private Vector2 noteSize =
        new Vector2(0.7f, 0.7f);

    [SerializeField] private float moveSpeed = 4f;

    [SerializeField] private float destroyY = -5f;

    private void Update()
    {
        Keyboard keyboard =
            Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.digit1Key.wasPressedThisFrame)
            Spawn(0);

        if (keyboard.digit2Key.wasPressedThisFrame)
            Spawn(1);

        if (keyboard.digit3Key.wasPressedThisFrame)
            Spawn(2);

        if (keyboard.digit4Key.wasPressedThisFrame)
            Spawn(3);
    }

    private void Spawn(int slotIndex)
    {
        if (slots == null ||
            slotIndex < 0 ||
            slotIndex >= slots.Length)
        {
            return;
        }

        if (slots[slotIndex] == null)
            return;

        GameObject hitNote =
            new GameObject(
                $"HitNote_Test_{slotIndex + 1}"
            );

        hitNote.transform.position =
            new Vector3(
                slots[slotIndex].position.x,
                spawnY,
                0f
            );

        hitNote.transform.localScale =
            new Vector3(
                noteSize.x,
                noteSize.y,
                1f
            );

        // -------------------------
        // Sprite
        // -------------------------

        SpriteRenderer renderer =
            hitNote.AddComponent<SpriteRenderer>();

        renderer.sprite =
            CreateSquareSprite();

        renderer.color =
            Color.red;

        renderer.sortingOrder =
            20;

        // -------------------------
        // Collider
        // -------------------------

        BoxCollider2D collider =
            hitNote.AddComponent<BoxCollider2D>();

        collider.isTrigger =
            true;

        // -------------------------
        // Rigidbody
        // -------------------------

        Rigidbody2D body =
            hitNote.AddComponent<Rigidbody2D>();

        body.bodyType =
            RigidbodyType2D.Kinematic;

        body.gravityScale =
            0f;

        // -------------------------
        // Runtime
        // -------------------------

        HitNoteRuntime runtime =
            hitNote.AddComponent<HitNoteRuntime>();

        runtime.Setup(
            moveSpeed,
            destroyY
        );
    }

    private Sprite CreateSquareSprite()
    {
        Texture2D texture =
            new Texture2D(
                1,
                1
            );

        texture.SetPixel(
            0,
            0,
            Color.white
        );

        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(
                0,
                0,
                1,
                1
            ),
            new Vector2(
                0.5f,
                0.5f
            ),
            1f
        );
    }
}