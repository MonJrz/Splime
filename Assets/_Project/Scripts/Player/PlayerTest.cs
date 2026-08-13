using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerTest : MonoBehaviour
{
    public InputAction moveAction;
    public InputAction jumpAction;

    [Header("Movement")]
    public float speed = 15f;
    public float boundaryX = 10f;
    public float boundaryZ = 20f;

    [Header("Jump")]
    public float jumpForce = 7f;

    [Header("Ground Check")]
    // public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    private Vector2 moveInput;
    private Rigidbody rb;

    private bool jumpRequested;
    private bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
    }

    void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();

        // Comprobar si estamos tocando el suelo
        // isGrounded = Physics.CheckSphere(
        //     groundCheck.position,
        //     groundCheckRadius,
        //     groundLayer
        // );

        // Guardamos la petición para ejecutarla en FixedUpdate
        if (jumpAction.WasPressedThisFrame() && isGrounded)
        {
            jumpRequested = true;
        }
    }

    void FixedUpdate()
    {
        Vector3 movement = new Vector3(
            moveInput.x,
            0f,
            moveInput.y
        );

        Vector3 newPosition =
            rb.position +
            movement * speed * Time.fixedDeltaTime;

        newPosition.x = Mathf.Clamp(
            newPosition.x,
            -boundaryX,
            boundaryX
        );

        newPosition.z = Mathf.Clamp(
            newPosition.z,
            -boundaryZ,
            boundaryZ
        );

        rb.MovePosition(newPosition);

        // Salto
        if (jumpRequested)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            jumpRequested = false;
        }
    }

    // Permite visualizar el Ground Check en la escena
    // void OnDrawGizmosSelected()
    // {
    //     if (groundCheck == null)
    //         return;

    //     Gizmos.DrawWireSphere(
    //         groundCheck.position,
    //         groundCheckRadius
    //     );
    // }
}