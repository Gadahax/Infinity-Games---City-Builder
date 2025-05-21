using UnityEngine;
using System.Collections.Generic;

public class GridSystem : MonoBehaviour
{
    [SerializeField] private Vector2Int gridSize = new Vector2Int(30, 30);
    [SerializeField] private float cellSize = 1.0f;

    public float CellSize { get { return cellSize; } }
    public int gridWidth { get { return gridSize.x; } } // Added property
    public int gridHeight { get { return gridSize.y; } } // Added property

    private bool[,] occupiedCells;

    // Dictionary to keep track of which object occupies which cells
    private Dictionary<GameObject, List<Vector2Int>> objectCells = new Dictionary<GameObject, List<Vector2Int>>();

    private void Awake()
    {
        occupiedCells = new bool[gridSize.x, gridSize.y];
    }

    // Convert local position to grid cell
    public Vector2Int WorldToGrid(Vector3 localPosition)
    {
        int x = Mathf.FloorToInt((localPosition.x + gridSize.x * cellSize / 2) / cellSize);
        int z = Mathf.FloorToInt((localPosition.z + gridSize.y * cellSize / 2) / cellSize);

        x = Mathf.Clamp(x, 0, gridSize.x - 1);
        z = Mathf.Clamp(z, 0, gridSize.y - 1);

        return new Vector2Int(x, z);
    }

    // Convert grid cell to local position
    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        float x = gridPosition.x * cellSize - gridSize.x * cellSize / 2 + cellSize / 2;
        float z = gridPosition.y * cellSize - gridSize.y * cellSize / 2 + cellSize / 2;

        return new Vector3(x, 0, z);
    }

    // Check if a single cell is occupied (helper method)
    public bool IsOccupied(Vector2Int cellPos)
    {
        if (cellPos.x < 0 || cellPos.x >= gridSize.x ||
            cellPos.y < 0 || cellPos.y >= gridSize.y)
        {
            return true; // Out of bounds is considered occupied
        }

        return occupiedCells[cellPos.x, cellPos.y];
    }

    // Mark a single cell as occupied and track which object occupies it (helper method)
    public void SetOccupied(Vector2Int cellPos, GameObject obj)
    {
        if (cellPos.x >= 0 && cellPos.x < gridSize.x &&
            cellPos.y >= 0 && cellPos.y < gridSize.y)
        {
            occupiedCells[cellPos.x, cellPos.y] = true;

            // Track which object occupies this cell
            if (!objectCells.ContainsKey(obj))
            {
                objectCells[obj] = new List<Vector2Int>();
            }

            objectCells[obj].Add(cellPos);
        }
    }

    // Check if any cell in the object's footprint is occupied
    public bool CanPlaceObject(Vector2Int gridPos, Vector2Int size)
    {
        // Check if any cell in the rectangle defined by gridPos and size is already occupied
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                Vector2Int cellPos = new Vector2Int(gridPos.x + x, gridPos.y + z);

                // Check if out of bounds
                if (cellPos.x < 0 || cellPos.x >= gridWidth || cellPos.y < 0 || cellPos.y >= gridHeight)
                    return false;

                // Check if cell is already occupied
                if (IsOccupied(cellPos))
                    return false;
            }
        }

        return true;
    }

    // Similar update for SetObjectOccupied to handle rotated objects
    public void SetObjectOccupied(Vector2Int gridPos, Vector2Int size, GameObject obj)
    {
        // Mark all cells in the object's footprint as occupied
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                Vector2Int cellPos = new Vector2Int(gridPos.x + x, gridPos.y + z);

                // Skip if out of bounds
                if (cellPos.x < 0 || cellPos.x >= gridWidth || cellPos.y < 0 || cellPos.y >= gridHeight)
                    continue;

                // Mark cell as occupied and store reference to the object
                SetOccupied(cellPos, obj);
            }
        }
    }

    // Remove an object and free its cells
    public void RemoveObject(GameObject obj)
    {
        if (objectCells.ContainsKey(obj))
        {
            foreach (Vector2Int cellPos in objectCells[obj])
            {
                if (cellPos.x >= 0 && cellPos.x < gridSize.x &&
                    cellPos.y >= 0 && cellPos.y < gridSize.y)
                {
                    occupiedCells[cellPos.x, cellPos.y] = false;
                }
            }

            objectCells.Remove(obj);
        }
    }

    // Check if a single cell is occupied (for backward compatibility)
    public bool IsCellOccupied(Vector2Int gridPosition)
    {
        return IsOccupied(gridPosition);
    }

    // Mark a single cell as occupied (for backward compatibility)
    public void SetCellOccupied(Vector2Int gridPosition, bool isOccupied)
    {
        if (gridPosition.x >= 0 && gridPosition.x < gridSize.x &&
            gridPosition.y >= 0 && gridPosition.y < gridSize.y)
        {
            occupiedCells[gridPosition.x, gridPosition.y] = isOccupied;
        }
    }

    // Optional: Visualize the grid (for debugging)
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int z = 0; z < gridSize.y; z++)
            {
                Vector3 center = GridToWorld(new Vector2Int(x, z));
                Gizmos.DrawWireCube(center, new Vector3(cellSize, 0.1f, cellSize));
            }
        }
    }
}