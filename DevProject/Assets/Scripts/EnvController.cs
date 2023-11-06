//#define DEBUGGWP
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using System;
using EnvironmentConfiguration;
using MazeConfiguration;
using System.Linq;

public enum BehaviouralPattern
{
    Decentralized = 0,
    Cooperative = 1,
    Competitive = 2 //not implemented yet
}


/// <summary>
/// A base class to set up game objects in the scene
/// It is characterised by the object itself, its local position, rotation and type
/// </summary>
[System.Serializable]
public class Info
{
    [HideInInspector]
    public Vector3 StartingPos;
    [HideInInspector]
    public Quaternion StartingRot;
    [HideInInspector]

    public virtual string EnumTypeToString()
    {
        //dummy virtual method
        return new string("");
    }

    /// <summary>
    /// Converts an Info class instance to one that could be serialised in `.json` format 
    /// </summary>
    /// <returns></returns>
    public NonSerializableBuildingBlock ConvertForSerialization()
    {
        return new NonSerializableBuildingBlock
        (
            StartingPos, StartingRot, this.EnumTypeToString()
        );
    }
}

[System.Serializable]
public class PlayerInfo : Info
{
    public MAPFAgent Agent;
    public Team teamId;

    public override string EnumTypeToString()
    {
        return teamId.ToString();
    }
}

[System.Serializable]
public class ObstacleInfo : Info
{
    public Obstacle obstacleElement;
    public ObstacleType obstacleType;

    public override string  EnumTypeToString()
    {
        return obstacleType.ToString();
    }
}

[System.Serializable]
public class GoalInfo : Info
{
    public GoalInstance Goal;
    public GoalType goalType;

    public override string EnumTypeToString()
    {
        return goalType.ToString();
    }

}


/// <summary>
/// This class controlls behaviour of the whole environment
/// </summary>
public class EnvController : MonoBehaviour
{   
    private GameObject area;

    [HideInInspector]
    public EnvSettings envSettings;

    private BehaviouralPattern behaviouralPattern = BehaviouralPattern.Decentralized;
    
    //to dump or load configuations using files 
    private MazeBuilder mazeBuilder = new MazeBuilder();

    [HideInInspector]
    private Bounds areaBounds;
    
    [HideInInspector]
    public Bounds AreaBounds {get; private set;}
    
    [HideInInspector]
    private Vector3 areaPosition;

    [HideInInspector]
    public Vector3 AreaPosition {get; private set;}

    [HideInInspector]
    public RectangularGrid areaRectangularGrid;

    //List of Agents On Platform
    public List<PlayerInfo> AgentsList = new List<PlayerInfo>();
    public List<string> AgentsNames = new();

    //List of Blocks On Platform
    public List<ObstacleInfo> BlocksList = new List<ObstacleInfo>();
    //List of Possible goals to achieve
    public List<GoalInfo> GoalsList = new List<GoalInfo>();

    public Dictionary<string, float[]> DistanceThr  = new();

    private SimpleMultiAgentGroup m_AgentGroup;

    //spawn flags
    public bool RandomizeAgentPosition = true;
    public bool RandomizeAgentRotation = true;
    public bool RandomizeGoalPosition = true;
    
    public bool differentiateRoles = false;
    private bool onConstruction = false; 

    private int numberActiveAgents = 0;
    private int resetTimer = 0;
    public int MaxEnvironmentSteps;

