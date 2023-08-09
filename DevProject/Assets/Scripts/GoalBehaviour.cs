using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EnvironmentConfiguration;

public class GoalBehaviour : MonoBehaviour
{   
    private EnvSettings envSettings = new EnvSettings();
    //via method it does not work, why?
    public bool isCompleted = false;
    public bool isTouched = false;
    
    public void Reconfigure(GoalType goalType)
    {
        // //TODO: change primitive of a goal to the given type
        // this.gameObject.SetActive(false);
        // switch ((int)goalType)
        // {
        //     case 0:
        //         //cube
        //         GameObject.CreatePrimitive(PrimitiveType.Cube);
        //         break;
        //     case 1:
        //         //sphere
        //         GameObject.CreatePrimitive(PrimitiveType.Sphere);
        //         break;
        //     case 2:
        //         //cylinder
        //         GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        //         break;
        // }
        // this.transform.localPosition = goalTransform.localPosition;

        // this.GetComponent<Renderer>().material.color = envSettings.goalColour;
    }

    public void Reset()
    {   
        this.gameObject.SetActive(true);
        this.GetComponent<Renderer>().material.color = envSettings.goalColour;
        isCompleted = false;
        isTouched = false;
    }
    void Start()
    {
        Reset();
    }


    void OnTriggerEnter(Collider other) 
    {
        if (other.gameObject.CompareTag("ActiveAgent") || other.gameObject.CompareTag("Agent"))
        {
            isTouched = true;
            if (other.gameObject.CompareTag("ActiveAgent"))
            {
                GetComponent<Renderer>().material.color = envSettings.completedGoalColour;
                isCompleted = true;
            }
        }
    }
}
