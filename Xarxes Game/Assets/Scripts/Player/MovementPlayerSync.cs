using UnityEngine;

public class PlayerAnimationSyncer : MonoBehaviour
{
    public Animator animator;
    public Rigidbody rb;
    private PlayerNetwork netObj;

    private Vector3 lastPosition;

    // Umbrales para detectar movimiento
    float moveThreshold = 0.05f;
    float jumpThreshold = 0.5f;

    void Awake()
    {
        // Intenta buscar las referencias si están vacías
        if (animator == null) animator = GetComponent<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        netObj = GetComponent<PlayerNetwork>();
    }

    void Start()
    {
        lastPosition = transform.position;
    }

    void Update()
    {
        if (netObj == null) return;

        Vector3 velocity = Vector3.zero;

        // ESTRATEGIA: Calcular velocidad siempre por posición para ser consistentes
        // tanto en local como en remoto. Es más fiable visualmente.
        Vector3 displacement = transform.position - lastPosition;
        velocity = displacement / Time.deltaTime;

        // Guardamos posición para el siguiente frame
        lastPosition = transform.position;

        // --- 1. MOVIMIENTO (Suelo) ---
        // Solo nos importa la velocidad horizontal (X y Z)
        Vector3 horizontalVel = new Vector3(velocity.x, 0, velocity.z);
        bool isMoving = horizontalVel.magnitude > moveThreshold;

        animator.SetBool("Moving", isMoving);

        // Debug para ver si detecta movimiento en consola
        // Debug.Log($"Player {gameObject.name} Speed: {horizontalVel.magnitude} | Moving: {isMoving}");


        // --- 2. SALTO / CAÍDA (Vertical) ---
        float yVel = velocity.y;

        // Limpiamos estados
        animator.SetBool("Jumping", false);
        animator.SetBool("Falling", false);

        if (yVel > jumpThreshold)
        {
            animator.SetBool("Jumping", true);
        }
        else if (yVel < -jumpThreshold)
        {
            animator.SetBool("Falling", true);
        }
    }
}