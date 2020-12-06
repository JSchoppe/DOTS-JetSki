using UnityEngine;
using Unity.Mathematics;

/// <summary>
/// Controller for the JetSki hull.
/// </summary>
public sealed class JetSki : MonoBehaviour
{
    #region Fields
    private Ray[] hullPoints;
    #endregion
    #region Inspector Fields
    [Tooltip("The body that forces will be applied to.")]
    [SerializeField] private Rigidbody body = null;
    [Tooltip("The fluid that applies bouyancy to the jetski.")]
    [SerializeField] private WaterBodyRenderer fluid = null;
    [Tooltip("The hull used to calculate bouyant forces.")]
    [SerializeField] private MeshFilter bouyancyHull = null;
    [Tooltip("Defines the location and direction of applied acceleration.")]
    [SerializeField] private Transform accelerationDirection = null;
    [Tooltip("The camera that will follow this jetski.")]
    [SerializeField] private Transform cameraRig = null;
    [Header("Movement Parameters")]
    [Tooltip("Intensity of the bouyancy applied to the body.")]
    [SerializeField] private float bouyancyForce = 1f;
    [Tooltip("Magnitude of forward/backward acceleration force.")]
    [SerializeField] private float accelerationForce = 5f;
    [Tooltip("Magnitude of twisting turning force.")]
    [SerializeField] private float torqueForce = 5f;
    [Header("Camera Parameters")]
    [Tooltip("Meters of distance camera follows behind the jetski.")]
    [SerializeField] private float followDistance = 5f;
    #endregion
#if DEBUG
    #region Editor Functions
    private void OnValidate()
    {
        // Clamp inspector fields.
        bouyancyForce = Mathf.Clamp(bouyancyForce, 0f, float.MaxValue);
        accelerationForce = Mathf.Clamp(accelerationForce, 0f, float.MaxValue);
        torqueForce = Mathf.Clamp(torqueForce, 0f, float.MaxValue);
    }
    #endregion
#endif
    #region MonoBehaviour - Initialize Hull
    private void Start()
    {
        // Extract mesh data from the hull.
        Vector3[] vertices = bouyancyHull.mesh.vertices;
        Vector3[] normals = bouyancyHull.mesh.normals;
        // Generate the normal force vectors
        // for bouyancy.
        hullPoints = new Ray[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            hullPoints[i].origin = vertices[i];
            hullPoints[i].direction = -normals[i];
        }
    }
    #endregion
    #region MonoBehaviour - Update Camera
    private void Update()
    {
        // Update the camera positioning to follow the jetski.
        cameraRig.transform.position = new Vector3
        {
            x = transform.position.x - transform.forward.x * followDistance,
            z = transform.position.z - transform.forward.z * followDistance
        };
        cameraRig.transform.LookAt(new Vector3
        {
            x = transform.position.x,
            z = transform.position.z
        });
    }
    #endregion
    #region MonoBehaviour - Process Input, Apply Forces
    private void FixedUpdate()
    {
        // TODO input should depend on whether jetski is in water or airbourne.
        // Handle input forces.
        Vector2 stickInput = new Vector2
        {
            x = Input.GetAxis("Horizontal"),
            y = Input.GetAxis("Vertical")
        };
        if (stickInput.y != 0f)
        {
            body.AddForceAtPosition(
                accelerationDirection.forward * stickInput.y * accelerationForce * Time.fixedDeltaTime,
                accelerationDirection.position,
                ForceMode.Impulse
            );
        }
        if (stickInput.x != 0f)
        {
            body.AddTorque(
                Vector3.up * stickInput.x * torqueForce * Time.fixedDeltaTime,
                ForceMode.Impulse
            );
        }
        // Apply bouyant forces along the surface of the hull.
        // TODO this could probably be a job.
        foreach (Ray hullPoint in hullPoints)
        {
            Vector3 surfacePoint = transform.TransformPoint(hullPoint.origin);
            float submersion = fluid.GetElevation(new Vector2(surfacePoint.x, surfacePoint.z)) - surfacePoint.y;
            if (submersion > 0f)
            {
                Vector3 forceDirection = transform.TransformDirection(hullPoint.direction);
                body.AddForceAtPosition(
                    forceDirection * submersion * bouyancyForce * Time.fixedDeltaTime,
                    surfacePoint,
                    ForceMode.Impulse
                );
            }
        }

        if (body.velocity.magnitude > 2f)
        {
            fluid.AddWave(
                new WaveForce(((float3)(transform.position + body.velocity.normalized)).TrimY()
                , Time.time, 0.02f * body.velocity.magnitude, 0.05f)
            );
        }
    }
    #endregion
}
