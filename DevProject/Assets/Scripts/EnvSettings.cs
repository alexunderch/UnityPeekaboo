using UnityEngine;
using System.Collections.Generic;

namespace EnvironmentConfiguration
{   
    /// <summary>
    /// Enum for evertying that should be rewarded because it'd happened
    /// </summary>
    public enum GameEvent
    {
        ActiveAgentHitGoal = 0,
        ActiveAgentAssisted = 1,
        AgentHitGoal = 2,
        AgentOutOfBounds = 3,
        AgentHitObstacle = 4,
        ActiveAgentHitMovableObstacle = 5,
        AgentHitAgent = 6,
        AllGoalsCompleted = 7,
    }

    /// <summary>
    /// Enum for all the roles possible for agents.
    /// Active agent can move some objects and demonatrate special behavioural types
    /// Coopeative agent differs form the active one by the fact it cannot reach goals themselves 
    /// but can assist doing so
    /// </summary>
    public enum Team 
    {
        Passive = 0,
        Active = 1,
        ActiveCooperative = 2
    }

    /// <summary>
    /// Enum for wall's behavioural types
    /// </summary>
    public enum ObstacleType 
    {
        Movable = 0,
        Immovable = 1
    }

    /// <summary>
    /// WIP! every goal has its own type,
    /// and the behaviour depends on it.
    /// Also, there is also possible type transormations given a set of rules
    /// </summary>
    public enum GoalType
    {
        //goal types could ever appear
        Cube = 0,
        Sphere = 1,
        Capsule = 2,
        Default = 3
    }

    /// <summary>
    /// A collection of constants for the environment. 
    /// Reward mapping is also specified here.
    /// </summary>
    public class EnvSettings : MonoBehaviour {


        public List<string> tags = new() {
            "Goal", "Obstacle", "MovableObstacle", "Agent", "ActiveAgent", "ActiveCooperativeAgent", "Surface", "Barrier"};

        public string backupConfigFile = "../configs/maps/dev_map.json";
        public string baseConfigFile = "../configs/maps/dev_map2.json";
        public bool saveEnvironmentConfiguration = false;
        public bool loadEnvironmentConfiguration = false;

        public int seed = -1; // -1 means "no seed"

        public bool recordDemonstrations = false;
        public string demonstrationDirectory = "./Assets/Demonstrations";
        public int recordLength = 0;      

        public float agentMass = 10.337f;
        public float agentMovingSpeed = 79f;
        public float agentRotationSpeed = 99.5f;
        public float agentJumpForce = 300.0f;
        public float agentFallingForce = 500.0f;
        public float differentiateRolesProb = 0.5f;
        public float obstacleAvoidanceDistance = 18.1f;

        //necessary raycast vision arguments
        public int raysPerDirection = 4;
        public float maxRayDegrees = 45f;
        public float raycastSpereRadius = 0.33f;
        public int rayLength = 69;

        public float globalSymmetricScale = 10f;

        public Color activeAgentColour = new Color32(255, 215, 0, 255);
        public Color passiveAgentColour =  new Color32(192, 192, 192, 255);

        public Color immovableObstacleColour = new Color32(90, 90, 90, 255); // new Color(0.9f, 0.1f, 0.52f);
        public Color movableObstacleColour = Color.magenta;  
        public  float movableObstacleSpeed = 100f;

        public Color goalColour = Color.green; // new Color(0.84f, 0.14f, 0.38f);
        public Color completedGoalColour = Color.yellow;// new Color(0.14f, 0.58f, 0.78f);

        public Color defaultAreaColor = new Color(0.83f, 0.72f, 0.69f);
        public Color hintAreaColor = new Color(0.5f, 0.6f, 0.4f);
        /// <summary>
        /// The spawn area margin multiplier.
        /// ex: .9 means 90% of spawn area will be used.
        /// .1 margin will be left (so players don't spawn off of the edge).
        /// The higher this value, the longer training time required.
        /// </summary>
        public  float spawnAreaMarginMultiplier = 0.9f;
        public Vector2 SpawnOverlapBox = new Vector2(10.1f, 10.1f);
        public float[] rotationAngles = new[] { 0f, 360.0f };

        //grid-related parameters
        public bool useGridMovement = true;
        public Vector3 gridCellSize = new Vector3(40f, 0f, 40f);
        public Vector3 gridCellGap = Vector3.zero;

        //basic rewards, more is specified in Envroller.cs
        public Dictionary<GameEvent, float> groupRewards = new Dictionary<GameEvent, float>()
        {
            {GameEvent.AllGoalsCompleted, 1f},
            {GameEvent.ActiveAgentAssisted, 1f}
        };

        public Dictionary<GameEvent, float> invdividualRewards = new Dictionary<GameEvent, float>()
        {
            {GameEvent.AgentHitObstacle, -0.01f},
            {GameEvent.ActiveAgentHitMovableObstacle, 0.01f},
            {GameEvent.AgentHitAgent, -0.1f},
            {GameEvent.AgentOutOfBounds, -1f},
            {GameEvent.ActiveAgentHitGoal, 1f},
        };

        public Dictionary<GameEvent, float> competitiveRewards = new Dictionary<GameEvent, float>()
        {
            //TBD
        };

    }
}