    private void RecoverSceneFromConfig(string sourceConfigFile)
    {
        mazeBuilder.LoadMaze(sourceConfigFile);
        var mapSize = mazeBuilder.MapSize;
        var buildingBlockSize = mazeBuilder.BaseBlockSize;

        // mazeBuilder.Room -> BlocksList
        // mazeBuilder.Agents -> AgentsList
        // mazeBuilder.Goals -> GoalsList

        //creating a battleground
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
     
        plane.transform.localScale = new Vector3(mapSize.x, 1, mapSize.y);
        plane.tag = "Surface";
        plane.name = "RPlane";

        plane.transform.localPosition = this.transform.localPosition; // this.transform.localPosition;
        
        plane.transform.SetParent(this.transform);
        area = plane; 

        for (int i = 0; i < mazeBuilder.Room.Count; i++)
        {
            //shit
            var item = mazeBuilder.Room[i];

            GameObject tmpObject = new();
            Obstacle.ConfigureComponents(ref tmpObject);
            tmpObject.transform.SetParent(this.transform);
            tmpObject.AddComponent<Obstacle>();

            tmpObject.transform.localScale = buildingBlockSize;
            tmpObject.transform.SetLocalPositionAndRotation(item.Position, item.Rotation);

            var obstacle = new ObstacleInfo
            {
                obstacleElement = tmpObject.GetComponent<Obstacle>(),
                StartingPos = tmpObject.transform.localPosition,
                StartingRot = tmpObject.transform.localRotation
            };

            obstacle.obstacleElement.name = $"{i + 1}";


            if (Enum.TryParse(item.Type, out ObstacleType obstacleType))
                obstacle.obstacleType = obstacleType;
            
            obstacle.obstacleElement.isMovable = obstacleType == ObstacleType.Movable;
            BlocksList.Add(obstacle);
        }

        for (int i = 0; i < mazeBuilder.Agents.Count; i++)
        {
            var item = mazeBuilder.Agents[i];
            GameObject tmpObject = new();
            MAPFAgent.ConfigureComponents(ref tmpObject);
            tmpObject.transform.SetParent(this.transform);

            tmpObject.AddComponent<MAPFAgent>();
            tmpObject.transform.SetLocalPositionAndRotation(item.Position, item.Rotation);

            var agent = new PlayerInfo
            {
                Agent = tmpObject.GetComponent<MAPFAgent>(),
                StartingPos = tmpObject.transform.localPosition,
                StartingRot = tmpObject.transform.localRotation
            };

            agent.Agent.name = $"{i + 1}";

            if (Enum.TryParse(item.Type, out Team teamId))
                agent.teamId = teamId;

            agent.Agent.tag = "Agent";
            agent.Agent.isActive = false;

            if (agent.teamId == Team.Active)
            {
                agent.Agent.tag = "ActiveAgent";
                agent.Agent.isActive = true;
                this.SubscribeAgentToObstacles(ref agent.Agent);
            }
            else if (agent.teamId == Team.ActiveCooperative)
            {
                agent.Agent.tag = "ActiveCooperativeAgent";
                agent.Agent.isActive = true;
                agent.Agent.willingToCooperate = true;
                this.SubscribeAgentToObstacles(ref agent.Agent);
            }

            m_AgentGroup.RegisterAgent(agent.Agent);

            AgentsList.Add(agent);
            AgentsNames.Add(agent.Agent.name);
        }

        for (int i =0; i < mazeBuilder.Goals.Count; i++)
        {
            var item = mazeBuilder.Goals[i];
            GameObject tmpObject = new();
            GoalInstance.ConfigureComponents(ref tmpObject);
            tmpObject.transform.SetParent(this.transform);

            tmpObject.AddComponent<GoalInstance>();
            tmpObject.transform.SetLocalPositionAndRotation(item.Position, item.Rotation);
            
            if (Enum.TryParse(item.Type, out GoalType goalType))
            {
                GameObject obj; 
    
                switch ((int) goalType)
                {
                    case (int)GoalType.Sphere:
                        tmpObject.AddComponent<SphereCollider>();
                        obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        break;
                    case (int)GoalType.Cube:
                        tmpObject.AddComponent<BoxCollider>();
                        obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        break;
                    case (int)GoalType.Capsule:
                        tmpObject.AddComponent<CapsuleCollider>();
                        obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        break;
                    default:
                        tmpObject.AddComponent<SphereCollider>();
                        obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        break;
                }

                Mesh mesh = Instantiate(obj.GetComponent<MeshFilter>().mesh);
                tmpObject.GetComponent<MeshFilter>().sharedMesh = mesh;
                Destroy(obj);
            }

            Collider collider = tmpObject.GetComponent<Collider>();
            collider.isTrigger = true;
            tmpObject.name = $"Goal{i+1}";
            tmpObject.tag = "Goal";
            tmpObject.transform.localScale = Vector3.one * envSettings.globalSymmetricScale;


            var goal = new GoalInfo
            {
                Goal = tmpObject.GetComponent<GoalInstance>(),
                StartingPos = tmpObject.transform.localPosition,
                StartingRot = tmpObject.transform.localRotation
            };
            
                goal.goalType = goalType;

            GoalsList.Add(goal);
        }

        mazeBuilder.Clear();
    }

