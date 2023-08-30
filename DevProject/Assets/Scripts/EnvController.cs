using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using System;
using EnvironmentConfiguration;
using MazeConfiguration;



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


    //List of Agents On Platform
    public List<PlayerInfo> AgentsList = new List<PlayerInfo>();
    //List of Blocks On Platform
    public List<ObstacleInfo> BlocksList = new List<ObstacleInfo>();
    //List of Possible goals to achieve
    public List<GoalInfo> GoalsList = new List<GoalInfo>();

    private SimpleMultiAgentGroup m_AgentGroup;

    //spawn flags
    public bool RandomizeAgentPosition = true;
    public bool RandomizeAgentRotation = true;
    public bool RandomizeGoalPosition = true;
    
    public bool differentiateRoles = false;
    private bool onConstruction = true; 

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

        foreach (var item in mazeBuilder.Room)
        {
            //shit
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


            if (Enum.TryParse(item.Type, out ObstacleType obstacleType))
                obstacle.obstacleType = obstacleType;
            
            obstacle.obstacleElement.isMovable = obstacleType == ObstacleType.Movable;
            BlocksList.Add(obstacle);
        }

        foreach (var item in mazeBuilder.Agents)
        {
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

            m_AgentGroup.RegisterAgent(agent.Agent);

            AgentsList.Add(agent);
        }

        foreach (var item in mazeBuilder.Goals)
        {
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
            tmpObject.name = "Goal";
            tmpObject.tag = "Goal";
            tmpObject.transform.localScale = Vector3.one * 10f;


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
            numberActiveAgents += item.teamId == Team.Active ? 1 : 0;
        }

        if (numberActiveAgents > 0)
            return true;

        return false;

    }

    public void Clear()
    {
        AgentsList.Clear();
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
        envSettings = this.GetComponent<EnvSettings>();
        m_AgentGroup = new SimpleMultiAgentGroup();
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
            if (item.Agent.transform.localPosition.y < area.transform.localPosition.y + 0.5f)
            {
                //even one agent falls out of borders implies a reset
                //even if the goals are completed all agents should stay alive!
                m_AgentGroup.AddGroupReward(envSettings.invdividualRewards[GameEvent.AgentOutOfBounds]);
                m_AgentGroup.GroupEpisodeInterrupted();
                ResetScene();
            }
        }
    }

    private Vector3 GetRandomSpawnPos()
    {
        Vector3 randomSpawnPos = Vector3.zero;
        while(true)
        {           
            var randomPosX = UnityEngine.Random.Range(-areaBounds.extents.x * envSettings.spawnAreaMarginMultiplier,
                                                      areaBounds.extents.x * envSettings.spawnAreaMarginMultiplier);

            var randomPosZ = UnityEngine.Random.Range(-areaBounds.extents.z * envSettings.spawnAreaMarginMultiplier,
                                                      areaBounds.extents.z * envSettings.spawnAreaMarginMultiplier);

            
            //random shift 
            
            randomSpawnPos = new Vector3(areaPosition.x + randomPosX, 0f, areaPosition.z + randomPosZ);

            //check on a collider type.
            if (Physics.CheckBox(randomSpawnPos, envSettings.SpawnOverlapBox) == false)
            {
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
            foreach (var item in FindObjectsOfType<Obstacle>())
            {
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


            foreach (var item in FindObjectsOfType<MAPFAgent>())
            {
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
                        agent.teamId = agent.Agent.isActive ? Team.Active : Team.Passive;
                    }

                    if (agent.teamId == Team.Active)
                    {
                        this.SubscribeAgentToObstacles(ref agent.Agent);
                    }
                    agent.StartingPos = item.transform.localPosition;
                    agent.StartingRot = item.transform.localRotation;
                    AgentsList.Add(agent);

                    m_AgentGroup.RegisterAgent(item);
                }

            }

            foreach (var item in FindObjectsOfType<GoalInstance>())
            {
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

            foreach (var item in GoalsList)
            {
                var goalSubSeed = GoalsList.IndexOf(item);
                var pos = RandomizeGoalPosition ? GetRandomSpawnPos() : item.StartingPos;
                var rot = item.StartingRot;
                item.Goal.Reset();
                item.Goal.transform.SetLocalPositionAndRotation(pos, rot);
                //changing goalType --- for real?
            }
            Physics.SyncTransforms();

            foreach (var item in AgentsList)
            {
                var agentSubSeed = AgentsList.IndexOf(item);
                var pos = RandomizeAgentPosition ? GetRandomSpawnPos() : item.StartingPos;
                var rot = RandomizeAgentRotation ? GetRandomSpawnRotation() : item.StartingRot;
                item.Agent.transform.SetLocalPositionAndRotation(pos, rot);
                item.Agent.agentRb.velocity = Vector3.zero;
                item.Agent.agentRb.angularVelocity = Vector3.zero;
            }

            mazeBuilder.Clear();
        }
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
        }
 
        if (terminated)
        {
            //Reset assets due to goal completion
            m_AgentGroup.EndGroupEpisode();
            ResetScene();
            foreach (var item in GoalsList)
            {
                item.Goal.Reset();
            }
        }
    }

    IEnumerator GoalScoredSwapGroundColor(Color c, Renderer renderer, float time)
    {
        renderer.material.color = c;

        yield return new WaitForSeconds(time);
        
        renderer.material.color = envSettings.defaultAreaColor;

    }

}