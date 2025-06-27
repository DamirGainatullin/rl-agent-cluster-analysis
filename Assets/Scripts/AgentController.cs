using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Globalization;

public class AgentController : Agent
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private MazeGenerator maze;
    [SerializeField] private RayPerceptionSensor rayPerceptionSensor;

    private bool episodeEnded = false;


    private float distanceTraveled = 0f;
    private Vector3 lastPosition;
    private int stepCount = 0;
    private int episodeId = 0;


    [Header("Maze Settings")]
    [Range(3, 31)] public int mazeSize = 3; 

    private Rigidbody rb;
    private int[] validPositions;
    private HashSet<Vector2Int> visitedCells = new HashSet<Vector2Int>();
    private int globalEpisodeId = -1;


    private int GetGlobalEpisodeId()
    {
        string path = "MLAGTEST/Logs/global_episode_counter.txt";
        if (!System.IO.File.Exists(path))
        {
            System.IO.File.WriteAllText(path, "1");
            return 1;
        }
        else
        {
            int id = int.Parse(System.IO.File.ReadAllText(path));
            id++;
            System.IO.File.WriteAllText(path, id.ToString());
            return id;
        }
    }



    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        UpdateValidPositions(mazeSize);
    }

    private void UpdateValidPositions(int size)
    {
        List<int> positions = new List<int>();
        int half = size / 2 + 1;
        for (int i = -half; i <= half; i++)
        {
            if (i % 2 == 0)
                positions.Add(i);
        }
        validPositions = positions.ToArray();
    }

    public override void OnEpisodeBegin()
    {
        episodeId = GetGlobalEpisodeId();
        stepCount = 0;
        distanceTraveled = 0f;
        episodeEnded = false;

        UpdateValidPositions(mazeSize);

        
        List<Vector2Int> allValidPositions = new List<Vector2Int>();
        foreach (int x in validPositions)
        {
            foreach (int z in validPositions)
            {
                allValidPositions.Add(new Vector2Int(x, z));
            }
        }

        
        if (allValidPositions.Count < 2)
        {
            Debug.LogError("Not enough valid positions!");
            return;
        }

        
        int agentIndex = Random.Range(0, allValidPositions.Count);
        Vector2Int agentGrid = allValidPositions[agentIndex];
        
        
        allValidPositions.RemoveAt(agentIndex);

        
        int targetIndex = Random.Range(0, allValidPositions.Count);
        Vector2Int targetGrid = allValidPositions[targetIndex];

        Vector3 agentPos = new Vector3(agentGrid.x, 0.25f, agentGrid.y);
        Vector3 targetPos = new Vector3(targetGrid.x, 0.25f, targetGrid.y);

        transform.localPosition = agentPos;
        lastPosition = agentPos;
        target.localPosition = targetPos;

        visitedCells.Clear();
        maze.GenerateMaze(mazeSize, agentPos, targetPos);
        maze.SaveMaze(episodeId, agentPos, targetPos);

        
        if (Vector3.Distance(agentPos, targetPos) < 0.1f)
        {
            Debug.LogError($"Agent and target too close! Agent: {agentPos}, Target: {targetPos}");
        }
    }


    public override void OnActionReceived(ActionBuffers actions)
    {
        stepCount++;
        float moveRotate = actions.ContinuousActions[0];
        float moveForward = actions.ContinuousActions[1];
        float moveSpeed = 4f;

        rb.MovePosition(transform.position + transform.forward * moveForward * moveSpeed * Time.deltaTime);
        transform.Rotate(0f, moveRotate * moveSpeed, 0f, Space.Self);

        distanceTraveled += Vector3.Distance(transform.localPosition, lastPosition);
        lastPosition = transform.localPosition;

        Vector2Int currentCell = new Vector2Int(
            Mathf.RoundToInt(transform.localPosition.x),
            Mathf.RoundToInt(transform.localPosition.z)
        );

        if (!visitedCells.Contains(currentCell))
        {
            visitedCells.Add(currentCell);
            AddReward(+0.01f);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");
    }


    private void LogEpisode(bool success)
    {
        if (stepCount <= 1 || visitedCells.Count == 0)
            return;

        string path = "/MLAGTEST/Logs/episode_data.csv";

        if (!System.IO.File.Exists(path))
        {
            string header = "episode_id,total_reward,success,time_to_goal,visited_cells_count,distance_traveled,maze_size";
            System.IO.File.WriteAllText(path, header + "\n");
        }

        string log = $"{episodeId}," +
             $"{GetCumulativeReward().ToString("F3", CultureInfo.InvariantCulture)}," +
             $"{(success ? 1 : 0)}," +
             $"{stepCount}," +
             $"{visitedCells.Count}," +
             $"{distanceTraveled.ToString("F3", CultureInfo.InvariantCulture)}," +
             $"{mazeSize}";

        System.IO.File.AppendAllText(path, log + "\n");
    }


    private void OnTriggerEnter(Collider other)
    {
        if (episodeEnded) return;

        if (other.CompareTag("Pellet"))
        {
            AddReward(2f);
            LogEpisode(true);
            episodeEnded = true;
            EndEpisode();
        }
        else if (other.CompareTag("Wall"))
        {
            AddReward(-1f);
            LogEpisode(false);
            episodeEnded = true;
            EndEpisode();
        }
    }

    public int GetMazeSize() => mazeSize;
}