    public void DumpSceneToConfig(string destinationConfigFile)
    {
        mazeBuilder.SetMapSizes
        (
            new Vector2(area.transform.localScale.x, area.transform.localScale.z), 
            BlocksList[0].obstacleElement.transform.localScale
        );

        foreach (var item in BlocksList) 
            mazeBuilder.Room.Add(item.ConvertForSerialization());
        
        foreach (var item in AgentsList)
            mazeBuilder.Agents.Add(item.ConvertForSerialization());
        
        foreach (var item in GoalsList)
            mazeBuilder.Goals.Add(item.ConvertForSerialization());

        mazeBuilder.DumpMaze(destinationConfigFile);
    }

    private Team AssignRolesRandomly(float assign_prob)
    {   

        System.Random rand = new(envSettings.seed + 420);

        if (rand.NextDouble() >= assign_prob)
        {    
            return Team.Active;
        }

        return Team.Passive;

    }

    public bool CheckActiveAgents()
    {
        numberActiveAgents = 0;
        foreach (var item in AgentsList)
        {
            numberActiveAgents += (item.teamId == Team.Active || item.teamId == Team.ActiveCooperative) ? 1 : 0;
            if (item.teamId == Team.ActiveCooperative)
            {
                behaviouralPattern = BehaviouralPattern.Cooperative;
            }
        }

        if (numberActiveAgents > 0)
            return true;

        return false;

    }

    public void Clear()
    {
        AgentsList.Clear();
        AgentsNames.Clear();
        //areaRectangularGrid.Clear();
        BlocksList.Clear();
        GoalsList.Clear();
    }
    
    private void SubscribeAgentToObstacles(ref MAPFAgent agent)
    {
        foreach (var item in BlocksList)
        {   
            if (item.obstacleType ==  ObstacleType.Movable)
            {   
                agent.OnMoveRequested += (sender, args) => { item.obstacleElement.MovingRequestHandler(args.ObjectID); };
            }
        }
    }

    public void Start() 
    {      
        Clear();
        onConstruction = true;
        envSettings = this.GetComponent<EnvSettings>();
        m_AgentGroup = new SimpleMultiAgentGroup();
        if (envSettings.useGridMovement)
        {
            areaRectangularGrid = this.GetComponent<RectangularGrid>();
        }
        //mlagents to know the reset func
        Academy.Instance.OnEnvironmentReset += ResetScene;  
    }

    public void FixedUpdate() {
        // here episode termination is tracked
        resetTimer += 1;
        if (resetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0)
        {
            //reset agents iteratively
            m_AgentGroup.GroupEpisodeInterrupted();
            ResetScene();
        }

        foreach (var item in AgentsList)
        {
            if (item.Agent.transform.localPosition.y - 1.1f< area.transform.localPosition.y)
            {
                //even one agent falls out of borders causes a reset
                //even if the goals are completed all agents should stay alive!
                item.Agent.AddReward(envSettings.invdividualRewards[GameEvent.AgentOutOfBounds]);
                m_AgentGroup.AddGroupReward(envSettings.invdividualRewards[GameEvent.AgentOutOfBounds]);
                m_AgentGroup.GroupEpisodeInterrupted();
                ResetScene();
            }
        }

        var rewardForCooperation = 0f;
        foreach (var agentName in AgentsNames)
        {
            float tmpReward = 0f;
            if (DistanceThr.TryGetValue(agentName, out float[] goalThr))
            {
                foreach (var item in goalThr)
                {
                    tmpReward = Mathf.Min(item, tmpReward); 
                }
                rewardForCooperation += tmpReward;
            }
        }
        m_AgentGroup.AddGroupReward(Mathf.Clamp(rewardForCooperation, 0f, 0.33f));
    }

    private Vector3 GetRandomSpawnPos(Collider collider)
    {
        Vector3 randomSpawnPos = Vector3.zero;
        int i = 0;
        int maxEfforts = 1000;
        while (true)
        {
            var randomPosX = UnityEngine.Random.Range(-areaBounds.extents.x * envSettings.spawnAreaMarginMultiplier,
                                                      areaBounds.extents.x * envSettings.spawnAreaMarginMultiplier);

            var randomPosZ = UnityEngine.Random.Range(-areaBounds.extents.z * envSettings.spawnAreaMarginMultiplier,
                                                      areaBounds.extents.z * envSettings.spawnAreaMarginMultiplier);


            //random shift 
            if (this.envSettings.useGridMovement)
            {
                randomSpawnPos = areaRectangularGrid.GetCoords(UnityEngine.Random.Range(0, areaRectangularGrid.maxCellIndex));
            }
            else
            {
                randomSpawnPos = new Vector3(areaPosition.x + randomPosX, 
                                             collider.transform.localPosition.y + 1.1f, 
                                             areaPosition.z + randomPosZ);
            }

            var overlapBox = new Vector3(envSettings.SpawnOverlapBox.x,
                                         collider.transform.localPosition.y,
                                         envSettings.SpawnOverlapBox.y);

            //check on a collider type.

#if DEBUGGWP
            var tmp = Physics.OverlapBox(randomSpawnPos, overlapBox);
            {

                if (tmp.Length == 1)
                    Debug.Log(
                    randomSpawnPos + " " + tmp[0].tag + " " + tmp[0].transform.localPosition
                );
            }
#endif
            if (Physics.CheckBox(randomSpawnPos, overlapBox) == false)
            {
                break;
            }

            i++;
            if (i >= maxEfforts)
            {
                Debug.Log("Tried to respawns >= " + maxEfforts + " stimes. Last spawn returned");
                break;
            }
        }
        return randomSpawnPos;
    }


