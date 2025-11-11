using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementRB : MonoBehaviour
{
    public Animator animatotor;

    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float rotationSmoothTime = 0.1f;

    [Header("Cámara")]
    public Transform cameraTransform;

    [Header("Salto")]
    public float jumpForce = 5f;
    public LayerMask groundMask;
    public float groundCheckDistance = 0.3f;

    [Header("Dash")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.3f;
    public float dashPushForce = 15f;
    public float dashCooldown = 1.0f;
    private bool isDashing = false;
    private bool canDash = true;

    [Header("Agarre")]
    public float grabRange = 2f;
    public float grabThrowForce = 30f;
    public LayerMask grabMask;
    private Rigidbody grabbedObjectRb;
    private FixedJoint grabJoint;
    private bool isGrabbing = false;

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
        rb.freezeRotation = true;

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
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector3.zero;
        inputActions.Player.Jump.performed += ctx => jumpPressed = true;
        inputActions.Player.Dash.performed += HandleDashInput;
        inputActions.Player.Grab.performed += HandleGrabInput;
    }

    void OnDisable()
    {
        inputActions.Player.Disable();
        inputActions.Player.Dash.performed -= HandleDashInput;
        inputActions.Player.Grab.performed -= HandleGrabInput;
    }

    void FixedUpdate()
    {
        if (netObj.isLocalPlayer)
        {
            if (!isDashing)
            {
                MovePlayer();
            }
            HandleJump();
        }
    }

    void MovePlayer()
    {
        Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        if (direction.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, rotationSmoothTime);

            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            Vector3 targetVelocity = moveDir * moveSpeed;

            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
            animatotor.SetBool("Moving", true);
        }
        else
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            animatotor.SetBool("Moving", false);
        }

        if (isGrounded)
        {
            animatotor.SetBool("Jumping", false);
            animatotor.SetBool("Falling", false);
        }
        else if (rb.linearVelocity.y > 0.1f)
        {
            animatotor.SetBool("Jumping", true);
            animatotor.SetBool("Falling", false);
        }
        else if (rb.linearVelocity.y < -0.1f)
        {
            animatotor.SetBool("Jumping", false);
            animatotor.SetBool("Falling", true);
        }
    }

    void HandleJump()
    {
        isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance, groundMask);

        if (jumpPressed && isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
        jumpPressed = false;
    }

    [System.Obsolete]
    private void HandleDashInput(InputAction.CallbackContext context)
    {
        if (isGrabbing && grabbedObjectRb != null)
            _Handle_Throw_();
        else if (canDash && netObj.isLocalPlayer)
            StartCoroutine(DashCoroutine());
    }

    private void HandleGrabInput(InputAction.CallbackContext context)
    {
        if (!isGrabbing)
            _Try_Grab_();
        else
            _Release_Grab_();
    }

    private void _Try_Grab_()
    {
        Vector3 grabPosition = transform.position + transform.forward * 1.0f;
        Collider[] hits = Physics.OverlapSphere(grabPosition, grabRange, grabMask);

        foreach (Collider hit in hits)
        {
            Rigidbody hitRb = hit.GetComponent<Rigidbody>();
            if (hitRb != null && hitRb != rb && !hitRb.isKinematic)
            {
                grabbedObjectRb = hitRb;
                isGrabbing = true;

                grabJoint = gameObject.AddComponent<FixedJoint>();
                grabJoint.connectedBody = grabbedObjectRb;
                grabJoint.breakForce = Mathf.Infinity;
                grabJoint.breakTorque = Mathf.Infinity;

                var hitNetObj = hitRb.GetComponent<NetworkObject>();
                if (hitNetObj != null)
                {
                    // Pedir ownership al servidor para poder mover ese objeto
                    NetworkManager.instance.RequestOwnership(hitNetObj.id);
                }

                animatotor.SetBool("Grabbing", true);
                break;
            }
        }
    }

    private void _Release_Grab_()
    {
        if (grabJoint != null)
        {
            // notificar al servidor que liberamos la propiedad antes de eliminar el joint
            if (grabbedObjectRb != null)
            {
                var hitNetObj = grabbedObjectRb.GetComponent<NetworkObject>();
                if (hitNetObj != null)
                {
                    NetworkManager.instance.RequestReleaseOwnership(hitNetObj.id);
                    // localmente dejar de considerar que lo controlamos
                    hitNetObj.isLocalPlayer = false;
                }
            }

            Destroy(grabJoint);
        }

        grabbedObjectRb = null;
        isGrabbing = false;
        animatotor.SetBool("Grabbing", false);
    }

    private void _Handle_Throw_()
    {
        Vector3 throwDirection = (cameraTransform.forward + Vector3.up * 0.2f).normalized;

        Rigidbody tempRb = grabbedObjectRb;

        _Release_Grab_();

        if (tempRb != null)
        {
            tempRb.AddForce(throwDirection * grabThrowForce, ForceMode.Impulse);
        }
    }

    [System.Obsolete]
    private IEnumerator DashCoroutine()
    {
        canDash = false;
        isDashing = true;
        animatotor.SetBool("Dashing", true);

        bool originalGravity = rb.useGravity;
        rb.useGravity = false;

        float verticalVelocity = rb.velocity.y;

        Vector3 dashDirection;
        Vector3 moveDirectionInput = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        if (moveDirectionInput.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(moveDirectionInput.x, moveDirectionInput.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            dashDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            animatotor.SetBool("Sprint", true);
        }
        else
        {
            dashDirection = transform.forward;
        }

        Vector3 dashVelocity = dashDirection * dashSpeed;

        float dashTimer = 0f;
        while (dashTimer < dashDuration)
        {
            rb.velocity = new Vector3(dashVelocity.x, verticalVelocity, dashVelocity.z);

            dashTimer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        isDashing = false;
        animatotor.SetBool("Sprint", false);
        animatotor.SetBool("Dashing", false);
        rb.useGravity = originalGravity;

        rb.velocity = new Vector3(0f, rb.velocity.y, 0f);

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Bouncy"))
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce * 1.2f, ForceMode.Impulse);
        }

        if (isDashing)
        {
            Rigidbody hitRb = collision.gameObject.GetComponent<Rigidbody>();

            if (hitRb != null && hitRb != rb && !hitRb.isKinematic)
            {
                Vector3 pushDirection = (collision.transform.position - transform.position).normalized;
                pushDirection.y = 0.1f;
                hitRb.AddForce(pushDirection * dashPushForce, ForceMode.Impulse);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, transform.position + Vector3.up * 0.1f + Vector3.down * groundCheckDistance);

        Gizmos.color = Color.blue;
        Vector3 grabPosition = transform.position + transform.forward * 1.0f;
        Gizmos.DrawWireSphere(grabPosition, grabRange);
    }
}