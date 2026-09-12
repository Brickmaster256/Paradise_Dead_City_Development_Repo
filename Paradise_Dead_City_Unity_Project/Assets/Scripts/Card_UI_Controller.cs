using TMPro;
using UnityEngine.UI;
using UnityEngine;
using System.Collections;

public class Card_UI_Controller : MonoBehaviour
{
    // ============================================================
    // CONSTANTS
    // ============================================================

    private const string Description_Bullet = "• ";
    private const string Default_Name = "---";
    private const string Default_Health = "-/-";
    private const string Default_Stat = "-";
    private const string Default_Description = "Select a model to view details";

    private static readonly int Open_Trigger = Animator.StringToHash("Open");
    private static readonly int Close_Trigger = Animator.StringToHash("Close");


    // ============================================================
    // SERIALIZED FIELDS
    // ============================================================

    [Header("Animator")]
    [SerializeField] private Animator Card_Animator;

    [Header("References")]
    [SerializeField] private Battle_Board_Behavior Battle_Board;

    [Header("Buttons")]
    [SerializeField] private Button Attack_Button;
    [SerializeField] private Button Sprint_Button;

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
    [Tooltip("How long the close animation takes. Card data remains visible for this duration.")]
    [SerializeField] private float Close_Animation_Duration = 0.5f;

    [Tooltip("Extra delay between close and open when transitioning from one model to another.")]
    [SerializeField] private float Card_Transition_Delay = 0.3f;


    // ============================================================
    // STATE
    // ============================================================

    private int Active_Player = 1;

    private bool Is_Card_Open = false;
    private Model_Standard_Behavior Current_Displayed_Model;
    private Faction_Data_SO Current_Faction;
    private Coroutine Current_Transition;

    private Combat_Manager Combat_Mgr;

    private Coroutine Current_Close;


    // ============================================================
    // UNITY LIFECYCLE
    // ============================================================

    private void Awake()
    {
        if (Card_Animator == null)
        {
            Card_Animator = GetComponent<Animator>();
            if (Card_Animator == null)
                Card_Animator = GetComponentInChildren<Animator>();

            if (Card_Animator == null)
                Debug.LogWarning("Card_UI_Controller: No Animator found! Please assign one in the Inspector.");
        }

        Combat_Mgr = FindAnyObjectByType<Combat_Manager>();
        if (Combat_Mgr == null)
            Debug.LogWarning("Card_UI_Controller: Combat_Manager not found in scene! Attack button won't work.");

        if (Battle_Board == null)
            Battle_Board = FindAnyObjectByType<Battle_Board_Behavior>();
        if (Battle_Board == null)
            Debug.LogWarning("Card_UI_Controller: Battle_Board not found in scene! Sprint button won't work.");
    }

    private void Start()
    {
        Clear_Card();

        if (Card_Animator != null)
        {
            Card_Animator.ResetTrigger(Open_Trigger);
            Card_Animator.ResetTrigger(Close_Trigger);
        }

        if (Attack_Button != null)
            Attack_Button.onClick.AddListener(On_Attack_Button_Clicked);

        if (Sprint_Button != null)
            Sprint_Button.onClick.AddListener(On_Sprint_Button_Clicked);
    }


    // ============================================================
    // PUBLIC API
    // ============================================================

    public void Show_Model_Info(Model_Standard_Behavior Model, Faction_Data_SO Faction)
    {
        if (Model == null || Model.Stats == null)
        {
            Debug.LogWarning("Card_UI_Controller: Cannot display null model or model without stats");
            return;
        }

        // Cancel any in-progress close; we're about to (re)open the card.
        if (Current_Close != null)
        {
            StopCoroutine(Current_Close);
            Current_Close = null;
        }

        if (Is_Card_Open && Current_Displayed_Model == Model)
        {
            // Already showing this model - just refresh data/buttons without animation.
            Refresh_Card_For_Model(Model);
            return;
        }

        if (Is_Card_Open && Current_Displayed_Model != Model)
        {
            if (Current_Transition != null)
                StopCoroutine(Current_Transition);

            Current_Transition = StartCoroutine(Transition_To_New_Model(Model, Faction));
            return;
        }

        Current_Faction = Faction;
        Current_Displayed_Model = Model;
        Populate_Card_Data(Model, Faction);
        Trigger_Open();
    }

    public void Hide_Model_Info()
    {
        if (!Is_Card_Open)
            return;

        if (Current_Transition != null)
        {
            StopCoroutine(Current_Transition);
            Current_Transition = null;
        }

        if (Current_Close != null)
            StopCoroutine(Current_Close);

        Current_Close = StartCoroutine(Close_Card_Sequence());
    }

    public void Set_Active_Player(int Player)
    {
        Active_Player = Player;
        Update_Attack_Button_State();
    }


    // ============================================================
    // TRANSITIONS
    // ============================================================

    private IEnumerator Close_Card_Sequence()
    {
        Trigger_Close();
        Is_Card_Open = false;

        // Data stays visible during the close animation
        yield return new WaitForSeconds(Close_Animation_Duration);

        Clear_Card();
        Current_Displayed_Model = null;
    }

    private IEnumerator Transition_To_New_Model(Model_Standard_Behavior New_Model, Faction_Data_SO New_Faction)
    {
        Trigger_Close();
        Is_Card_Open = false;

        yield return new WaitForSeconds(Close_Animation_Duration + Card_Transition_Delay);

        Current_Displayed_Model = New_Model;
        Populate_Card_Data(New_Model, New_Faction);

        Trigger_Open();
        Is_Card_Open = true;

        Current_Transition = null;
    }

