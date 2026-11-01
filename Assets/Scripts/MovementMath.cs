using UnityEngine;

// Rigidbody를 변경하지 않고 다음 속도만 계산한다. 지상과 공중에 같은 규칙을 쓴다.
internal static class MovementMath
{
    internal static Vector3 CalculateVelocity(Vector3 velocity, Vector2 moveInput,
        float maxSpeed, float acceleration, float deceleration, float turnAcceleration, float deltaTime)
    {
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        Vector3 targetVelocity = moveDirection * maxSpeed;
        float speedChangeRate = moveDirection switch
        {
            var direction when direction.sqrMagnitude < 0.0001f => deceleration,
            var direction when horizontalVelocity.sqrMagnitude > 0.0001f
                && Vector3.Dot(horizontalVelocity.normalized, direction.normalized) < 0.95f => turnAcceleration,
            _ when horizontalVelocity.magnitude > targetVelocity.magnitude => deceleration,
            _ => acceleration
        };
        Vector3 nextVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, speedChangeRate * deltaTime);
        return new Vector3(nextVelocity.x, velocity.y, nextVelocity.z);
    }

    internal static float CalculateJumpSpeed(float jumpHeight, float gravity, float deltaTime)
    {
        // 물리 적분으로 줄어드는 반 스텝 분량의 높이를 초기 속도에서 보정한다.
        return Mathf.Sqrt(2f * gravity * jumpHeight) + gravity * deltaTime * 0.5f;
    }

    internal static float CalculateGlideVerticalVelocity(float currentFootHeight, float targetFootHeight,
        float currentVerticalVelocity, float response, float maxAcceleration, float deltaTime)
    {
        // 현재 물리 속도를 감쇠한다. 전개·충돌 이후에도 별도 속도 캐시나 위치 스냅은 없다.
        float error = targetFootHeight - currentFootHeight;
        float acceleration = Mathf.Clamp(response * response * error - 2f * response * currentVerticalVelocity,
            -maxAcceleration, maxAcceleration);
        return currentVerticalVelocity + acceleration * deltaTime;
    }
}
