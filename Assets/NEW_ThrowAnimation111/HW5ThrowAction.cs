using UnityEngine;
using System.Collections;
using System.Collections.Generic;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class HW5ThrowAction : MonoBehaviour
{
    [Header("場景物件")]
    public Transform trainer;
    public Transform ball;
    public Transform pokemon;
    public ParticleSystem captureEffect;
    public Terrain terrain;
    public Camera gameCamera;

    [Header("投球設定")]
    public float flightTime = 1.2f;
    public float arcHeight = 2.5f;
    public float ballRadius = 0.25f;

    [Header("滑鼠拖曳設定")]
    public float pickRadiusPixels = 65f;
    public float minimumDragPixels = 35f;
    public float fullPowerPixels = 250f;

    private Transform visual;
    private Transform arm;
    private Transform grip;

    private Vector3 pokemonScale;

    private bool ready;
    private bool busy;
    private bool captured;
    private bool dragging;

    private Vector2 dragStart;
    private Vector2 pointer;

    private string message = "Hold the ball, drag, then release.";

    private readonly List<Renderer> oldRenderers =
        new List<Renderer>();

    private readonly List<Material> materials =
        new List<Material>();

    private Quaternion Rest
    {
        get { return Quaternion.Euler(-65f, 0f, 0f); }
    }

    void Start()
    {
        if (gameCamera == null)
        {
            gameCamera = Camera.main;
        }

        ready =
            trainer != null &&
            ball != null &&
            pokemon != null &&
            captureEffect != null &&
            gameCamera != null;

        if (!ready || ball == pokemon)
        {
            ready = false;

            Debug.LogError(
                "請指定 Trainer、Ball、Pokemon、Capture Effect、Game Camera。" +
                "Ball 和 Pokemon 不可以是同一個物件。",
                this
            );

            return;
        }

        pokemonScale = pokemon.localScale;

        CreateAnimatedTrainer();
        ResetScene();
    }

    void CreateAnimatedTrainer()
    {
        // 暫時隱藏原本人物的外觀。
        foreach (Renderer r in trainer.GetComponentsInChildren<Renderer>())
        {
            if (r.enabled)
            {
                oldRenderers.Add(r);
                r.enabled = false;
            }
        }

        visual = new GameObject("AnimatedTrainer_Runtime").transform;
        visual.SetParent(trainer, false);

        Vector3 direction = pokemon.position - trainer.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            visual.rotation = Quaternion.LookRotation(direction);
        }

        Material skin = MakeMaterial(new Color(0.87f, 0.64f, 0.43f));
        Material red = MakeMaterial(new Color(0.85f, 0.06f, 0.07f));
        Material blue = MakeMaterial(new Color(0.1f, 0.26f, 0.65f));
        Material black = MakeMaterial(new Color(0.03f, 0.04f, 0.04f));
        Material white = MakeMaterial(Color.white);

        // 身體與衣服。
        Part(
            visual, PrimitiveType.Capsule,
            new Vector3(0f, 1.25f, 0f),
            new Vector3(0.65f, 0.4f, 0.42f), blue
        );

        Part(
            visual, PrimitiveType.Cube,
            new Vector3(0f, 1.25f, 0.22f),
            new Vector3(0.12f, 0.55f, 0.04f), white
        );

        // 雙腿與鞋子。
        foreach (float x in new float[] { -0.18f, 0.18f })
        {
            Part(
                visual, PrimitiveType.Capsule,
                new Vector3(x, 0.59f, 0f),
                new Vector3(0.24f, 0.36f, 0.26f), blue
            );

            Part(
                visual, PrimitiveType.Sphere,
                new Vector3(x, 0.12f, 0.08f),
                new Vector3(0.3f, 0.24f, 0.48f), black
            );
        }

        // 頭與帽子。
        Part(
            visual, PrimitiveType.Sphere,
            new Vector3(0f, 1.94f, 0f),
            new Vector3(0.52f, 0.58f, 0.5f), skin
        );

        Part(
            visual, PrimitiveType.Sphere,
            new Vector3(0f, 2.13f, 0f),
            new Vector3(0.57f, 0.24f, 0.55f), red
        );

        Part(
            visual, PrimitiveType.Cube,
            new Vector3(0f, 2.12f, 0.27f),
            new Vector3(0.5f, 0.06f, 0.34f), red
        );

        foreach (float x in new float[] { -0.1f, 0.1f })
        {
            Part(
                visual, PrimitiveType.Sphere,
                new Vector3(x, 1.98f, 0.24f),
                new Vector3(0.06f, 0.08f, 0.04f), black
            );
        }

        // 左手。
        Part(
            visual, PrimitiveType.Capsule,
            new Vector3(-0.43f, 1.25f, 0f),
            new Vector3(0.22f, 0.3f, 0.22f), red
        );

        Part(
            visual, PrimitiveType.Sphere,
            new Vector3(-0.43f, 0.92f, 0f),
            Vector3.one * 0.23f, skin
        );

        // 右手以肩膀為旋轉中心。
        arm = new GameObject("RightShoulder").transform;
        arm.SetParent(visual, false);
        arm.localPosition = new Vector3(0.43f, 1.52f, 0f);

        Part(
            arm, PrimitiveType.Capsule,
            new Vector3(0f, -0.24f, 0f),
            new Vector3(0.22f, 0.27f, 0.22f), red
        );

        Part(
            arm, PrimitiveType.Sphere,
            new Vector3(0f, -0.56f, 0f),
            Vector3.one * 0.23f, skin
        );

        grip = new GameObject("BallGrip").transform;
        grip.SetParent(arm, false);
        grip.localPosition = new Vector3(0f, -0.7f, 0f);
    }

    Material MakeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;
        materials.Add(material);

        return material;
    }

    void Part(
        Transform parent,
        PrimitiveType type,
        Vector3 position,
        Vector3 scale,
        Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(type);

        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = scale;

        obj.GetComponent<Renderer>().sharedMaterial = material;

        Collider col = obj.GetComponent<Collider>();

        if (col != null)
        {
            col.enabled = false;
            Destroy(col);
        }
    }

    bool ReadPointer(
        out Vector2 position,
        out bool down,
        out bool held,
        out bool up)
    {
        position = Vector2.zero;
        down = false;
        held = false;
        up = false;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null)
        {
            return false;
        }

        position = Mouse.current.position.ReadValue();
        down = Mouse.current.leftButton.wasPressedThisFrame;
        held = Mouse.current.leftButton.isPressed;
        up = Mouse.current.leftButton.wasReleasedThisFrame;

        return true;

