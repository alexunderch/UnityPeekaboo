using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EnvironmentConfiguration;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors.Reflection;

[RequireComponent(typeof(Rigidbody))]
public class MAPFAgent : Agent
{   
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
    public override void Initialize()
    {
        envSettings = new EnvSettings();
        agentRb = this.GetComponent<Rigidbody>();
        agentCl = this.GetComponent<Collider>();
        raySensor = this.GetComponent<RayPerceptionSensorComponent3D>();
        var agentColour = envSettings.activeAgentColour;

        envController = GetComponentInParent<EnvController>();
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

        if (other.gameObject.CompareTag("Wall"))
        {
            //to prevent an agent incapable to move walls bump into them on purpose
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

        if (other.gameObject.CompareTag("Wall"))
        {
            var reward = envSettings.invdividualRewards[GameEvent.AgentHitAgent];

            if (!isActive) {
                reward *= 10f;
            }
            AddReward(reward);

        }
    
    
    }

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
        if (other.gameObject.CompareTag("Surface")) 
        {
            isGrounded = true;
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        //reward per step;
        AddReward(-1f/(envController.MaxEnvironmentSteps+1f));
        MoveAgentDiscrete(actionBuffers.DiscreteActions);
    }


    // For human controller
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
