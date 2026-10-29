using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField, Min(0.001f), Tooltip("지상 최고 수평 속도 (m/s)")]
    private float maxSpeed = 8f;
    [SerializeField, Min(0.001f), Tooltip("출발과 직진 가속도 (m/s²)")]
    private float acceleration = 10f;
    [SerializeField, Min(0.001f), Tooltip("정지와 초과 속도 감속도 (m/s²)")]
    private float deceleration = 50f;
    [SerializeField, Min(0.001f), Tooltip("이동 중 방향 전환 가속도 (m/s²)")]
    private float turnAcceleration = 50f;
    [SerializeField, Min(0.001f), Tooltip("이륙 지면 대비 점프 높이 (m)")]
    private float jumpHeight = 2f;
    [SerializeField] private Transform groundCheckTransform;
    [SerializeField, Min(0.001f)] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    private readonly ContactPoint[] contactBuffer = new ContactPoint[16];
    private readonly HashSet<Collider> supportingColliders = new HashSet<Collider>();
    private const float SupportTolerance = 0.03f;
    private const float MinSupportNormal = 0.7f;
    private bool reportedContactOverflow;

    private Rigidbody body;
    private PlayerInput playerInput;
    private Vector2 input;
    private bool grounded;
    private bool focused;
    private bool waitingForNeutral;
    private bool pendingJump;
    private bool jumpHeld;
    private bool jumpReadyAfterRelease;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();
        ValidateSettings();
    }

    private void OnEnable()
    {
        focused = Application.isFocused;
        waitingForNeutral = true;
        input = Vector2.zero;
        ResetJumpInput();
        if (body != null && !body.isKinematic)
            body.WakeUp(); // Rebuild support callbacks after re-enabling a sleeping player.
        InputSystem.onAfterUpdate += CheckForNeutralInput;
    }

    private void Start()
    {
        if (playerInput == null || playerInput.actions == null
            || playerInput.actions.FindAction("Player/Move") == null
            || playerInput.actions.FindAction("Player/Jump") == null)
        {
            Debug.LogError("PlayerMovement: PlayerInput must provide Player/Move and Player/Jump.", this);
            enabled = false;
        }
    }

    private void OnDisable()
    {
        InputSystem.onAfterUpdate -= CheckForNeutralInput;
        waitingForNeutral = true;
        supportingColliders.Clear();
        grounded = false;
        ResetJumpInput();
        StopHorizontalMovement();
    }

    public void OnMove(InputValue value)
    {
        input = focused && !waitingForNeutral
            ? Vector2.ClampMagnitude(value.Get<Vector2>(), 1f)
            : Vector2.zero;
    }

    public void OnJump(InputValue value)
    {
        bool pressed = value.isPressed;
        bool newPress = pressed && !jumpHeld;
        jumpHeld = pressed;
        if (!isActiveAndEnabled || !focused)
            return;
        if (!pressed)
        {
            jumpReadyAfterRelease = true;
            return;
        }
        if (newPress && jumpReadyAfterRelease && HasGroundSupport())
            pendingJump = true;
    }

    private void ResetJumpInput()
    {
        pendingJump = false;
        jumpHeld = false;
        jumpReadyAfterRelease = false;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        focused = hasFocus;
        waitingForNeutral = true;
        ResetJumpInput();
        StopHorizontalMovement();
    }

    private void CheckForNeutralInput()
    {
        if (!focused || playerInput == null || playerInput.actions == null)
            return;
        var jump = playerInput.actions.FindAction("Player/Jump");
        // SendMessages omits canceled callbacks for Button actions. Observe release here.
        if (jump != null && jump.enabled)
        {
            bool actuated = false;
            foreach (var control in jump.controls)
                actuated |= control.IsActuated();
            if (!actuated)
            {
                jumpHeld = false;
                jumpReadyAfterRelease = true;
            }
        }
        if (!waitingForNeutral)
            return;
        var move = playerInput.actions.FindAction("Player/Move");
        if (move == null || !move.enabled)
            return;
        // Check after device synchronization, including opposing keys whose vector cancels out.
        foreach (var control in move.controls)
            if (control.IsActuated())
                return;
        waitingForNeutral = false;
    }

    private void FixedUpdate()
    {
        grounded = HasGroundSupport();
        bool launch = pendingJump;
        pendingJump = false;
        if (focused && launch && grounded)
        {
            // Compensate the half-step loss of semi-implicit gravity integration.
            float gravity = -Physics.gravity.y;
            float speed = Mathf.Sqrt(2f * gravity * jumpHeight) + gravity * Time.fixedDeltaTime * 0.5f;
            body.linearVelocity = new Vector3(body.linearVelocity.x, speed, body.linearVelocity.z);
            grounded = false;
        }
        if (!focused || waitingForNeutral)
        {
            StopHorizontalMovement();
            return;
        }

        Vector3 current = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        Vector3 target = direction * maxSpeed;
        float rate;
        if (direction.sqrMagnitude < 0.0001f)
            rate = deceleration;
        else if (current.sqrMagnitude > 0.0001f && Vector3.Dot(current.normalized, direction.normalized) < 0.95f)
            rate = turnAcceleration;
        else
            rate = current.magnitude > target.magnitude ? deceleration : acceleration;
        Vector3 next = Vector3.MoveTowards(current, target, rate * Time.fixedDeltaTime);
        body.linearVelocity = new Vector3(next.x, body.linearVelocity.y, next.z);
    }

    // Contact callbacks provide actual support, not merely a nearby collider.
    private void OnCollisionEnter(Collision collision) => RefreshSupport(collision);
    private void OnCollisionStay(Collision collision) => RefreshSupport(collision);
    private void OnCollisionExit(Collision collision) => supportingColliders.Remove(collision.collider);

    private Vector3 FootPosition => body.position + (groundCheckTransform.position - transform.position);

    private void RefreshSupport(Collision collision)
    {
        // Unity also delivers collision messages to disabled MonoBehaviours.
        if (!isActiveAndEnabled || groundCheckTransform == null)
            return;
        supportingColliders.Remove(collision.collider);
        if ((groundLayer.value & (1 << collision.gameObject.layer)) == 0)
            return;
        int count = collision.GetContacts(contactBuffer);
        if (count == contactBuffer.Length && !reportedContactOverflow)
        {
            reportedContactOverflow = true;
            Debug.LogError("PlayerMovement: ground contact buffer is full; review collider complexity.", this);
        }
        Vector3 foot = FootPosition;
        for (int i = 0; i < count; i++)
        {
            ContactPoint contact = contactBuffer[i];
            if (contact.normal.y >= MinSupportNormal && contact.separation <= SupportTolerance
                && Mathf.Abs(contact.point.y - foot.y) <= SupportTolerance)
            {
                supportingColliders.Add(collision.collider);
                return;
            }
        }
    }

    private bool HasGroundSupport()
    {
        if (body.linearVelocity.y > 0.1f || supportingColliders.Count == 0)
            return false;
        // Recheck current geometry: cached contacts alone must not grant coyote time.
        Vector3 foot = FootPosition;
        return Physics.Raycast(foot + Vector3.up * 0.05f, Vector3.down, out RaycastHit hit,
            0.05f + SupportTolerance, groundLayer, QueryTriggerInteraction.Ignore)
            && hit.normal.y >= MinSupportNormal && supportingColliders.Contains(hit.collider);
    }

    private void StopHorizontalMovement()
    {
        input = Vector2.zero;
        if (body != null && !body.isKinematic)
            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
    }

    private void OnValidate()
    {
        if (Application.isPlaying && body != null)
            ValidateSettings();
    }

    private void ValidateSettings()
    {
        int ground = LayerMask.NameToLayer("Ground");
        if (playerInput != null && groundCheckTransform != null && groundCheckTransform != transform
            && groundCheckTransform.IsChildOf(transform) && groundLayer.value != 0
            && ground >= 0 && (groundLayer.value & (1 << ground)) != 0
            && (groundLayer.value & (1 << gameObject.layer)) == 0
            && Positive(maxSpeed) && Positive(acceleration) && Positive(deceleration)
            && Positive(turnAcceleration) && Positive(groundCheckRadius) && Positive(jumpHeight)
            && body != null && !body.isKinematic
            && body.useGravity && body.linearDamping == 0f
            && (body.constraints & RigidbodyConstraints.FreezePositionY) == 0
            && Positive(-Physics.gravity.y) && Physics.gravity.x == 0f && Physics.gravity.z == 0f)
            return;
        Debug.LogError("PlayerMovement: check PlayerInput/Move/Jump, child ground probe, Ground mask, dynamic gravity Rigidbody (zero damping, Y unlocked), downward gravity and finite positive movement/jump values.", this);
        enabled = false;
    }

    private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);

    private void OnDrawGizmosSelected()
    {
        if (groundCheckTransform == null)
            return;
        Gizmos.color = grounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheckTransform.position, groundCheckRadius);
    }
}
