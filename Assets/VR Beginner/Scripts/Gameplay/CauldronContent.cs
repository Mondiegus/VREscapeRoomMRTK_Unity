using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.VFX;
using Random = UnityEngine.Random;
using Microsoft.MixedReality.Toolkit.UI;
using TMPro;

/// <summary>
/// Handle all operations related to dropping thing in the cauldron and brewing things.
/// </summary>
public class CauldronContent : MonoBehaviour
{
    [System.Serializable]
    public class Recipe
    {
        public string name;
        public string[] ingredients;
        public int temperature;
        public int rotation;
    }

    [System.Serializable]
    public class BrewEvent : UnityEvent<Recipe> { };
    int step = 0;
    int prev_step = 0;

    public Recipe[] Recipes;
    public int TemperatureIncrement;

    [Header("Effects")]
    public GameObject SplashEffect;
    public Animator CauldronAnimator;

    private VisualEffect splashVFX;
    public VisualEffect brewEffect;

    /// <summary>
    /// Will be called when the cauldron finish brewing, with the recipe as parameters or null if no recipe match.
    /// </summary>
    public BrewEvent OnBrew;

    [Header("Audio")]
    public AudioSource AmbientSoundSource;
    public AudioSource BrewingSoundSource;
    public AudioClip[] SplashClips;

    bool m_CanBrew = false;

    List<string> m_CurrentIngredientsIn = new List<string>();
    public int m_Temperature = 0;
    int m_Rotation = -1;
    int prev_m_Rotation = -1;

    float m_StartingVolume;

    private CauldronEffects m_CauldronEffect;

    private void Start()
    {
        m_CauldronEffect = GetComponent<CauldronEffects>();
        splashVFX = SplashEffect.GetComponent<VisualEffect>();

        m_StartingVolume = AmbientSoundSource.volume;
        AmbientSoundSource.volume = m_StartingVolume * 0.2f;
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("OnTriggerEnter");
        CauldronIngredient ingredient = other.attachedRigidbody.GetComponentInChildren<CauldronIngredient>();
        Debug.Log("ingredient.IngredientType: " + ingredient.IngredientType);

        //Vector3 contactPosition = other.attachedRigidbody.gameObject.transform.position;
        //contactPosition.y = gameObject.transform.position.y;

        //SplashEffect.transform.position = contactPosition;

        //SFXPlayer.Instance.PlaySFX(SplashClips[Random.Range(0, SplashClips.Length)], contactPosition, new SFXPlayer.PlayParameters()
        //{
        //    Pitch = Random.Range(0.8f, 1.2f),
        //    SourceID = 17624,
        //    Volume = 1.0f
        //}, 0.2f, true);

        //splashVFX.Play();

        RespawnableObject respawnableObject = ingredient;
        if (ingredient != null)
        {
            Debug.Log("Correct ingredient added");
            m_CurrentIngredientsIn.Add(ingredient.IngredientType);
        }
        else
        {
            Debug.Log("Wrong ingredient added");

            //added an object that is not an ingredient, it will make automatically fail any recipe
            m_CurrentIngredientsIn.Add("INVALID");
            respawnableObject = other.attachedRigidbody.GetComponentInChildren<RespawnableObject>();
        }

        if (respawnableObject != null)
        {
            respawnableObject.Respawn();
        }
        else
        {
            Destroy(other.attachedRigidbody.gameObject, 0.5f);
        }
    }

    public void OnAngleUpdated(ManipulationEventData eventData)
    {
        float data = 0;
#if UNITY_EDITOR
        data = UnityEditor.TransformUtils.GetInspectorRotation(GameObject.Find("Knob").transform).y;
#endif

        if (data >= -15 && data <= 15)
            step = 0;
        else if (data > 15 && data <= 65 || data < -315 && data >= -360)
            step = 1;
        else if (data > 65 && data <= 115 || data < -265 && data >= -315)
            step = 2;
        else if (data > 115 && data <= 165 || data < -215 && data >= -265)
            step = 3;
        else if (data > 165 && data <= 215 || data < -165 && data >= -215)
            step = 4;
        else if (data > 215 && data <= 265 || data < -115 && data >= -165)
            step = 5;
        else if (data > 265 && data <= 315 || data < -65 && data >= -115)
            step = 6;
        else if (data > 315 && data <= 360 || data < -15 && data >= -65)
            step = 7;

        if (step != prev_step)
        {
            Debug.Log("Step: " + step);
            m_Temperature = TemperatureIncrement * step;
            m_CauldronEffect.SetBubbleIntensity(step);
            prev_step = step;
        }
    }

    public void ChangeTemperature(int step)
    {

        m_Temperature = TemperatureIncrement * step;
        Debug.Log("Temp changed to: " + m_Temperature);

        m_CauldronEffect.SetBubbleIntensity(step);
    }

    public void OnSliderUpdated(SliderEventData eventData)
    {
        m_Rotation = (int)Math.Round(eventData.NewValue * 2, MidpointRounding.AwayFromZero);
        if (prev_m_Rotation != m_Rotation)
        {
            m_CauldronEffect.SetRotationSpeed(m_Rotation);
            prev_m_Rotation = m_Rotation;
        }
    }

    public void ChangeRotation(int step)
    {
        m_Rotation = step - 1;
        m_CauldronEffect.SetRotationSpeed(m_Rotation);
    }

    public void Brew()
    {
        if (!m_CanBrew)
            return;

        brewEffect.SendEvent("StartLongSpawn");
        CauldronAnimator.SetTrigger("Brew");

        Recipe recipeBewed = null;
        foreach (Recipe recipe in Recipes)
        {
            Debug.Log("recipe.temperature: " + recipe.temperature + ", m_Temperature: " + m_Temperature);
            Debug.Log("recipe.rotation: " + recipe.rotation + ", m_Rotation: " + m_Rotation);
            if (recipe.temperature != m_Temperature || recipe.rotation != m_Rotation)
                continue;

            List<string> copyOfIngredient = new List<string>(m_CurrentIngredientsIn);
            int ingredientCount = 0;
            foreach (var ing in recipe.ingredients)
            {
                if (copyOfIngredient.Contains(ing))
                {
                    ingredientCount += 1;
                    copyOfIngredient.Remove(ing);
                }
            }

            if (ingredientCount == recipe.ingredients.Length)
            {
                recipeBewed = recipe;
                break;
            }
        }

        ResetCauldron();

        StartCoroutine(WaitForBrewCoroutine(recipeBewed));
    }

    IEnumerator WaitForBrewCoroutine(Recipe recipe)
    {
        BrewingSoundSource.Play();
        AmbientSoundSource.volume = m_StartingVolume * 0.2f;
        m_CanBrew = false;
        yield return new WaitForSeconds(3.0f);
        brewEffect.SendEvent("EndLongSpawn");
        CauldronAnimator.SetTrigger("Open");
        BrewingSoundSource.Stop();

        OnBrew.Invoke(recipe);
        m_CanBrew = true;
        AmbientSoundSource.volume = m_StartingVolume;
    }

    void ResetCauldron()
    {
        m_CurrentIngredientsIn.Clear();

    }

    public void Open()
    {
        CauldronAnimator.SetTrigger("Open");
        m_CanBrew = true;
        AmbientSoundSource.volume = m_StartingVolume;
    }
}
