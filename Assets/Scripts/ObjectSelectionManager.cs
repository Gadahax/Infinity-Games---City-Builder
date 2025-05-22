using UnityEngine;
using UnityEngine.UI;

public class ObjectSelectionManager : MonoBehaviour
{
    // Tool mode enum
    public enum ToolMode
    {
        None,
        Build,
        Demolish
    }

    // Current tool mode
    [SerializeField] private ToolMode currentToolMode = ToolMode.None;

    // UI buttons for tool selection
    [SerializeField] private Button buildModeButton;
    [SerializeField] private Button demolishModeButton;

    [System.Serializable]
    public class PlaceableObject
    {
        public string name;
        public GameObject prefab;
        public Sprite icon;
    }

    [SerializeField] private PlaceableObject[] placeableObjects;
    [SerializeField] private Color selectedButtonColor = new Color(0.8f, 0.8f, 1f);

    // Panels
    [SerializeField] private GameObject categoryPanel; // Panel for category buttons
    [SerializeField] private GameObject buildingPanel;
    [SerializeField] private GameObject roadPanel;
    [SerializeField] private GameObject naturePanel;

    private Color defaultButtonColor;
    private ObjectPlacementManager placementManager;
    private GameObject currentSelectedButton;
    private DragAndDropManager dragAndDropManager;

    void Start()
    {
        placementManager = Object.FindFirstObjectByType<ObjectPlacementManager>();
        if (placementManager == null)
        {
            Debug.LogError("ObjectPlacementManager not found in the scene!");
        }

        dragAndDropManager = Object.FindFirstObjectByType<DragAndDropManager>();
        if (dragAndDropManager == null)
        {
            Debug.LogError("DragAndDropManager not found in the scene!");
        }

        // Initialize button colors
        if (buildModeButton != null)
        {
            defaultButtonColor = buildModeButton.GetComponent<Image>().color;
        }

        // First, hide all content panels
        if (buildingPanel != null) buildingPanel.SetActive(false);
        if (roadPanel != null) roadPanel.SetActive(false);
        if (naturePanel != null) naturePanel.SetActive(false);

        // Make sure category panel is active
        if (categoryPanel != null) categoryPanel.SetActive(true);

        // Initialize buttons in each panel
        SetupButtonsInPanels();

        // Start with build mode
        SetToolMode(ToolMode.Build);
    }

    // Method to set up all buttons in the various panels
    void SetupButtonsInPanels()
    {
        // Find all buttons with the tag
        GameObject[] buildingButtons = GameObject.FindGameObjectsWithTag("BuildingButton");
        GameObject[] roadButtons = GameObject.FindGameObjectsWithTag("RoadButton");
        GameObject[] natureButtons = GameObject.FindGameObjectsWithTag("NatureButton");

        // Log number of buttons found
        Debug.Log($"Found {buildingButtons.Length} building buttons, {roadButtons.Length} road buttons, {natureButtons.Length} nature buttons");

        // Setup each type of button
        SetupButtonsOfType(buildingButtons, "Building");
        SetupButtonsOfType(roadButtons, "Road");
        SetupButtonsOfType(natureButtons, "Nature");
    }

    // Helper method to set up buttons of a specific type
    // Helper method to set up buttons of a specific type
    void SetupButtonsOfType(GameObject[] buttons, string category)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            GameObject button = buttons[i];

            // Get button name or assign one based on category
            string objName = button.name.Replace("Button_", "");
            if (!button.name.StartsWith("Button_"))
            {
                // Use the loop index instead of IndexOf
                objName = $"{category}{i}";
                button.name = $"Button_{objName}";
            }

            // Find matching prefab in placeableObjects
            PlaceableObject matchingObj = null;
            int matchingIndex = -1;

            for (int j = 0; j < placeableObjects.Length; j++)
            {
                if (placeableObjects[j].name == objName)
                {
                    matchingObj = placeableObjects[j];
                    matchingIndex = j;
                    break;
                }
            }

