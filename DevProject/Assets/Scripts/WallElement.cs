using EnvironmentConfiguration;
using UnityEngine;


[RequireComponent(typeof(Rigidbody))]
public class Obstacle : MonoBehaviour
{
    /// <summary>
    /// This class represents a basic building block 
    /// for obstacles contruction. Ones can be movable or not.
    /// If all obstacles are immovable, then it is a classic maze.
    /// </summary>

    private EnvSettings envSettings;
    private EnvController envController;

    [SerializeField] private bool isMovable = false;

    //if an object is grounded, then smth can move upon it
    [SerializeField] private bool isWalkable = false;
    private bool isReconfigurable = false; // WIP; maybe it'll stay dummy 

    private Material obstacleMaterial;
    private Color obstacleMovableColour;
    
    private Collider obstacleCollider = null;
    private Rigidbody obstacleRigidbody = null;

    public bool IsMovable { get; set; }
    public bool IsWalkable { get; private set; }

    public float ObstacleMovingSpeed { get; private set; }
    
    void Awake()
    {
        obstacleCollider = GetComponent<Collider>();
        obstacleRigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        envController = GetComponentInParent<EnvController>();
        envSettings = envController.envSettings;

        obstacleRigidbody.useGravity = true;
        obstacleRigidbody.constraints = RigidbodyConstraints.FreezePositionY;

        this.gameObject.tag = "Obstacle";

        if (isMovable)
        {   
            GetComponent<Renderer>().material.color = obstacleMovableColour;
            obstacleRigidbody.mass /= 2f;
            obstacleRigidbody.isKinematic = false;
        }
        else
        {
            //bigger mass to restrict any possible collisions
            obstacleRigidbody.mass *= 1000f;
            obstacleRigidbody.drag = 10;
            obstacleRigidbody.isKinematic = true;
            obstacleRigidbody.constraints = RigidbodyConstraints.FreezePosition;
            obstacleRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            GetComponent<Renderer>().material.color = envSettings.immovableObstacleColour;
        }

        if (isWalkable)
        {
            //to walk upon smth the object should have more friction
            obstacleRigidbody.drag *= 4;
            this.gameObject.tag = "WalkableObstacle";
        }
    }

    /// <summary>
    /// This method responds for collision handling.
    /// Only agents tagged as "ActiveAgent" are able to move obstacles.
    /// Obstacle moves to a direction the agent is moving to, so there could be pushes or pulls.
    /// --- Unity's method.
    /// </summary>
    /// <param name="other"></param>
    public void OnCollisionStay(Collision other)
    {   
        if (isMovable)
        {   
            if (other.gameObject.CompareTag("ActiveAgent"))        
            {
                var dirToMove = other.rigidbody.velocity.normalized;
                transform.Translate(dirToMove * Time.deltaTime * envSettings.movableObstacleSpeed);
            } 
            else if (other.gameObject.CompareTag("Agent"))
            {
                //what a dirty thing -- kinda hopping on my beautiful programming knowledge
                obstacleRigidbody.isKinematic = true;
            }     

        }

    }


}