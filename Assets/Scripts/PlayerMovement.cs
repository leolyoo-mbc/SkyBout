using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Tooltip("지상 수평 최고 달리기 속도 (m/s)")]
    [SerializeField] private float maxSpeed = 8f;

    [Tooltip("발로 땅을 미는 추진력 크기 (N)")]
    [SerializeField] private float moveForce = 800f;

    [Tooltip("정지 및 역방향 전환 시 제동력 배율 (배)")]
    [SerializeField] private float brakeMultiplier = 5f;

    [SerializeField] private Transform groundCheckTransform;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Tooltip("지상 대비 공중 이동 제어 비율 (0 = 완전 관성, 1 = 지상과 동일)")]
    [Range(0f, 1f)]
    [SerializeField] private float airControl = 0.3f;


    private Rigidbody _rb;
    private Vector2 _input;
    private bool _isGrounded;


    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public void OnMove(InputValue value)
    {
        _input = value.Get<Vector2>();
    }

    private void Update()
    {
        _isGrounded = Physics.CheckSphere(groundCheckTransform.position, groundCheckRadius, groundLayer);
    }

    private void FixedUpdate()
    {
        Vector3 moveDirNormalized = new Vector3(_input.x, 0f, _input.y).normalized;
        Vector3 currentVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);


        if (_isGrounded)
        {
            Vector3 targetVelocity = moveDirNormalized * maxSpeed;

            // 현재 이동 방향과 목표 방향의 일치도 계산 (1.0 = 완전 직진, 0 = 90도 턴)
            float directionMatch = Vector3.Dot(currentVelocity.normalized, moveDirNormalized);

            // 키를 뗐거나(정지), 방향을 조금이라도 꺾을 때(일치도 95% 미만) 제동력 가동
            if (moveDirNormalized.sqrMagnitude < 0.001f || directionMatch < 0.95f)
            {
                // 1프레임당 늘릴 수 있는 최대 속도 보폭 계산
                float maxDeltaSpeed = (moveForce / _rb.mass) * brakeMultiplier * Time.fixedDeltaTime;

                Vector3 nextVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, maxDeltaSpeed);
                _rb.linearVelocity = new Vector3(nextVelocity.x, _rb.linearVelocity.y, nextVelocity.z);
            }
            else
            {
                // 1프레임당 늘릴 수 있는 최대 속도 보폭 계산
                float maxDeltaSpeed = (moveForce / _rb.mass) * Time.fixedDeltaTime;

                Vector3 nextVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, maxDeltaSpeed);
                _rb.linearVelocity = new Vector3(nextVelocity.x, _rb.linearVelocity.y, nextVelocity.z);
            }
        }
        else
        {
            if (moveDirNormalized.sqrMagnitude > 0.001f)
            {
                Vector3 targetVelocity = moveDirNormalized * maxSpeed;

                // 1프레임당 늘릴 수 있는 최대 속도 보폭 (0.2 m/s)
                float maxDeltaSpeed = (moveForce / _rb.mass) * airControl * Time.fixedDeltaTime;

                // 현재 속도에서 목표 속도를 향해 보폭만큼 이동
                Vector3 nextVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, maxDeltaSpeed);

                // Y축 중력 낙하 속도는 유지한 채 수평 속도만 갱신
                _rb.linearVelocity = new Vector3(nextVelocity.x, _rb.linearVelocity.y, nextVelocity.z);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheckTransform.position, groundCheckRadius);
    }
}
