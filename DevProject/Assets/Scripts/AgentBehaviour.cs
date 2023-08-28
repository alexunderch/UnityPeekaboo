using UnityEngine;
using EnvironmentConfiguration;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Demonstrations;
using System;
using Unity.VisualScripting;
using Unity.Burst.CompilerServices;

public enum MoveActions 
{
    Forward = 1,
    Backward = 2,
    Left = 3,
    Right = 4
}

public enum RotateActions 
{
    Clockwise = 1,
    Anticlockwise = 2,
}


/// <summary>
/// This class specifies behaviour of an agent
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MAPFAgent : Agent
{       
    //by default, each agent is active
    [SerializeField] public bool isActive = false;
    [SerializeField] public bool maskActions = true;

    public Rigidbody agentRb;
    private Collider agentCl;

    private BehaviorParameters behaviourParameters;
    private RayPerceptionSensorComponent3D raySensor;
    private DemonstrationRecorder demonstrationRecorder;

    private EnvSettings envSettings;
    private EnvController envController;
    EnvironmentParameters m_ResetParams;

    private Vector3 dirToGo;
    private Vector3 dirToRotate;
    private Vector3 jump;
    private Vector3 dirToRestrict = Vector3.zero;

    //for jumps
    private bool isGrounded;
    private int collisionCounter = 0;

    public bool IsActive {get; set;}

    [HideInInspector]
    public int CollisionCounter {get; private set;}

    public class MoveRequestEventArgs : System.EventArgs
    {
        public string ObjectID { get; set; }
    }

    private bool moveRequested = false;
    public delegate void AgentMoveRequested(object sender, MoveRequestEventArgs e);
    public event AgentMoveRequested OnMoveRequested;

    public override void OnEpisodeBegin() 
    {
        moveRequested = false;
        collisionCounter = 0;
        dirToGo = Vector3.zero;
        dirToRotate = Vector3.zero;
        jump = new Vector3(0.0f, 1.0f, 0.0f);
    }
    
    public void Start() {
        this.gameObject.SetActive(true);
        MaxStep = envController.MaxEnvironmentSteps;

        this.gameObject.tag = "Agent";
        if (isActive)
            this.gameObject.tag = "ActiveAgent";

    }

    /// <summary>
    /// Initialising agent here: rigidbody, colours, materials... 
    /// </summary>
    public override void Initialize()
    {
        envController = GetComponentInParent<EnvController>();
        envSettings = envController.envSettings;
        m_ResetParams = Academy.Instance.EnvironmentParameters;

        agentRb = this.GetComponent<Rigidbody>();

        agentCl = this.GetComponent<Collider>();
        //vision is done via raycasting
        raySensor = this.GetComponent<RayPerceptionSensorComponent3D>();
        demonstrationRecorder = this.GetComponent<DemonstrationRecorder>();

        demonstrationRecorder.Record = envSettings.recordDemonstrations;
        demonstrationRecorder.DemonstrationName = "PeekabooDemo";
        demonstrationRecorder.NumStepsToRecord = envSettings.recordLength;
        demonstrationRecorder.DemonstrationDirectory = envSettings.demonstrationDirectory;

        var agentColour = envSettings.activeAgentColour;

        if (!isActive)
        {
            agentColour = envSettings.passiveAgentColour;
        }

        this.GetComponent<Renderer>().material.color = agentColour;
    }

    public void Update()
    {
    }

    public void OnTriggerEnter(Collider other) 
    {  
        if (other.gameObject.CompareTag("Goal"))
        {
            //no need to handle role models here explicitly
            AddReward(envSettings.invdividualRewards[GameEvent.ActiveAgentHitGoal]);
            envController.UpdateStatistics();
        }
    }
    private void AvoidPosition(Vector3 localPosition)
    {
        transform.localPosition = localPosition + (-localPosition + transform.localPosition).normalized * envSettings.obstacleAvoidanceDistance;
    }

    public void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Barrier"))
        {   
           AddReward(envSettings.invdividualRewards[GameEvent.AgentOutOfBounds]);
        }

        if (other.gameObject.CompareTag("ActiveAgent"))
        {
            AddReward(envSettings.invdividualRewards[GameEvent.AgentHitAgent]);
        }

        if (other.gameObject.CompareTag("Obstacle"))
        {
            //to prevent an agent incapable to move obstacles bump into them on purpose
            collisionCounter++;
            AddReward(-(float)collisionCounter/1000f);

            var reward = envSettings.invdividualRewards[GameEvent.AgentHitObstacle];

            if (!isActive) {
                reward *= 5f;
            }
            AddReward(reward);
        }



    }

    public void OnCollisionStay(Collision other)
    {
        //forawhile the agent could stand only on surface
        //possibly, could be extended 
        if (other.gameObject.CompareTag("Surface") || other.gameObject.CompareTag("Obstacle")) 
        {
            isGrounded = true;
        }

        if (other.gameObject.CompareTag("MovableObstacle") && moveRequested)
        {
            AddReward(envSettings.invdividualRewards[GameEvent.ActiveAgentHitMovableObstacle]);
            OnMoveRequested?.Invoke(this, new MoveRequestEventArgs {ObjectID = other.gameObject.name});
        }

        if (this.tag == "Agent" && (other.gameObject.CompareTag("Obstacle") || other.gameObject.CompareTag("MovableObstacle")))
        {
            AddReward(envSettings.invdividualRewards[GameEvent.AgentHitObstacle]);
        }

    }

    public void OnCollisionExit(Collision other)
    {
        if (other.gameObject.CompareTag("MovableObstacle") && moveRequested)
            moveRequested = false;

    }

    /// <summary>
    /// The method is respondible for mappting actions as numbers to doing smth in the environment 
    /// </summary>
    /// <param name="actions"></param>
    private void MoveAgentDiscrete(ActionSegment<int> actions) 
    {
        //3 types of actions
        //goAction: WASD
        //rotateAction: clockwise/anticlockwise
        //jumpAction: indicative one
        //USEACTION (????)


        int dirToGoAction = actions[0];
        int dirToRotateAction = actions[1];
        int jumpAction = actions[2];
        int useAction = actions[3];


        switch (dirToGoAction)
        {
            case (int) MoveActions.Forward:
                // forward
                dirToGo = transform.forward * 1f;
                break;
            case (int) MoveActions.Backward:
                dirToGo = transform.forward * (-1f);
                break;
            case (int) MoveActions.Left:
                //left
                dirToGo = transform.right * (-0.75f);
                break;
            case (int) MoveActions.Right: 
                dirToGo = transform.right * 0.75f;
                break;
            default:
                dirToGo =  Vector3.zero;
                break;
        }

        switch (dirToRotateAction) 
        {
            case (int) RotateActions.Clockwise:
                dirToRotate = transform.up * 1f;
                break;
            case (int) RotateActions.Anticlockwise:
                dirToRotate = transform.up * (-1f);
                break;
            default:
                dirToRotate = Vector3.zero;
                break;
        }

        if (dirToRotateAction> 0)
        {
            this.transform.Rotate(dirToRotate.normalized * Time.deltaTime * envSettings.agentRotationSpeed);
        }

        if (dirToGoAction > 0)
        {
            //TODO: check once more
            Vector3 moveAction = dirToGo * Time.deltaTime * envSettings.agentMovingSpeed;
            this.transform.Translate(moveAction);
        }
           
        if ((jumpAction > 0) && isGrounded)
        {
            agentRb.AddForce(jump * envSettings.agentJumpForce, ForceMode.Impulse);
            isGrounded = false;
        }

        if (!isGrounded)
        {
            //the agent should not fly for too long
            agentRb.AddForce(
                Vector3.down * envSettings.agentFallingForce, ForceMode.Acceleration);
        }

        if (useAction == 1)
        {
            moveRequested = true;
        }

    }

    /// <summary>
    /// Action Distribution is here
    /// </summary>
    /// <param name="actionBuffers"></param>
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        //reward per step;
        AddReward(-1f/(envController.MaxEnvironmentSteps+1f));
        MoveAgentDiscrete(actionBuffers.DiscreteActions);
    }


    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        var localRotation = this.transform.localRotation;
        // Mask the necessary actions if selected by the user.
        if (maskActions)
        {

            // Prevents the agent from the `use action` if it is not active
            if (!isActive)
            {
                actionMask.SetActionEnabled(3, 1, false);
                actionMask.SetActionEnabled(2, 1, false);
            }

            //// Prevents the agent from moving through the walls
            //// TODO: fix rotations
            RaycastHit hit; 
            if (Physics.Raycast(this.transform.position, 
                transform.TransformDirection(this.transform.forward), 
                out hit, 
                envSettings.obstacleAvoidanceDistance))
            {
                actionMask.SetActionEnabled(0, (int)MoveActions.Forward, isActive && hit.collider.CompareTag("MovableObstacle"));
            }

            if (Physics.Raycast(this.transform.position,
                transform.TransformDirection(-this.transform.forward),
                out hit, 
                envSettings.obstacleAvoidanceDistance))
            {
                actionMask.SetActionEnabled(0, (int)MoveActions.Backward, isActive && hit.collider.CompareTag("MovableObstacle"));
            }

            if (Physics.Raycast(this.transform.position,
                transform.TransformDirection(this.transform.right),
                out hit, 
                envSettings.obstacleAvoidanceDistance))
            {
                actionMask.SetActionEnabled(0, (int)MoveActions.Right, isActive && hit.collider.CompareTag("MovableObstacle"));
            }

            if (Physics.Raycast(this.transform.position, 
                transform.TransformDirection(-this.transform.right), 
                out hit, 
                envSettings.obstacleAvoidanceDistance))
            {
                actionMask.SetActionEnabled(0, (int)MoveActions.Left, isActive && hit.collider.CompareTag("MovableObstacle"));
            }

            agentRb.velocity = Vector3.zero;

        }
    }


    /// <summary>
    /// Human controller, a Unity internal method
    /// </summary>
    /// <param name="actionsOut"></param>
    public override void Heuristic(in ActionBuffers actionsOut)
    {

        int moveAction = 0;
        int rotateAction = 1;
        int jumpAction = 2;
        int useAction = 3;

        var discreteActionsOut = actionsOut.DiscreteActions;
        //moves
        if (Input.GetKey(KeyCode.W))
        {
            discreteActionsOut[moveAction] = (int) MoveActions.Forward;
        } 
        if (Input.GetKey(KeyCode.S))
        {
            discreteActionsOut[moveAction] = (int) MoveActions.Backward;
        }
        if (Input.GetKey(KeyCode.A))
        {
            discreteActionsOut[moveAction] = (int) MoveActions.Left;
        }
        if (Input.GetKey(KeyCode.D))
        {
            discreteActionsOut[moveAction] = (int) MoveActions.Right;
        }
        //rotations
        if (Input.GetKey(KeyCode.F))
        {
            discreteActionsOut[rotateAction] = (int) RotateActions.Clockwise;
        }
        if (Input.GetKey(KeyCode.C))
        {
            discreteActionsOut[rotateAction] = (int) RotateActions.Anticlockwise;
        }
        //jump
        if(Input.GetKey(KeyCode.Q))
        {
            discreteActionsOut[jumpAction] = 1;
        }

        //usekey
        if(Input.GetKey(KeyCode.E))
        {
            discreteActionsOut[useAction] = 1;
        }

    }
        
}
