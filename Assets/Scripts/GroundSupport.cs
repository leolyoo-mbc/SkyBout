using System.Collections.Generic;
using UnityEngine;

// 실제 접촉과 현재 발밑 형상을 함께 확인한다. 근처 지면만으로 접지로 취급하지 않는다.
internal sealed class GroundSupport
{
    private const float ContactTolerance = 0.03f;
    private const float MinNormalY = 0.7f;
    private readonly ContactPoint[] contacts = new ContactPoint[16];
    private readonly HashSet<Collider> colliders = new HashSet<Collider>();
    private bool overflowReported;

    internal void UpdateContacts(Collision collision, Vector3 feetPosition, LayerMask groundLayer, Object logContext)
    {
        colliders.Remove(collision.collider);
        if ((groundLayer.value & (1 << collision.gameObject.layer)) == 0)
            return;

        int contactCount = collision.GetContacts(contacts);
        if (contactCount == contacts.Length && !overflowReported)
        {
            overflowReported = true;
            Debug.LogError("PlayerMovement: ground contact buffer is full; review collider complexity.", logContext);
        }
        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint contact = contacts[i];
            if (contact.normal.y >= MinNormalY && contact.separation <= ContactTolerance
                && Mathf.Abs(contact.point.y - feetPosition.y) <= ContactTolerance)
            {
                colliders.Add(collision.collider);
                return;
            }
        }
    }

    internal bool IsStanding(Vector3 feetPosition, float verticalSpeed, LayerMask groundLayer)
    {
        if (verticalSpeed > 0.1f || colliders.Count == 0)
            return false;

        // 이전 틱의 접촉만으로 가장자리를 벗어난 뒤의 점프를 허용하지 않는다.
        return Physics.Raycast(feetPosition + Vector3.up * 0.05f, Vector3.down, out RaycastHit hit,
            0.05f + ContactTolerance, groundLayer, QueryTriggerInteraction.Ignore)
            && hit.normal.y >= MinNormalY && colliders.Contains(hit.collider);
    }

    internal void Remove(Collider collider) => colliders.Remove(collider);
    internal void Clear() => colliders.Clear();
}
