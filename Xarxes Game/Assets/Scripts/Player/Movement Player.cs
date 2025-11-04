using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementRB : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float rotationSmoothTime = 0.1f;

    [Header("C?mara")]
    public Transform cameraTransform;

    [Header("Salto")]
    public float jumpForce = 5f;
    public LayerMask groundMask;
    public float groundCheckDistance = 0.3f;

    private Rigidbody rb;
    private PlayerInputActions inputActions;
    private Vector2 moveInput;
    private bool jumpPressed;
    private bool isGrounded;
    private float turnSmoothVelocity;

    private NetworkObject netObj;
    public GameObject playerCamera;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // evita que se caiga o gire por f?sicas

        inputActions = new PlayerInputActions();
        netObj = GetComponent<NetworkObject>();
    }

    private void Start()
    {
        if (netObj.isLocalPlayer)
        {
            playerCamera.SetActive(true);
        }
        else
        {
            playerCamera.SetActive(false);
        }
    }


    void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        inputActions.Player.Jump.performed += ctx => jumpPressed = true;
    }

    void OnDisable()
    {
        inputActions.Player.Disable();
    }

    void FixedUpdate()
    {
        if (netObj.isLocalPlayer)
        {
            MovePlayer();
            HandleJump();
        }

    }

    void MovePlayer()
    {
        // Movimiento en el plano XZ relativo a la c?mara
        Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        if (direction.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, rotationSmoothTime);

            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            Vector3 targetVelocity = moveDir * moveSpeed;

            // Solo modificamos la velocidad horizontal, mantenemos la vertical
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        }
        else
        {
            // Si no hay input, solo dejamos la velocidad vertical (ca?da o salto)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    void HandleJump()
    {
        // Comprobamos si est? tocando el suelo
        isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance, groundMask);

        if (jumpPressed && isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z); // reset vertical
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        jumpPressed = false; // reseteamos el estado del salto
    }

    void OnDrawGizmosSelected()
    {
        // Dibuja el rayo de suelo en el editor para depuraci?n
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, transform.position + Vector3.up * 0.1f + Vector3.down * groundCheckDistance);
    }
}