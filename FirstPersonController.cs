using UnityEngine;
using UnityEngine.InputSystem; // ✅ New Input System

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    // 🔑 same key as the menu
    private const string SENS_KEY = "player_sensitivity";

    [Header("Movement Settings")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 7f;
    public float crouchSpeed = 2f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;

    [Header("Look Settings")]
    public Transform playerCamera;            // 👈 stays Transform
    public float lookSensitivity = 1.5f;      // 👈 will be overwritten by saved value
    public float verticalLookLimit = 85f;

    [Header("Crouch Settings")]
    public float crouchHeightMultiplier = 0.5f;

    [Header("Animation Settings")]
    public Animator animator;

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation;
    private bool isGrounded;
    private bool isSprinting;
    private bool isCrouching;
    private float originalHeight;
    private Vector3 originalCenter;

    // --- Input actions ---
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        originalHeight = controller.height;
        originalCenter = controller.center;

        // ✅ Fix: assign camera’s Transform properly
        if (!playerCamera && Camera.main != null)
            playerCamera = Camera.main.transform;

        // --- Input setup (new Input System) ---
        moveAction   = new InputAction("Move",   InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        lookAction   = new InputAction("Look",   InputActionType.Value, "<Mouse>/delta");
        jumpAction   = new InputAction("Jump",   InputActionType.Button, "<Keyboard>/space");
        sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
        crouchAction = new InputAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");

        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
        sprintAction.Enable();
        crouchAction.Enable();

        // ✅ pick up saved sensitivity (default to current value if none saved)
        float savedSens = PlayerPrefs.GetFloat(SENS_KEY, lookSensitivity);
        lookSensitivity = savedSens;
        // Debug.Log($"🖱 FPS controller using sensitivity: {lookSensitivity}");

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();
        HandleCrouch();
        HandleJumpAndGravity();
        UpdateAnimator();
    }

    // -----------------------------
    // CAMERA LOOK
    // -----------------------------
    private void HandleLook()
    {
        if (!playerCamera) return;

        Vector2 lookDelta = lookAction.ReadValue<Vector2>() * lookSensitivity;
        xRotation -= lookDelta.y;
        xRotation = Mathf.Clamp(xRotation, -verticalLookLimit, verticalLookLimit);

        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * lookDelta.x);
    }

    // -----------------------------
    // MOVEMENT
    // -----------------------------
    private void HandleMovement()
    {
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

        // ✅ Normalize so diagonals aren’t faster
        if (move.magnitude > 1f)
            move.Normalize();

        isGrounded = controller.isGrounded;

        bool sprintHeld = sprintAction.IsPressed();
        isSprinting = sprintHeld && !isCrouching && moveInput.y > 0.1f && isGrounded;

        float currentSpeed = isCrouching ? crouchSpeed :
                             isSprinting ? sprintSpeed : walkSpeed;

        controller.Move(move * currentSpeed * Time.deltaTime);
    }

    // -----------------------------
    // CROUCH
    // -----------------------------
    private void HandleCrouch()
    {
        // ✅ Optional: only allow crouch on ground
        if (!isGrounded)
            return;

        if (crouchAction.WasPressedThisFrame())
        {
            if (!isCrouching)
            {
                controller.height = originalHeight * crouchHeightMultiplier;
                controller.center = new Vector3(
                    originalCenter.x,
                    originalCenter.y * crouchHeightMultiplier, // ✅ cleaner
                    originalCenter.z
                );
                isCrouching = true;
            }
            else
            {
                controller.height = originalHeight;
                controller.center = originalCenter;
                isCrouching = false;
            }
        }
    }

    // -----------------------------
    // JUMP / GRAVITY
    // -----------------------------
    private void HandleJumpAndGravity()
    {
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        if (jumpAction.WasPressedThisFrame() && isGrounded && !isCrouching)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // -----------------------------
    // ANIMATION HOOKS
    // -----------------------------
    private void UpdateAnimator()
    {
        if (!animator) return;

        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        bool isWalking = moveInput.sqrMagnitude > 0.1f && isGrounded && !isSprinting;

        animator.SetBool("isWalking", isWalking);
        animator.SetBool("isSprinting", isSprinting);
        animator.SetBool("isCrouching", isCrouching);
        animator.SetBool("isJumping", !isGrounded && velocity.y > 0f);
        animator.SetBool("isFalling", !isGrounded && velocity.y < 0f);
    }

    private void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
        sprintAction.Disable();
        crouchAction.Disable();
    }

    // -----------------------------
    // PUBLIC TOGGLE (you added)
    // -----------------------------
    public void EnableInput()
    {
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
        sprintAction.Enable();
        crouchAction.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        enabled = true;
    }

    public void DisableInput()
    {
        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
        sprintAction.Disable();
        crouchAction.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        enabled = false;
    }

    // 👇 extra helper so other scripts (like your ApplyPlayerSettings) can change it at runtime
    public void SetSensitivity(float newSens, bool save = false)
    {
        lookSensitivity = newSens;
        if (save)
        {
            PlayerPrefs.SetFloat(SENS_KEY, newSens);
            PlayerPrefs.Save();
        }
    }
}
