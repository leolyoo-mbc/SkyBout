using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    private enum PlayerState { Grounded, Airborne, Eliminated }

    // Inspector 설정: 이동·점프·낙사
    [FormerlySerializedAs("maxSpeed")]
    [SerializeField, Min(0.001f), Tooltip("지상·공중 공통 최고 수평 속도 (m/s)")]
    private float maxHorizontalSpeed = 8f;
    [SerializeField, Min(0.001f), Tooltip("출발과 직진 가속도 (m/s²)")]
    private float acceleration = 10f;
    [SerializeField, Min(0.001f), Tooltip("정지와 초과 속도 감속도 (m/s²)")]
    private float deceleration = 50f;
    [SerializeField, Min(0.001f), Tooltip("이동 중 방향 전환 가속도 (m/s²)")]
    private float turnAcceleration = 50f;
    [SerializeField, Min(0.001f), Tooltip("이륙 지면 대비 점프 높이 (m)")]
    private float jumpHeight = 2f;
    [SerializeField, Tooltip("발 위치가 이 월드 Y 높이보다 낮으면 탈락")]
    private float fallDeathHeight = -10f;

    // Inspector 참조: 발 위치·지면 레이어·카메라
    [FormerlySerializedAs("groundCheckTransform")]
    [SerializeField, Tooltip("캐릭터의 발바닥 위치")]
    private Transform feetTransform;
    [FormerlySerializedAs("groundCheckRadius")]
    [SerializeField, Min(0.001f), Tooltip("발 위치를 표시하는 디버그 구의 반경. 접지 판정에는 사용하지 않음")]
    private float feetMarkerRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private TopDownCameraFollow cameraFollow;

    // 컴포넌트 참조와 현재 지상·공중·탈락 상태
    private Rigidbody playerRigidbody;
    private PlayerInputActions playerInput;
    private PlayerInputReader inputReader;
    private readonly GroundSupport groundSupport = new GroundSupport();
    private readonly PlayerEliminationOverlay eliminationOverlay = new PlayerEliminationOverlay();
    private PlayerState state = PlayerState.Airborne;

    // 입력 시점에 허용된 요청을 다음 물리 갱신에서 한 번만 실행한다.
    private bool jumpRequested;
    private bool restartRequested;

    // 재시작할 최초 자세
    private Vector3 startingPosition;
    private Quaternion startingRotation;

    public bool IsEliminated => state == PlayerState.Eliminated;
    private Vector3 FootPosition => playerRigidbody.position + (feetTransform.position - transform.position);

    // ── Unity 생명주기와 물리 갱신 순서 ──

    private void Awake() // 생성 시 참조와 시작 자세를 준비한다.
    {
        playerRigidbody = GetComponent<Rigidbody>();
        playerInput = new PlayerInputActions();
        playerInput.bindingMask = InputBinding.MaskByGroup("Keyboard&Mouse");
        inputReader = new PlayerInputReader(playerInput);
        inputReader.JumpPressed += RequestJump;
        inputReader.RestartPressed += RequestRestart;
        startingPosition = playerRigidbody.position;
        startingRotation = playerRigidbody.rotation;
        ValidateSettings();
    }

    private void OnEnable() // 활성화 시 입력을 연결한다.
    {
        ResetRequests();
        if (playerRigidbody != null && !playerRigidbody.isKinematic)
            playerRigidbody.WakeUp(); // 재활성화된 플레이어의 지면 접촉 콜백을 다시 받는다.
        inputReader?.Enable(Application.isFocused);
    }

    private void OnDisable() // 비활성화 시 입력과 지면 접촉 정보를 정리한다.
    {
        inputReader?.Disable();
        groundSupport.Clear();
        if (state != PlayerState.Eliminated)
            state = PlayerState.Airborne;
        ResetRequests();
        StopHorizontalMovement();
    }

    private void OnDestroy() // 삭제 시 입력 자산을 해제한다.
    {
        if (inputReader != null)
        {
            inputReader.Disable();
            inputReader.JumpPressed -= RequestJump;
            inputReader.RestartPressed -= RequestRestart;
        }
        if (playerInput != null)
        {
            playerInput.Disable();
            playerInput.Dispose();
            playerInput = null;
        }
    }

    private void OnApplicationFocus(bool hasFocus) // 창 초점이 바뀌면 이전 입력을 버린다.
    {
        inputReader?.SetFocus(hasFocus);
        ResetRequests();
        StopHorizontalMovement();
    }

    private void FixedUpdate() // 일정한 물리 간격으로 플레이어 동작을 처리한다.
    {
        // 새로 낙사한 틱에는 재시작·점프·이동을 처리하지 않는다.
        if (!IsEliminated && FootPosition.y < fallDeathHeight)
        {
            EliminatePlayer();
            return;
        }

        // 탈락 상태에서는 초점이 있을 때 새 재시작 요청만 실행한다.
        if (IsEliminated)
        {
            if (restartRequested && inputReader.HasFocus)
                RestartAtStartingPosition();
            else
                StopHorizontalMovement();

            return;
        }

        UpdateGroundedState();
        ProcessJumpRequest();

        // 초점·이동 키 해제 대기는 수평 이동만 막는다. 공중 물리는 계속된다.
        if (!inputReader.CanMove)
        {
            StopHorizontalMovement();
            return;
        }

        UpdateHorizontalVelocity();
    }

    // ── 입력 시점의 게임 규칙 확인 ──

    private void RequestJump()
    {
        // 공중에서 누른 입력이 착지 후 실행되지 않도록 입력 시점에도 접지를 확인한다.
        if (isActiveAndEnabled && !IsEliminated && IsStandingOnGround())
            jumpRequested = true;
    }

    private void RequestRestart()
    {
        if (isActiveAndEnabled && IsEliminated)
            restartRequested = true;
    }

    private void ResetRequests()
    {
        jumpRequested = false;
        restartRequested = false;
    }

    // ── 탈락과 시작 위치 복귀 ──

    private void EliminatePlayer()
    {
        state = PlayerState.Eliminated;
        ResetRequests();
        groundSupport.Clear();
        inputReader.SetEliminated(true);
        StopHorizontalMovement();
    }

    private void RestartAtStartingPosition()
    {
        ResetRequests();
        playerRigidbody.position = startingPosition;
        playerRigidbody.rotation = startingRotation;
        transform.SetPositionAndRotation(startingPosition, startingRotation);
        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;
        groundSupport.Clear();
        state = PlayerState.Airborne;
        inputReader.SetEliminated(false);
        playerRigidbody.WakeUp();
        if (cameraFollow != null)
            cameraFollow.SnapToTarget();
    }

    // ── 생존 중 접지·점프·수평 속도 갱신 ──

    private void UpdateGroundedState()
    {
        state = IsStandingOnGround() ? PlayerState.Grounded : PlayerState.Airborne;
    }

    private void ProcessJumpRequest()
    {
        bool shouldJump = jumpRequested;

        // 실행할 수 없는 요청도 이번 물리 갱신에서 버린다.
        jumpRequested = false;

        if (!shouldJump || !inputReader.HasFocus || state != PlayerState.Grounded)
            return;

        ApplyJumpVelocity();
        state = PlayerState.Airborne;
    }

    private void ApplyJumpVelocity()
    {
        float jumpSpeed = MovementMath.CalculateJumpSpeed(jumpHeight, -Physics.gravity.y, Time.fixedDeltaTime);
        playerRigidbody.linearVelocity = new Vector3(playerRigidbody.linearVelocity.x, jumpSpeed, playerRigidbody.linearVelocity.z);
    }

    private void UpdateHorizontalVelocity()
    {
        playerRigidbody.linearVelocity = MovementMath.CalculateVelocity(playerRigidbody.linearVelocity, inputReader.Move,
            maxHorizontalSpeed, acceleration, deceleration, turnAcceleration, Time.fixedDeltaTime);
    }

    private void StopHorizontalMovement()
    {
        inputReader?.ClearMovement();
        if (playerRigidbody != null && !playerRigidbody.isKinematic)
            playerRigidbody.linearVelocity = new Vector3(0f, playerRigidbody.linearVelocity.y, 0f);
    }

    // ── 발밑 지면 접촉 수집과 현재 위치 재검사 ──

    private void OnCollisionEnter(Collision collision) => UpdateGroundContacts(collision);
    private void OnCollisionStay(Collision collision) => UpdateGroundContacts(collision);
    private void OnCollisionExit(Collision collision) => groundSupport.Remove(collision.collider);

    private void UpdateGroundContacts(Collision collision)
    {
        // Unity는 비활성 MonoBehaviour에도 충돌 메시지를 보낼 수 있다.
        if (state == PlayerState.Eliminated || !isActiveAndEnabled || feetTransform == null)
            return;
        groundSupport.UpdateContacts(collision, FootPosition, groundLayer, this);
    }

    private bool IsStandingOnGround()
    {
        return !IsEliminated && groundSupport.IsStanding(FootPosition, playerRigidbody.linearVelocity.y, groundLayer);
    }

    // ── 설정 검증 ──

    private void OnValidate() // 실행 중 Inspector 값이 바뀌면 설정을 재검사한다.
    {
        if (Application.isPlaying && playerRigidbody != null)
            ValidateSettings();
    }

    private void ValidateSettings()
    {
        int groundLayerIndex = LayerMask.NameToLayer("Ground");
        bool groundIsConfigured = feetTransform != null && feetTransform != transform
            && feetTransform.IsChildOf(transform) && groundLayer.value != 0
            && groundLayerIndex >= 0 && (groundLayer.value & (1 << groundLayerIndex)) != 0
            && (groundLayer.value & (1 << gameObject.layer)) == 0;
        bool movementSettingsAreValid = IsFinitePositive(maxHorizontalSpeed)
            && IsFinitePositive(acceleration) && IsFinitePositive(deceleration) && IsFinitePositive(turnAcceleration)
            && IsFinitePositive(feetMarkerRadius) && IsFinitePositive(jumpHeight)
            && !float.IsNaN(fallDeathHeight) && !float.IsInfinity(fallDeathHeight);
        bool physicsSettingsAreValid = playerRigidbody != null && !playerRigidbody.isKinematic
            && playerRigidbody.useGravity && playerRigidbody.linearDamping == 0f
            && (playerRigidbody.constraints & RigidbodyConstraints.FreezePositionY) == 0
            && IsFinitePositive(-Physics.gravity.y) && Physics.gravity.x == 0f && Physics.gravity.z == 0f;

        if (playerInput != null && groundIsConfigured && movementSettingsAreValid && physicsSettingsAreValid)
            return;
        Debug.LogError("PlayerMovement: check input actions, child ground probe, Ground mask, dynamic gravity Rigidbody (zero damping, Y unlocked), downward gravity and finite positive movement/jump values plus a finite fall death height.", this);
        enabled = false;
    }

    private static bool IsFinitePositive(float value) => value > 0f && !float.IsInfinity(value);

    // ── 탈락 안내와 디버그 표시 ──

    private void OnGUI() // 화면을 그릴 때 탈락 안내를 표시한다.
    {
        if (IsEliminated)
            eliminationOverlay.Draw();
    }

    private void OnDrawGizmosSelected() // Editor에서 선택한 플레이어의 발 위치를 표시한다.
    {
        if (feetTransform == null)
            return;
        Gizmos.color = state == PlayerState.Grounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(feetTransform.position, feetMarkerRadius);
    }
}
