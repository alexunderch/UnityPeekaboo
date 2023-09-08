using EnvironmentConfiguration;
using UnityEngine;
using System;
using Unity.VisualScripting;

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

    [SerializeField] public bool isMovable = false;
    private bool allowedToMove = false;

    //if an object is grounded, then smth can move upon it
    [SerializeField] private bool isWalkable = false;
    // private bool isReconfigurable = false; // WIP; maybe it'll stay dummy 

    private Material obstacleMaterial = null;
    private Collider obstacleCollider = null;
    private Rigidbody obstacleRigidbody = null;

    // public bool IsMovable { get; set; }
    // public bool AllowedToMove { get; private set;}

    public float ObstacleMovingSpeed { get; private set; }


    /// <summary>
    /// The method carries all visual components to be added via code
    /// to recover the object from a config
    /// </summary>
    /// <returns></returns>
    public static GameObject ConfigureComponents(ref GameObject gameObject)
    {
        gameObject.AddComponent<MeshCollider>();
        gameObject.AddComponent<BoxCollider>();
        gameObject.AddComponent<MeshFilter>();
        gameObject.AddComponent<MeshRenderer>();
        return gameObject;
    }
    public void Awake()
    {
     
        this.gameObject.SetActive(true);
        if (!isMovable)
        {
            this.gameObject.name = "Obstacle";
            this.gameObject.tag = "Obstacle";

        }
        else
        {
            this.gameObject.name = "Secret Wall";
            this.gameObject.tag = "MovableObstacle";

        }

    }
    public void Reset()
    {
        allowedToMove = false;
        obstacleRigidbody.isKinematic = true;
        obstacleRigidbody.useGravity = true;
    }

    public void Start()
    {
        var meshCollider = this.GetComponent<MeshCollider>();
        meshCollider.enabled = true;
        meshCollider.convex = true;
        meshCollider.providesContacts = true;

        meshCollider.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        this.GetComponent<MeshFilter>().sharedMesh = meshCollider.sharedMesh;

        obstacleCollider = GetComponent<Collider>();
        obstacleRigidbody = GetComponent<Rigidbody>();
        envController = GetComponentInParent<EnvController>();
        envSettings = GetComponentInParent<EnvSettings>();

        this.gameObject.tag = "Obstacle";
        Reset();

        if (isMovable)
        {   
            this.gameObject.tag = "MovableObstacle";
            obstacleRigidbody.constraints = RigidbodyConstraints.FreezePositionY;
            GetComponent<Renderer>().material.color = envSettings.movableObstacleColour;
        }
        else
        {
            //bigger mass to restrict any possible collisions
            obstacleRigidbody.mass *= 1000f;
            obstacleRigidbody.drag = 10;
            obstacleRigidbody.constraints = RigidbodyConstraints.FreezePosition 
                                            | RigidbodyConstraints.FreezeRotation;
            GetComponent<Renderer>().material.color = envSettings.immovableObstacleColour;
        }

        if (isWalkable)
        {
            //to walk upon smth the object should have more friction
            obstacleRigidbody.drag *= 4;
        }
    }

    public void MovingRequestHandler(string name)
    {   
        if (this.name == name)
        {
            obstacleRigidbody.isKinematic = false;
            allowedToMove = true;
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
        bool isValidAgent = other.gameObject.CompareTag("ActiveAgent") || other.gameObject.CompareTag("ActiveCooperativeAgent");
        if (isMovable && allowedToMove)
        {   
            if (isValidAgent)        
            {
                var dirToMove = other.rigidbody.velocity.normalized;
                transform.Translate(dirToMove * Time.deltaTime * envSettings.movableObstacleSpeed);
                Debug.Log("Moved");
                
            }
            allowedToMove = false; //disallow to be moved after one execution   
        }
        if (isMovable && isValidAgent)
        {
            Reset();
        }
    }

}