using UnityEngine;

public class CityRotationController : MonoBehaviour
{
    [SerializeField] private Transform cityContainer; // Parent object containing all city elements
    [SerializeField] private float rotationSpeed = 100.0f;
    [SerializeField] private float zoomSpeed = 5.0f;
    [SerializeField] private float minZoom = 5.0f;
    [SerializeField] private float maxZoom = 20.0f;

    private Camera mainCamera;
    private float currentZoom;

    void Start()
    {
        mainCamera = Camera.main;

        // Find city container if not assigned
        if (cityContainer == null)
        {
            cityContainer = GameObject.Find("CityContainer").transform;
            if (cityContainer == null)
            {
                Debug.LogError("CityContainer not found! Please create a GameObject named CityContainer.");
            }
        }

        // Initialize zoom based on camera type
        if (mainCamera.orthographic)
        {
            currentZoom = mainCamera.orthographicSize;
        }
        else
        {
            currentZoom = mainCamera.transform.position.y;
        }
    }

    void Update()
    {
        // Rotate city with right mouse button
        if (Input.GetMouseButton(1))
        {
            float rotationDelta = Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            cityContainer.Rotate(0, -rotationDelta, 0); // Negative to make it feel natural
        }

        // Zoom with scroll wheel
        float zoomDelta = Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        currentZoom = Mathf.Clamp(currentZoom - zoomDelta, minZoom, maxZoom);

        // Apply zoom based on camera type
        if (mainCamera.orthographic)
        {
            // For orthographic camera, adjust the size
            mainCamera.orthographicSize = currentZoom;
        }
        else
        {
            // For perspective camera, adjust the height
            Vector3 camPos = mainCamera.transform.position;
            mainCamera.transform.position = new Vector3(camPos.x, currentZoom, camPos.z);
        }
    }
}