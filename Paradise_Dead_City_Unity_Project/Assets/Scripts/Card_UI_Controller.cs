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
    [SerializeField] private Button Ability_Button;

    [Header("Status Row")]
    [SerializeField] private Status_Row_UI Status_Row;

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
        }

        Combat_Mgr = FindAnyObjectByType<Combat_Manager>();

        if (Battle_Board == null)
            Battle_Board = FindAnyObjectByType<Battle_Board_Behavior>();
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

        if (Ability_Button != null)
            Ability_Button.onClick.AddListener(On_Ability_Button_Clicked);
    }


    // ============================================================
    // PUBLIC API
    // ============================================================

    public void Show_Model_Info(Model_Standard_Behavior Model, Faction_Data_SO Faction)
    {
        if (Model == null || Model.Stats == null) return;

        if (Current_Close != null)
        {
            StopCoroutine(Current_Close);
            Current_Close = null;
        }

        if (Is_Card_Open && Current_Displayed_Model == Model)
        {
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
        if (!Is_Card_Open) return;

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
        Update_Button_States();
    }

    public Model_Standard_Behavior Get_Displayed_Model() => Current_Displayed_Model;

    // ============================================================
    // TRANSITIONS
    // ============================================================

    private IEnumerator Close_Card_Sequence()
    {
        Trigger_Close();
        Is_Card_Open = false;

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

        Is_Card_Open = true;
    }

    public void Refresh_Card_For_Model(Model_Standard_Behavior Model)
    {
        if (Model == null || Model.Stats == null) return;
        if (Current_Displayed_Model != Model) return;

        Populate_Card_Data(Model, Current_Faction);
    }

    private void Trigger_Close()
    {
        if (Card_Animator != null)
        {
            Card_Animator.ResetTrigger(Open_Trigger);
            Card_Animator.SetTrigger(Close_Trigger);
        }
    }


    // ============================================================
    // CARD POPULATION
    // ============================================================

    private void Populate_Card_Data(Model_Standard_Behavior Model, Faction_Data_SO Faction)
    {
        Set_Text(Name_Text, Model.Stats.Model_Name);
        Set_Text(Health_Num, $"{Model.Current_Health}/{Model.Stats.Health}");
        Set_Text(Movement_Num, Model.Movement_Remaining_This_Turn.ToString());
        Set_Text(Attack_Num, $"{Model.Stats.Attack_Skill}+");
        Set_Text(Range_Num, Model.Stats.Attack_Range.ToString());
        Set_Text(Damage_Num, Model.Stats.Attack_Damage.ToString());
        Set_Text(Armor_Num, $"{Model.Stats.Armor_Saves}/{Model.Stats.Armor_Target}+");
        Set_Text(Description_Text, Build_Description(Model.Stats));

        Set_Image(Faction_Icon, Faction?.Get_Faction_Icon(), Default_Faction_Icon);
        Set_Image(Model_Icon, Faction?.Get_Model_Icon(Model.Type), Default_Model_Icon);

        if (Status_Row != null)
            Status_Row.Show_Statuses(Model.Get_Active_Statuses());

        Update_Button_States();
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

        if (Faction_Icon != null) Faction_Icon.sprite = Default_Faction_Icon;
        if (Model_Icon != null) Model_Icon.sprite = Default_Model_Icon;

        if (Status_Row != null) Status_Row.Clear();

        if (Attack_Button != null) Attack_Button.gameObject.SetActive(false);
        if (Sprint_Button != null) Sprint_Button.gameObject.SetActive(false);
        if (Ability_Button != null) Ability_Button.gameObject.SetActive(false);
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

        if (Stats.Ability_Data != null)
        {
            string Kind_Label = Stats.Ability_Data.Kind == Ability_Kind.Active
                ? "Active"
                : "Passive";

            Description += $"{Description_Bullet}{Kind_Label}: {Stats.Ability_Data.Ability_Name}\n";
        }

        if (string.IsNullOrEmpty(Description))
            return "No special abilities";

        return Description.TrimEnd('\n');
    }

    private static void Set_Text(TextMeshProUGUI Field, string Value)
    {
        if (Field != null) Field.text = Value;
    }

    private static void Set_Image(Image Field, Sprite Primary, Sprite Fallback)
    {
        if (Field == null) return;
        Field.sprite = Primary != null ? Primary : Fallback;
    }

    /// <summary>
    /// Updates both the visibility and the interactability of every button
    /// based on the currently displayed model and the active player.
    /// Visibility is a model property (has an active ability or not);
    /// interactability is a turn-state property (already acted, etc.).
    /// </summary>
    private void Update_Button_States()
    {
        Model_Standard_Behavior Displayed = Current_Displayed_Model;

        bool Has_Model = Displayed != null;
        bool Is_Mine = Has_Model && Displayed.Team == Active_Player;

        bool Can_Act = Is_Mine
               && !Displayed.Has_Attacked_This_Turn
               && !Displayed.Has_Ended_Turn;

        bool Is_Sprinting = Has_Model && Displayed.Is_Sprinting_This_Turn;

        // Death on Wheels locks the unit to movement for the rest of the
        // turn. Sprint and Attack are disabled; the Ability button is
        // already greyed out by its recharge.
        bool Locked_To_Movement = Has_Model
                                  && Displayed.Has_Status(Status_Type.Death_On_Wheels);

        // ---- Visibility ----
        // Buttons only appear on our own models. Enemy cards are read-only,
        // so the entire button column collapses for them.
        if (Attack_Button != null)
            Attack_Button.gameObject.SetActive(Is_Mine);

        if (Sprint_Button != null)
            Sprint_Button.gameObject.SetActive(Is_Mine);

        if (Ability_Button != null)
        {
            bool Has_Active_Ability = Is_Mine
                && Displayed.Stats != null
                && Displayed.Stats.Get_Ability_Kind() == Ability_Kind.Active;

            Ability_Button.gameObject.SetActive(Has_Active_Ability);
        }

        // ---- Interactability ----
        // Only meaningful while a button is visible, but harmless to set
        // unconditionally.
        if (Attack_Button != null)
            Attack_Button.interactable = Can_Act && !Is_Sprinting && !Locked_To_Movement;

        if (Sprint_Button != null)
            Sprint_Button.interactable = Can_Act && !Locked_To_Movement;

        if (Ability_Button != null && Ability_Button.gameObject.activeSelf)
        {
            bool Ability_Ready = Displayed.Ability == null || Displayed.Ability_Is_Ready;
            Ability_Button.interactable = Can_Act && !Is_Sprinting && Ability_Ready;
        }
    }

    public void Refresh_Displayed_Stats(Model_Standard_Behavior Model)
    {
        if (Current_Displayed_Model != Model) return;

        Set_Text(Health_Num, $"{Model.Current_Health}/{Model.Stats.Health}");
        Set_Text(Movement_Num, Model.Movement_Remaining_This_Turn.ToString());
        Update_Button_States();
    }


    // ============================================================
    // BUTTON CALLBACKS
    // ============================================================

    private void On_Attack_Button_Clicked()
    {
        if (Battle_Board != null) Battle_Board.Notify_UI_Button_Pressed();

        if (Combat_Mgr != null)
            Combat_Mgr.On_Attack_Button_Pressed();
    }

    private void On_Sprint_Button_Clicked()
    {
        if (Battle_Board == null) return;

        Battle_Board.Notify_UI_Button_Pressed();

        Model_Standard_Behavior Selected = Battle_Board.Get_Selected_Model();
        if (Selected != null && Selected.Is_Sprinting_This_Turn)
        {
            if (Battle_Board.Cancel_Sprint(Selected))
                Battle_Board.Select_Model_Public(Selected);
            return;
        }

        if (Battle_Board.Begin_Sprint())
            Hide_Model_Info();
    }

    private void On_Ability_Button_Clicked()
    {
        if (Battle_Board == null) return;

        Battle_Board.Notify_UI_Button_Pressed();

        // Second press confirms the targeting session if one is active.
        var Targeting = Battle_Board.Get_Ability_Targeting_Controller();
        if (Targeting != null && Targeting.Is_Currently_Targeting())
        {
            Targeting.Confirm();

            // Refresh so the button reflects the new cooldown and any
            // status changes on the displayed model.
            if (Current_Displayed_Model != null)
                Refresh_Card_For_Model(Current_Displayed_Model);
            return;
        }

        Model_Standard_Behavior Selected = Battle_Board.Get_Selected_Model();
        if (Selected == null || Selected.Ability == null) return;

        if (!Selected.Ability.Can_Activate(Selected, Battle_Board)) return;

        Selected.Ability.Activate(Selected, Battle_Board);

        if (Current_Displayed_Model == Selected)
            Refresh_Card_For_Model(Selected);
    }
}