using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DragAndDropManager : MonoBehaviour
{
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform cityContainer;
    [SerializeField] private float transitionDuration = 0.8f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [SerializeField] private Material validPlacementMaterial;
    [SerializeField] private Material invalidPlacementMaterial;
    [SerializeField] private Material demolishMaterial;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] buildSounds = new AudioClip[2];
    [SerializeField] private AudioClip[] demolishSounds = new AudioClip[2];
    [SerializeField][Range(0f, 1f)] private float buildVolume = 1f;
    [SerializeField][Range(0f, 1f)] private float demolishVolume = 1f;

    // Object Selection Manager reference
    [SerializeField] private ObjectSelectionManager objectSelectionManager;

    // Current tool mode
    private ObjectSelectionManager.ToolMode currentToolMode = ObjectSelectionManager.ToolMode.None;

    // Object sizes
    [System.Serializable]
    public class ObjectSize
    {
        public string prefabName;
        public Vector2Int size = Vector2Int.one;
    }

    [SerializeField] private ObjectSize[] objectSizes;

    private GameObject draggedIcon;
    private Transform canvasTransform;
    private bool isDragging = false;
    private string currentPrefabName;
    private Vector2Int currentObjectSize = Vector2Int.one;

    private GameObject placementIndicator;
    private bool isValidPlacement = false;

    // Rotation controls
    [Header("Rotation Settings")]
    [SerializeField] private KeyCode rotateClockwiseKey = KeyCode.E;
    [SerializeField] private KeyCode rotateCounterClockwiseKey = KeyCode.Q;
    [SerializeField] private float rotationStep = 90f;  // Amount to rotate in degrees
    private int currentRotationIndex = 0;  // 0 = 0°, 1 = 90°, 2 = 180°, 3 = 270°

    // Optional UI for rotation
    [SerializeField] private Button rotateClockwiseButton;
    [SerializeField] private Button rotateCounterClockwiseButton;

    // Demolish effect
    [SerializeField] private float demolishDuration = 0.5f;
    [SerializeField] private ParticleSystem demolishParticlesPrefab;

    void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (gridSystem == null)
            gridSystem = FindObjectOfType<GridSystem>();

        if (cityContainer == null)
            cityContainer = GameObject.Find("CityContainer").transform;

        if (objectSelectionManager == null)
            objectSelectionManager = FindObjectOfType<ObjectSelectionManager>();

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
            canvasTransform = canvas.transform;

        if (validPlacementMaterial == null)
        {
            validPlacementMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            validPlacementMaterial.color = new Color(0, 1, 0, 0.5f);
        }

        if (invalidPlacementMaterial == null)
        {
            invalidPlacementMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            invalidPlacementMaterial.color = new Color(1, 0, 0, 0.5f);
        }

        if (demolishMaterial == null)
        {
            demolishMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            demolishMaterial.color = new Color(1, 0, 0, 0.7f);
        }

        CreatePlacementIndicator();

        // Set up rotation buttons if provided
        if (rotateClockwiseButton != null)
            rotateClockwiseButton.onClick.AddListener(RotateClockwise);

        if (rotateCounterClockwiseButton != null)
            rotateCounterClockwiseButton.onClick.AddListener(RotateCounterClockwise);
    }

    private void PlayRandomBuildSound()
    {
        if (audioSource != null && buildSounds.Length > 0)
        {
            int randomIndex = Random.Range(0, buildSounds.Length);
            if (buildSounds[randomIndex] != null)
            {
                audioSource.PlayOneShot(buildSounds[randomIndex], buildVolume);
            }
        }
    }

    private void PlayRandomDemolishSound()
    {
        if (audioSource != null && demolishSounds.Length > 0)
        {
            int randomIndex = Random.Range(0, demolishSounds.Length);
            if (demolishSounds[randomIndex] != null)
            {
                audioSource.PlayOneShot(demolishSounds[randomIndex], demolishVolume);
            }
        }
    }

    void CreatePlacementIndicator()
    {
        placementIndicator = GameObject.CreatePrimitive(PrimitiveType.Quad);
        placementIndicator.name = "PlacementIndicator";
        placementIndicator.transform.rotation = Quaternion.Euler(90, 0, 0);
        Destroy(placementIndicator.GetComponent<Collider>());
        placementIndicator.SetActive(false);
    }

    // Set the current tool mode
    public void SetToolMode(ObjectSelectionManager.ToolMode mode)
    {
        currentToolMode = mode;
        Debug.Log($"Current tool mode: {currentToolMode}");

        // Reset state when changing modes
        if (isDragging)
        {
            if (draggedIcon != null)
                Destroy(draggedIcon);

            isDragging = false;
        }

        // Reset rotation when changing modes
        currentRotationIndex = 0;

        // Show/hide rotation buttons based on mode
        if (rotateClockwiseButton != null)
            rotateClockwiseButton.gameObject.SetActive(mode == ObjectSelectionManager.ToolMode.Build);

        if (rotateCounterClockwiseButton != null)
            rotateCounterClockwiseButton.gameObject.SetActive(mode == ObjectSelectionManager.ToolMode.Build);

        // Update indicator appearance based on mode
        if (currentToolMode == ObjectSelectionManager.ToolMode.Demolish)
        {
            placementIndicator.transform.localScale = new Vector3(1, 1, 1);
            placementIndicator.GetComponent<Renderer>().material = demolishMaterial;
        }
        else
        {
            placementIndicator.SetActive(false);
        }
    }

    void Update()
    {
        // Handle different modes
        if (currentToolMode == ObjectSelectionManager.ToolMode.Build)
        {
            HandleBuildMode();
        }
        else if (currentToolMode == ObjectSelectionManager.ToolMode.Demolish)
        {
            HandleDemolishMode();
        }
    }

    void HandleBuildMode()
    {
        // Start dragging when a button is clicked
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                GameObject clickedObj = EventSystem.current.currentSelectedGameObject;

                if (clickedObj != null && (
                    clickedObj.CompareTag("BuildingButton") ||
                    clickedObj.CompareTag("RoadButton") ||
                    clickedObj.CompareTag("NatureButton")))
                {
                    if (clickedObj.transform.childCount > 0)
                    {
                        Transform iconTransform = clickedObj.transform.GetChild(0);
                        if (iconTransform != null)
                        {
                            Image buttonIcon = iconTransform.GetComponent<Image>();
                            if (buttonIcon != null)
                            {
                                draggedIcon = new GameObject("DraggedIcon");
                                draggedIcon.AddComponent<RectTransform>();
                                Image iconImage = draggedIcon.AddComponent<Image>();
                                iconImage.sprite = buttonIcon.sprite;

                                // Make the dragged icon semi-transparent
                                Color iconColor = buttonIcon.color;
                                iconColor.a = 0.7f; // 50% transparent
                                iconImage.color = iconColor;

                                iconImage.raycastTarget = false;

                                // Set the parent with worldPositionStays = false to keep local position/rotation
                                draggedIcon.transform.SetParent(canvasTransform, false);
                                draggedIcon.transform.position = Input.mousePosition;

                                // Also use this opportunity to set the pivot to center for better rotation
                                RectTransform rt = draggedIcon.GetComponent<RectTransform>();
                                rt.pivot = new Vector2(0.5f, 0.5f);

                                // Set the size to match the original icon
                                RectTransform originalRT = buttonIcon.GetComponent<RectTransform>();
                                if (originalRT != null)
                                {
                                    rt.sizeDelta = originalRT.sizeDelta;
                                }

                                currentPrefabName = clickedObj.name.Replace("Button_", "");
                                FindObjectSize(currentPrefabName);

                                isDragging = true;
                                placementIndicator.SetActive(true);

                                // Reset rotation index when starting new drag
                                currentRotationIndex = 0;
                            }
                        }
                    }
                }
            }
        }

        if (isDragging && draggedIcon != null)
        {
            // Move the dragged icon with the mouse
            draggedIcon.transform.position = Input.mousePosition;

            // Update the placement indicator
            UpdatePlacementIndicator();

            // Handle rotation input
            HandleRotationInput();

            // Place the object on left mouse release
            if (Input.GetMouseButtonUp(0))
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit))
                {
                    Vector3 localHitPoint = cityContainer.InverseTransformPoint(hit.point);
                    Vector2Int gridPos = gridSystem.WorldToGrid(localHitPoint);

                    if (isValidPlacement)
                    {
                        // Calculate precise placement position
                        Vector3 localCellPos = gridSystem.GridToWorld(gridPos);

                        // Calculate final rotation
                        Quaternion finalRotation = cityContainer.rotation * Quaternion.Euler(0, currentRotationIndex * rotationStep, 0);

                        // Calculate offset based on object size and rotation
                        Vector2Int rotatedSize = RotateSize(currentObjectSize, currentRotationIndex);
                        float halfSizeX = (rotatedSize.x * gridSystem.CellSize) / 2.0f - gridSystem.CellSize / 2.0f;
                        float halfSizeZ = (rotatedSize.y * gridSystem.CellSize) / 2.0f - gridSystem.CellSize / 2.0f;

                        // Create local space offset
                        Vector3 localOffset = new Vector3(halfSizeX, 0, halfSizeZ);

                        // Convert to world space with rotation
                        Vector3 worldCellPos = cityContainer.TransformPoint(localCellPos);
                        Vector3 worldOffset = cityContainer.TransformDirection(localOffset);

                        // Set final target position with correct offset
                        Vector3 finalPosition = worldCellPos + worldOffset;

                        StartCoroutine(TransitionToPlacedObject(finalPosition, finalRotation, gridPos));
                    }
                    else
                    {
                        Destroy(draggedIcon);
                    }
                }
                else
                {
                    Destroy(draggedIcon);
                }

                isDragging = false;
                placementIndicator.SetActive(false);
            }
        }
    }

    void HandleRotationInput()
    {
        // Handle keyboard rotation
        if (Input.GetKeyDown(rotateClockwiseKey))
        {
            RotateClockwise();
        }
        else if (Input.GetKeyDown(rotateCounterClockwiseKey))
        {
            RotateCounterClockwise();
        }
    }

    public void RotateClockwise()
    {
        if (isDragging)
        {
            // Increment rotation index (0, 1, 2, 3) representing 0°, 90°, 180°, 270°
            currentRotationIndex = (currentRotationIndex + 1) % 4;

            // Rotate the dragged icon in 2D UI space
            // For UI elements, we rotate around the Z axis
            if (draggedIcon != null)
            {
                // For UI elements, rotation is around the Z axis
                draggedIcon.transform.rotation = Quaternion.Euler(0, 0, -currentRotationIndex * rotationStep);
            }

            // Update indicator to show new rotation
            UpdatePlacementIndicator();
        }
    }

    public void RotateCounterClockwise()
    {
        if (isDragging)
        {
            // Decrement rotation index, wrapping around from 0 to 3
            currentRotationIndex = (currentRotationIndex + 3) % 4; // +3 instead of -1 to handle wrap-around

            // Rotate the dragged icon in 2D UI space
            if (draggedIcon != null)
            {
                draggedIcon.transform.rotation = Quaternion.Euler(0, 0, -currentRotationIndex * rotationStep);
            }

            // Update indicator to show new rotation
            UpdatePlacementIndicator();
        }
    }

    // Helper method to rotate a size based on rotation index
    Vector2Int RotateSize(Vector2Int size, int rotationIndex)
    {
        // For 90° and 270° rotations, swap x and y
        if (rotationIndex % 2 == 1)
        {
            return new Vector2Int(size.y, size.x);
        }

        // For 0° and 180° rotations, keep original size
        return size;
    }

    void HandleDemolishMode()
    {
        // Show demolish indicator
        placementIndicator.SetActive(true);

        // Update indicator position
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            // Position the indicator at the hit point
            placementIndicator.transform.position = new Vector3(
                hit.point.x,
                0.02f, // Slightly above ground to avoid z-fighting
                hit.point.z
            );

            // Check if we hit a placeable object
            GameObject hitObject = hit.transform.gameObject;

            // Store the original hit object for later
            GameObject originalHitObject = hitObject;

            // Find the root parent (which should be a direct child of cityContainer)
            while (hitObject.transform.parent != null && hitObject.transform.parent != cityContainer)
            {
                hitObject = hitObject.transform.parent.gameObject;
            }

            // Highlight if it's a valid object to demolish
            // IMPORTANT: Check if it's not the ground!
            bool canDemolish = (hitObject.transform.parent == cityContainer && !hitObject.CompareTag("Ground"));

            // If we couldn't find a city container child, check if the original hit object is itself a placeable
            if (!canDemolish && originalHitObject != hitObject)
            {
                // For roads and nature objects that might be direct children
                if (originalHitObject.transform.parent == cityContainer && !originalHitObject.CompareTag("Ground"))
                {
                    hitObject = originalHitObject;
                    canDemolish = true;
                }
            }

            // Change indicator size based on object bounds if possible
            if (canDemolish)
            {
                // Get object bounds
                Renderer[] renderers = hitObject.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }

                    // Size the indicator to match object footprint
                    placementIndicator.transform.localScale = new Vector3(
                        bounds.size.x,
                        bounds.size.z,
                        1
                    );
                }
                else
                {
                    // Default size if no renderers
                    placementIndicator.transform.localScale = new Vector3(1, 1, 1);
                }

                // Set demolish material
                placementIndicator.GetComponent<Renderer>().material = demolishMaterial;
            }
            else
            {
                // Default size for invalid targets
                placementIndicator.transform.localScale = new Vector3(1, 1, 1);

                // Change material to invalid
                placementIndicator.GetComponent<Renderer>().material = invalidPlacementMaterial;
            }

            // Set indicator rotation to match the city rotation
            placementIndicator.transform.rotation = Quaternion.Euler(90, cityContainer.eulerAngles.y, 0);

            // When clicked, demolish the object if valid
            if (Input.GetMouseButtonDown(0) && canDemolish)
            {
                DemolishObject(hitObject);
            }
        }
        else
        {
            // Default size when not hitting anything
            placementIndicator.transform.localScale = new Vector3(1, 1, 1);

            // Use invalid material when not hitting anything
            placementIndicator.GetComponent<Renderer>().material = invalidPlacementMaterial;
        }
    }

    void DemolishObject(GameObject obj)
    {
        // Debug info to help diagnose
        Debug.Log($"Demolishing object: {obj.name}, Is child of cityContainer: {obj.transform.parent == cityContainer}");

        // Free grid cells
        gridSystem.RemoveObject(obj);

        // Play demolish animation
        StartCoroutine(AnimateDemolish(obj));
    }

    void FindObjectSize(string objectName)
    {
        foreach (ObjectSize objSize in objectSizes)
        {
            if (objSize.prefabName == objectName)
            {
                currentObjectSize = objSize.size;
                return;
            }
        }

        // Default if not found
        currentObjectSize = new Vector2Int(1, 1);
        Debug.LogWarning($"No size defined for {objectName}, using default 1x1");
    }

    void UpdatePlacementIndicator()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            Vector3 localHitPoint = cityContainer.InverseTransformPoint(hit.point);
            Vector2Int gridPos = gridSystem.WorldToGrid(localHitPoint);

            // Get rotated size for grid check
            Vector2Int rotatedSize = RotateSize(currentObjectSize, currentRotationIndex);

            // Check if placement is valid with rotated dimensions
            isValidPlacement = gridSystem.CanPlaceObject(gridPos, rotatedSize);

            Vector3 localCellPos = gridSystem.GridToWorld(gridPos);
            Vector3 worldCellPos = cityContainer.TransformPoint(localCellPos);

            // Calculate offset based on object size and rotation
            float halfSizeX = (rotatedSize.x * gridSystem.CellSize) / 2.0f - gridSystem.CellSize / 2.0f;
            float halfSizeZ = (rotatedSize.y * gridSystem.CellSize) / 2.0f - gridSystem.CellSize / 2.0f;

            // Create local space offset
            Vector3 localOffset = new Vector3(halfSizeX, 0, halfSizeZ);

            // Convert to world space with rotation
            Vector3 worldOffset = cityContainer.TransformDirection(localOffset);

            // Update indicator position
            placementIndicator.transform.position = new Vector3(
                worldCellPos.x + worldOffset.x,
                0.02f, // Slightly above ground
                worldCellPos.z + worldOffset.z
            );

            // Update indicator rotation - include both city rotation and object rotation
            placementIndicator.transform.rotation = Quaternion.Euler(
                90, // Face upward
                cityContainer.eulerAngles.y + (currentRotationIndex * rotationStep), // Apply city rotation + object rotation
                0
            );

            // Update indicator scale
            placementIndicator.transform.localScale = new Vector3(
                rotatedSize.x * gridSystem.CellSize,
                rotatedSize.y * gridSystem.CellSize,
                1
            );

            // Update indicator material
            placementIndicator.GetComponent<Renderer>().material =
                isValidPlacement ? validPlacementMaterial : invalidPlacementMaterial;
        }
    }

    IEnumerator TransitionToPlacedObject(Vector3 finalPosition, Quaternion finalRotation, Vector2Int gridPos)
    {
        PlayRandomBuildSound();
        // Get screen position of dragged icon for animation
        Vector3 startScreenPos = draggedIcon.transform.position;

        // Calculate world position for start of animation
        Vector3 startWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(
            startScreenPos.x,
            startScreenPos.y,
            mainCamera.WorldToScreenPoint(finalPosition).z
        ));

        // Load the prefab
        GameObject prefab = Resources.Load<GameObject>("Prefabs/" + currentPrefabName);

        if (prefab == null)
        {
            Debug.LogError("Prefab not found: " + currentPrefabName);
            Destroy(draggedIcon);
            yield break;
        }

        // Create the 3D object at start position with zero scale and initial rotation
        GameObject placedObject = Instantiate(prefab, startWorldPos, Quaternion.identity);

        // Get rotated size for grid
        Vector2Int rotatedSize = RotateSize(currentObjectSize, currentRotationIndex);

        // Start at zero scale
        placedObject.transform.localScale = Vector3.zero;

        // Store original scale
        Vector3 finalScale = prefab.transform.localScale;

        float elapsed = 0;

        // Create a starting rotation - halfway between identity and final
        Quaternion startRotation = Quaternion.Euler(0, currentRotationIndex * rotationStep, 0);

        while (elapsed < transitionDuration)
        {
            float t = transitionCurve.Evaluate(elapsed / transitionDuration);

            // Fade out the 2D icon
            Image iconImage = draggedIcon.GetComponent<Image>();
            Color iconColor = iconImage.color;
            iconColor.a = 1 - t;
            iconImage.color = iconColor;

            // Move the 3D object
            placedObject.transform.position = Vector3.Lerp(startWorldPos, finalPosition, t);

            // Scale up the 3D object
            placedObject.transform.localScale = Vector3.Lerp(Vector3.zero, finalScale, t);

            // Rotate the object smoothly to the final rotation
            placedObject.transform.rotation = Quaternion.Slerp(startRotation, finalRotation, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Finalize object placement
        placedObject.transform.position = finalPosition;
        placedObject.transform.localScale = finalScale;
        placedObject.transform.rotation = finalRotation;
        placedObject.transform.SetParent(cityContainer);

        // Mark grid cells as occupied with rotated size
        gridSystem.SetObjectOccupied(gridPos, rotatedSize, placedObject);

        

        // Destroy the dragged icon
        Destroy(draggedIcon);
    }

    IEnumerator AnimateDemolish(GameObject obj)
    {
        // Store original properties
        Vector3 originalScale = obj.transform.localScale;
        Vector3 originalPosition = obj.transform.position;
        Quaternion originalRotation = obj.transform.rotation;
        PlayRandomDemolishSound();

        // Optional: Spawn particle effect
        if (demolishParticlesPrefab != null)
        {
            ParticleSystem particles = Instantiate(demolishParticlesPrefab, originalPosition, Quaternion.identity);
            particles.Play();

            // Destroy particles after they finish
            Destroy(particles.gameObject, particles.main.duration + particles.main.startLifetime.constantMax);
        }

        // Shake effect
        float shakeIntensity = 0.1f;
        float shakeTime = demolishDuration * 0.7f; // Shake for part of the animation
        float elapsed = 0;

        while (elapsed < shakeTime)
        {
            // Random shake
            Vector3 shakeOffset = new Vector3(
                Random.Range(-1f, 1f) * shakeIntensity,
                Random.Range(-1f, 1f) * shakeIntensity,
                Random.Range(-1f, 1f) * shakeIntensity
            );

            obj.transform.position = originalPosition + shakeOffset;

            // Small random rotation
            obj.transform.rotation = originalRotation * Quaternion.Euler(
                Random.Range(-5f, 5f),
                Random.Range(-5f, 5f),
                Random.Range(-5f, 5f)
            );

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Reset position after shake
        obj.transform.position = originalPosition;
        obj.transform.rotation = originalRotation;

        // Collapse/scale down
        elapsed = 0;
        float collapseTime = demolishDuration * 0.3f; // Collapse for the rest of the animation

        while (elapsed < collapseTime)
        {
            float t = elapsed / collapseTime;

            // Scale down
            obj.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);

            // Sink into ground
            obj.transform.position = new Vector3(
                originalPosition.x,
                originalPosition.y - t * 0.5f, // Sink effect
                originalPosition.z
            );

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Destroy the object
        Destroy(obj);
    }
}