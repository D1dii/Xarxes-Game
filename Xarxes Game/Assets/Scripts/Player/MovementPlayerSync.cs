using UnityEngine;

public class RemotePlayerAnimator : MonoBehaviour
{
    private Animator anim;
    private Vector3 lastPosition;

    // Ajusta esto según el tamaño de tu personaje (mínimo movimiento para animar)
    public float minSpeedToWalk = 0.1f;

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        // Guardamos la posición inicial para no tener errores en el primer frame
        lastPosition = transform.position;
    }

    void Update()
    {
        // 1. Calculamos cuánto se ha movido desde el último frame
        Vector3 displacement = transform.position - lastPosition;

        // 2. Calculamos la velocidad REAL (distancia / tiempo)
        // Usamos magnitude para saber la velocidad total
        float currentSpeed = displacement.magnitude / Time.deltaTime;

        // 3. Separamos la velocidad Horizontal (Caminar) de la Vertical (Saltar)

        // -- MOVIMIENTO HORIZONTAL (X y Z) --
        // Creamos un vector que ignora la altura (Y)
        Vector3 horizontalMove = new Vector3(displacement.x, 0, displacement.z);
        float horizontalSpeed = horizontalMove.magnitude / Time.deltaTime;

        // Enviamos al Animator (puedes usar un Bool o un Float para Blend Tree)
        anim.SetBool("isWalking", horizontalSpeed > minSpeedToWalk);
        // O si usas Blend Tree: anim.SetFloat("Speed", horizontalSpeed);


        // -- MOVIMIENTO VERTICAL (SALTO) --
        float verticalSpeed = displacement.y / Time.deltaTime;

        // Si sube rápido (Salto)
        if (verticalSpeed > 2.0f) // Ajusta este 2.0f a tu gusto
        {
            anim.SetBool("isJumping", true);
        }
        // Si ya no sube o está bajando, quitamos la animación de salto
        else if (verticalSpeed <= 0.1f)
        {
            anim.SetBool("isJumping", false);
        }

        // 4. IMPORTANTE: Actualizar la última posición para el siguiente frame
        lastPosition = transform.position;
    }
}