using UnityEngine;

[RequireComponent(typeof(Camera))]
public class TopDownCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField, Range(0.1f, 89.9f)] private float pitch = 55f;
    [SerializeField, Min(0.001f)] private float distance = 18f;
    [SerializeField, Range(0.1f, 179.9f)] private float fieldOfView = 60f;
    [SerializeField, Min(0.001f), Tooltip("클수록 빠르게 따라갑니다")]
    private float followSharpness = 10f;

    private Camera view;

    private void Awake()
    {
        view = GetComponent<Camera>();
    }

    private void Start() => SnapToTarget();

    public void SnapToTarget()
    {
        if (!ValidateSettings())
            return;
        view.orthographic = false;
        view.fieldOfView = fieldOfView;
        transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        transform.position = target.position - transform.forward * distance;
    }

    private void LateUpdate()
    {
        if (!ValidateSettings())
            return;
        transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        view.fieldOfView = fieldOfView;
        Vector3 desired = target.position - transform.forward * distance;
        float blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, blend);
    }

    private bool ValidateSettings()
    {
        if (target != null && view != null && pitch > 0f && pitch < 90f
            && Positive(distance) && fieldOfView > 0f && fieldOfView < 180f
            && Positive(followSharpness))
            return true;
        Debug.LogError("TopDownCameraFollow: assign a target and finite positive distance/follow values, pitch (0,90), FOV (0,180).", this);
        enabled = false;
        return false;
    }

    private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
}
