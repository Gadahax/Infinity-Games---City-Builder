using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class ObjectPlacementManager : MonoBehaviour
{
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform cityContainer;
    [SerializeField] private GameObject placementIndicator;
    [SerializeField] private Material validPlacementMaterial;
    [SerializeField] private Material invalidPlacementMaterial;
    [SerializeField] private float placementTransitionDuration = 0.5f;
    [SerializeField] private AnimationCurve placementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private GameObject selectedObjectPrefab;
    private GameObject previewObject;
    private Vector2Int currentGridPosition;
    private bool isValidPlacement = false;

    void Start()
    {
        // Find references if not assigned
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (gridSystem == null)
            gridSystem = Object.FindFirstObjectByType<GridSystem>();

        if (cityContainer == null)
            cityContainer = GameObject.Find("CityContainer").transform;

        // Create a simple quad as placement indicator if none provided
        if (placementIndicator == null)
        {
            placementIndicator = GameObject.CreatePrimitive(PrimitiveType.Quad);
            placementIndicator.transform.rotation = Quaternion.Euler(90, 0, 0);
            Destroy(placementIndicator.GetComponent<Collider>());
            placementIndicator.SetActive(false);

            // Create materials if not assigned
            if (validPlacementMaterial == null)
            {
                validPlacementMaterial = new Material(Shader.Find("Standard"));
                validPlacementMaterial.color = new Color(0, 1, 0, 0.5f); // Green transparent
            }

            if (invalidPlacementMaterial == null)
            {
                invalidPlacementMaterial = new Material(Shader.Find("Standard"));
                invalidPlacementMaterial.color = new Color(1, 0, 0, 0.5f); // Red transparent
            }
        }
    }

    void Update()
    {
        if (EventSystem.current.IsPointerOverGameObject())
            return; // Don't place objects when clicking on UI

        if (selectedObjectPrefab != null)
        {
            UpdatePlacementPreview();

            // Place object on left click
            if (Input.GetMouseButtonDown(0) && isValidPlacement)
            {
                PlaceObject();
            }
        }
        else
        {
            // Hide indicator when no object selected
            if (placementIndicator != null)
                placementIndicator.SetActive(false);

            // Destroy preview if it exists
            if (previewObject != null)
            {
                Destroy(previewObject);
                previewObject = null;
            }
        }
    }

    void UpdatePlacementPreview()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Only cast against the ground layer
        if (Physics.Raycast(ray, out hit))
        {
            // Convert hit point from world space to local space (accounting for city rotation)
            Vector3 localHitPoint = cityContainer.InverseTransformPoint(hit.point);

            // Get grid position
            Vector2Int gridPosition = gridSystem.WorldToGrid(localHitPoint);

            // Check if position changed
            if (gridPosition != currentGridPosition || previewObject == null)
            {
                currentGridPosition = gridPosition;

                // Get local position from grid
                Vector3 localPosition = gridSystem.GridToWorld(gridPosition);

                // Convert back to world space
                Vector3 worldPosition = cityContainer.TransformPoint(localPosition);

                // Create preview object if it doesn't exist
                if (previewObject == null)
                {
                    previewObject = Instantiate(selectedObjectPrefab);
                    previewObject.GetComponent<Collider>().enabled = false; // Disable collider on preview

                    // Make it semi-transparent
                    foreach (Renderer renderer in previewObject.GetComponentsInChildren<Renderer>())
                    {
                        Material[] materials = renderer.sharedMaterials;
                        Material[] newMaterials = new Material[materials.Length];

                        for (int i = 0; i < materials.Length; i++)
                        {
                            newMaterials[i] = new Material(materials[i]);
                            Color color = newMaterials[i].color;
                            color.a = 0.5f;
                            newMaterials[i].color = color;
                        }

                        renderer.materials = newMaterials;
                    }
                }

                // Update preview position and rotation
                previewObject.transform.position = worldPosition;
                previewObject.transform.rotation = cityContainer.rotation;

                // Update placement indicator
                placementIndicator.transform.position = new Vector3(
                    worldPosition.x,
                    0.01f, // Slightly above ground
                    worldPosition.z
                );
                placementIndicator.transform.rotation = Quaternion.Euler(90, cityContainer.eulerAngles.y, 0);

                // Use the CellSize property instead of cellSize
                placementIndicator.transform.localScale = new Vector3(
                    gridSystem.CellSize,
                    gridSystem.CellSize,
                    1
                );

                placementIndicator.SetActive(true);

                // Check if placement is valid
                isValidPlacement = !gridSystem.IsCellOccupied(gridPosition);

                // Update indicator material
                placementIndicator.GetComponent<Renderer>().material =
                    isValidPlacement ? validPlacementMaterial : invalidPlacementMaterial;
            }
        }
    }

    void PlaceObject()
    {
        // Get local position from grid
        Vector3 localPosition = gridSystem.GridToWorld(currentGridPosition);

        // Convert to world position
        Vector3 worldPosition = cityContainer.TransformPoint(localPosition);

        // Create the actual object
        GameObject newObject = Instantiate(selectedObjectPrefab, worldPosition, cityContainer.rotation);

        // Parent to city container
        newObject.transform.SetParent(cityContainer);

        // Mark grid cell as occupied
        gridSystem.SetCellOccupied(currentGridPosition, true);

        // Hide preview temporarily
        if (previewObject != null)
            previewObject.SetActive(false);

        // Play placement animation
        StartCoroutine(AnimatePlacement(newObject));
    }

    IEnumerator AnimatePlacement(GameObject obj)
    {
        // Store original scale and position
        Vector3 originalScale = obj.transform.localScale;
        Vector3 originalPosition = obj.transform.position;

        // Start from zero scale
        obj.transform.localScale = Vector3.zero;

        // Animate over time
        float timeElapsed = 0;

        while (timeElapsed < placementTransitionDuration)
        {
            float t = placementCurve.Evaluate(timeElapsed / placementTransitionDuration);

            // Scale up
            obj.transform.localScale = Vector3.Lerp(Vector3.zero, originalScale, t);

            // Small bounce effect
            float yOffset = Mathf.Sin(t * Mathf.PI) * 0.5f;
            obj.transform.position = new Vector3(
                originalPosition.x,
                originalPosition.y + yOffset,
                originalPosition.z
            );

            timeElapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure final values are set exactly
        obj.transform.localScale = originalScale;
        obj.transform.position = originalPosition;

        // Re-enable preview
        if (previewObject != null)
            previewObject.SetActive(true);
    }

    public void SetSelectedObject(GameObject prefab)
    {
        selectedObjectPrefab = prefab;

        // Destroy previous preview if it exists
        if (previewObject != null)
        {
            Destroy(previewObject);
            previewObject = null;
        }
    }

    void OnDestroy()
    {
        // Clean up
        if (previewObject != null)
            Destroy(previewObject);
    }
}