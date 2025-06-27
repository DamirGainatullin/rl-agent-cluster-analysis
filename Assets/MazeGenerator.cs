using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;

[System.Serializable]
public class MazeCellData
{
    public int x;
    public int y;
    public int top;
    public int bottom;
    public int left;
    public int right;
}

[System.Serializable]
public class MazeSaveData
{
    public int mazeSize;
    public Vector2Int agentStart;
    public Vector2Int target;
    public List<MazeCellData> cells = new List<MazeCellData>();
}

public class MazeGenerator : MonoBehaviour
{
    public float cellSize = 2f;
    public GameObject wallPrefab;
    public GameObject floorPlane;

    private int width;
    private int height;
    private Cell[,] grid;

    private struct Cell
    {
        public GameObject wallTop;
        public GameObject wallBottom;
        public GameObject wallLeft;
        public GameObject wallRight;

        public bool hasTop;
        public bool hasBottom;
        public bool hasLeft;
        public bool hasRight;

        public bool visited;
    }

    public void GenerateMaze(int mazeSize, Vector3 agentPos, Vector3 targetPos)
    {
        width = mazeSize;
        height = mazeSize;

        ClearMaze();
        ResizeFloor();
        InitializeGrid();

        Vector2Int start = WorldToGrid(agentPos);
        DFS(start);
    }

    private void ResizeFloor()
    {
        if (floorPlane != null)
        {
            float scaleX = width * cellSize / 10f;
            float scaleZ = height * cellSize / 10f;
            floorPlane.transform.localScale = new Vector3(scaleX, 1f, scaleZ);
        }
    }

    public void SaveMaze(int episodeId, Vector3 agentPos, Vector3 targetPos)
    {
        MazeSaveData saveData = new MazeSaveData();
        saveData.mazeSize = width;
        saveData.agentStart = WorldToGrid(agentPos);
        saveData.target = WorldToGrid(targetPos);

        if (agentPos == targetPos)
        {
            Debug.LogError("Невалидный лабиринт — позиции совпадают.");
            return;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Cell cell = grid[x, y];
                MazeCellData cellData = new MazeCellData
                {
                    x = x,
                    y = y,
                    top = cell.hasTop ? 1 : 0,
                    bottom = cell.hasBottom ? 1 : 0,
                    left = cell.hasLeft ? 1 : 0,
                    right = cell.hasRight ? 1 : 0
                };
                saveData.cells.Add(cellData);
            }
        }

        string folderPath = "/MLAGTEST/Mazes/";
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText($"{folderPath}maze_episode_{episodeId}.json", json);

        Debug.Log($"Maze saved: episode {episodeId}");
    }

    private void ClearMaze()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }

    private void InitializeGrid()
    {
        grid = new Cell[width, height];

        Vector3 origin = transform.position;
        float startX = -((width - 1) * cellSize) / 2f;
        float startZ = -((height - 1) * cellSize) / 2f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 cellCenter = origin + new Vector3(startX + x * cellSize, 0, startZ + y * cellSize);

                Vector3 topPos = cellCenter + new Vector3(0, 0, cellSize / 2);
                Vector3 bottomPos = cellCenter - new Vector3(0, 0, cellSize / 2);
                Vector3 leftPos = cellCenter - new Vector3(cellSize / 2, 0, 0);
                Vector3 rightPos = cellCenter + new Vector3(cellSize / 2, 0, 0);

                GameObject top = Instantiate(wallPrefab, topPos, Quaternion.identity, transform);
                GameObject bottom = Instantiate(wallPrefab, bottomPos, Quaternion.identity, transform);
                GameObject left = Instantiate(wallPrefab, leftPos, Quaternion.Euler(0, 90, 0), transform);
                GameObject right = Instantiate(wallPrefab, rightPos, Quaternion.Euler(0, 90, 0), transform);

                grid[x, y] = new Cell
                {
                    wallTop = top,
                    wallBottom = bottom,
                    wallLeft = left,
                    wallRight = right,
                    hasTop = true,
                    hasBottom = true,
                    hasLeft = true,
                    hasRight = true,
                    visited = false
                };
            }
        }
    }

    private void DFS(Vector2Int current)
    {
        grid[current.x, current.y].visited = true;

        List<Vector2Int> directions = new List<Vector2Int>
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        Shuffle(directions);

        foreach (Vector2Int dir in directions)
        {
            Vector2Int neighbor = current + dir;

            if (IsInBounds(neighbor) && !grid[neighbor.x, neighbor.y].visited)
            {
                RemoveWallBetween(current, neighbor, dir);
                DFS(neighbor);
            }
        }
    }

    private void RemoveWallBetween(Vector2Int current, Vector2Int neighbor, Vector2Int direction)
    {
        if (direction == Vector2Int.up)
        {
            Destroy(grid[current.x, current.y].wallTop);
            Destroy(grid[neighbor.x, neighbor.y].wallBottom);
            grid[current.x, current.y].hasTop = false;
            grid[neighbor.x, neighbor.y].hasBottom = false;
        }
        else if (direction == Vector2Int.down)
        {
            Destroy(grid[current.x, current.y].wallBottom);
            Destroy(grid[neighbor.x, neighbor.y].wallTop);
            grid[current.x, current.y].hasBottom = false;
            grid[neighbor.x, neighbor.y].hasTop = false;
        }
        else if (direction == Vector2Int.left)
        {
            Destroy(grid[current.x, current.y].wallLeft);
            Destroy(grid[neighbor.x, neighbor.y].wallRight);
            grid[current.x, current.y].hasLeft = false;
            grid[neighbor.x, neighbor.y].hasRight = false;
        }
        else if (direction == Vector2Int.right)
        {
            Destroy(grid[current.x, current.y].wallRight);
            Destroy(grid[neighbor.x, neighbor.y].wallLeft);
            grid[current.x, current.y].hasRight = false;
            grid[neighbor.x, neighbor.y].hasLeft = false;
        }
    }

    private bool IsInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    private void Shuffle(List<Vector2Int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            Vector2Int temp = list[i];
            int randIndex = Random.Range(i, list.Count);
            list[i] = list[randIndex];
            list[randIndex] = temp;
        }
    }

    public Vector2Int WorldToGrid(Vector3 position)
    {
        
        Vector3 localPos = position - transform.position;
        
        
        float totalWidth = width * cellSize;
        float totalHeight = height * cellSize;
        
        
        const float epsilon = 0.001f;
        float halfCell = cellSize / 2f;
        
        
        int x = Mathf.FloorToInt((localPos.x + totalWidth/2 + halfCell + epsilon) / cellSize);
        int y = Mathf.FloorToInt((localPos.z + totalHeight/2 + halfCell + epsilon) / cellSize);
        
        
        return new Vector2Int(
            Mathf.Clamp(x, 0, width-1),
            Mathf.Clamp(y, 0, height-1)
        );
    }
}
