using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;
using EnvironmentConfiguration;


[RequireComponent(typeof(Rigidbody))]
public class WallElement : MonoBehaviour
{
    private EnvSettings envSettings = new EnvSettings();

    [SerializeField] private bool isMovable = false;
    [SerializeField] public Material wallMaterial;
    [SerializeField] public Color wallMovableColour;
    

    private Collider wallCollider = null;
    private Rigidbody wallRigidbody = null;
    private Vector3 currentPosition = Vector3.zero;
    

    public bool IsMovable { get; set; }
    public float WallMovingSpeed { get; private set; }
    
    void Awake()
    {
        wallCollider = GetComponent<Collider>();
        wallRigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {

        currentPosition = transform.localPosition;
        wallRigidbody.useGravity = true;
        wallRigidbody.constraints = RigidbodyConstraints.FreezePositionY;

        if (isMovable)
        {   
            GetComponent<Renderer>().material.color = wallMovableColour;
            wallRigidbody.mass /= 2f;
            wallRigidbody.isKinematic = false;
        }
        else
        {
            wallRigidbody.mass *= 1000f;
            wallRigidbody.drag = 10;
            wallRigidbody.isKinematic = true;
            wallRigidbody.constraints = RigidbodyConstraints.FreezePosition;
            wallRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            GetComponent<Renderer>().material.color = envSettings.immovableWallColour;
        }
    }


    public void OnCollisionStay(Collision other)
    {   
        if (isMovable)
        {   
            if (other.gameObject.CompareTag("ActiveAgent"))        
            {
                var dirToMove = other.rigidbody.velocity.normalized; //moving the wall to the direction a pusher moves to
                transform.Translate(dirToMove * Time.deltaTime * envSettings.movableWallSpeed);
            } 
            else if (other.gameObject.CompareTag("Agent"))
            {
                //what a dirty thing -- kinda hopping on my beautiful programming knowledge
                wallRigidbody.isKinematic = true;
            }     

        }
    }


}