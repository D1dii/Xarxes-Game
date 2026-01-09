using UnityEngine;
using UnityEngine.InputSystem;

public class CameraControllerMouse : MonoBehaviour
{
    [Header("Objetivos")]
    public Transform target; // Arrastra a tu personaje aquí

    [Header("Configuración")]
    public float distance = 5f;
    public float minDistance = 1f;
    public float sensitivity = 0.5f; // Ajusta esto si va muy rápido o lento
    public float smoothTime = 0.1f;
    public Vector2 verticalLimits = new Vector2(-40f, 70f);
    public LayerMask collisionLayers; // Capas con las que choca (Default, Ground, Walls)

    // Estado interno
    private Vector2 currentRotation;
    private Vector2 rotationVelocity;
    private Vector2 inputDelta;
    private PlayerInputActions inputActions;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void Start()
    {
        // --- ESTO ES CLAVE PARA PC ---
        // Oculta el cursor y lo bloquea en el centro para que puedas girar infinitamente
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Inicializamos la rotación actual para que no pegue un salto al empezar
        currentRotation = new Vector2(transform.eulerAngles.x, transform.eulerAngles.y);
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();

        // Leemos el movimiento del ratón (Delta)
        inputActions.Player.Look.performed += ctx => inputDelta = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += ctx => inputDelta = Vector2.zero;
    }

    private void OnDisable()
    {
        inputActions.Player.Disable();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Desbloquear ratón con ESC (útil para pruebas)
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Si pulsas clic en pantalla, volvemos a bloquear (opcional, buena práctica)
        if (Mouse.current.leftButton.wasPressedThisFrame && Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        HandleRotation();
        HandleCameraPosition();
    }

    void HandleRotation()
    {
        // Nota: En ratón, a veces hay que invertir la Y. Si va al revés, pon un + en vez de -
        float targetPitch = currentRotation.x - (inputDelta.y * sensitivity);
        float targetYaw = currentRotation.y + (inputDelta.x * sensitivity);

        // Limitamos para que no dé la vuelta completa verticalmente
        targetPitch = Mathf.Clamp(targetPitch, verticalLimits.x, verticalLimits.y);

        // Suavizado
        currentRotation.x = Mathf.SmoothDamp(currentRotation.x, targetPitch, ref rotationVelocity.x, smoothTime);
        currentRotation.y = Mathf.SmoothDamp(currentRotation.y, targetYaw, ref rotationVelocity.y, smoothTime);
    }

    void HandleCameraPosition()
    {
        Quaternion rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0);
        Vector3 direction = rotation * -Vector3.forward;

        // Pivote: Miramos al cuello/cabeza del player, no a los pies
        Vector3 pivotPoint = target.position + Vector3.up * 1.5f;
        float finalDistance = distance;

        // Sistema anti-paredes
        RaycastHit hit;
        if (Physics.SphereCast(pivotPoint, 0.2f, direction, out hit, distance, collisionLayers))
        {
            finalDistance = hit.distance;
        }

        finalDistance = Mathf.Max(finalDistance, minDistance);

        transform.position = pivotPoint + (direction * finalDistance);
        transform.LookAt(pivotPoint);
    }
}