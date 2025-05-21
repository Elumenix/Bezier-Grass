using UnityEngine;

public class PlanetCameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 0.01f;
    public float rotationSpeed = 100f;
    public float zoomSpeed = 10f;

    private Vector3 position;
    private Quaternion rotation = Quaternion.identity;

    private void Start()
    {
        position = transform.position;
        rotation = transform.rotation;
    }

    private void Update()
    {
        HandleRotation();
        HandleMovement();
        HandleZoom();
    }

    private void HandleRotation()
    {
        if (Input.GetMouseButton(0))
        {
            float mouseX = Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
            rotation *= Quaternion.Euler(-mouseY, mouseX, 0);
        }
    }

    private void HandleMovement()
    {
        Vector3 move = new Vector3(
            Input.GetAxis("Horizontal"),
            Input.GetKey(KeyCode.Q) ? 1 : Input.GetKey(KeyCode.E) ? -1 : 0,
            Input.GetAxis("Vertical")
        );

        if (Input.GetKey(KeyCode.LeftShift)) move *= 100f;
        
        position += rotation * move * moveSpeed * Time.deltaTime;
        transform.SetPositionAndRotation(position, rotation);
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        position += rotation * Vector3.forward * scroll * zoomSpeed;
        transform.position = position;
    }
}