            if (matchingObj != null)
            {
                // Set icon if the button has a child image
                if (button.transform.childCount > 0)
                {
                    Transform iconTransform = button.transform.GetChild(0);
                    Image iconImage = iconTransform.GetComponent<Image>();
                    if (iconImage != null)
                    {
                        iconImage.sprite = matchingObj.icon;
                    }
                }

                // Add click handler
                Button buttonComponent = button.GetComponent<Button>();
                if (buttonComponent != null)
                {
                    int index = matchingIndex; // Capture for lambda
                    GameObject buttonObj = button; // Capture for lambda
                    buttonComponent.onClick.AddListener(() => SelectObject(index, buttonObj));
                }
            }
            else
            {
                Debug.LogWarning($"No matching prefab found for button: {button.name}");
            }
        }
    }

    // Method for UI buttons to call
    public void SetToolMode(int modeIndex)
    {
        SetToolMode((ToolMode)modeIndex);
    }

    public void SetToolMode(ToolMode mode)
    {
        currentToolMode = mode;

        // Update button visuals
        if (buildModeButton != null)
        {
            ColorBlock colors = buildModeButton.colors;
            colors.normalColor = (currentToolMode == ToolMode.Build) ? selectedButtonColor : defaultButtonColor;
            buildModeButton.colors = colors;
        }

        if (demolishModeButton != null)
        {
            ColorBlock colors = demolishModeButton.colors;
            colors.normalColor = (currentToolMode == ToolMode.Demolish) ? selectedButtonColor : defaultButtonColor;
            demolishModeButton.colors = colors;
        }

        // Always keep category panel active, handle content panels separately
        if (categoryPanel != null) categoryPanel.SetActive(true);

        // Reset selection when changing modes
        if (currentSelectedButton != null)
        {
            currentSelectedButton.GetComponent<Image>().color = defaultButtonColor;
            currentSelectedButton = null;
        }

        // Notify DragAndDropManager
        if (dragAndDropManager != null)
        {
            dragAndDropManager.SetToolMode(currentToolMode);
        }

        // Also disable object placement when not in build mode
        if (placementManager != null)
        {
            placementManager.SetSelectedObject(null);
        }
    }

    void SelectObject(int index, GameObject buttonGO)
    {
        // Only process selection in build mode
        if (currentToolMode != ToolMode.Build)
            return;

        // Reset previous selected button color
        if (currentSelectedButton != null)
        {
            currentSelectedButton.GetComponent<Image>().color = defaultButtonColor;
        }

        // Set new selected button color
        currentSelectedButton = buttonGO;
        currentSelectedButton.GetComponent<Image>().color = selectedButtonColor;

        // Notify placement manager
        if (placementManager != null && index >= 0 && index < placeableObjects.Length)
        {
            placementManager.SetSelectedObject(placeableObjects[index].prefab);
        }
    }

    // Panel switching methods
    public void ShowBuildingPanel()
    {
        if (currentToolMode != ToolMode.Build)
            SetToolMode(ToolMode.Build);

        if (buildingPanel != null) buildingPanel.SetActive(true);
        if (roadPanel != null) roadPanel.SetActive(false);
        if (naturePanel != null) naturePanel.SetActive(false);
    }

    public void ShowRoadPanel()
    {
        if (currentToolMode != ToolMode.Build)
            SetToolMode(ToolMode.Build);

        if (buildingPanel != null) buildingPanel.SetActive(false);
        if (roadPanel != null) roadPanel.SetActive(true);
        if (naturePanel != null) naturePanel.SetActive(false);
    }

    public void ShowNaturePanel()
    {
        if (currentToolMode != ToolMode.Build)
            SetToolMode(ToolMode.Build);

        if (buildingPanel != null) buildingPanel.SetActive(false);
        if (roadPanel != null) roadPanel.SetActive(false);
        if (naturePanel != null) naturePanel.SetActive(true);
    }
}