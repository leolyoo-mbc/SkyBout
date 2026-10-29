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
    [SerializeField] private Transform groundCheckTransform;
    [SerializeField, Min(0.001f)] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField, Range(0f, 1f), Tooltip("기존 공중 이동 제어 비율")]
    private float airControl = 0.3f;

    private Rigidbody body;
    private PlayerInput playerInput;
    private Vector2 input;
    private bool grounded;
    private bool focused;
    private bool waitingForNeutral;

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
        InputSystem.onAfterUpdate += CheckForNeutralInput;
    }

    private void Start()
    {
        if (playerInput.actions == null || playerInput.actions.FindAction("Player/Move") == null)
        {
            Debug.LogError("PlayerMovement: PlayerInput must provide Player/Move.", this);
            enabled = false;
        }
    }

    private void OnDisable()
    {
        InputSystem.onAfterUpdate -= CheckForNeutralInput;
        waitingForNeutral = true;
        StopHorizontalMovement();
    }

    public void OnMove(InputValue value)
    {
        input = focused && !waitingForNeutral
            ? Vector2.ClampMagnitude(value.Get<Vector2>(), 1f)
            : Vector2.zero;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        focused = hasFocus;
        waitingForNeutral = true;
        StopHorizontalMovement();
    }

    private void CheckForNeutralInput()
    {
        if (!focused || !waitingForNeutral || playerInput == null || playerInput.actions == null)
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
        grounded = Physics.CheckSphere(groundCheckTransform.position, groundCheckRadius,
            groundLayer, QueryTriggerInteraction.Ignore);
        if (!focused || waitingForNeutral)
        {
            StopHorizontalMovement();
            return;
        }

        Vector3 current = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        Vector3 target = direction * maxSpeed;
        float rate;
        if (grounded)
        {
            if (direction.sqrMagnitude < 0.0001f)
                rate = deceleration;
            else if (current.sqrMagnitude > 0.0001f && Vector3.Dot(current.normalized, direction.normalized) < 0.95f)
                rate = turnAcceleration;
            else
                rate = current.magnitude > target.magnitude ? deceleration : acceleration;
        }
        else
        {
            if (direction.sqrMagnitude < 0.0001f)
                return;
            rate = acceleration * airControl;
        }
        Vector3 next = Vector3.MoveTowards(current, target, rate * Time.fixedDeltaTime);
        body.linearVelocity = new Vector3(next.x, body.linearVelocity.y, next.z);
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
            && Positive(turnAcceleration) && Positive(groundCheckRadius)
            && airControl >= 0f && airControl <= 1f && body != null && !body.isKinematic)
            return;
        Debug.LogError("PlayerMovement: check PlayerInput/Move, child ground probe, Ground mask, dynamic Rigidbody and finite positive movement values.", this);
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
