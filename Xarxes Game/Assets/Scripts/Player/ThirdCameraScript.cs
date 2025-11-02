using UnityEngine;
using UnityEngine.InputSystem;
public class ThirdCameraScript : MonoBehaviour
{
    public Transform target;
    public float distance = 4f;
    public float height = 2f;
    public float sensitivityX = 120f;
    public float sensitivityY = 120f;
    public float minY = -30f;
    public float maxY = 70f;

    private float rotX;
    private float rotY;
    private PlayerInputActions inputActions;
    private Vector2 lookInput;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;
    }

    private void OnDisable()
    {
        inputActions.Player.Disable();
    }

    void LateUpdate()
    {
        rotY += lookInput.x * sensitivityX * Time.deltaTime;
        rotX -= lookInput.y * sensitivityY * Time.deltaTime;
        rotX = Mathf.Clamp(rotX, minY, maxY);

        Quaternion rotation = Quaternion.Euler(rotX, rotY, 0);
        Vector3 offset = rotation * new Vector3(0, height, -distance);

        transform.position = target.position + offset;
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }
}
