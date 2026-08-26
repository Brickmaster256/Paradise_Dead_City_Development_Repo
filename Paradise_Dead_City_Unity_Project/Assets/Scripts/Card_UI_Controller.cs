using TMPro;
using UnityEngine.UI;
using UnityEngine;
using System.Collections;

public class Card_UI_Controller : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator Card_Animator;

    [Header("Buttons")]
    [SerializeField] private Button Attack_Button;

    [Header("Text Fields")]
    [SerializeField] private TextMeshProUGUI Name_Text;
    [SerializeField] private TextMeshProUGUI Health_Num;
    [SerializeField] private TextMeshProUGUI Movement_Num;
    [SerializeField] private TextMeshProUGUI Armor_Num;
    [SerializeField] private TextMeshProUGUI Attack_Num;
    [SerializeField] private TextMeshProUGUI Range_Num;
    [SerializeField] private TextMeshProUGUI Damage_Num;
    [SerializeField] private TextMeshProUGUI Description_Text;

    [Header("Image Fields")]
    [SerializeField] private Image Faction_Icon;
    [SerializeField] private Image Model_Icon;

    [Header("Default Placeholders")]
    [SerializeField] private Sprite Default_Faction_Icon;
    [SerializeField] private Sprite Default_Model_Icon;

    [Header("Transition Settings")]
    [SerializeField] private float Close_Animation_Duration = 0.5f;
    [SerializeField] private float Card_Transition_Delay = 0.3f;

    // Animator
    private static readonly int Open_Trigger = Animator.StringToHash("Open");
    private static readonly int Close_Trigger = Animator.StringToHash("Close");

    // Track current state
    private bool Is_Card_Open = false;
    private Model_Standard_Behavior Current_Displayed_Model;
    private Coroutine Current_Transition;

    // Cached Combat_Manager reference
    private Combat_Manager Combat_Mgr;

    private void Awake()
    {
        // Try to find the Animator if not assigned
        if (Card_Animator == null)
        {
            Card_Animator = GetComponent<Animator>();
            if (Card_Animator == null)
            {
                Card_Animator = GetComponentInChildren<Animator>();
                if (Card_Animator == null)
                {
                    Debug.LogWarning("Card_UI_Controller: No Animator found! Please assign one in the Inspector.");
                }
            }
        }

        // Cache the Combat_Manager reference using the new API
        Combat_Mgr = FindAnyObjectByType<Combat_Manager>();
        if (Combat_Mgr == null)
        {
            Debug.LogWarning("Card_UI_Controller: Combat_Manager not found in scene! Attack button won't work.");
        }
    }

    private void Start()
    {
        // Initialize with placeholder data
        Clear_Card();

        // Ensure card starts closed
        if (Card_Animator != null)
        {
            Card_Animator.ResetTrigger(Open_Trigger);
            Card_Animator.ResetTrigger(Close_Trigger);
        }

        // Set up attack button listener
        if (Attack_Button != null)
        {
            Attack_Button.onClick.AddListener(On_Attack_Button_Clicked);
        }
    }

    public void Show_Model_Info(Model_Standard_Behavior Model, Faction_Data_SO Faction)
    {
        if (Model == null || Model.Stats == null)
        {
            Debug.LogWarning("Card_UI_Controller: Cannot display null model or model without stats");
            return;
        }

        // If card is already open with this same model, don't re-trigger animation
        if (Is_Card_Open && Current_Displayed_Model == Model)
            return;

        // If card is open but with a different model, do the close/open transition
        if (Is_Card_Open && Current_Displayed_Model != Model)
        {
            // Start the transition: Close -> Wait -> Open with new info
            if (Current_Transition != null)
                StopCoroutine(Current_Transition);

            Current_Transition = StartCoroutine(Transition_To_New_Model(Model, Faction));
            return;
        }

        // Card is closed, just open it directly
        Current_Displayed_Model = Model;
        Populate_Card_Data(Model, Faction);
        Trigger_Open();
    }


    public void Hide_Model_Info()
    {
        if (!Is_Card_Open)
            return;

        // Stop any in-progress transition
        if (Current_Transition != null)
        {
            StopCoroutine(Current_Transition);
            Current_Transition = null;
        }

        StartCoroutine(Close_Card_Sequence());
    }

    private IEnumerator Close_Card_Sequence()
    {
        Debug.Log($"Card closing - data stays visible during animation");

        // Step 1: Trigger the close animation
        Trigger_Close();
        Is_Card_Open = false;

        // Step 2: Wait for the close animation to complete
        // The card data REMAINS visible during this time
        yield return new WaitForSeconds(Close_Animation_Duration);

        // Step 3: Now that the animation is done, clear the card data
        Clear_Card();
        Current_Displayed_Model = null;

        Debug.Log("Card fully closed - data cleared");
    }

    private IEnumerator Transition_To_New_Model(Model_Standard_Behavior New_Model, Faction_Data_SO New_Faction)
    {
        Debug.Log($"Card transitioning from {Current_Displayed_Model?.Stats?.Model_Name} to {New_Model?.Stats?.Model_Name}");

        Trigger_Close();
        Is_Card_Open = false;

        yield return new WaitForSeconds(Close_Animation_Duration + Card_Transition_Delay);

        Current_Displayed_Model = New_Model;
        Populate_Card_Data(New_Model, New_Faction);

        Trigger_Open();
        Is_Card_Open = true;

        Current_Transition = null;
        Debug.Log($"Card transition complete - now showing {New_Model?.Stats?.Model_Name}");
    }

    private void Populate_Card_Data(Model_Standard_Behavior Model, Faction_Data_SO Faction)
    {
        // -- NAME --
        if (Name_Text != null)
            Name_Text.text = Model.Stats.Model_Name;

        // -- HEALTH --
        if (Health_Num != null)
            Health_Num.text = $"{Model.Current_Health}/{Model.Stats.Health}";

        // -- MOVEMENT --
        if (Movement_Num != null)
            Movement_Num.text = Model.Stats.Movement_Range.ToString();

        // -- ATTACK SKILL (D6 target) --
        if (Attack_Num != null)
            Attack_Num.text = $"{Model.Stats.Attack_Skill}+";

        // -- RANGE --
        if (Range_Num != null)
            Range_Num.text = Model.Stats.Attack_Range.ToString();

        // -- DAMAGE --
        if (Damage_Num != null)
            Damage_Num.text = Model.Stats.Attack_Damage.ToString();

        // -- ARMOR (shows saves and target, e.g., "1/4+") --
        if (Armor_Num != null)
            Armor_Num.text = $"{Model.Stats.Armor_Saves}/{Model.Stats.Armor_Target}+";

        // -- DESCRIPTION --
        if (Description_Text != null)
        {
            string description = "";

            if (Model.Stats.Is_Ranged)
                description += "• Ranged Attack\n";

            if (Model.Stats.Has_Splash_Damage)
                description += "• Splash Damage\n";

            if (Model.Stats.Has_Ability)
                description += "• Special Ability\n";

            if (string.IsNullOrEmpty(description))
                description = "No special abilities";

            Description_Text.text = description.TrimEnd('\n');
        }

        // -- FACTION ICON --
        if (Faction_Icon != null && Faction != null)
        {
            Sprite factionIcon = Faction.Get_Faction_Icon();
            if (factionIcon != null)
            {
                Faction_Icon.sprite = factionIcon;
            }
            else if (Default_Faction_Icon != null)
            {
                Faction_Icon.sprite = Default_Faction_Icon;
            }
        }

        // -- MODEL ICON --
        if (Model_Icon != null && Faction != null)
        {
            Sprite modelIcon = Faction.Get_Model_Icon(Model.Type);
            if (modelIcon != null)
            {
                Model_Icon.sprite = modelIcon;
            }
            else if (Default_Model_Icon != null)
            {
                Model_Icon.sprite = Default_Model_Icon;
            }
        }

        // -- ENABLE/DISABLE ATTACK BUTTON --
        if (Attack_Button != null)
        {
            Attack_Button.interactable = !Model.Has_Attacked_This_Turn;
        }
    }

    private void Trigger_Open()
    {
        if (Card_Animator != null)
        {
            Card_Animator.ResetTrigger(Close_Trigger);
            Card_Animator.SetTrigger(Open_Trigger);
            Is_Card_Open = true;
        }
        else
        {
            Debug.LogWarning("Card_UI_Controller: Cannot play Open animation - Animator is missing!");
            Is_Card_Open = true;
        }
    }

    private void Trigger_Close()
    {
        if (Card_Animator != null)
        {
            Card_Animator.ResetTrigger(Open_Trigger);
            Card_Animator.SetTrigger(Close_Trigger);
        }
        else
        {
            Debug.LogWarning("Card_UI_Controller: Cannot play Close animation - Animator is missing!");
        }
    }

    private void Clear_Card()
    {
        if (Name_Text != null)
            Name_Text.text = "---";

        if (Health_Num != null)
            Health_Num.text = "-/-";

        if (Movement_Num != null)
            Movement_Num.text = "-";

        if (Armor_Num != null)
            Armor_Num.text = "-";

        if (Attack_Num != null)
            Attack_Num.text = "-";

        if (Range_Num != null)
            Range_Num.text = "-";

        if (Damage_Num != null)
            Damage_Num.text = "-";

        if (Description_Text != null)
            Description_Text.text = "Select a model to view details";

        if (Faction_Icon != null)
            Faction_Icon.sprite = Default_Faction_Icon;

        if (Model_Icon != null)
            Model_Icon.sprite = Default_Model_Icon;

        // Disable attack button when no model is selected
        if (Attack_Button != null)
        {
            Attack_Button.interactable = false;
        }
    }

    public bool Is_Displaying_Model(Model_Standard_Behavior Model)
    {
        return Current_Displayed_Model == Model && Is_Card_Open;
    }

    public void Refresh_Displayed_Model()
    {
        if (Current_Displayed_Model != null && Is_Card_Open)
        {
            // Update health display
            if (Health_Num != null)
                Health_Num.text = $"{Current_Displayed_Model.Current_Health}/{Current_Displayed_Model.Stats.Health}";

            // Update attack button state
            if (Attack_Button != null)
                Attack_Button.interactable = !Current_Displayed_Model.Has_Attacked_This_Turn;
        }
    }

    private void On_Attack_Button_Clicked()
    {
        if (Combat_Mgr != null)
        {
            Combat_Mgr.On_Attack_Button_Pressed();
        }
        else
        {
            Debug.LogError("Combat_Manager not found in scene! Make sure it exists on a GameObject.");
        }
    }
}