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
        AgentHitGoal = 1,
        AgentOutOfBounds = 2,
        AgentHitObstacle = 3,
        ActiveAgentHitMovableObstacle = 4,
        AgentHitAgent = 5,
        AllGoalsCompleted = 6,
    }

    /// <summary>
    /// Enum for all the roles possible for agents.
    /// Active agent can move some objects and demonatrate special behavioural types
    /// </summary>
    public enum Team 
    {
        Passive = 0,
        Active = 1,
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
        Cylinder = 2,
        Default = 3
    }

    /// <summary>
    /// A collection of constants for the environment. 
    /// Reward mapping is also specified here.
    /// </summary>
    public class EnvSettings {

        public string backupConfigFile = "../configs/maps/dev_map.json";
        public string baseConfigFile;
        public bool saveEnvironmentConfiguration = false;
        public bool loadEnvironmentConfiguration = false;

        public int seed = -1; // -1 means "no seed"

        public bool recordDemonstrations = false;
        public string demonstrationDirectory = "./Assets/Demonstrations";
        public int recordLength = 0;      

        public float agentMass = 1.337f;
        public float agentMovingSpeed = 69f;
        public float agentRotationSpeed = 99.5f;
        public float agentJumpForce = 300.0f;
        public float agentFallingForce = 400.0f;
        public float differentiateRolesProb = 0.5f;

        public Color activeAgentColour = Color.red;
        public Color passiveAgentColour =  new Color(1.0f, 0.64f, 0.0f);

        public Color immovableObstacleColour = Color.grey; // new Color(0.9f, 0.1f, 0.52f);
        public Color movableObstacleColour = Color.magenta;  
        public  float movableObstacleSpeed = 12f;

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
        public Vector3 SpawnOverlapBox = new Vector3(10.1f, 10.1f, 10.1f);
        public float[] rotationAngles = new[] { 0f, 360.0f };

        //basic rewards, more is specified in Envroller.cs
        public Dictionary<GameEvent, float> groupRewards = new Dictionary<GameEvent, float>()
        {
            {GameEvent.AllGoalsCompleted, 1f}
        };

        public Dictionary<GameEvent, float> invdividualRewards = new Dictionary<GameEvent, float>()
        {
            {GameEvent.AgentHitObstacle, -0.01f},
            {GameEvent.ActiveAgentHitMovableObstacle, 0.01f},
            {GameEvent.AgentHitAgent, -0.1f},
            {GameEvent.AgentOutOfBounds, -1f},
            {GameEvent.ActiveAgentHitGoal, 1f},
        };

        public Dictionary<GameEvent, float> CompetitiveRewards = new Dictionary<GameEvent, float>()
        {
            //TBD
        };

    }
}