#elif ENABLE_LEGACY_INPUT_MANAGER
        position = Input.mousePosition;
        down = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
        up = Input.GetMouseButtonUp(0);

        return true;

#else
        return false;
#endif
    }

    void Update()
    {
        if (!ready || busy || captured)
        {
            return;
        }

        bool down;
        bool held;
        bool up;

        if (!ReadPointer(out pointer, out down, out held, out up))
        {
            return;
        }

        // Input 的 Y 原點在底部，GUI 的 Y 原點在頂部。
        Vector2 guiPointer = new Vector2(
            pointer.x,
            Screen.height - pointer.y
        );

        bool overReset = new Rect(20f, 20f, 200f, 55f)
            .Contains(guiPointer);

        if (down && !overReset)
        {
            Vector3 screenPosition =
                gameCamera.WorldToScreenPoint(ball.position);

            Vector2 ballScreenPosition = new Vector2(
                screenPosition.x,
                screenPosition.y
            );

            float distance =
                Vector2.Distance(pointer, ballScreenPosition);

            if (screenPosition.z > 0f &&
                distance <= pickRadiusPixels)
            {
                dragging = true;
                dragStart = pointer;

                message = "Drag to charge. Release to throw.";
            }
            else
            {
                message = "Hold the ball beside the trainer.";
            }
        }

        if (dragging && held)
        {
            float power = GetDragPower();

            arm.localRotation = Quaternion.Slerp(
                Rest,
                Quaternion.Euler(150f, 0f, 0f),
                power
            );

            ball.position = grip.position;
        }

        if (dragging && up)
        {
            dragging = false;

            float distance =
                Vector2.Distance(pointer, dragStart);

            if (distance < minimumDragPixels)
            {
                arm.localRotation = Rest;
                ball.position = grip.position;

                message = "Drag farther, then release.";
            }
            else
            {
                StartCoroutine(ThrowBall(GetDragPower()));
            }
        }
    }

    float GetDragPower()
    {
        float distance = Vector2.Distance(pointer, dragStart);

        return Mathf.Clamp01(
            distance / Mathf.Max(1f, fullPowerPixels)
        );
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && dragging)
        {
            dragging = false;

            if (arm != null && ball != null && grip != null)
            {
                arm.localRotation = Rest;
                ball.position = grip.position;
            }
        }
    }

    void OnGUI()
    {
        GUI.enabled = ready && !busy;

        if (GUI.Button(new Rect(20f, 20f, 200f, 55f), "RESET"))
        {
            ResetScene();
        }

        GUI.enabled = true;

        float messageWidth = Mathf.Max(
            200f,
            Mathf.Min(520f, Screen.width - 40f)
        );

        GUI.Box(
            new Rect(20f, 85f, messageWidth, 55f),
            ready
                ? message
                : "Assign references, then restart Play."
        );

        if (ready && !busy && !captured)
        {
            Vector3 screenPosition =
                gameCamera.WorldToScreenPoint(ball.position);

            if (screenPosition.z > 0f)
            {
                GUI.Label(
                    new Rect(
                        screenPosition.x - 45f,
                        Screen.height - screenPosition.y - 40f,
                        150f,
                        30f
                    ),
                    "HOLD BALL"
                );
            }
        }

        if (dragging)
        {
            GUI.Box(
                new Rect(20f, 150f, 240f, 35f),
                "POWER: " +
                Mathf.RoundToInt(GetDragPower() * 100f) +
                "%"
            );
        }
    }

    void ResetScene()
    {
        dragging = false;

        pokemon.gameObject.SetActive(true);
        pokemon.localScale = pokemonScale;

        arm.localRotation = Rest;
        ball.position = grip.position;

        captureEffect.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        captured = false;
        message = "Hold the ball, drag, then release.";
    }

    IEnumerator ThrowBall(float power)
    {
        busy = true;
        message = "Throwing...";

        // 放開滑鼠後，先揮手到出手位置。
        Quaternion startArm = arm.localRotation;
        float elapsed = 0f;

        while (elapsed < 0.18f)
        {
            elapsed += Time.deltaTime;

            arm.localRotation = Quaternion.Slerp(
                startArm,
                Quaternion.Euler(235f, 0f, 0f),
                Mathf.Clamp01(elapsed / 0.18f)
            );

            ball.position = grip.position;

            yield return null;
        }

        Vector3 from = ball.position;
        Vector3 target = pokemon.position + Vector3.up;

        Quaternion releaseRotation = arm.localRotation;

        float duration = Mathf.Max(
            0.2f,
            flightTime * Mathf.Lerp(1.25f, 0.7f, power)
        );

        float height =
            arcHeight * Mathf.Lerp(0.65f, 1.35f, power);

        elapsed = 0f;

        // 球離手後沿拋物線飛行。
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 position = Vector3.Lerp(from, target, t);
            position.y += 4f * height * t * (1f - t);

            ball.position = position;

            ball.Rotate(
                0f,
                0f,
                -600f * Time.deltaTime,
                Space.World
            );

            arm.localRotation = Quaternion.Slerp(
                releaseRotation,
                Rest,
                Mathf.Clamp01(elapsed / 0.5f)
            );

            yield return null;
        }

        ball.position = target;
        arm.localRotation = Rest;

        captureEffect.transform.position = target;
        captureEffect.Play();

        // 寶可夢縮小消失。
        elapsed = 0f;

        while (elapsed < 0.6f)
        {
            elapsed += Time.deltaTime;

            pokemon.localScale = Vector3.Lerp(
                pokemonScale,
                Vector3.zero,
                Mathf.Clamp01(elapsed / 0.6f)
            );

            yield return null;
        }

        pokemon.gameObject.SetActive(false);

        // 精靈球落到地面。
        float floorHeight = pokemon.position.y;

        if (terrain != null)
        {
            floorHeight =
                terrain.SampleHeight(target) +
                terrain.transform.position.y;
        }

        Vector3 landingPosition = new Vector3(
            target.x,
            floorHeight + ballRadius,
            target.z
        );

        elapsed = 0f;

        while (elapsed < 0.5f)
        {
            elapsed += Time.deltaTime;

            ball.position = Vector3.Lerp(
                target,
                landingPosition,
                Mathf.Clamp01(elapsed / 0.5f)
            );

            yield return null;
        }

        ball.position = landingPosition;

        captured = true;
        busy = false;
        message = "Captured! Press RESET to try again.";
    }

    void OnDestroy()
    {
        foreach (Renderer r in oldRenderers)
        {
            if (r != null)
            {
                r.enabled = true;
            }
        }

        if (visual != null)
        {
            Destroy(visual.gameObject);
        }

        foreach (Material material in materials)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}