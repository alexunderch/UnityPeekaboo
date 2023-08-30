using UnityEngine;
using EnvironmentConfiguration;
using Unity.VisualScripting;
using Unity.MLAgents.Sensors;

/// <summary>
/// This class responds 
/// </summary>
public class GoalInstance : MonoBehaviour
{
    private EnvSettings envSettings;
    private EnvController envController;
    //via method it does not work, why?
    [HideInInspector] public bool isCompleted = false;
    [HideInInspector] public bool isTouched = false;
  
    public void Reconfigure(GoalType goalType)
    {
        //The instance should behave given the scenario
        /*
         For example, it changes its primitive type, colour, position/rotation
         if the agent acted accordingly. Triggering one should lead to the completion, no more
         */
    }

    /// <summary>
    /// The method carries all visual components to be added via code
    /// to recover the object from a config
    /// </summary>
    /// <returns></returns>
    public static GameObject ConfigureComponents(ref GameObject gameObject)
    {
        gameObject.AddComponent<MeshRenderer>();
        gameObject.AddComponent<MeshFilter>();
        gameObject.AddComponent<VectorSensorComponent>();

        return gameObject;
    }

    /// <summary>
    /// This methods resets the goal: renders it properly
    /// </summary>
    public void Reset()
    {   
        //TODO: add material and primitive
        this.gameObject.SetActive(true);
        this.GetComponent<Renderer>().material.color = envSettings.goalColour;
        isCompleted = false;
        isTouched = false;
    }
    void Start()
    {
        envController = GetComponentInParent<EnvController>();
        envSettings = GetComponentInParent<EnvSettings>();

        var vectorSensor = this.GetComponent<VectorSensorComponent>();
        vectorSensor.ObservationType = ObservationType.GoalSignal;
        vectorSensor.ObservationSize = 4;

        Reset();
    }

    /// <summary>
    /// This method responds for the trigger hadling
    /// If any agent triggered the goal, it would be marked as `touched`
    /// If an active agent did that, it would be marked as `completed`
    /// </summary>
    /// <param name="other"></param>
    void OnTriggerEnter(Collider other) 
    {
        if (other.gameObject.CompareTag("ActiveAgent") || other.gameObject.CompareTag("Agent"))
        {
            isTouched = true;
            GetComponent<Renderer>().material.color = envSettings.completedGoalColour;
            isCompleted = true;
        }
    }
}
