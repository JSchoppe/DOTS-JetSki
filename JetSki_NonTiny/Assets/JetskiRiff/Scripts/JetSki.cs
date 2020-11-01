using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class JetSki : MonoBehaviour
{

    [SerializeField] private Transform cameraRig = null;

    [SerializeField] private WaterBodyRenderer fluid = null;

    [SerializeField] private float bouyancyForce = 1f;

    [SerializeField] private Rigidbody body = null;

    [SerializeField] private float accelerationForce = 5f;
    [SerializeField] private Transform accelerationVector = null;

    [SerializeField] private MeshFilter bouyancyHull = null;

    private Ray[] hullPoints;

    // Start is called before the first frame update
    void Start()
    {
        Vector3[] vertices = bouyancyHull.mesh.vertices;
        Vector3[] normals = bouyancyHull.mesh.normals;

        hullPoints = new Ray[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            hullPoints[i].origin = vertices[i];
            hullPoints[i].direction = - normals[i];
        }

        foreach (Ray ray in hullPoints)
        {
            Debug.DrawRay(ray.origin, ray.direction, Color.yellow);

        }
    }

    // Update is called once per frame
    void Update()
    {
        cameraRig.transform.position = new Vector3(transform.position.x - transform.forward.x * 5, 0f, transform.position.z - transform.forward.z * 5);

        cameraRig.transform.LookAt(new Vector3(transform.position.x, 0f, transform.position.z));


        foreach (Ray hullPoint in hullPoints)
        {
            Vector3 point = transform.TransformPoint(hullPoint.origin);
            float submersion = fluid.GetElevation(new Vector2(point.x, point.z)) - point.y;
            if (submersion > 0f)
            {
                Vector3 direction = transform.TransformDirection(hullPoint.direction);
                body.AddForceAtPosition(direction * submersion * bouyancyForce * Time.deltaTime, point, ForceMode.Impulse);
            }
        }

        if (Input.GetAxis("Vertical") != 0f)
        {
            body.AddForceAtPosition(accelerationVector.forward * accelerationForce * Time.deltaTime, accelerationVector.position);
        }
    }
}