    Quaternion GetRandomSpawnRotation()
    {
        var rotationAngles = envSettings.rotationAngles;
        return Quaternion.Euler(0, UnityEngine.Random.Range(rotationAngles[0], rotationAngles[1]), 0);
    }

    public void ConstructScene()
    {
        if (!envSettings.loadEnvironmentConfiguration) //from visual editor;
        {
            area = GameObject.FindGameObjectsWithTag("Surface")[0];
            this.transform.position = areaBounds.center;
            var foundObstacles = FindObjectsOfType<Obstacle>();
            for (int i = 0; i < foundObstacles.Length; i++)
            {
                var item = foundObstacles[i];
                item.name += $"{i+1}";
                if (item.enabled)
                {
                    
                    ObstacleInfo obstacle = new ObstacleInfo
                    {
                        obstacleElement = item,
                        StartingPos = item.transform.localPosition,
                        StartingRot = item.transform.localRotation,
                        obstacleType = item.isMovable ? ObstacleType.Movable : ObstacleType.Immovable
                    };
                    BlocksList.Add(obstacle);
                }
            }

            var foundAgents = FindObjectsOfType<MAPFAgent>();
            for (int i = 0; i < foundAgents.Length; i++)
            {
                var item = foundAgents[i];
                item.name += $"{i + 1}";
                if (item.enabled)
                {
                    PlayerInfo agent = new();
                    agent.Agent = item;
                    if (differentiateRoles)
                    {
                        agent.teamId = AssignRolesRandomly(envSettings.differentiateRolesProb);
                        agent.Agent.isActive = agent.teamId == Team.Active;
                    }
                    else
                    {
                        if (agent.Agent.isActive && agent.Agent.willingToCooperate)
                        {
                            agent.teamId = Team.ActiveCooperative;
                        }
                        else if (agent.Agent.isActive && !agent.Agent.willingToCooperate)
                        {
                            agent.teamId = Team.Active;
                        }
                        else 
                        {
                            agent.teamId = Team.Passive;
                        }
                   
                    }

                    if (agent.teamId == Team.Active || agent.teamId == Team.ActiveCooperative)
                    {
                        this.SubscribeAgentToObstacles(ref agent.Agent);
                    }
                    agent.StartingPos = item.transform.localPosition;
                    agent.StartingRot = item.transform.localRotation;
                    AgentsList.Add(agent);
                    
                    AgentsNames.Add(agent.Agent.name);
                    m_AgentGroup.RegisterAgent(item);
                }

            }

            var foundGoals = FindObjectsOfType<GoalInstance>();
            for (int i = 0; i < foundGoals.Length; i++)
            {
                var item = foundGoals[i];
                item.name += $"{i + 1}";
                if (item.enabled)
                {
                    GoalInfo goal = new GoalInfo
                    {
                        Goal = item,
                        StartingPos = item.transform.localPosition,
                        StartingRot = item.transform.localRotation,
                        goalType = GoalType.Sphere
                    };
                    GoalsList.Add(goal);
                }
            }

        }
        else
        {
            try
            {
                RecoverSceneFromConfig(envSettings.baseConfigFile);
            }
            catch
            {
                Debug.Log("Something went wrong when reading your config file!");
            }
        }

        // Sanity checks:
        // at least one goal
        // at least one active agent
        area.GetComponent<Renderer>().material.color = envSettings.defaultAreaColor;
        areaBounds = area.GetComponent<Collider>().bounds;
        area.GetComponent<MeshCollider>().convex = true;

        areaPosition = area.transform.localPosition;

        if (!CheckActiveAgents())
        {
            throw new Exception("Should be at least one active agent");
        }

        if (GoalsList.Count == 0)
        {
            throw new Exception("Should be at least one goal initialised");
        }
    }

