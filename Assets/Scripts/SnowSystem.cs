using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SnowSystem : MonoBehaviour
{
    [Header("Snow Particles")]
    [SerializeField] private ParticleSystem snowParticles;
    [SerializeField] private float snowFallRate = 100f;

    [Header("Snow Accumulation")]
    [SerializeField] private float accumulationRate = 0.05f; // Snow buildup speed
    [SerializeField] private float maxSnowHeight = 0.2f;    // Maximum snow depth
    [SerializeField] private Material snowMaterial;         // Custom snow shader material
    [SerializeField] private LayerMask snowableObjects;     // What layers can accumulate snow

    [Header("Snow Removal")]
    [SerializeField] private float removalRotationThreshold = 15f; // Degrees of rotation needed to dislodge snow
    [SerializeField] private float removalRate = 0.1f;             // How fast snow falls off when tilting

    // Runtime tracking values
    private float currentSnowAmount = 0f;
    private float lastFrameRotationY = 0f;
    private float rotationDeltaAccumulator = 0f;

    // Cached references
    private Transform cityContainer;
    private Dictionary<Renderer, float> snowedObjects = new Dictionary<Renderer, float>();

    void Start()
    {
        // Find city container
        cityContainer = GameObject.Find("CityContainer").transform;

        // Create snow particle system if not assigned
        if (snowParticles == null)
        {
            CreateSnowParticleSystem();
        }

        // Initialize snow shader material for URP
        if (snowMaterial == null)
        {
            // Use URP shader instead of Standard
            snowMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (snowMaterial.shader.name == "Hidden/InternalErrorShader")
            {
                // Fallback if URP shader not found
                Debug.LogWarning("URP Lit shader not found, falling back to a simpler shader");
                snowMaterial = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));

                // If still not found, use an extremely basic shader that should work
                if (snowMaterial.shader.name == "Hidden/InternalErrorShader")
                {
                    snowMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    if (snowMaterial.shader.name == "Hidden/InternalErrorShader")
                    {
                        // Last resort
                        Debug.LogError("No compatible URP shaders found! Snow material will appear pink.");
                        snowMaterial.color = Color.white;
                    }
                }
            }

            // Set properties that exist in URP shaders
            snowMaterial.SetFloat("_Smoothness", 0.1f);
            snowMaterial.SetColor("_BaseColor", Color.white);
        }

        // Record initial rotation
        if (cityContainer != null)
        {
            lastFrameRotationY = cityContainer.eulerAngles.y;
        }

        // Start snow systems
        StartCoroutine(FindSnowableObjects());
    }

    void Update()
    {
        // Update snow accumulation
        UpdateSnowAccumulation();

        // Check for rotation-based snow removal
        if (cityContainer != null)
        {
            CheckRotationBasedRemoval();
        }
    }

    void CreateSnowParticleSystem()
    {
        // Create a game object for the particles
        GameObject snowObj = new GameObject("SnowParticles");
        snowObj.transform.parent = transform;

        // Add and configure particle system
        snowParticles = snowObj.AddComponent<ParticleSystem>();

        // Configure the particle system for snow
        var main = snowParticles.main;
        main.startSpeed = 2f;
        main.startSize = 0.1f;
        main.startLifetime = 10f;
        main.loop = true;
        main.maxParticles = 5000;

        // Emission module
        var emission = snowParticles.emission;
        emission.rateOverTime = snowFallRate;
        emission.enabled = true;

        // Shape module
        var shape = snowParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(50, 0, 50); // Make the snow cover a large area
        shape.position = new Vector3(0, 20, 0); // Position it high up

        // Renderer
        var renderer = snowParticles.GetComponent<ParticleSystemRenderer>();

        // For URP, use a compatible material
        Material particleMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        if (particleMat.shader.name == "Hidden/InternalErrorShader")
        {
            // Fallback
            particleMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
            if (particleMat.shader.name == "Hidden/InternalErrorShader")
            {
                // Another fallback
                Debug.LogWarning("Using default URP material for snow particles");
                particleMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            }
        }

        renderer.material = particleMat;
        renderer.material.color = Color.white;
        renderer.sortingOrder = 100;

        // Start the particles
        snowParticles.Play();
    }

    IEnumerator FindSnowableObjects()
    {
        while (true)
        {
            // Find all renderers that can have snow
            Renderer[] renderers = FindObjectsOfType<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                // Check if it's on a snowable layer
                if (((1 << renderer.gameObject.layer) & snowableObjects.value) != 0)
                {
                    // Add to tracking dictionary if not already there
                    if (!snowedObjects.ContainsKey(renderer))
                    {
                        snowedObjects.Add(renderer, 0f);
                    }
                }
            }

            // Wait before refreshing
            yield return new WaitForSeconds(5f);
        }
    }

    void UpdateSnowAccumulation()
    {
        // Increase global snow amount up to maximum
        currentSnowAmount = Mathf.Min(currentSnowAmount + accumulationRate * Time.deltaTime, maxSnowHeight);

        // Create a temporary list of renderers to avoid collection modification during enumeration
        List<Renderer> currentRenderers = new List<Renderer>(snowedObjects.Keys);

        // Apply snow to all snowable objects
        foreach (var renderer in currentRenderers)
        {
            if (renderer != null)
            {
                // Get the current snow amount for this object
                float objectSnowAmount = 0f;
                if (snowedObjects.TryGetValue(renderer, out objectSnowAmount))
                {
                    // Calculate this object's snow amount
                    objectSnowAmount = Mathf.Min(objectSnowAmount + accumulationRate * Time.deltaTime, currentSnowAmount);
                    snowedObjects[renderer] = objectSnowAmount;

                    // Apply snow material or snow parameter to shader
                    ApplySnowToRenderer(renderer, objectSnowAmount / maxSnowHeight);
                }
            }
        }
    }

    void ApplySnowToRenderer(Renderer renderer, float normalizedAmount)
    {
        if (renderer == null) return;

        // For ground, create a simple snow overlay
        if (renderer.gameObject.CompareTag("Ground"))
        {
            // Find or create a snow overlay child
            Transform snowOverlay = renderer.transform.Find("SnowOverlay");

            // Only show when there's enough snow
            if (normalizedAmount > 0.05f)
            {
                if (snowOverlay == null)
                {
                    GameObject overlayObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    overlayObj.name = "SnowOverlay";
                    overlayObj.transform.parent = renderer.transform;

                    // Position just above the ground
                    overlayObj.transform.localPosition = new Vector3(0, 0.01f, 0);
                    overlayObj.transform.localRotation = Quaternion.Euler(90, 0, 0); // Face upward

                    // Size to match parent
                    Bounds bounds = renderer.bounds;
                    overlayObj.transform.localScale = new Vector3(
                        bounds.size.x,
                        bounds.size.z,
                        1
                    );

                    // Apply snow material
                    Renderer snowRenderer = overlayObj.GetComponent<Renderer>(); // Renamed from overlayRenderer
                    snowRenderer.material = new Material(snowMaterial); // Create a new instance to avoid modifying shared material

                    // Make sure to disable the collider to prevent interference
                    Collider collider = overlayObj.GetComponent<Collider>();
                    if (collider != null) Destroy(collider);

                    snowOverlay = overlayObj.transform;
                }

                // Update snow appearance
                Renderer overlayRenderer = snowOverlay.GetComponent<Renderer>();
                if (overlayRenderer != null)
                {
                    // Get current color (preserve existing alpha transitions)
                    Color currentColor = overlayRenderer.material.color;
                    // Target alpha based on snow amount, but transition smoothly
                    float targetAlpha = normalizedAmount;
                    // Smoothly interpolate to the target alpha
                    currentColor.a = Mathf.Lerp(currentColor.a, targetAlpha, Time.deltaTime * 2f);
                    overlayRenderer.material.color = currentColor;
                }
            }
            else if (snowOverlay != null)
            {
                // Fade out when snow amount is too low
                Renderer fadeRenderer = snowOverlay.GetComponent<Renderer>(); // Renamed from overlayRenderer
                if (fadeRenderer != null)
                {
                    Color currentColor = fadeRenderer.material.color;
                    currentColor.a = Mathf.Lerp(currentColor.a, 0, Time.deltaTime * 5f);
                    fadeRenderer.material.color = currentColor;

                    // Destroy when fully transparent
                    if (currentColor.a < 0.01f)
                    {
                        Destroy(snowOverlay.gameObject);
                    }
                }
            }
        }
        // For regular objects, we could just adjust their existing material
        else if (normalizedAmount > 0.01f)
        {
            // Simple approach: just lerp the object's color toward white based on snow amount
            // This won't look as good as a proper shader but it's compatible with any material
            try
            {
                // Safely try to adjust the material color
                Color originalColor = renderer.material.color;
                Color snowyColor = Color.Lerp(originalColor, Color.white, normalizedAmount * 0.7f);
                renderer.material.color = snowyColor;
            }
            catch (System.Exception e)
            {
                // If we can't modify the material (e.g., it's a shared material), just log and continue
                Debug.LogWarning("Could not apply snow to " + renderer.name + ": " + e.Message);
                // Remove from tracked objects to avoid future errors
                if (snowedObjects.ContainsKey(renderer))
                {
                    snowedObjects.Remove(renderer);
                }
            }
        }
    }

    void CheckRotationBasedRemoval()
    {
        // Get current rotation
        float currentRotationY = cityContainer.eulerAngles.y;

        // Calculate absolute rotation delta (handles wraparound at 360)
        float rotationDelta = Mathf.Abs(Mathf.DeltaAngle(lastFrameRotationY, currentRotationY));

        // Accumulate rotation
        rotationDeltaAccumulator += rotationDelta;

        // Check if we've rotated enough to dislodge snow
        if (rotationDeltaAccumulator >= removalRotationThreshold)
        {
            // Calculate how much snow to remove based on rotation speed
            float removalFactor = rotationDeltaAccumulator / 360f; // Full rotation = maximum effect
            RemoveSnow(removalFactor * removalRate);

            // Reset accumulator
            rotationDeltaAccumulator = 0f;
        }

        // Update last frame rotation
        lastFrameRotationY = currentRotationY;
    }

    void RemoveSnow(float amount)
    {
        // Reduce global snow amount
        currentSnowAmount = Mathf.Max(0f, currentSnowAmount - amount);

        // Create a temporary list to avoid collection modification during enumeration
        List<Renderer> currentRenderers = new List<Renderer>(snowedObjects.Keys);

        // Apply reduction to all objects
        foreach (var renderer in currentRenderers)
        {
            if (renderer != null)
            {
                float objectSnow = snowedObjects[renderer];
                objectSnow = Mathf.Max(0f, objectSnow - amount);
                snowedObjects[renderer] = objectSnow;

                // Update snow rendering
                ApplySnowToRenderer(renderer, objectSnow / maxSnowHeight);
            }
        }
    }

    // Debug information that can be uncommented to help diagnose issues
    void OnGUI()
    {
        // Uncomment for debugging
        /*
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("Snow System Debug:");
        GUILayout.Label($"Current Snow: {currentSnowAmount:F2}/{maxSnowHeight:F2}");
        GUILayout.Label($"Rotation Delta: {rotationDeltaAccumulator:F2}�");
        GUILayout.Label($"Snow Material: {(snowMaterial != null ? snowMaterial.shader.name : "None")}");
        GUILayout.Label($"Tracked Objects: {snowedObjects.Count}");
        GUILayout.EndArea();
        */
    }
}