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
    public EnvSettings envSettings = new EnvSettings();

    
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
    
    // to deprecate one!
    public bool differentiateRoles = false;


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
        Instantiate(plane, Vector3.zero, Quaternion.identity);
        //

        foreach (var item in mazeBuilder.Room)
        {
            //shit
            GameObject tmpObject = new GameObject();
            tmpObject.AddComponent<Obstacle>();
            tmpObject.transform.SetLocalPositionAndRotation(item.Position, item.Rotation);            
            
            var obstacle = new ObstacleInfo();
            obstacle.obstacleElement = tmpObject.GetComponent<Obstacle>();
            obstacle.StartingPos = tmpObject.transform.localPosition;
            obstacle.StartingRot = tmpObject.transform.localRotation;
            if (Enum.TryParse(item.Type, out ObstacleType obstacleType))
                obstacle.obstacleType = obstacleType;

            BlocksList.Add(obstacle);
        }

        foreach (var item in mazeBuilder.Agents)
        {
            GameObject tmpObject = new GameObject();
            tmpObject.AddComponent<GoalInstance>();
            tmpObject.transform.SetLocalPositionAndRotation(item.Position, item.Rotation);            
            
            var agent = new PlayerInfo();
            agent.Agent = tmpObject.GetComponent<MAPFAgent>();
            agent.StartingPos = tmpObject.transform.localPosition;
            agent.StartingRot = tmpObject.transform.localRotation;

            if (Enum.TryParse(item.Type, out Team teamId))
                agent.teamId = teamId;

            if (agent.teamId == Team.Active)
            {
                this.SubscribeAgentToObstacles(ref agent.Agent);
            }

            m_AgentGroup.RegisterAgent(agent.Agent);

            AgentsList.Add(agent);
        }

        foreach (var item in mazeBuilder.Goals)
        {
            GameObject tmpObject = new GameObject();
            tmpObject.AddComponent<GoalInstance>();
            tmpObject.transform.SetLocalPositionAndRotation(item.Position, item.Rotation);            
            
            var goal = new GoalInfo();
            goal.Goal = tmpObject.GetComponent<GoalInstance>();
            goal.StartingPos = tmpObject.transform.localPosition;
            goal.StartingRot = tmpObject.transform.localRotation;
            if (Enum.TryParse(item.Type, out GoalType goalType)) 
                goal.goalType = goalType;

            GoalsList.Add(goal);
        }

        mazeBuilder.Clear();


    }

    public void DumpSceneToConfig(string destinationConfigFile)
    {
        mazeBuilder.SetMapSizes
        (
            new Vector2(areaBounds.size.x, areaBounds.size.z), 
            BlocksList[0].obstacleElement.GetComponent<Renderer>().bounds.size
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

        System.Random rand = new System.Random(envSettings.seed + 420);

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
            numberActiveAgents += (item.teamId == Team.Active ? 1 : 0);
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
        m_AgentGroup = new SimpleMultiAgentGroup();      
              
        if (!envSettings.loadEnvironmentConfiguration)
        {
            area = GameObject.FindGameObjectsWithTag("Surface")[0];
            this.transform.position = areaBounds.center;
            foreach (var item in FindObjectsOfType<Obstacle>())
            {   
                if (item.enabled)   
                {
                    var obstacle = new ObstacleInfo();
                    obstacle.obstacleElement = item;
                    obstacle.StartingPos = item.transform.localPosition;
                    obstacle.StartingRot = item.transform.localRotation;
                    obstacle.obstacleType = item.isMovable ? ObstacleType.Movable : ObstacleType.Immovable;
                    BlocksList.Add(obstacle);  
                }
            }


            foreach (var item in FindObjectsOfType<MAPFAgent>())
            {
                if (item.enabled)   
                {
                    var agent = new PlayerInfo();
                    agent.Agent = item;
                    if (differentiateRoles)
                    {
                        agent.teamId = AssignRolesRandomly(envSettings.differentiateRolesProb);    
                        agent.Agent.isActive = agent.teamId==Team.Active ? true : false;
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
                    var goal = new GoalInfo();
                    goal.Goal = item;
                    goal.StartingPos = item.transform.localPosition;
                    goal.StartingRot = item.transform.localRotation;
                    goal.goalType = GoalType.Sphere;            
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

        if (!CheckActiveAgents())
        {
            throw new Exception("Should be at least one active agent");
        }

        if (GoalsList.Count == 0)
        {
            throw new Exception("Should be at least one goal initialised");
        }

        area.GetComponent<Renderer>().material.color = envSettings.defaultAreaColor;
        areaBounds = area.GetComponent<Collider>().bounds;
        areaPosition = area.transform.localPosition;


        if (envSettings.saveEnvironmentConfiguration && envSettings.backupConfigFile != null)
        {
            this.DumpSceneToConfig(envSettings.backupConfigFile);
        }
        //mlagents to know reset func
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
            if (item.Agent.transform.localPosition.y < area.transform.localPosition.y)
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
        var randomSpawnPos = Vector3.zero;
//        int i = 0;
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
    public void ResetScene()
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