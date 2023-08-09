using UnityEngine;
using System.Collections.Generic;

namespace EnvironmentConfiguration
{
    public enum GameEvent
    {
        ActiveAgentHitGoal = 0,
        AgentHitGoal = 1,
        AgentOutOfBounds = 2,
        AgentHitWall = 3,
        AgentHitAgent = 4,
        AllGoalsCompleted = 5,
    }

    public enum Team 
    {
        //active agent can move some objects 
        //and demonstrate special behavioural types
        //but passive agents cannot
        Active = 0,
        Passive = 1,
    }

    public enum WallType 
    {
        Movable = 0,
        Immovable = 1
    }

    public enum GoalType
    {
        //goal types could ever appear
        Cube = 0,
        Sphere = 1,
        Cylinder = 2,
        Default = 3
    }


    public class EnvSettings {

        public string backupConfigFile = "./configs/maps/dev_map.json";
        public string baseConfigFile;
        public bool saveEnvironmentConfiguration = false;
        public bool loadEnvironmentConfiguration = false;


        public int seed = -1; // -1 means "no seed"

        public float agentMass = 1f;
        public float agentMovingSpeed = 50f;
        public float agentRotationSpeed = 99.5f;
        public float agentJumpForce = 600.0f;
        public float agentFallingForce = 100.0f;

        public Color activeAgentColour = Color.red;
        public Color passiveAgentColour = Color.yellow;  

        public Color immovableWallColour = Color.grey; // new Color(0.9f, 0.1f, 0.52f);
        public Color movableWallColour = new Color(0.84f, 0.54f, 0.18f);  
        public  float movableWallSpeed = 15f;

        public Color goalColour = Color.green; // new Color(0.84f, 0.14f, 0.38f);
        public Color completedGoalColour = Color.blue;// new Color(0.14f, 0.58f, 0.78f);

        public Color defaultAreaColor = new Color(0.83f, 0.72f, 0.69f);
        public Color hintAreaColor = new Color(0.5f, 0.6f, 0.4f);
        /// <summary>
        /// The spawn area margin multiplier.
        /// ex: .9 means 90% of spawn area will be used.
        /// .1 margin will be left (so players don't spawn off of the edge).
        /// The higher this value, the longer training time required.
        /// </summary>
        public  float spawnAreaMarginMultiplier = 0.95f;
        public Vector3 SpawnOverlapBox = new Vector3(10.1f, 10.1f, 10.1f);
        public float[] rotationAngles = new[] { 0f, 360.0f };

        //basic rewards, more is specified in Envroller.cs
        public Dictionary<GameEvent, float> groupRewards = new Dictionary<GameEvent, float>()
        {
            {GameEvent.AllGoalsCompleted, 100f}
        };

        public Dictionary<GameEvent, float> invdividualRewards = new Dictionary<GameEvent, float>()
        {
            {GameEvent.AgentHitWall, -1f},
            {GameEvent.AgentHitAgent, -10f},
            {GameEvent.AgentOutOfBounds, -100f},
            {GameEvent.ActiveAgentHitGoal, 100f},
        };

        public Dictionary<GameEvent, float> CompetitiveRewards = new Dictionary<GameEvent, float>()
        {
            //TBD
        };

    }
}
