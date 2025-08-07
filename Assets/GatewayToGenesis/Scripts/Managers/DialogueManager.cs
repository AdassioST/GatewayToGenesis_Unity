using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Ink.Runtime;
using UnityEngine.EventSystems;


public class DialogueManager : MonoBehaviour
{
    [SerializeField] private TextAsset inkJson;
    [SerializeField] public GameObject decisionPanel;
    [SerializeField] private Decision decision;
    [SerializeField] private StatManager statManager;
    private static DialogueManager instance;
    private bool canContinueToNextLine = false;
    public string path;
    private bool canChoose=false;

    [Header("Choices UI")]
    [SerializeField] private GameObject[] choices;
    [SerializeField] TextMeshProUGUI[] choicesText;

    Story currentStory;
    
    // Start is called before the first frame update
    void Awake()
    {

        if (instance == null)
        {
            Debug.LogWarning("Found more than one DialogueManager");
        }
        instance = this;
        
    }

    private void Start()
    {
        // get all of the choices text 
        choicesText = new TextMeshProUGUI[choices.Length];
        int index = 0;
        foreach (GameObject choice in choices)
        {
            choicesText[index] = choice.GetComponentInChildren<TextMeshProUGUI>();
            index++;
        }
        decisionPanel.SetActive(false); // Hide the panel at the start
        EnterDialogueMode(inkJson);
        currentStory.BindExternalFunction("CheckStat", (string statName, int statValue) => statManager.CheckStat(statName, statValue));
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            decisionPanel.SetActive(true);
            JumpToKnot(decision._name);
            
        }
    }

    public static DialogueManager GetInstance()
    {
        return instance;
    }

    public void EnterDialogueMode(TextAsset inkJSON)
    {
        if (inkJSON != null)
        {
            currentStory = new Story(inkJSON.text);
            Debug.Log("Story initialized."); // Debug log to confirm initialization
    
        }
        else
        {
            Debug.LogError("Ink JSON is null. Make sure the asset is assigned.");
        }
    }
    private void ContinueStory()
    {
        if (currentStory.canContinue)
        {
            Debug.Log(currentStory.ContinueMaximally());
            DisplayChoices(); // Show choices after continuing the story
        }
        else
        {
            Debug.LogWarning("No more content to continue or story is not initialized.");
            decisionPanel.SetActive(false); // Hide the panel if there's nothing more to show
        }
    }
    public void JumpToKnot(string knotName)
    {
        if (currentStory != null)
        {
            currentStory.ResetState(); // Reset the story state if needed
            currentStory.ChoosePathString(knotName); // Jump to the knot
            if (currentStory.canContinue)
            {
                ContinueStory();
            }// Continue after jumping
        }
        else
        {
            Debug.LogError("Current story is not initialized.");
        }
    }


    private void DisplayChoices()
    {
        List<Choice> currentChoices = currentStory.currentChoices;



        // defensive check to make sure our UI can support the number of choices coming in
        if (currentChoices.Count > choices.Length)
        {
            Debug.LogError("More choices were given than the UI can support. Number of choices given: "
                + currentChoices.Count);
        }

        int index = 0;
        // enable and initialize the choices up to the amount of choices for this line of dialogue
        foreach (Choice choice in currentChoices)
        {
            choices[index].gameObject.SetActive(true);
            choicesText[index].text = choice.text;
            
            index++;
        }
        // go through the remaining choices the UI supports and make sure they're hidden
        for (int i = index; i < choices.Length; i++)
        {
            choices[i].gameObject.SetActive(false);
        }

        decisionPanel.SetActive(currentChoices.Count > 0);

        StartCoroutine(SelectFirstChoice());
    }
    private IEnumerator SelectFirstChoice()
    {
        // Event System requires we clear it first, then wait
        // for at least one frame before we set the current selected object.
        EventSystem.current.SetSelectedGameObject(null);
        yield return new WaitForEndOfFrame();
        EventSystem.current.SetSelectedGameObject(choices[0].gameObject);
    }
    public void MakeChoice(int choiceIndex)
    {
        // Check if the choiceIndex is valid
        if (currentStory.currentChoices.Count > choiceIndex)
        {
            if (decision._requirement._statvalue > 0)
            {
                if(statManager.CheckStat(decision._requirement._statname, decision._requirement._statvalue)) 
                {
                    currentStory.ChooseChoiceIndex(choiceIndex); // Process the player's choice
                }
                else {
                    Debug.LogWarning("Cant make Choice");
                }
            }
            else
            {
                currentStory.ChooseChoiceIndex(choiceIndex); // Process the player's choice
            }
            

            // Now check if the story can continue after making the choice
            if (currentStory.canContinue)
            {
                Debug.Log(currentStory.ContinueMaximally()); // Log the next part of the story

            }
            else
            {
                decisionPanel.SetActive(false); // Hide the panel at the start
            }

            // Display next set of choices
            DisplayChoices(); // Always attempt to display choices after a selection
        }
        else
        {
            Debug.LogError("Invalid choice index: " + choiceIndex);
        }
    }
}
