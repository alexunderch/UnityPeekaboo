using UnityEngine;
using EnvironmentConfiguration;

/// <summary>
/// This class responds 
/// </summary>
public class GoalInstance : MonoBehaviour
{
    private EnvSettings envSettings;
    private EnvController envController;
    //via method it does not work, why?
    public bool isCompleted = false;
    public bool isTouched = false;
  
    public void Reconfigure(GoalType goalType)
    {
        //The instance should behave given the scenario
        /*
         For example, it changes its primitive type, colour, position/rotation
         if the agent acted accordingly. Triggering one should lead to the completion, no more
         */
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
        envSettings = envController.envSettings;
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
