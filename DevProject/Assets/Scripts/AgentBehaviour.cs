using UnityEngine;
using EnvironmentConfiguration;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;


/// <summary>
/// This class specifies behaviour of an agent
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MAPFAgent : Agent
{       
    //by default, each agent is active
    [SerializeField] public bool isActive = true;

    public Rigidbody agentRb;
    private Collider agentCl;

    private BehaviorParameters behaviourParameters;
    private RayPerceptionSensorComponent3D raySensor;
    private EnvSettings envSettings;
    private EnvController envController;
    
    private Vector3 dirToGo;
    private Vector3 dirToRotate;
    private Vector3 jump;

    //for jumps
    private bool isGrounded;
    private int collisionCounter = 0;

    public bool IsActive {get; set;}

    [HideInInspector]
    public int CollisionCounter {get; private set;}

    public override void OnEpisodeBegin() 
    {
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

        agentRb = this.GetComponent<Rigidbody>();
        agentCl = this.GetComponent<Collider>();
        //vision is done via raycasting
        raySensor = this.GetComponent<RayPerceptionSensorComponent3D>();
        var agentColour = envSettings.activeAgentColour;

        if (!isActive)
        {
            agentColour = envSettings.passiveAgentColour;
        }

        this.GetComponent<Renderer>().material.color = agentColour;
    }

    public void OnTriggerEnter(Collider other) 
    {  
        if (other.gameObject.CompareTag("Goal"))
        {
            //no need to handle role models here explicitly
            AddReward(envSettings.invdividualRewards[GameEvent.ActiveAgentHitGoal]);
            envController.UpdateStatistics();
        }

        if (other.gameObject.CompareTag("Obstacle"))
        {
            //to prevent an agent incapable to move obstacles bump into them on purpose
            collisionCounter++;
            AddReward(-(float)collisionCounter/100f);
        }
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
            var reward = envSettings.invdividualRewards[GameEvent.AgentHitAgent];

            if (!isActive) {
                reward *= 5f;
            }
            AddReward(reward);

        }
    
    
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


        switch (dirToGoAction)
        {
            case 1:
                // forward
                dirToGo = transform.forward * 1f;
                break;
            case 2:
                dirToGo = transform.forward * (-1f);
                break;
            case 3:
                //left
                dirToGo = transform.right * (-0.75f);
                break;
            case 4: 
                dirToGo = transform.right * (0.75f);
                break;
            default:
                dirToGo =  Vector3.zero;
                break;
        }

        switch (dirToRotateAction) 
        {
            case 1:
                // forward
                dirToRotate = transform.up * 1f;
                break;
            case 2:
                dirToRotate = transform.up * (-1f);
                break;
            default:
                dirToRotate = Vector3.zero;
                break;
        }
        if (dirToRotateAction> 0)
            transform.Rotate(dirToRotate * Time.deltaTime * envSettings.agentRotationSpeed);
        
        if (dirToGoAction > 0)
        {
            //TODO: check once more
            Vector3 moveAction = dirToGo * Time.deltaTime * envSettings.agentMovingSpeed;
            // if (envController.AreaBounds.Contains(moveAction))
            // {
            transform.Translate(moveAction);
            // }
        }
           
        if ((jumpAction > 0) && isGrounded)
        {
            agentRb.AddForce(jump * envSettings.agentJumpForce, ForceMode.Impulse);
            isGrounded = false;
        }

        if (!isGrounded)
        {
            agentRb.AddForce(
                Vector3.down * envSettings.agentFallingForce, ForceMode.Acceleration);
        }
    }
   
    public void OnCollisionStay(Collision other)
    {
        //forawhile the agent could stand only on surface
        //possibly, could be extended 
        if (other.gameObject.CompareTag("Surface") || other.gameObject.CompareTag("WalkableObstacle")) 
        {
            isGrounded = true;
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

    /// <summary>
    /// Human controller, a Unity internal method
    /// </summary>
    /// <param name="actionsOut"></param>
    public override void Heuristic(in ActionBuffers actionsOut)
    {

        var discreteActionsOut = actionsOut.DiscreteActions;
        //moves
        if (Input.GetKey(KeyCode.W))
        {
            discreteActionsOut[0] = 1;
        } 
        if (Input.GetKey(KeyCode.S))
        {
            discreteActionsOut[0] = 2;
        }
        if (Input.GetKey(KeyCode.A))
        {
            discreteActionsOut[0] = 3;
        }
        if (Input.GetKey(KeyCode.D))
        {
            discreteActionsOut[0] = 4;
        }
        //rotations
        if (Input.GetKey(KeyCode.F))
        {
            discreteActionsOut[1] = 1;
        }
        if (Input.GetKey(KeyCode.C))
        {
            discreteActionsOut[1] = 2;
        }
        //jump
        if(Input.GetKey(KeyCode.Q))
        {
            discreteActionsOut[2] = 1;
        }

    }
        
}