    private void Trigger_Open()
    {
        if (Card_Animator != null)
        {
            Card_Animator.ResetTrigger(Close_Trigger);
            Card_Animator.SetTrigger(Open_Trigger);
        }
        else
        {
            Debug.LogWarning("Card_UI_Controller: Cannot play Open animation - Animator is missing!");
        }

        Is_Card_Open = true;
    }

    /// <summary>
    /// Forces a full re-population of the card for the given model, even if
    /// it's already the displayed model. Use after state changes (movement,
    /// damage) that should update button interactability.
    /// </summary>
    public void Refresh_Card_For_Model(Model_Standard_Behavior Model)
    {
        if (Model == null || Model.Stats == null)
            return;

        if (Current_Displayed_Model != Model)
            return;

        Populate_Card_Data(Model, Current_Faction);
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


    // ============================================================
    // CARD POPULATION
    // ============================================================

    private void Populate_Card_Data(Model_Standard_Behavior Model, Faction_Data_SO Faction)
    {
        Set_Text(Name_Text, Model.Stats.Model_Name);
        Set_Text(Health_Num, $"{Model.Current_Health}/{Model.Stats.Health}");
        Set_Text(Movement_Num, Model.Stats.Movement_Range.ToString());
        Set_Text(Attack_Num, $"{Model.Stats.Attack_Skill}+");
        Set_Text(Range_Num, Model.Stats.Attack_Range.ToString());
        Set_Text(Damage_Num, Model.Stats.Attack_Damage.ToString());
        Set_Text(Armor_Num, $"{Model.Stats.Armor_Saves}/{Model.Stats.Armor_Target}+");
        Set_Text(Description_Text, Build_Description(Model.Stats));

        Set_Image(Faction_Icon, Faction?.Get_Faction_Icon(), Default_Faction_Icon);
        Set_Image(Model_Icon, Faction?.Get_Model_Icon(Model.Type), Default_Model_Icon);

        Update_Attack_Button_State();
    }

    private void Clear_Card()
    {
        Set_Text(Name_Text, Default_Name);
        Set_Text(Health_Num, Default_Health);
        Set_Text(Movement_Num, Default_Stat);
        Set_Text(Armor_Num, Default_Stat);
        Set_Text(Attack_Num, Default_Stat);
        Set_Text(Range_Num, Default_Stat);
        Set_Text(Damage_Num, Default_Stat);
        Set_Text(Description_Text, Default_Description);

        if (Faction_Icon != null)
            Faction_Icon.sprite = Default_Faction_Icon;
        if (Model_Icon != null)
            Model_Icon.sprite = Default_Model_Icon;

        if (Attack_Button != null)
            Attack_Button.interactable = false;

        if (Sprint_Button != null)
            Sprint_Button.interactable = false;
    }


    // ============================================================
    // HELPERS
    // ============================================================

    private static string Build_Description(Model_Stats_SO Stats)
    {
        string Description = "";

        if (Stats.Is_Ranged)
            Description += $"{Description_Bullet}Ranged Attack\n";

        if (Stats.Has_Splash_Damage)
            Description += $"{Description_Bullet}Splash Damage\n";

        if (Stats.Has_Ability)
            Description += $"{Description_Bullet}Special Ability\n";

        if (string.IsNullOrEmpty(Description))
            return "No special abilities";

        return Description.TrimEnd('\n');
    }

    private static void Set_Text(TextMeshProUGUI Field, string Value)
    {
        if (Field != null)
            Field.text = Value;
    }

    private static void Set_Image(Image Field, Sprite Primary, Sprite Fallback)
    {
        if (Field == null)
            return;

        Field.sprite = Primary != null ? Primary : Fallback;
    }

    private void Update_Attack_Button_State()
    {
        if (Attack_Button == null && Sprint_Button == null)
            return;

        bool Is_Mine = Current_Displayed_Model != null
                       && Current_Displayed_Model.Team == Active_Player;

        bool Can_Act = Is_Mine
                       && !Current_Displayed_Model.Has_Attacked_This_Turn
                       && !Current_Displayed_Model.Has_Ended_Turn;

        bool Can_Attack = Can_Act && !Current_Displayed_Model.Is_Sprinting_This_Turn;
        bool Can_Sprint = Can_Act && !Current_Displayed_Model.Is_Sprinting_This_Turn;

        if (Attack_Button != null)
            Attack_Button.interactable = Can_Attack;

        if (Sprint_Button != null)
            Sprint_Button.interactable = Can_Sprint;

        string Name = Current_Displayed_Model != null
            ? Current_Displayed_Model.Stats.Model_Name : "(none)";
    }

    /// <summary>
    /// Updates the displayed health for the currently-displayed model
    /// without triggering an animation. Does nothing if the currently
    /// displayed model isn't the one passed in.
    /// </summary>
    public void Refresh_Displayed_Health(Model_Standard_Behavior Model)
    {
        if (Current_Displayed_Model != Model)
            return;

        Set_Text(Health_Num, $"{Model.Current_Health}/{Model.Stats.Health}");
        Update_Attack_Button_State();
    }

    // ============================================================
    // BUTTON CALLBACKS
    // ============================================================

    private void On_Attack_Button_Clicked()
    {
        if (Battle_Board != null)
            Battle_Board.Notify_UI_Button_Pressed();

        if (Combat_Mgr != null)
            Combat_Mgr.On_Attack_Button_Pressed();
        else
            Debug.LogError("Combat_Manager not found in scene!");
    }

    private void On_Sprint_Button_Clicked()
    {
        if (Battle_Board == null)
        {
            Debug.LogError("Card_UI_Controller: Battle_Board reference missing.");
            return;
        }

        Battle_Board.Notify_UI_Button_Pressed();

        if (Battle_Board.Begin_Sprint())
            Hide_Model_Info();
    }
}