    public void ResetScene()
    {
        if (onConstruction)
        {
            ConstructScene();

            if (envSettings.saveEnvironmentConfiguration && envSettings.backupConfigFile != null)
            {
                this.DumpSceneToConfig(envSettings.backupConfigFile);
            }

            onConstruction = false;
        }
        else
        {

            //resetting the environent according to a given seed
            var seed = envSettings.seed;
            if (seed != -1)
            {
                UnityEngine.Random.InitState(seed);
            }

            resetTimer = 0;

            foreach (var item in BlocksList)
            {
                var pos = item.StartingPos;
                var rot = item.StartingRot;
                item.obstacleElement.transform.SetLocalPositionAndRotation(pos, rot);
                item.obstacleElement.Reset();

            }
            Physics.SyncTransforms();


            foreach (var item in AgentsList)
            {
                var agentSubSeed = AgentsList.IndexOf(item);
                var pos = RandomizeAgentPosition ? GetRandomSpawnPos(item.Agent.agentCl) : item.StartingPos;
                var rot = RandomizeAgentRotation ? GetRandomSpawnRotation() : item.StartingRot;
                item.Agent.transform.SetLocalPositionAndRotation(pos, rot);
                item.Agent.agentRb.velocity = Vector3.zero;
                item.Agent.agentRb.angularVelocity = Vector3.zero;
            }

            Physics.SyncTransforms();

            foreach (var item in GoalsList)
            {
                var goalSubSeed = GoalsList.IndexOf(item);
                var pos = RandomizeGoalPosition ? GetRandomSpawnPos(item.Goal.goalCl) : item.StartingPos;
                var rot = item.StartingRot;
                
                item.Goal.Reset();
                item.Goal.transform.SetLocalPositionAndRotation(pos, rot);
                //changing goalType --- for real?
            }
            Physics.SyncTransforms();

            mazeBuilder.Clear();
        }

        //resetting the table
        DistanceThr.Clear();
        foreach (var item in AgentsList)
        {
            var arr = new float[GoalsList.Count];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = Mathf.Infinity;
            }
            DistanceThr.Add(item.Agent.name, arr);
        }

    }

    public float[] UpdateThresholds(string agentName)
    {
        float[] prevDist = (float[])DistanceThr[agentName].Clone();

        var agentP = AgentsList[AgentsNames.IndexOf(agentName)].Agent.transform.localPosition;
        for (int i = 0; i < GoalsList.Count; i++) 
        {
            var goalP = GoalsList[i].Goal.transform.localPosition;
            DistanceThr[agentName][i] = Mathf.Min(Vector3.Distance(agentP, goalP), DistanceThr[agentName][i]);
        }

        return prevDist.Select((x, index) => x - DistanceThr[agentName][index]).ToArray();
    }

    public void UpdateStatistics() 
    {
        
        bool terminated = false;

        int goalsCompleted = 0;
        foreach (var item in GoalsList)
        {
            var goal = item.Goal;

            if (goal.isCompleted)
            {
                goalsCompleted++;
            }
            if (goal.isTouched)
            {
                StartCoroutine(GoalScoredSwapGroundColor(envSettings.hintAreaColor,
                                            area.GetComponent<Renderer>(),
                                            0.5f));
            }
        }

        if (goalsCompleted == GoalsList.Count)
        {
            terminated = true;
            m_AgentGroup.AddGroupReward(goalsCompleted * envSettings.invdividualRewards[GameEvent.ActiveAgentHitGoal]);
            if (behaviouralPattern == BehaviouralPattern.Cooperative)
            {
                //cooperative setting should reward assistance in goal reaching
                m_AgentGroup.AddGroupReward(envSettings.groupRewards[GameEvent.ActiveAgentAssisted]);
            }
        }

        if (terminated)
        {
            //Reset assets due to goal completion
            //Debug.Log("Resetting Due to termination");
            m_AgentGroup.EndGroupEpisode();
            ResetScene();
        }
    }

    IEnumerator GoalScoredSwapGroundColor(Color c, Renderer renderer, float time)
    {
        renderer.material.color = c;

        yield return new WaitForSeconds(time);
        
        renderer.material.color = envSettings.defaultAreaColor;

    }

}