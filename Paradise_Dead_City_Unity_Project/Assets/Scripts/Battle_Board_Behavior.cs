using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public enum Game_State
{
    Player_1_Place_Spawn,
    Player_1_Place_Models,
    Player_2_Place_Spawn,
    Player_2_Place_Models,
    Gameplay
}

public enum Board_Modifiers
{
    None,
    Cover,
    Wall,
    Terrain,
    Hazard,
    Spawn
}

public class Battle_Board_Behavior : MonoBehaviour
{
    // ============================================================
    // CONSTANTS
    // ============================================================

    private const int Tile_Count_X = 8;
    private const int Tile_Count_Y = 8;

    private const int Spawn_Row_Depth = 2;

    private const int Player_1 = 1;
    private const int Player_2 = 2;

    private const float Raycast_Max_Distance = 100f;
    private const float Board_Collider_Thickness = 0.01f;

    private static readonly Vector2Int No_Tile = new Vector2Int(-1, -1);

    private static readonly Vector2Int[] Cardinal_Directions =
    {
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0)
    };

    private const float UI_Block_Duration = 0.1f;


    // ============================================================
    // SERIALIZED FIELDS
    // ============================================================

    [Header("Assets")]
    [SerializeField] private Material Tile_Material;
    [SerializeField] private float Tile_Size = 1.0f;
    [SerializeField] private float Y_Offset = 0.2f;
    [SerializeField] private Vector3 Board_Center = Vector3.zero;

    [Header("UI References")]
    [SerializeField] private Card_UI_Controller Card_UI;

    [Header("Combat References")]
    [SerializeField] private Combat_Manager Combat_Manager_Ref;

    [Header("Map Data")]
    [SerializeField] private Map_Data_SO Current_Map;
    [SerializeField] private GameObject Cover_Tile_Prefab;
    [SerializeField] private GameObject Wall_Tile_Prefab;
    [SerializeField] private GameObject Terrain_Tile_Prefab;
    [SerializeField] private GameObject Hazard_Tile_Prefab;
    [SerializeField] private float Terrain_Tile_Y_Offset = 0.1f;

    [Header("Drag Settings")]
    [SerializeField] private float Normal_Drag_Offset = 0.5f;
    [SerializeField] private float Altered_Drag_Offset = 1.0f;
    [SerializeField] private float Drag_Detect_Radius = 0.4f;
    [SerializeField] private float Drag_Hold_Time = 0.2f;

    [Header("Factions")]
    [SerializeField] private Faction_Data_SO Player_1_Faction;
    [SerializeField] private Faction_Data_SO Player_2_Faction;

    [Header("Spawn Settings")]
    [Range(1, 8)][SerializeField] private int Spawn_Zone_Width = 5;
    [Range(1, 4)][SerializeField] private int Spawn_Zone_Depth = 2;

    [Header("Highlight Materials")]
    [SerializeField] private Material Valid_Spawn_Tile_Locations_Material;
    [SerializeField] private Material Model_Placement_Material;
    [SerializeField] private Material Movement_Range_Material;
    [SerializeField] private Material Hazard_Path_Material;

    [Header("Path Materials (Priority Order)")]
    [Tooltip("Index 0 is the primary path, 1 is secondary, 2 is tertiary, etc. " +
             "How many of these are actually used is controlled by Path_Slot_Count.")]
    [SerializeField] private Material[] Path_Materials = new Material[6];

    [Header("Path Slots")]
    [Tooltip("How many path priority levels are available. Values above the " +
             "number of assigned Path_Materials fall back to the last assigned " +
             "material. Set to 4 for the original behaviour.")]
    [Range(1, 8)]
    [SerializeField] private int Path_Slot_Count = 4;

    [Header("Shove Materials")]
    [Tooltip("Applied to an enemy tile that can be shoved from the current drag position.")]
    [SerializeField] private Material Shove_Target_Material;

    [Tooltip("Applied to tiles the defender would be pushed into.")]
    [SerializeField] private Material Shove_Destination_Material;

    [Tooltip("Optional: applied to the enemy tile when the full shove path is " +
             "blocked (a damage shove). Leave unassigned to skip.")]
    [SerializeField] private Material Shove_Blocked_Material;

    [Header("Shove Path Generation")]
    [Tooltip("Maximum number of shove paths shown to the player. Generation may " +
             "produce more candidates; the curator trims to this many.")]
    [Range(1, 12)]
    [SerializeField] private int Max_Shove_Paths_Shown = 8;

    [Tooltip("If true, keep every shortest route to each stop tile, so a single " +
             "approach direction can appear multiple times (once per route). " +
             "If false, keep only one route per approach direction.")]
    [SerializeField] private bool Show_Alternate_Routes_Per_Direction = true;

    [Tooltip("Within a direction, prefer routes with fewer hazard tiles when " +
             "choosing which routes survive the cap.")]
    [SerializeField] private bool Prefer_Hazard_Free_Routes = true;

    [Header("Flashing")]
    [SerializeField] private float Flash_Speed = 2.0f;
    [SerializeField] private float Flash_Min_Alpha = 0.3f;
    [SerializeField] private float Flash_Max_Alpha = 1.0f;

    [Header("Input System")]
    [SerializeField] private InputActionAsset InputActions;

    [Header("Debug Logging")]
    [SerializeField] private bool Debug_Log_Input = false;
    [SerializeField] private bool Debug_Log_Pathfinding = false;
    [SerializeField] private bool Debug_Log_Movement = false;
    [SerializeField] private bool Debug_Log_Placement = false;
    [SerializeField] private bool Debug_Log_Shove = false;
    [Tooltip("Log every path-material lookup. Very noisy - only enable when " +
             "you suspect a slot is unassigned.")]
    [SerializeField] private bool Debug_Log_Path_Materials = false;


    // ============================================================
    // BOARD STATE
    // ============================================================

    private GameObject[,] Tiles;
    private Camera Main_Camera;
    private Vector2Int Current_Mouse_Hover;
    private Vector3 Bounds;
    private Vector3 Board_Origin;

    private Board_Modifiers[,] Map_Tiles;
    private GameObject[,] Terrain_Objects;

    private int Tile_Layer;
    private int Hover_Layer;
    private int Raycast_Layer_Mask;


    // ============================================================
    // MODEL STATE
    // ============================================================

    private Model_Standard_Behavior[,] Models;
    private Model_Standard_Behavior Dragged_Model;
    private Model_Standard_Behavior Selected_Model;
    private Material Player_1_Mat;
    private Material Player_2_Mat;


    // ============================================================
    // PLAYER STATE
    // ============================================================

    private int Active_Player = Player_1;
    public int Get_Active_Player() => Active_Player;


    // ============================================================
    // DRAG STATE
    // ============================================================

    private float Current_Hold_Time = 0f;
    private bool Is_Holding = false;
    private Vector2Int Hold_Start_Position;


    // ============================================================
    // PATH STATE
    // ============================================================

    private List<List<Vector2Int>> All_Paths_To_Target = new List<List<Vector2Int>>();
    private int Current_Path_Index = 0;
    private Vector2Int Current_Hover_Target = No_Tile;
    private List<Vector2Int> Path_Highlight_Tiles = new List<Vector2Int>();

    private int Shove_Path_Rebuild_Count = 0;


    // ============================================================
    // SHOVE STATE
    // ============================================================

    private List<Vector2Int> Shove_Target_Highlights = new List<Vector2Int>();
    private List<Vector2Int> Shove_Destination_Highlights = new List<Vector2Int>();
    private bool Shove_Highlights_Are_Blocked = false;


    // ============================================================
    // SPAWN STATE
    // ============================================================

    private GameObject Player_1_Spawn_Tile;
    private GameObject Player_2_Spawn_Tile;
    private Vector2Int Player_1_Spawn_Position;
    private Vector2Int Player_2_Spawn_Position;


    // ============================================================
    // HIGHLIGHT STATE
    // ============================================================

    private List<Vector2Int> Spawn_Tile_Highlights = new List<Vector2Int>();
    private List<Vector2Int> Model_Placement_Highlights = new List<Vector2Int>();
    private List<Vector2Int> Movement_Range_Highlights = new List<Vector2Int>();
    private Material Current_Flash_Material;


    // ============================================================
    // GAME STATE
    // ============================================================

    private Game_State Current_Phase = Game_State.Player_1_Place_Spawn;
    private Army_Composition_SO Player_1_Army;
    private Army_Composition_SO Player_2_Army;


    // ============================================================
    // UI INPUT GUARD
    // ============================================================

    private float UI_Button_Pressed_Time = -1f;

    public void Notify_UI_Button_Pressed()
    {
        UI_Button_Pressed_Time = Time.unscaledTime;
    }

    private bool Was_UI_Button_Just_Pressed()
    {
        return Time.unscaledTime - UI_Button_Pressed_Time < UI_Block_Duration;
    }

    private PointerEventData UI_Pointer_Data;
    private readonly List<RaycastResult> UI_Raycast_Results = new List<RaycastResult>();


    // ============================================================
    // INPUT
    // ============================================================

    private InputAction Click_Action;
    private InputAction RightClick_Action;
    private InputAction MousePosition_Action;
    private InputAction Space_Bar_Action;
    private Vector2 Current_Mouse_Position;


    // ============================================================
    // UNITY LIFECYCLE
    // ============================================================

    private void Awake()
    {
        Cache_Layers();
        Generate_All_Tiles(Tile_Size);
        Assign_Team_Colors();

        if (Current_Map != null)
            Generate_Map_Terrain();

        if (Player_1_Faction != null && Player_1_Faction.Army_Composition != null)
            Player_1_Army = Instantiate(Player_1_Faction.Army_Composition);
        if (Player_2_Faction != null && Player_2_Faction.Army_Composition != null)
            Player_2_Army = Instantiate(Player_2_Faction.Army_Composition);

        Models = new Model_Standard_Behavior[Tile_Count_X, Tile_Count_Y];

        Setup_Input_System();
        Show_Spawn_Zone_Highlights(Player_1);

        Debug.Log($"Game Started! Phase: {Current_Phase}. Player 1 place your spawn tile (Rows {Get_Spawn_Min_Row(Player_1)}-{Get_Spawn_Max_Row(Player_1)})");
    }

    private void OnEnable()
    {
        if (InputActions != null)
            InputActions.Enable();
    }

    private void OnDisable()
    {
        if (InputActions != null)
            InputActions.Disable();
    }

    private void Update()
    {
        if (!Main_Camera)
        {
            Main_Camera = Camera.main;
            return;
        }

        Update_Mouse_Position();

        RaycastHit Info;
        Ray Ray = Main_Camera.ScreenPointToRay(Current_Mouse_Position);

        if (Physics.Raycast(Ray, out Info, Raycast_Max_Distance, Raycast_Layer_Mask))
        {
            Vector2Int Hit_Position = Convert_World_To_Tile_Position(Info.point);
            Update_Tile_Hover(Hit_Position);
            Process_Current_Phase(Hit_Position, Ray);
        }
        else
        {
            Reset_Tile_Hover();
            Handle_Drag_Release_Outside_Board();
        }

        Update_Flashing_Effect();
    }

    private void Cache_Layers()
    {
        Tile_Layer = LayerMask.NameToLayer("Tile");
        Hover_Layer = LayerMask.NameToLayer("Hover");
        Raycast_Layer_Mask = LayerMask.GetMask("Tile", "Hover");
    }


    // ============================================================
    // SPAWN ZONE HELPERS
    // ============================================================

    private static int Get_Spawn_Min_Row(int Player)
    {
        return Player == Player_1 ? 0 : Tile_Count_X - Spawn_Row_Depth;
    }

    private static int Get_Spawn_Max_Row(int Player)
    {
        return Player == Player_1 ? Spawn_Row_Depth - 1 : Tile_Count_X - 1;
    }


    // ============================================================
    // INPUT SYSTEM
    // ============================================================

    private void Setup_Input_System()
    {
        if (InputActions == null)
        {
            Debug.LogError("Input Actions Asset not assigned! Please assign it in the Inspector.");
            return;
        }

        var Action_Map = InputActions.FindActionMap("Battle_Board");
        if (Action_Map == null)
        {
            Debug.LogError("Battle_Board action map not found!");
            return;
        }

        Click_Action = Action_Map.FindAction("Left_Click");
        RightClick_Action = Action_Map.FindAction("Right_Click");
        MousePosition_Action = Action_Map.FindAction("Mouse_Position");
        Space_Bar_Action = Action_Map.FindAction("Space_Bar");

        if (Click_Action == null) Debug.LogError("Left_Click action not found!");
        if (RightClick_Action == null) Debug.LogError("Right_Click action not found!");
        if (MousePosition_Action == null) Debug.LogError("Mouse_Position action not found!");
        if (Space_Bar_Action == null)
            Debug.LogWarning("Space Bar action not found! Create a 'Space Bar' action bound to Space key.");
    }

    private void Update_Mouse_Position()
    {
        if (MousePosition_Action != null)
            Current_Mouse_Position = MousePosition_Action.ReadValue<Vector2>();
    }

    /// <summary>
    /// Manual EventSystem raycast because the parameterless
    /// IsPointerOverGameObject() is unreliable with InputSystemUIInputModule.
    /// </summary>
    private bool Is_Pointer_Over_UI()
    {
        if (EventSystem.current == null)
            return false;

        if (UI_Pointer_Data == null)
            UI_Pointer_Data = new PointerEventData(EventSystem.current);

        UI_Pointer_Data.position = Current_Mouse_Position;
        UI_Raycast_Results.Clear();
        EventSystem.current.RaycastAll(UI_Pointer_Data, UI_Raycast_Results);

        return UI_Raycast_Results.Count > 0;
    }


    // ============================================================
    // PHASE MANAGEMENT
    // ============================================================

    private void Process_Current_Phase(Vector2Int Hit_Position, Ray Ray)
    {
        switch (Current_Phase)
        {
            case Game_State.Player_1_Place_Spawn:
                Handle_Spawn_Placement_Input(Hit_Position, Player_1);
                break;

            case Game_State.Player_2_Place_Spawn:
                Handle_Spawn_Placement_Input(Hit_Position, Player_2);
                break;

            case Game_State.Player_1_Place_Models:
                Handle_Model_Placement_Input(Hit_Position, Player_1, Player_1_Army, Player_1_Mat);
                break;

            case Game_State.Player_2_Place_Models:
                Handle_Model_Placement_Input(Hit_Position, Player_2, Player_2_Army, Player_2_Mat);
                break;

            case Game_State.Gameplay:
                Process_Gameplay_Input(Hit_Position, Ray);
                break;
        }
    }


    // ============================================================
    // GAMEPLAY INPUT
    // ============================================================

    private void Process_Gameplay_Input(Vector2Int Hit_Position, Ray Ray)
    {
        if (Debug_Log_Input)
        {
            Debug.Log($"[Input] EventSystem={(EventSystem.current != null ? "present" : "NULL")}, " +
                      $"OverUI={Is_Pointer_Over_UI()}, " +
                      $"HitTile={Hit_Position}, " +
                      $"LeftPressed={Click_Action?.WasPressedThisFrame()}, " +
                      $"LeftReleased={Click_Action?.WasReleasedThisFrame()}, " +
                      $"UIBlock={Was_UI_Button_Just_Pressed()}");
        }

        if (Is_Pointer_Over_UI())
            return;

        if (Was_UI_Button_Just_Pressed())
            return;

        // Combat targeting takes absolute priority and blocks all other input.
        if (Combat_Manager_Ref != null && Combat_Manager_Ref.Is_Currently_Selecting_Target())
        {
            if (Click_Action != null && Click_Action.WasPressedThisFrame())
                Combat_Manager_Ref.Try_Select_Target(Hit_Position);

            if (RightClick_Action != null && RightClick_Action.WasPressedThisFrame())
                Combat_Manager_Ref.Cancel_Attack();

            return;
        }

        if (Click_Action != null && Click_Action.WasPressedThisFrame())
            Handle_Model_Selection(Hit_Position);

        if (Click_Action != null && Click_Action.WasReleasedThisFrame())
            Handle_Model_Drop(Hit_Position);

        if (RightClick_Action != null && RightClick_Action.WasPressedThisFrame())
            Deselect_Current_Model();

        // Read the space-bar press once. WasPressedThisFrame() on the same
        // action can observe false on a second call within the same frame.
        bool space_pressed = Space_Bar_Action != null && Space_Bar_Action.WasPressedThisFrame();

        if (space_pressed && Dragged_Model != null)
            Switch_To_Next_Path();

        Update_Drag_Hold_Timer();
        Update_Dragged_Model_Position(Ray, Hit_Position);
    }


    // ============================================================
    // MODEL SELECTION
    // ============================================================

    public void Select_Model_Public(Model_Standard_Behavior Model)
    {
        if (Model == null || !Model.gameObject.activeSelf)
            return;

        if (Model.Team != Active_Player)
        {
            Debug.Log($"Select_Model_Public: Cannot select enemy model (Team {Model.Team}). Active player is {Active_Player}.");
            return;
        }

        if (Selected_Model != null && Selected_Model != Model)
        {
            Clear_Movement_Range_Highlights();
            Clear_Path_Highlights();
        }

        Selected_Model = Model;
        Display_Movement_Range(Model);

        if (Card_UI != null)
        {
            Faction_Data_SO Faction = Model.Team == Player_1 ? Player_1_Faction : Player_2_Faction;
            Card_UI.Show_Model_Info(Model, Faction);
        }

        if (Debug_Log_Input)
            Debug.Log($"Re-selected {Model.Stats.Model_Name} at ({Model.Current_X}, {Model.Current_Y})");
    }

    private void Handle_Model_Selection(Vector2Int Hit_Position)
    {
        Model_Standard_Behavior Clicked_Model = Models[Hit_Position.x, Hit_Position.y];

        if (Clicked_Model != null)
        {
            if (Clicked_Model.Team != Active_Player)
            {
                Debug.Log($"Cannot select model from opposing team (Player {Clicked_Model.Team}). Active player is {Active_Player}.");
                return;
            }

            if (Clicked_Model == Selected_Model)
            {
                Start_Hold_Timer(Hit_Position);
                return;
            }

            Clear_Previous_Selection();
            Select_New_Model(Clicked_Model, Hit_Position);
        }
        else
        {
            Deselect_Current_Model();
        }
    }

    private void Select_New_Model(Model_Standard_Behavior Model, Vector2Int Hit_Position)
    {
        Selected_Model = Model;
        Display_Movement_Range(Model);
        Start_Hold_Timer(Hit_Position);

        if (Card_UI != null)
        {
            Faction_Data_SO Faction = Model.Team == Player_1 ? Player_1_Faction : Player_2_Faction;
            Card_UI.Show_Model_Info(Model, Faction);
        }
    }

    private void Clear_Previous_Selection()
    {
        if (Selected_Model != null)
        {
            Clear_Movement_Range_Highlights();
            Dragged_Model = null;
        }
    }

    public void Deselect_Current_Model_Public()
    {
        Deselect_Current_Model();
    }

    private void Deselect_Current_Model()
    {
        if (Selected_Model != null && Dragged_Model != null)
            Snap_Model_Back_To_Position(Dragged_Model, new Vector2Int(Dragged_Model.Current_X, Dragged_Model.Current_Y));

        if (Combat_Manager_Ref != null)
            Combat_Manager_Ref.Force_Clear_Combat_State();

        Clear_Movement_Range_Highlights();
        Clear_Path_Highlights();

        Selected_Model = null;
        Dragged_Model = null;
        Reset_Drag_State();

        if (Card_UI != null)
            Card_UI.Hide_Model_Info();
    }


    // ============================================================
    // DRAG / HOLD
    // ============================================================

    private void Start_Hold_Timer(Vector2Int Hit_Position)
    {
        Is_Holding = true;
        Current_Hold_Time = 0f;
        Hold_Start_Position = Hit_Position;
    }

    private void Update_Drag_Hold_Timer()
    {
        if (Is_Holding && Selected_Model != null && Click_Action != null && Click_Action.IsPressed())
        {
            Current_Hold_Time += Time.deltaTime;
            if (Current_Hold_Time >= Drag_Hold_Time && Dragged_Model == null)
                Dragged_Model = Selected_Model;
        }
    }

    private void Update_Dragged_Model_Position(Ray Ray, Vector2Int Hit_Position)
    {
        if (!Dragged_Model)
            return;

        if (Click_Action != null && Click_Action.WasReleasedThisFrame())
            return;

        Plane Horizontal_Plane = new Plane(Vector3.up, Vector3.up * Y_Offset);
        if (Horizontal_Plane.Raycast(Ray, out float Distance))
        {
            Vector3 Mouse_Position = Ray.GetPoint(Distance);
            float Current_Drag_Offset = Calculate_Drag_Offset(Mouse_Position);
            Dragged_Model.Set_Position(Mouse_Position + Vector3.up * Current_Drag_Offset);
        }

        if (Shove_Target_Highlights.Contains(Hit_Position))
        {
            Show_Shove_Paths(Hit_Position);
            return;
        }

        Clear_Shove_Destination_Highlights();

        if (Movement_Range_Highlights.Contains(Hit_Position))
            Show_Paths_To_Target(Hit_Position);
        else
            Clear_Path_Highlights();
    }

    private void Handle_Model_Drop(Vector2Int Hit_Position)
    {
        if (Dragged_Model == null)
            return;

        Model_Standard_Behavior Moved_Model = Dragged_Model;

        // Snapshot before Reset_Drag_State / Try_Move_Model can wipe the
        // path state we still need for hazard damage.
        List<Vector2Int> Chosen_Path = Get_Selected_Path_Copy();
        Vector2Int Previous_Position = new Vector2Int(Moved_Model.Current_X, Moved_Model.Current_Y);

        Reset_Drag_State();

        bool Valid_Move = Try_Move_Model(Moved_Model, Hit_Position.x, Hit_Position.y);

        if (!Valid_Move)
        {
            Snap_Model_Back_To_Position(Moved_Model, Previous_Position);
            if (Debug_Log_Movement)
                Debug.Log("Invalid move - snapping back");
        }
        else
        {
            if (Debug_Log_Movement)
                Debug.Log($"Model moved from {Previous_Position} to {Hit_Position}");

            Clear_Path_Highlights();
            Refresh_Movement_Range_Highlights(Moved_Model);
            Apply_Hazard_Damage(Moved_Model, Chosen_Path);

            if (Moved_Model.gameObject.activeSelf && Selected_Model == Moved_Model)
            {
                Select_Model_Public(Moved_Model);

                if (Card_UI != null)
                    Card_UI.Refresh_Card_For_Model(Moved_Model);
            }
        }
    }

    /// <summary>
    /// Must be called before Clear_Path_Highlights, which clears
    /// All_Paths_To_Target.
    /// </summary>
    private List<Vector2Int> Get_Selected_Path_Copy()
    {
        if (All_Paths_To_Target.Count == 0)
            return new List<Vector2Int>();

        int Index = Mathf.Clamp(Current_Path_Index, 0, All_Paths_To_Target.Count - 1);
        return new List<Vector2Int>(All_Paths_To_Target[Index]);
    }

    /// <summary>
    /// 1 damage per hazard tile the path crosses, including the model's
    /// starting tile if it moved off a hazard. A model that stays put never
    /// reaches this method, so it can rest on a hazard without taking damage.
    /// </summary>
    private void Apply_Hazard_Damage(Model_Standard_Behavior Model, List<Vector2Int> Path)
    {
        int Hazards_Crossed = 0;

        foreach (Vector2Int Tile in Path)
        {
            if (Get_Tile_Type_At(Tile.x, Tile.y) == Board_Modifiers.Hazard)
                Hazards_Crossed++;
        }

        if (Hazards_Crossed <= 0)
            return;

        if (Debug_Log_Movement)
            Debug.Log($"{Model.Stats.Model_Name} crossed {Hazards_Crossed} hazard tile(s). Taking {Hazards_Crossed} damage.");

        bool Survived = Model.Take_Damage(Hazards_Crossed);

        if (!Survived)
        {
            Debug.Log($"{Model.Stats.Model_Name} was slain by hazards!");

            if (Selected_Model == Model)
                Deselect_Current_Model();

            if (Combat_Manager_Ref != null)
                Combat_Manager_Ref.Kill_Model(Model);
        }
        else
        {
            if (Card_UI != null)
                Card_UI.Refresh_Displayed_Health(Model);
        }
    }

    private void Handle_Drag_Release_Outside_Board()
    {
        if (Dragged_Model && Click_Action != null && !Click_Action.IsPressed())
        {
            Snap_Model_Back_To_Position(Dragged_Model, new Vector2Int(Dragged_Model.Current_X, Dragged_Model.Current_Y));
            Reset_Drag_State();
        }
    }

    private void Reset_Drag_State()
    {
        Dragged_Model = null;
        Is_Holding = false;
        Current_Hold_Time = 0f;
    }

    private float Calculate_Drag_Offset(Vector3 Mouse_Position)
    {
        foreach (Model_Standard_Behavior Model in Models)
        {
            if (Model == null || Model == Dragged_Model)
                continue;

            float Distance = Vector3.Distance(Mouse_Position, Model.transform.position);
            if (Distance < Drag_Detect_Radius)
                return Altered_Drag_Offset;
        }
        return Normal_Drag_Offset;
    }


    // ============================================================
    // MOVEMENT LOGIC
    // ============================================================

    private bool Try_Move_Model(Model_Standard_Behavior Model, int X, int Y)
    {
        Vector2Int Previous_Position = new Vector2Int(Model.Current_X, Model.Current_Y);
        Vector2Int Target_Position = new Vector2Int(X, Y);

        // Enemy tile: intercept as a shove.
        Model_Standard_Behavior Target_Occupant = Models[X, Y];
        if (Target_Occupant != null && Target_Occupant.Team != Model.Team)
        {
            if (!Model.Is_Sprinting_This_Turn || Model.Stats == null || !Model.Stats.Can_Shove || Model.Stats.Shove_Distance <= 0)
            {
                if (Debug_Log_Movement)
                    Debug.Log("Cannot move onto enemy tile (shove not available).");
                return false;
            }

            if (!Shove_Target_Highlights.Contains(Target_Position))
            {
                if (Debug_Log_Shove)
                    Debug.Log($"({X}, {Y}) is an enemy but not a valid shove target.");
                return false;
            }

            return Try_Shove_Model(Model, Target_Occupant, Previous_Position, Target_Position);
        }

        if (Model.Has_Ended_Turn)
        {
            if (Debug_Log_Movement)
                Debug.Log($"{Model.Type} has ended its turn.");
            return false;
        }

        if (Model.Movement_Remaining_This_Turn <= 0)
        {
            if (Debug_Log_Movement)
                Debug.Log($"{Model.Type} has no movement remaining.");
            return false;
        }

        if (Models[X, Y] != null)
        {
            if (Debug_Log_Movement)
                Debug.Log(Models[X, Y].Team != Model.Team ? "Cannot move onto enemy tile!" : "Tile occupied by friendly model!");
            return false;
        }

        if (!Movement_Range_Highlights.Contains(Target_Position))
        {
            if (Debug_Log_Movement)
                Debug.Log($"Cannot move to ({X}, {Y}) - not in valid movement range!");
            return false;
        }

        int Move_Cost = Get_Selected_Move_Cost(Previous_Position, Target_Position);

        if (Move_Cost > Model.Movement_Remaining_This_Turn)
        {
            if (Debug_Log_Movement)
                Debug.Log($"{Model.Type} doesn't have enough movement ({Model.Movement_Remaining_This_Turn} left, needs {Move_Cost}).");
            return false;
        }

        Model.Movement_Remaining_This_Turn -= Move_Cost;

        // Deliberately not setting Has_Ended_Turn: a model with no movement
        // left can still attack or sprint.

        Models[X, Y] = Model;
        Models[Previous_Position.x, Previous_Position.y] = null;
        Smooth_Move_To_Position(X, Y);
        return true;
    }

    /// <summary>
    /// Converts the rest of this model's turn into a sprint. Adds the sprint
    /// bonus to remaining movement, capped at the sprint range. The model
    /// cannot attack after sprinting. No-op if already sprinting.
    /// </summary>
    public bool Begin_Sprint()
    {
        Model_Standard_Behavior Selected = Get_Selected_Model();

        if (Selected == null) return false;
        if (Selected.Team != Active_Player) return false;
        if (Selected.Has_Ended_Turn) return false;
        if (Selected.Stats == null) return false;
        if (Selected.Is_Sprinting_This_Turn) return false;
        if (Selected.Stats.Sprint_Bonus <= 0) return false;

        Selected.Is_Sprinting_This_Turn = true;

        int Cap = Selected.Stats.Get_Sprint_Range();
        Selected.Movement_Remaining_This_Turn = Mathf.Min(
            Selected.Movement_Remaining_This_Turn + Selected.Stats.Sprint_Bonus,
            Cap);

        Refresh_Movement_Range_Highlights(Selected);

        if (Debug_Log_Movement)
            Debug.Log($"{Selected.Stats.Model_Name} is sprinting. Remaining movement: {Selected.Movement_Remaining_This_Turn}.");

        return true;
    }


    // ============================================================
    // SHOVE LOGIC
    // ============================================================

    /// <summary>
    /// The sprinter moves to the tile adjacent to the defender in the
    /// direction of approach. The defender is pushed Shove_Distance tiles
    /// in the same direction, or takes 1 damage if the push is blocked.
    /// Either way, the sprinter ends its turn.
    /// </summary>
    private bool Try_Shove_Model(
        Model_Standard_Behavior Sprinter,
        Model_Standard_Behavior Defender,
        Vector2Int Sprinter_Start,
        Vector2Int Defender_Position)
    {
        Vector2Int Direction;
        Vector2Int Stop_Position;

        // Prefer the direction implied by the selected path (StopTile = Path[^2]).
        if (All_Paths_To_Target.Count > 0)
        {
            int Index = Mathf.Clamp(Current_Path_Index, 0, All_Paths_To_Target.Count - 1);
            List<Vector2Int> Path = All_Paths_To_Target[Index];

            if (Path.Count >= 2 && Path[Path.Count - 1] == Defender_Position)
            {
                Stop_Position = Path[Path.Count - 2];
                Direction = Defender_Position - Stop_Position;
            }
            else
            {
                Direction = Get_Cardinal_Direction(Defender_Position - Sprinter_Start);
                Stop_Position = Defender_Position - Direction;
            }
        }
        else
        {
            Direction = Get_Cardinal_Direction(Defender_Position - Sprinter_Start);
            Stop_Position = Defender_Position - Direction;
        }

        if (Direction == Vector2Int.zero)
        {
            if (Debug_Log_Shove)
                Debug.Log("Shove failed: no cardinal direction.");
            return false;
        }

        int Move_Cost = Mathf.Abs(Stop_Position.x - Sprinter_Start.x)
                      + Mathf.Abs(Stop_Position.y - Sprinter_Start.y);

        if (Move_Cost > Sprinter.Movement_Remaining_This_Turn)
        {
            if (Debug_Log_Shove)
                Debug.Log($"Shove failed: needs {Move_Cost} movement, has {Sprinter.Movement_Remaining_This_Turn}.");
            return false;
        }

        // Stop tile must be free (or the sprinter's own tile). Without this
        // guard we can silently overwrite another unit in the Models array.
        if (Stop_Position != Sprinter_Start && Models[Stop_Position.x, Stop_Position.y] != null)
        {
            if (Debug_Log_Shove)
                Debug.Log($"Shove failed: stop tile ({Stop_Position.x}, {Stop_Position.y}) is occupied by {Models[Stop_Position.x, Stop_Position.y].Stats.Model_Name}.");
            return false;
        }

        int Shove_Distance = Sprinter.Stats.Shove_Distance;
        List<Vector2Int> Push_Path = new List<Vector2Int>();
        bool Blocked = false;

        for (int Step = 1; Step <= Shove_Distance; Step++)
        {
            Vector2Int Dest = Defender_Position + Direction * Step;

            if (!Is_Valid_Shove_Destination(Dest))
            {
                Blocked = true;
                break;
            }

            Push_Path.Add(Dest);
        }

        // Re-verify the final tile before committing. Is_Valid_Shove_Destination
        // checked it, but state could have changed between preview and drop.
        if (!Blocked)
        {
            Vector2Int Final_Tile = Push_Path[Push_Path.Count - 1];
            if (Models[Final_Tile.x, Final_Tile.y] != null)
            {
                if (Debug_Log_Shove)
                    Debug.Log($"Shove aborted: destination ({Final_Tile.x}, {Final_Tile.y}) became occupied.");
                return false;
            }
        }

        if (Stop_Position != Sprinter_Start)
        {
            Models[Sprinter_Start.x, Sprinter_Start.y] = null;
            Models[Stop_Position.x, Stop_Position.y] = Sprinter;
            Sprinter.Current_X = Stop_Position.x;
            Sprinter.Current_Y = Stop_Position.y;
            Sprinter.Set_Position(Get_Tile_Center(Stop_Position.x, Stop_Position.y), false);
        }

        Sprinter.Movement_Remaining_This_Turn = 0;
        Sprinter.Has_Attacked_This_Turn = true;
        Sprinter.Has_Ended_Turn = true;

        if (Blocked)
        {
            if (Debug_Log_Shove)
                Debug.Log($"Shove blocked! {Defender.Stats.Model_Name} takes 1 damage.");

            bool Survived = Defender.Take_Damage(1);
            if (!Survived)
            {
                if (Combat_Manager_Ref != null)
                    Combat_Manager_Ref.Kill_Model(Defender);
            }
            else if (Card_UI != null)
            {
                Card_UI.Refresh_Displayed_Health(Defender);
            }
        }
        else
        {
            Vector2Int Final = Push_Path[Push_Path.Count - 1];

            Models[Defender_Position.x, Defender_Position.y] = null;
            Models[Final.x, Final.y] = Defender;
            Defender.Current_X = Final.x;
            Defender.Current_Y = Final.y;
            Defender.Set_Position(Get_Tile_Center(Final.x, Final.y), false);

            if (Debug_Log_Shove)
                Debug.Log($"{Sprinter.Stats.Model_Name} shoves {Defender.Stats.Model_Name} from {Defender_Position} to {Final}.");
        }

        return true;
    }

    private void Show_Shove_Paths(Vector2Int Enemy_Position)
    {
        if (Dragged_Model == null)
            return;

        if (Enemy_Position == Current_Hover_Target)
        {
            Update_Shove_Preview_From_Path(Enemy_Position);
            return;
        }

        if (Debug_Log_Pathfinding)
            Debug.Log($"[Shove] Hover changed: {Current_Hover_Target} -> {Enemy_Position}. Rebuilding.");

        Clear_Path_Highlights();
        Clear_Shove_Destination_Highlights();

        Current_Hover_Target = Enemy_Position;
        Current_Path_Index = 0;
        Shove_Path_Rebuild_Count++;

        Vector2Int Start = new Vector2Int(Dragged_Model.Current_X, Dragged_Model.Current_Y);
        int Max_Distance = Dragged_Model.Movement_Remaining_This_Turn;

        All_Paths_To_Target = Find_Shove_Paths(Start, Enemy_Position, Max_Distance);

        if (Debug_Log_Pathfinding)
        {
            Debug.Log($"[Shove] Rebuild #{Shove_Path_Rebuild_Count}: {All_Paths_To_Target.Count} paths " +
                      $"(Start={Start}, Target={Enemy_Position}, MaxDist={Max_Distance}).");

            for (int i = 0; i < All_Paths_To_Target.Count; i++)
                Debug.Log($"[Shove]   path{i}: {Describe_Path(All_Paths_To_Target[i])}");
        }

        if (All_Paths_To_Target.Count == 0)
        {
            if (Debug_Log_Shove)
                Debug.Log($"No shove path to ({Enemy_Position.x}, {Enemy_Position.y}).");
            return;
        }

        Path_Highlight_Tiles.Clear();
        Draw_All_Paths_With_Layering();
        Update_Shove_Preview_From_Path(Enemy_Position);

        Repaint_Shove_Highlights();

        if (Current_Flash_Material == null)
            Current_Flash_Material = Shove_Destination_Material != null ? Shove_Destination_Material : Movement_Range_Material;

        if (Debug_Log_Shove)
            Debug.Log($"Shove paths to ({Enemy_Position.x}, {Enemy_Position.y}): {All_Paths_To_Target.Count}");
    }

    /// <summary>
    /// Re-applies shove-target and shove-destination materials. Called after
    /// path drawing, since a destination tile is often also the stop tile of
    /// the opposite approach and the path pass would otherwise win the paint.
    /// </summary>
    private void Repaint_Shove_Highlights()
    {
        if (Shove_Target_Material != null)
        {
            foreach (Vector2Int Tile in Shove_Target_Highlights)
            {
                if (Tile.x < 0 || Tile.x >= Tile_Count_X || Tile.y < 0 || Tile.y >= Tile_Count_Y)
                    continue;

                if (Shove_Highlights_Are_Blocked && Shove_Blocked_Material != null)
                    Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Shove_Blocked_Material;
                else
                    Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Shove_Target_Material;
            }
        }

        if (Shove_Destination_Material != null)
        {
            foreach (Vector2Int Tile in Shove_Destination_Highlights)
            {
                if (Tile.x < 0 || Tile.x >= Tile_Count_X || Tile.y < 0 || Tile.y >= Tile_Count_Y)
                    continue;

                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Shove_Destination_Material;
            }
        }
    }

    private List<List<Vector2Int>> Find_Shove_Paths(
        Vector2Int Start,
        Vector2Int Enemy_Position,
        int Max_Distance)
    {
        List<List<Vector2Int>> Candidates = Generate_Shove_Path_Candidates(Start, Enemy_Position, Max_Distance);
        return Curate_Shove_Paths(Candidates, Enemy_Position);
    }

    /// <summary>
    /// One BFS per cardinal approach direction. If alternate routes are
    /// enabled, every shortest route to each stop tile is kept; otherwise
    /// only the single shortest route per direction.
    /// </summary>
    private List<List<Vector2Int>> Generate_Shove_Path_Candidates(
        Vector2Int Start,
        Vector2Int Enemy_Position,
        int Max_Distance)
    {
        var Result = new List<List<Vector2Int>>();

        if (Start == Enemy_Position || Max_Distance < 1)
            return Result;

        foreach (Vector2Int Dir in Cardinal_Directions)
        {
            Vector2Int Stop_Tile = Enemy_Position - Dir;

            if (Stop_Tile.x < 0 || Stop_Tile.x >= Tile_Count_X ||
                Stop_Tile.y < 0 || Stop_Tile.y >= Tile_Count_Y)
                continue;

            if (!Is_Tile_Passable(Stop_Tile.x, Stop_Tile.y))
                continue;

            Model_Standard_Behavior Occupant = Models[Stop_Tile.x, Stop_Tile.y];
            if (Occupant != null && Stop_Tile != Start)
                continue;

            List<List<Vector2Int>> Sub_Paths;

            if (Show_Alternate_Routes_Per_Direction)
            {
                Sub_Paths = Find_All_Paths(Start, Stop_Tile, Max_Distance - 1);
            }
            else
            {
                Sub_Paths = new List<List<Vector2Int>>();
                List<Vector2Int> Single = Find_Shortest_Path(Start, Stop_Tile, Max_Distance - 1);
                if (Single.Count > 0)
                    Sub_Paths.Add(Single);
            }

            foreach (var Sub_Path in Sub_Paths)
            {
                var Full_Path = new List<Vector2Int>(Sub_Path) { Enemy_Position };
                Result.Add(Full_Path);
            }
        }

        return Result;
    }

    /// <summary>
    /// Trims candidates to at most Max_Shove_Paths_Shown. One best route per
    /// direction survives first; leftovers fill empty slots if enabled, so
    /// the player can choose between routes to the same shove direction when
    /// one is safer (fewer hazards) than another.
    /// </summary>
    private List<List<Vector2Int>> Curate_Shove_Paths(
        List<List<Vector2Int>> Candidates,
        Vector2Int Enemy_Position)
    {
        if (Candidates.Count == 0)
            return Candidates;

        // Group by approach direction. Direction = Enemy - StopTile, which is
        // the direction the defender is shoved.
        var By_Direction = new Dictionary<Vector2Int, List<List<Vector2Int>>>();
        foreach (var Path in Candidates)
        {
            if (Path.Count < 2) continue;
            Vector2Int Dir = Path[Path.Count - 1] - Path[Path.Count - 2];
            if (!By_Direction.TryGetValue(Dir, out var List))
            {
                List = new List<List<Vector2Int>>();
                By_Direction[Dir] = List;
            }
            List.Add(Path);
        }

        // Within each direction, sort so the best route is first: shortest,
        // then fewest hazards (if the preference is enabled).
        foreach (var Entry in By_Direction)
        {
            Entry.Value.Sort((A, B) =>
            {
                int Length_Compare = A.Count.CompareTo(B.Count);
                if (Length_Compare != 0) return Length_Compare;

                if (Prefer_Hazard_Free_Routes)
                {
                    int Haz_A = Count_Hazards(A);
                    int Haz_B = Count_Hazards(B);
                    int Haz_Compare = Haz_A.CompareTo(Haz_B);
                    if (Haz_Compare != 0) return Haz_Compare;
                }

                return 0;
            });
        }

        // One representative per direction is the baseline. Additional routes
        // to the same direction are surfaced as extra cycle stops so the player
        // can pick the safer route without leaving the direction.
        var Chosen = new List<List<Vector2Int>>();
        var Remainders = new List<List<Vector2Int>>();

        foreach (var Entry in By_Direction)
        {
            Chosen.Add(Entry.Value[0]);
            for (int i = 1; i < Entry.Value.Count; i++)
                Remainders.Add(Entry.Value[i]);
        }

        if (Remainders.Count > 0 && Chosen.Count < Max_Shove_Paths_Shown)
        {
            Remainders.Sort((A, B) =>
            {
                int Length_Compare = A.Count.CompareTo(B.Count);
                if (Length_Compare != 0) return Length_Compare;

                if (Prefer_Hazard_Free_Routes)
                {
                    int Haz_A = Count_Hazards(A);
                    int Haz_B = Count_Hazards(B);
                    int Haz_Compare = Haz_A.CompareTo(Haz_B);
                    if (Haz_Compare != 0) return Haz_Compare;
                }

                return 0;
            });

            foreach (var Extra in Remainders)
            {
                if (Chosen.Count >= Max_Shove_Paths_Shown) break;
                Chosen.Add(Extra);
            }
        }

        Chosen.Sort((A, B) => A.Count.CompareTo(B.Count));

        if (Chosen.Count > Max_Shove_Paths_Shown)
            Chosen.RemoveRange(Max_Shove_Paths_Shown, Chosen.Count - Max_Shove_Paths_Shown);

        return Chosen;
    }

    private int Count_Hazards(List<Vector2Int> Path)
    {
        int Count = 0;
        foreach (Vector2Int Tile in Path)
            if (Get_Tile_Type_At(Tile.x, Tile.y) == Board_Modifiers.Hazard)
                Count++;
        return Count;
    }

    /// <summary>
    /// The shove direction is Defender - StopTile, where StopTile is the
    /// second-to-last tile of the selected path.
    /// </summary>
    private void Update_Shove_Preview_From_Path(Vector2Int Enemy_Position)
    {
        Clear_Shove_Destination_Highlights();

        if (Dragged_Model == null || Dragged_Model.Stats == null)
            return;

        if (All_Paths_To_Target.Count == 0)
            return;

        int Index = Mathf.Clamp(Current_Path_Index, 0, All_Paths_To_Target.Count - 1);
        List<Vector2Int> Path = All_Paths_To_Target[Index];

        if (Path.Count < 2)
            return;

        Vector2Int Stop_Tile = Path[Path.Count - 2];
        Vector2Int Direction = Enemy_Position - Stop_Tile;

        if (Direction == Vector2Int.zero)
            return;

        int Shove_Distance = Dragged_Model.Stats.Shove_Distance;
        bool All_Blocked = false;

        for (int Step = 1; Step <= Shove_Distance; Step++)
        {
            Vector2Int Dest = Enemy_Position + Direction * Step;

            if (!Is_Valid_Shove_Destination(Dest))
            {
                All_Blocked = true;
                break;
            }

            Shove_Destination_Highlights.Add(Dest);
            if (Shove_Destination_Material != null)
                Tiles[Dest.x, Dest.y].GetComponent<MeshRenderer>().material = Shove_Destination_Material;
        }

        Shove_Highlights_Are_Blocked = All_Blocked;
        if (All_Blocked && Shove_Blocked_Material != null)
            Tiles[Enemy_Position.x, Enemy_Position.y].GetComponent<MeshRenderer>().material = Shove_Blocked_Material;
    }

    private void Clear_Shove_Destination_Highlights()
    {
        foreach (Vector2Int Tile in Shove_Destination_Highlights)
        {
            if (Tile.x < 0 || Tile.x >= Tile_Count_X || Tile.y < 0 || Tile.y >= Tile_Count_Y)
                continue;

            // Priority order: Path > Shove target > Movement range > base.
            if (Shove_Target_Highlights.Contains(Tile))
                continue;

            if (Path_Highlight_Tiles.Contains(Tile))
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Get_Path_Material_For_Tile(Tile);
            else if (Movement_Range_Highlights.Contains(Tile) && Movement_Range_Material != null)
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Movement_Range_Material;
            else
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Tile_Material;

            Reset_Tile_Alpha(Tile);
        }

        Shove_Destination_Highlights.Clear();

        if (Shove_Highlights_Are_Blocked)
        {
            foreach (Vector2Int Tile in Shove_Target_Highlights)
            {
                if (Tile.x < 0 || Tile.x >= Tile_Count_X || Tile.y < 0 || Tile.y >= Tile_Count_Y)
                    continue;

                if (Shove_Target_Material != null)
                    Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Shove_Target_Material;
            }
            Shove_Highlights_Are_Blocked = false;
        }
    }

    private void Clear_Shove_Target_Highlights()
    {
        foreach (Vector2Int Tile in Shove_Target_Highlights)
        {
            if (Tile.x < 0 || Tile.x >= Tile_Count_X || Tile.y < 0 || Tile.y >= Tile_Count_Y)
                continue;

            Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Tile_Material;
            Reset_Tile_Alpha(Tile);
        }

        Shove_Target_Highlights.Clear();
        Clear_Shove_Destination_Highlights();
        Shove_Highlights_Are_Blocked = false;
    }

    private static Vector2Int Get_Cardinal_Direction(Vector2Int Delta)
    {
        if (Delta == Vector2Int.zero)
            return Vector2Int.zero;

        if (Mathf.Abs(Delta.x) >= Mathf.Abs(Delta.y))
            return Delta.x > 0 ? new Vector2Int(1, 0) : new Vector2Int(-1, 0);
        else
            return Delta.y > 0 ? new Vector2Int(0, 1) : new Vector2Int(0, -1);
    }

    /// <summary>
    /// Destination must be in bounds, passable, and free of any model.
    /// Hazard tiles are valid.
    /// </summary>
    private bool Is_Valid_Shove_Destination(Vector2Int Pos)
    {
        if (Pos.x < 0 || Pos.x >= Tile_Count_X || Pos.y < 0 || Pos.y >= Tile_Count_Y)
            return false;

        if (!Is_Tile_Passable(Pos.x, Pos.y))
            return false;

        if (Models[Pos.x, Pos.y] != null)
            return false;

        return true;
    }

    /// <summary>
    /// The path material the given tile should currently be wearing, based on
    /// which paths contain it and what priority each has right now. Used by
    /// the shove-destination clear path so it doesn't blow path paint away.
    /// </summary>
    private Material Get_Path_Material_For_Tile(Vector2Int Tile)
    {
        if (All_Paths_To_Target.Count == 0 || Current_Path_Index < 0)
            return Tile_Material;

        int Levels = Mathf.Min(Path_Slot_Count, All_Paths_To_Target.Count);
        int Best_Priority = int.MaxValue;
        Material Best_Material = null;

        for (int i = 0; i < All_Paths_To_Target.Count; i++)
        {
            if (!All_Paths_To_Target[i].Contains(Tile))
                continue;

            int Priority = (i - Current_Path_Index + All_Paths_To_Target.Count) % All_Paths_To_Target.Count;
            if (Priority >= Levels)
                Priority = Levels - 1;

            if (Priority >= Best_Priority)
                continue;

            Best_Priority = Priority;
            Best_Material = Get_Path_Material(Priority);
        }

        if (Best_Material == null)
            return Tile_Material;

        if (Best_Priority == 0 && Tile_Is_Hazard(Tile) && Hazard_Path_Material != null)
            return Hazard_Path_Material;

        return Best_Material;
    }


    // ============================================================
    // PATHFINDING & PATH HIGHLIGHTS
    // ============================================================

    private void Show_Paths_To_Target(Vector2Int Target)
    {
        if (Target == Current_Hover_Target || Dragged_Model == null)
            return;

        foreach (Vector2Int Tile in Path_Highlight_Tiles)
        {
            if (Movement_Range_Highlights.Contains(Tile) && Movement_Range_Material != null)
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Movement_Range_Material;
            else
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Tile_Material;

            Reset_Tile_Alpha(Tile);
        }

        Path_Highlight_Tiles.Clear();
        All_Paths_To_Target.Clear();
        Current_Path_Index = 0;
        Current_Hover_Target = Target;

        Vector2Int Start = new Vector2Int(Dragged_Model.Current_X, Dragged_Model.Current_Y);

        // Use remaining movement, not base stat, so sprint pathfinding works.
        int Max_Distance = Dragged_Model.Movement_Remaining_This_Turn;

        All_Paths_To_Target = Find_All_Paths(Start, Target, Max_Distance);

        if (All_Paths_To_Target.Count == 0)
        {
            if (Debug_Log_Pathfinding)
                Debug.Log("[Move] No valid path found.");
            return;
        }

        Draw_All_Paths_With_Layering();

        if (Current_Flash_Material == null)
            Current_Flash_Material = Get_Path_Material(0) != null ? Get_Path_Material(0) : Movement_Range_Material;

        if (Debug_Log_Pathfinding)
        {
            for (int i = 0; i < All_Paths_To_Target.Count; i++)
                Debug.Log($"[Move] path{i}: {Analyze_Path(All_Paths_To_Target[i])}");
        }
    }

    private void Switch_To_Next_Path()
    {
        if (All_Paths_To_Target.Count <= 1 || Current_Hover_Target == No_Tile)
            return;

        Current_Path_Index = (Current_Path_Index + 1) % All_Paths_To_Target.Count;

        Path_Highlight_Tiles.Clear();
        Draw_All_Paths_With_Layering();

        if (Shove_Target_Highlights.Contains(Current_Hover_Target))
        {
            Update_Shove_Preview_From_Path(Current_Hover_Target);
            Repaint_Shove_Highlights();
        }

        if (Debug_Log_Pathfinding)
            Debug.Log($"[Cycle] -> {Analyze_Path(All_Paths_To_Target[Current_Path_Index])} | {Describe_Path(All_Paths_To_Target[Current_Path_Index])}");
    }

    private void Draw_All_Paths_With_Layering()
    {
        if (All_Paths_To_Target.Count == 0)
            return;

        Path_Highlight_Tiles.Clear();

        int Levels = Mathf.Min(Path_Slot_Count, All_Paths_To_Target.Count);

        int[] Priority_Order = new int[All_Paths_To_Target.Count];
        for (int i = 0; i < All_Paths_To_Target.Count; i++)
        {
            int Order = (i - Current_Path_Index + All_Paths_To_Target.Count) % All_Paths_To_Target.Count;
            if (Order >= Levels)
                Order = Levels - 1;
            Priority_Order[i] = Order;
        }

        // Paint lowest priority first, highest last, so the primary wins ties.
        for (int Priority_Level = Levels - 1; Priority_Level >= 0; Priority_Level--)
        {
            for (int i = 0; i < All_Paths_To_Target.Count; i++)
            {
                if (Priority_Order[i] == Priority_Level)
                    Show_Path_Highlights(All_Paths_To_Target[i], Priority_Order[i]);
            }
        }

        if (Debug_Log_Pathfinding)
            Log_Path_Paint_Summary(Priority_Order, Levels);
    }

    private void Show_Path_Highlights(List<Vector2Int> Path, int Priority)
    {
        bool Is_Primary = (Priority == 0);
        Material Regular_Path_Material = Get_Path_Material(Priority);

        foreach (Vector2Int Tile in Path)
        {
            if (!Path_Highlight_Tiles.Contains(Tile))
                Path_Highlight_Tiles.Add(Tile);

            if (Is_Primary && Tile_Is_Hazard(Tile) && Hazard_Path_Material != null)
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Hazard_Path_Material;
            else if (Regular_Path_Material != null)
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Regular_Path_Material;
        }
    }

    /// <summary>
    /// Compact per-cycle summary. One header line, then one line per tile
    /// that belongs to more than one priority level. This replaces the
    /// per-tile paint spam and is the log you actually want when the
    /// primary path looks inconsistent across cycles.
    /// </summary>
    private void Log_Path_Paint_Summary(int[] Priority_Order, int Levels)
    {
        var Tiles_By_Priority = new Dictionary<int, List<Vector2Int>>();
        for (int p = 0; p < Levels; p++)
            Tiles_By_Priority[p] = new List<Vector2Int>();

        for (int i = 0; i < All_Paths_To_Target.Count; i++)
        {
            int P = Priority_Order[i];
            foreach (Vector2Int Tile in All_Paths_To_Target[i])
            {
                if (!Tiles_By_Priority[P].Contains(Tile))
                    Tiles_By_Priority[P].Add(Tile);
            }
        }

        string Header = $"[Cycle] idx={Current_Path_Index} (path {Current_Path_Index + 1}/{All_Paths_To_Target.Count}) | ";
        for (int p = 0; p < Levels; p++)
            Header += $"P{p}={Tiles_By_Priority[p].Count} ";
        Debug.Log(Header);

        // For each tile that appears on more than one priority level, print
        // the priority levels it belongs to and which one wins the paint.
        // Any tile listed here whose final priority is not the smallest in
        // its membership list is a paint-order bug.
        var Ownership = new Dictionary<Vector2Int, List<int>>();
        for (int p = 0; p < Levels; p++)
        {
            foreach (Vector2Int Tile in Tiles_By_Priority[p])
            {
                if (!Ownership.TryGetValue(Tile, out var List))
                {
                    List = new List<int>();
                    Ownership[Tile] = List;
                }
                List.Add(p);
            }
        }

        foreach (var Entry in Ownership)
        {
            if (Entry.Value.Count <= 1) continue;

            Entry.Value.Sort();
            string Owners = string.Join(",", Entry.Value);
            Debug.Log($"[Cycle]   tile {Entry.Key} on P[{Owners}] -> final P{Entry.Value[0]}");
        }
    }

    /// <summary>
    /// Priorities 0..N-1 map to Path_Materials[0..N-1], where N is the smaller
    /// of Path_Slot_Count and Path_Materials.Length. Priorities beyond the
    /// last assigned material fall back to that material, so a tile never
    /// ends up unpainted.
    /// </summary>
    private Material Get_Path_Material(int Priority)
    {
        int Slot_Count = Mathf.Min(Path_Slot_Count, Path_Materials.Length);
        if (Slot_Count <= 0)
        {
            if (Debug_Log_Path_Materials)
                Debug.LogWarning($"[PathMat] Get_Path_Material({Priority}): Slot_Count=0.");
            return null;
        }

        if (Priority < 0) Priority = 0;
        if (Priority >= Slot_Count) Priority = Slot_Count - 1;

        Material Mat = Path_Materials[Priority];
        if (Mat != null)
        {
            if (Debug_Log_Path_Materials)
                Debug.Log($"[PathMat] Get_Path_Material({Priority}) -> '{Mat.name}'");
            return Mat;
        }

        // Fall back to the nearest lower slot that has a material assigned.
        for (int i = Priority - 1; i >= 0; i--)
        {
            if (Path_Materials[i] != null)
            {
                if (Debug_Log_Path_Materials)
                    Debug.LogWarning($"[PathMat] slot {Priority} NULL, fell back to slot {i} ('{Path_Materials[i].name}')");
                return Path_Materials[i];
            }
        }

        if (Debug_Log_Path_Materials)
            Debug.LogWarning($"[PathMat] Get_Path_Material({Priority}) -> NULL.");

        return null;
    }

    /// <summary>
    /// Multi-parent BFS returning every shortest path from Start to Target
    /// within Max_Distance. Only equal-distance parents are recorded, so the
    /// parent graph is a DAG and Reconstruct_Paths cannot cycle.
    /// </summary>
    private List<List<Vector2Int>> Find_All_Paths(Vector2Int Start, Vector2Int Target, int Max_Distance)
    {
        List<List<Vector2Int>> All_Paths = new List<List<Vector2Int>>();

        if (Start == Target)
            return All_Paths;

        int Moving_Team = Dragged_Model != null ? Dragged_Model.Team : 0;

        Queue<Vector2Int> Queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> Distance = new Dictionary<Vector2Int, int>();
        Dictionary<Vector2Int, List<Vector2Int>> Parents = new Dictionary<Vector2Int, List<Vector2Int>>();

        Queue.Enqueue(Start);
        Distance[Start] = 0;
        Parents[Start] = new List<Vector2Int>();

        int Shortest_Path_Length = int.MaxValue;

        while (Queue.Count > 0)
        {
            Vector2Int Current = Queue.Dequeue();
            int Current_Dist = Distance[Current];

            if (Current_Dist >= Shortest_Path_Length) continue;
            if (Current_Dist >= Max_Distance) continue;

            foreach (Vector2Int Dir in Cardinal_Directions)
            {
                Vector2Int Neighbor = Current + Dir;

                if (Neighbor.x < 0 || Neighbor.x >= Tile_Count_X ||
                    Neighbor.y < 0 || Neighbor.y >= Tile_Count_Y)
                    continue;

                if (!Is_Tile_Passable(Neighbor.x, Neighbor.y))
                    continue;

                bool Is_Target = (Neighbor == Target);

                Model_Standard_Behavior Occupant = Models[Neighbor.x, Neighbor.y];
                if (Occupant != null)
                {
                    if (Occupant.Team == Moving_Team)
                    {
                        // Ally pass-through.
                    }
                    else
                    {
                        if (!Is_Target) continue;
                    }
                }

                int New_Dist = Current_Dist + 1;

                if (!Distance.ContainsKey(Neighbor))
                {
                    Distance[Neighbor] = New_Dist;
                    Parents[Neighbor] = new List<Vector2Int> { Current };
                    Queue.Enqueue(Neighbor);

                    if (Is_Target && New_Dist < Shortest_Path_Length)
                        Shortest_Path_Length = New_Dist;
                }
                else if (Distance[Neighbor] == New_Dist)
                {
                    Parents[Neighbor].Add(Current);
                }
            }
        }

        if (Parents.ContainsKey(Target))
            Reconstruct_Paths(Target, Parents, new List<Vector2Int>(), All_Paths);

        return All_Paths;
    }

    private void Reconstruct_Paths(Vector2Int Current, Dictionary<Vector2Int, List<Vector2Int>> Parents, List<Vector2Int> Current_Path, List<List<Vector2Int>> All_Paths)
    {
        if (Parents[Current].Count > 0)
            Current_Path.Insert(0, Current);

        if (Parents[Current].Count == 0)
        {
            Current_Path.Insert(0, Current);
            All_Paths.Add(new List<Vector2Int>(Current_Path));
            return;
        }

        foreach (Vector2Int Parent in Parents[Current])
            Reconstruct_Paths(Parent, Parents, new List<Vector2Int>(Current_Path), All_Paths);
    }

    /// <summary>
    /// Single-path BFS. Allies pass through; enemies block except the target
    /// tile itself. Returned list includes both Start and Target, or is empty.
    /// </summary>
    private List<Vector2Int> Find_Shortest_Path(
        Vector2Int Start,
        Vector2Int Target,
        int Max_Distance)
    {
        if (Start == Target)
            return new List<Vector2Int> { Start };

        if (Max_Distance < 1)
            return new List<Vector2Int>();

        int Moving_Team = Dragged_Model != null ? Dragged_Model.Team : 0;

        var Came_From = new Dictionary<Vector2Int, Vector2Int>();
        var Distance = new Dictionary<Vector2Int, int> { [Start] = 0 };
        var Queue = new Queue<Vector2Int>();
        Queue.Enqueue(Start);

        while (Queue.Count > 0)
        {
            Vector2Int Current = Queue.Dequeue();
            int Current_Dist = Distance[Current];

            if (Current_Dist >= Max_Distance)
                continue;

            foreach (Vector2Int Dir in Cardinal_Directions)
            {
                Vector2Int Neighbor = Current + Dir;

                if (Neighbor.x < 0 || Neighbor.x >= Tile_Count_X ||
                    Neighbor.y < 0 || Neighbor.y >= Tile_Count_Y)
                    continue;

                if (!Is_Tile_Passable(Neighbor.x, Neighbor.y))
                    continue;

                bool Is_Target = (Neighbor == Target);

                Model_Standard_Behavior Occupant = Models[Neighbor.x, Neighbor.y];
                if (Occupant != null && Occupant.Team != Moving_Team && !Is_Target)
                    continue;

                if (Distance.ContainsKey(Neighbor))
                    continue;

                Distance[Neighbor] = Current_Dist + 1;
                Came_From[Neighbor] = Current;
                Queue.Enqueue(Neighbor);

                if (Is_Target)
                    return Reconstruct_Single_Path(Came_From, Start, Target);
            }
        }

        return new List<Vector2Int>();
    }

    private static List<Vector2Int> Reconstruct_Single_Path(
        Dictionary<Vector2Int, Vector2Int> Came_From,
        Vector2Int Start,
        Vector2Int Target)
    {
        var Path = new List<Vector2Int> { Target };
        Vector2Int Current = Target;

        while (Current != Start)
        {
            Current = Came_From[Current];
            Path.Add(Current);
        }

        Path.Reverse();
        return Path;
    }

    private string Analyze_Path(List<Vector2Int> Path)
    {
        int Hazard_Count = 0;
        int Terrain_Count = 0;

        foreach (Vector2Int Tile in Path)
        {
            Board_Modifiers Modifier = Get_Tile_Type_At(Tile.x, Tile.y);
            if (Modifier == Board_Modifiers.Hazard) Hazard_Count++;
            else if (Modifier == Board_Modifiers.Terrain) Terrain_Count++;
        }

        string Message = $"len={Path.Count}";
        if (Hazard_Count > 0) Message += $", Haz={Hazard_Count}";
        if (Terrain_Count > 0) Message += $", Ter={Terrain_Count}";

        return Message;
    }

    /// <summary>
    /// Full tile-by-tile description of a path, for diagnostic logging.
    /// Unlike Analyze_Path, this prints the actual coordinates so you can
    /// diff two rebuilds of the same target and see which tiles changed.
    /// </summary>
    private string Describe_Path(List<Vector2Int> Path)
    {
        if (Path == null || Path.Count == 0)
            return "(empty)";

        string Result = "[";
        for (int i = 0; i < Path.Count; i++)
        {
            Result += $"({Path[i].x},{Path[i].y})";
            if (i < Path.Count - 1) Result += " -> ";
        }
        Result += "]";
        return Result;
    }

    private bool Tile_Is_Hazard(Vector2Int Tile)
    {
        return Get_Tile_Type_At(Tile.x, Tile.y) == Board_Modifiers.Hazard;
    }


    // ============================================================
    // TILE HOVER
    // ============================================================

    private void Update_Tile_Hover(Vector2Int Hit_Position)
    {
        if (Current_Mouse_Hover == No_Tile)
        {
            Current_Mouse_Hover = Hit_Position;
            Tiles[Hit_Position.x, Hit_Position.y].layer = Hover_Layer;
        }

        if (Current_Mouse_Hover != Hit_Position)
        {
            Tiles[Current_Mouse_Hover.x, Current_Mouse_Hover.y].layer = Tile_Layer;
            Current_Mouse_Hover = Hit_Position;
            Tiles[Current_Mouse_Hover.x, Current_Mouse_Hover.y].layer = Hover_Layer;
        }
    }

    private void Reset_Tile_Hover()
    {
        if (Current_Mouse_Hover != No_Tile)
        {
            Tiles[Current_Mouse_Hover.x, Current_Mouse_Hover.y].layer = Tile_Layer;
            Current_Mouse_Hover = No_Tile;
        }
    }


    // ============================================================
    // HIGHLIGHT MANAGEMENT
    // ============================================================

    private void Update_Flashing_Effect()
    {
        if (Current_Flash_Material == null)
            return;

        float Alpha = Mathf.Lerp(Flash_Min_Alpha, Flash_Max_Alpha,
            (Mathf.Sin(Time.time * Flash_Speed) + 1.0f) * 0.5f);

        if (Current_Phase == Game_State.Player_1_Place_Spawn || Current_Phase == Game_State.Player_2_Place_Spawn)
            Apply_Alpha_To_Highlight_Group(Spawn_Tile_Highlights, Alpha);
        else if (Current_Phase == Game_State.Player_1_Place_Models || Current_Phase == Game_State.Player_2_Place_Models)
            Apply_Alpha_To_Highlight_Group(Model_Placement_Highlights, Alpha);
        else if (Current_Phase == Game_State.Gameplay)
        {
            Apply_Alpha_To_Highlight_Group(Movement_Range_Highlights, Alpha);
            Apply_Alpha_To_Highlight_Group(Shove_Target_Highlights, Alpha);
            Apply_Alpha_To_Highlight_Group(Shove_Destination_Highlights, Alpha);
            Apply_Alpha_To_Path_Tiles(Alpha);
        }
    }

    private void Apply_Alpha_To_Highlight_Group(List<Vector2Int> Highlight_Group, float Alpha)
    {
        foreach (Vector2Int Tile in Highlight_Group)
        {
            Renderer Renderer = Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>();
            if (Renderer != null && Renderer.material != null)
            {
                Color Color = Renderer.material.color;
                Color.a = Alpha;
                Renderer.material.color = Color;
            }
        }
    }

    /// <summary>
    /// Path tiles can carry one of several priority materials, so we flash
    /// each tile's own material. Tiles owned by a higher-priority group are
    /// skipped so exactly one pass touches each tile.
    /// </summary>
    private void Apply_Alpha_To_Path_Tiles(float Alpha)
    {
        foreach (Vector2Int Tile in Path_Highlight_Tiles)
        {
            if (Tile.x < 0 || Tile.x >= Tile_Count_X || Tile.y < 0 || Tile.y >= Tile_Count_Y)
                continue;

            if (Movement_Range_Highlights.Contains(Tile)) continue;
            if (Shove_Target_Highlights.Contains(Tile)) continue;
            if (Shove_Destination_Highlights.Contains(Tile)) continue;

            Renderer Renderer = Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>();
            if (Renderer == null || Renderer.material == null) continue;

            Color Color = Renderer.material.color;
            Color.a = Alpha;
            Renderer.material.color = Color;
        }
    }

    private void Reset_Tile_Alpha(Vector2Int Tile)
    {
        Renderer Renderer = Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>();
        if (Renderer == null || Renderer.material == null) return;

        Color C = Renderer.material.color;
        C.a = 1f;
        Renderer.material.color = C;
    }

    private void Display_Movement_Range(Model_Standard_Behavior Model, int? Range_Override = null)
    {
        Clear_Movement_Range_Highlights();

        if (Model.Stats == null)
            return;

        if (Model.Has_Ended_Turn)
            return;

        int Movement_Range = Range_Override ?? Model.Movement_Remaining_This_Turn;

        if (Movement_Range <= 0)
            return;

        int Start_X = Model.Current_X;
        int Start_Y = Model.Current_Y;

        bool[,] Visited = new bool[Tile_Count_X, Tile_Count_Y];
        Queue<Vector2Int> To_Explore = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> Distance_Map = new Dictionary<Vector2Int, int>();

        Vector2Int Start = new Vector2Int(Start_X, Start_Y);
        To_Explore.Enqueue(Start);
        Visited[Start_X, Start_Y] = true;
        Distance_Map[Start] = 0;

        bool Can_Shove = Model.Is_Sprinting_This_Turn
                         && Model.Stats.Can_Shove
                         && Model.Stats.Shove_Distance > 0;

        while (To_Explore.Count > 0)
        {
            Vector2Int Current = To_Explore.Dequeue();
            int Current_Distance = Distance_Map[Current];

            if (Current_Distance >= Movement_Range)
                continue;

            foreach (Vector2Int Direction in Cardinal_Directions)
            {
                Vector2Int Neighbor = Current + Direction;

                if (Neighbor.x < 0 || Neighbor.x >= Tile_Count_X ||
                    Neighbor.y < 0 || Neighbor.y >= Tile_Count_Y)
                    continue;

                if (Visited[Neighbor.x, Neighbor.y])
                    continue;

                if (!Is_Tile_Passable(Neighbor.x, Neighbor.y))
                    continue;

                Model_Standard_Behavior Occupant = Models[Neighbor.x, Neighbor.y];

                if (Occupant != null)
                {
                    if (Occupant.Team == Model.Team)
                    {
                        // Allies are pass-through but can't be landed on, so
                        // they're not added to Movement_Range_Highlights.
                        Visited[Neighbor.x, Neighbor.y] = true;
                        int Ally_New_Distance = Current_Distance + 1;
                        Distance_Map[Neighbor] = Ally_New_Distance;

                        if (Ally_New_Distance < Movement_Range)
                            To_Explore.Enqueue(Neighbor);

                        continue;
                    }

                    if (!Can_Shove)
                        continue;

                    Visited[Neighbor.x, Neighbor.y] = true;
                    Shove_Target_Highlights.Add(Neighbor);
                    if (Shove_Target_Material != null)
                        Tiles[Neighbor.x, Neighbor.y].GetComponent<MeshRenderer>().material = Shove_Target_Material;

                    // The sprinter stops adjacent; no expansion past this tile.
                    continue;
                }

                Visited[Neighbor.x, Neighbor.y] = true;
                int New_Distance = Current_Distance + 1;
                Distance_Map[Neighbor] = New_Distance;

                if (New_Distance <= Movement_Range)
                {
                    Movement_Range_Highlights.Add(Neighbor);
                    if (Movement_Range_Material != null)
                        Tiles[Neighbor.x, Neighbor.y].GetComponent<MeshRenderer>().material = Movement_Range_Material;
                }

                if (New_Distance < Movement_Range)
                    To_Explore.Enqueue(Neighbor);
            }
        }

        if (Movement_Range_Highlights.Count > 0 || Shove_Target_Highlights.Count > 0)
            Current_Flash_Material = Movement_Range_Material;
    }

    private void Refresh_Movement_Range_Highlights(Model_Standard_Behavior Model)
    {
        Display_Movement_Range(Model);
    }

    private void Clear_Movement_Range_Highlights()
    {
        foreach (Vector2Int Tile in Movement_Range_Highlights)
        {
            Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Tile_Material;
            Reset_Tile_Alpha(Tile);
        }

        Movement_Range_Highlights.Clear();
        Clear_Shove_Target_Highlights();
        Current_Flash_Material = null;
    }

    private void Clear_Path_Highlights()
    {
        foreach (Vector2Int Tile in Path_Highlight_Tiles)
        {
            if (Movement_Range_Highlights.Contains(Tile) && Movement_Range_Material != null)
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Movement_Range_Material;
            else if (Shove_Target_Highlights.Contains(Tile) && Shove_Target_Material != null)
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Shove_Target_Material;
            else if (Shove_Destination_Highlights.Contains(Tile) && Shove_Destination_Material != null)
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Shove_Destination_Material;
            else
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Tile_Material;

            Reset_Tile_Alpha(Tile);
        }

        Path_Highlight_Tiles.Clear();
        All_Paths_To_Target.Clear();
        Current_Path_Index = 0;
        Current_Hover_Target = No_Tile;
        Shove_Path_Rebuild_Count = 0;
    }

    private void Clear_Spawn_Placement_Highlights()
    {
        foreach (Vector2Int Tile in Spawn_Tile_Highlights)
        {
            Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Tile_Material;
            Reset_Tile_Alpha(Tile);
        }

        Spawn_Tile_Highlights.Clear();
    }

    private void Clear_Model_Placement_Highlights()
    {
        foreach (Vector2Int Tile in Model_Placement_Highlights)
        {
            Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Tile_Material;
            Reset_Tile_Alpha(Tile);
        }

        Model_Placement_Highlights.Clear();
    }

    private void Clear_All_Highlights()
    {
        Clear_Spawn_Placement_Highlights();
        Clear_Model_Placement_Highlights();
    }


    // ============================================================
    // SPAWN SYSTEM
    // ============================================================

    private void Handle_Spawn_Placement_Input(Vector2Int Hit_Position, int Player)
    {
        int Min_Row = Get_Spawn_Min_Row(Player);
        int Max_Row = Get_Spawn_Max_Row(Player);

        if (Hit_Position.x < Min_Row || Hit_Position.x > Max_Row)
            return;

        if (Click_Action != null && Click_Action.WasPressedThisFrame())
            Place_Spawn_Tile_At(Hit_Position, Player);
    }

    private void Place_Spawn_Tile_At(Vector2Int Position, int Player)
    {
        Faction_Data_SO Faction = Player == Player_1 ? Player_1_Faction : Player_2_Faction;

        if (Faction == null || Faction.Army_Composition == null)
        {
            Debug.LogError($"Faction or Army Composition missing for Player {Player}!");
            return;
        }

        GameObject Spawn_Tile_Prefab = Faction.Army_Composition.Spawn_Tile_Prefab;

        if (Spawn_Tile_Prefab == null)
        {
            Debug.LogError($"Spawn tile prefab missing for Player {Player}!");
            return;
        }

        GameObject Spawn_Tile = Instantiate(Spawn_Tile_Prefab, Get_Tile_Center(Position.x, Position.y), Quaternion.identity, transform);

        if (Player == Player_1)
        {
            Player_1_Spawn_Tile = Spawn_Tile;
            Player_1_Spawn_Position = Position;
            Clear_Spawn_Placement_Highlights();
            Show_Model_Deployment_Zone(Position, Player);
            Current_Phase = Game_State.Player_1_Place_Models;

            if (Debug_Log_Placement)
                Debug.Log($"Player 1 spawn tile placed at {Position}. Phase: Player 1 place your models");
        }
        else
        {
            Player_2_Spawn_Tile = Spawn_Tile;
            Player_2_Spawn_Position = Position;
            Clear_Spawn_Placement_Highlights();
            Show_Model_Deployment_Zone(Position, Player);
            Current_Phase = Game_State.Player_2_Place_Models;

            if (Debug_Log_Placement)
                Debug.Log($"Player 2 spawn tile placed at {Position}. Phase: Player 2 place your models");
        }
    }

    private void Show_Spawn_Zone_Highlights(int Player)
    {
        Clear_Spawn_Placement_Highlights();
        Current_Flash_Material = Valid_Spawn_Tile_Locations_Material;

        int Min_Row = Get_Spawn_Min_Row(Player);
        int Max_Row = Get_Spawn_Max_Row(Player);

        for (int X = Min_Row; X <= Max_Row; X++)
        {
            for (int Y = 0; Y < Tile_Count_Y; Y++)
            {
                Spawn_Tile_Highlights.Add(new Vector2Int(X, Y));
                if (Valid_Spawn_Tile_Locations_Material != null)
                    Tiles[X, Y].GetComponent<MeshRenderer>().material = Valid_Spawn_Tile_Locations_Material;
            }
        }

        if (Debug_Log_Placement)
            Debug.Log($"Spawn tile zone shown for Player {Player} on rows {Min_Row}-{Max_Row}");
    }

    private void Show_Model_Deployment_Zone(Vector2Int Spawn_Position, int Player)
    {
        Clear_Model_Placement_Highlights();
        Current_Flash_Material = Model_Placement_Material;

        int Min_Row = Get_Spawn_Min_Row(Player);
        int Start_X = Mathf.Max(Min_Row, Spawn_Position.x - (Spawn_Zone_Depth - 1));

        int Half_Width = Spawn_Zone_Width / 2;
        int Start_Y = Spawn_Position.y - Half_Width;

        if (Start_Y < 0) Start_Y = 0;
        if (Start_Y + Spawn_Zone_Width > Tile_Count_Y) Start_Y = Tile_Count_Y - Spawn_Zone_Width;

        for (int X = Start_X; X < Start_X + Spawn_Zone_Depth && X < Tile_Count_X; X++)
        {
            for (int Y = Start_Y; Y < Start_Y + Spawn_Zone_Width && Y < Tile_Count_Y; Y++)
            {
                if (Is_Tile_Passable(X, Y))
                {
                    Model_Placement_Highlights.Add(new Vector2Int(X, Y));
                    if (Model_Placement_Material != null)
                        Tiles[X, Y].GetComponent<MeshRenderer>().material = Model_Placement_Material;
                }
            }
        }

        if (Debug_Log_Placement)
            Debug.Log($"Model placement zone shown for Player {Player} with {Model_Placement_Highlights.Count} tiles");
    }

    private void Handle_Model_Placement_Input(Vector2Int Hit_Position, int Player, Army_Composition_SO Army, Material Team_Mat)
    {
        if (!Model_Placement_Highlights.Contains(Hit_Position))
            return;

        if (Click_Action != null && Click_Action.WasPressedThisFrame())
            Deploy_Model_At(Hit_Position, Player, Army, Team_Mat);
    }

    private void Deploy_Model_At(Vector2Int Hit_Position, int Player, Army_Composition_SO Army, Material Team_Mat)
    {
        if (Models[Hit_Position.x, Hit_Position.y] != null)
        {
            Debug.Log("Tile already occupied!");
            return;
        }

        if (!Is_Tile_Passable(Hit_Position.x, Hit_Position.y))
        {
            Debug.Log("Cannot place model on impassable terrain!");
            return;
        }

        if (Army == null || !Army.Try_Get_Random_Model_Type(out Model_Type Random_Type))
        {
            Debug.Log($"No more models to place for Player {Player}!");
            return;
        }

        Model_Standard_Behavior Model = Create_Model(Random_Type, Team_Mat,
            Player == Player_1 ? Player_1_Faction : Player_2_Faction, Player);

        if (Model != null)
        {
            Models[Hit_Position.x, Hit_Position.y] = Model;
            Snap_Model_To_Position(Hit_Position.x, Hit_Position.y);

            if (Debug_Log_Placement)
                Debug.Log($"Player {Player} placed {Random_Type}. Remaining: {Army.Get_Remaining_Count()} models");

            if (!Army.Has_Models_Left())
                Advance_From_Model_Placement(Player);
        }
    }

    private void Advance_From_Model_Placement(int Player)
    {
        if (Debug_Log_Placement)
            Debug.Log($"Player {Player} has no more models to place!");

        if (Player == Player_1)
        {
            Current_Phase = Game_State.Player_2_Place_Spawn;
            Clear_Model_Placement_Highlights();
            Show_Spawn_Zone_Highlights(Player_2);

            if (Debug_Log_Placement)
                Debug.Log($"Player 1 finished placing models. Phase: Player 2 place your spawn tile (Rows {Get_Spawn_Min_Row(Player_2)}-{Get_Spawn_Max_Row(Player_2)})");
        }
        else
        {
            Current_Phase = Game_State.Gameplay;
            Clear_All_Highlights();

            Active_Player = Player_1;
            if (Card_UI != null)
                Card_UI.Set_Active_Player(Active_Player);
            Reset_Turn_Flags_For_Player(Active_Player);

            if (Debug_Log_Placement)
                Debug.Log("All models placed! Gameplay begins! Active player: Player 1");
        }
    }


    // ============================================================
    // MODEL CREATION & POSITIONING
    // ============================================================

    private Model_Standard_Behavior Create_Model(Model_Type Type, Material Team_Mat, Faction_Data_SO Faction, int Team_Number)
    {
        GameObject Prefab = Faction.Get_Prefab_By_Type(Type);

        if (Prefab == null)
        {
            Debug.LogError($"Prefab for {Type} not found in faction {Faction.Faction_Name}");
            return null;
        }

        Model_Standard_Behavior Model = Instantiate(Prefab, transform).GetComponent<Model_Standard_Behavior>();
        Model.Type = Type;
        Model.Team = Team_Number;
        Model.Stats = Faction.Get_Stats_By_Type(Type);

        if (Model.Stats != null)
            Model.Current_Health = Model.Stats.Health;

        Apply_Team_Material(Model, Team_Mat);
        return Model;
    }

    private void Apply_Team_Material(Model_Standard_Behavior Model, Material Team_Mat)
    {
        Renderer[] Renderers = Model.GetComponentsInChildren<Renderer>();

        foreach (Renderer Renderer in Renderers)
        {
            Material[] Materials = Renderer.materials;

            if (Materials.Length >= 2)
            {
                for (int i = 0; i < Materials.Length; i++)
                {
                    if (Materials[i] != null && Materials[i].name.Contains("Base"))
                    {
                        Materials[i] = new Material(Team_Mat);
                        Renderer.materials = Materials;
                    }
                }
            }
            else if (Materials.Length == 1)
            {
                Renderer.material = Team_Mat;
            }
        }
    }

    private void Snap_Model_To_Position(int X, int Y)
    {
        Models[X, Y].Current_X = X;
        Models[X, Y].Current_Y = Y;
        Models[X, Y].Set_Position(Get_Tile_Center(X, Y), true);
    }

    private void Snap_Model_Back_To_Position(Model_Standard_Behavior Model, Vector2Int Position)
    {
        Model.Set_Position(Get_Tile_Center(Position.x, Position.y));
    }

    private void Smooth_Move_To_Position(int X, int Y)
    {
        Models[X, Y].Current_X = X;
        Models[X, Y].Current_Y = Y;
        Models[X, Y].Set_Position(Get_Tile_Center(X, Y), false);
    }


    // ============================================================
    // PUBLIC GETTERS
    // ============================================================

    public Model_Standard_Behavior Get_Selected_Model() => Selected_Model;

    public Model_Standard_Behavior Get_Model_At(int X, int Y)
    {
        if (X < 0 || X >= Tile_Count_X || Y < 0 || Y >= Tile_Count_Y)
            return null;
        return Models[X, Y];
    }

    public void Remove_Model(int X, int Y)
    {
        if (X >= 0 && X < Tile_Count_X && Y >= 0 && Y < Tile_Count_Y)
            Models[X, Y] = null;
    }

    public GameObject[,] Get_Tiles() => Tiles;
    public Material Get_Tile_Material() => Tile_Material;
    public int Get_Tile_Count_Y() => Tile_Count_Y;
    public int Get_Tile_Count_X() => Tile_Count_X;


    // ============================================================
    // TURN MANAGEMENT
    // ============================================================

    public void Switch_Active_Player()
    {
        Active_Player = Active_Player == Player_1 ? Player_2 : Player_1;

        if (Card_UI != null)
            Card_UI.Set_Active_Player(Active_Player);

        if (Selected_Model != null && Selected_Model.Team != Active_Player)
            Deselect_Current_Model();

        Reset_Turn_Flags_For_Player(Active_Player);

        if (Debug_Log_Movement)
            Debug.Log($"Active player is now Player {Active_Player}");
    }

    private void Reset_Turn_Flags_For_Player(int Player)
    {
        if (Models == null)
            return;

        for (int X = 0; X < Tile_Count_X; X++)
        {
            for (int Y = 0; Y < Tile_Count_Y; Y++)
            {
                Model_Standard_Behavior Model = Models[X, Y];
                if (Model != null && Model.Team == Player)
                {
                    Model.Movement_Remaining_This_Turn = Model.Stats != null ? Model.Stats.Movement_Range : 0;
                    Model.Has_Attacked_This_Turn = false;
                    Model.Has_Ended_Turn = false;
                    Model.Is_Sprinting_This_Turn = false;
                }
            }
        }
    }

    public void End_Turn()
    {
        if (Current_Phase != Game_State.Gameplay)
        {
            Debug.Log("Cannot end turn outside of Gameplay phase.");
            return;
        }

        if (Combat_Manager_Ref != null && Combat_Manager_Ref.Is_Currently_Selecting_Target())
            Combat_Manager_Ref.Cancel_Attack();

        Clear_Movement_Highlights_Public();
        Switch_Active_Player();
    }

    public void Clear_Movement_Highlights_Public()
    {
        Clear_Movement_Range_Highlights();
    }


    // ============================================================
    // BOARD GENERATION
    // ============================================================

    private void Generate_All_Tiles(float Tile_Size)
    {
        float Absolute_Y_Offset = Y_Offset + transform.position.y;

        Bounds = new Vector3(
            (Tile_Count_X / 2.0f) * Tile_Size,
            0,
            (Tile_Count_Y / 2.0f) * Tile_Size
        ) + Board_Center;

        Board_Origin = new Vector3(0, Absolute_Y_Offset, 0) - Bounds;

        Tiles = new GameObject[Tile_Count_X, Tile_Count_Y];
        for (int x = 0; x < Tile_Count_X; x++)
            for (int y = 0; y < Tile_Count_Y; y++)
                Tiles[x, y] = Create_Single_Tile(Tile_Size, x, y, Absolute_Y_Offset);

        Create_Board_Collider(Absolute_Y_Offset);
    }

    private void Create_Board_Collider(float Absolute_Y_Offset)
    {
        GameObject Collider_Object = new GameObject("Board_Collider");
        Collider_Object.transform.parent = transform;
        Collider_Object.layer = Tile_Layer;

        BoxCollider Board_Collider = Collider_Object.AddComponent<BoxCollider>();
        float Width = Tile_Count_X * Tile_Size;
        float Depth = Tile_Count_Y * Tile_Size;

        Board_Collider.center = new Vector3(0, Absolute_Y_Offset, 0) - Bounds + new Vector3(Width / 2f, 0, Depth / 2f);
        Board_Collider.size = new Vector3(Width, Board_Collider_Thickness, Depth);
    }

    private GameObject Create_Single_Tile(float Tile_Size, int x, int y, float Absolute_Y_Offset)
    {
        GameObject Tile_Object = new GameObject($"X:{x}, Y:{y}");
        Tile_Object.transform.parent = transform;

        Mesh Mesh = new Mesh();
        Tile_Object.AddComponent<MeshFilter>().mesh = Mesh;
        Tile_Object.AddComponent<MeshRenderer>().material = Tile_Material;

        Vector3[] Vertices = new Vector3[4];
        Vertices[0] = new Vector3(x * Tile_Size, Absolute_Y_Offset, y * Tile_Size) - Bounds;
        Vertices[1] = new Vector3(x * Tile_Size, Absolute_Y_Offset, (y + 1) * Tile_Size) - Bounds;
        Vertices[2] = new Vector3((x + 1) * Tile_Size, Absolute_Y_Offset, y * Tile_Size) - Bounds;
        Vertices[3] = new Vector3((x + 1) * Tile_Size, Absolute_Y_Offset, (y + 1) * Tile_Size) - Bounds;

        int[] Tris = new int[] { 0, 1, 2, 1, 3, 2 };

        Mesh.vertices = Vertices;
        Mesh.triangles = Tris;
        Mesh.RecalculateNormals();

        Tile_Object.layer = Tile_Layer;
        return Tile_Object;
    }


    // ============================================================
    // MAP / TILE QUERIES
    // ============================================================

    public bool Is_Tile_Passable(int X, int Y)
    {
        if (X < 0 || X >= Tile_Count_X || Y < 0 || Y >= Tile_Count_Y)
            return false;

        Board_Modifiers Type = Map_Tiles[X, Y];
        return Type != Board_Modifiers.Wall && Type != Board_Modifiers.Cover;
    }

    public Board_Modifiers Get_Tile_Type_At(int X, int Y)
    {
        if (X < 0 || X >= Tile_Count_X || Y < 0 || Y >= Tile_Count_Y)
            return Board_Modifiers.None;

        return Map_Tiles[X, Y];
    }


    // ============================================================
    // MAP TERRAIN GENERATION
    // ============================================================

    private void Generate_Map_Terrain()
    {
        if (Current_Map == null)
        {
            Debug.LogWarning("No map data assigned! Board will be empty.");
            return;
        }

        Map_Tiles = new Board_Modifiers[Tile_Count_X, Tile_Count_Y];
        Terrain_Objects = new GameObject[Tile_Count_X, Tile_Count_Y];

        for (int X = 0; X < Tile_Count_X; X++)
        {
            for (int Y = 0; Y < Tile_Count_Y; Y++)
            {
                Board_Modifiers Current_Tile_Type = Current_Map.Get_Tile_Type(X, Y);
                Map_Tiles[X, Y] = Current_Tile_Type;

                GameObject Terrain_Object = Spawn_Terrain_Object(Current_Tile_Type, X, Y);

                if (Terrain_Object != null)
                    Terrain_Objects[X, Y] = Terrain_Object;
            }
        }

        Debug.Log($"Map '{Current_Map.Map_Name}' generated successfully.");
    }

    private GameObject Spawn_Terrain_Object(Board_Modifiers Type, int X, int Y)
    {
        GameObject Prefab = null;
        float Y_Pos = Y_Offset;
        string Object_Name = "";

        switch (Type)
        {
            case Board_Modifiers.Cover:
                Prefab = Cover_Tile_Prefab;
                Object_Name = $"Cover_{X}_{Y}";
                break;

            case Board_Modifiers.Wall:
                Prefab = Wall_Tile_Prefab;
                Object_Name = $"Wall_{X}_{Y}";
                break;

            case Board_Modifiers.Terrain:
                Prefab = Terrain_Tile_Prefab;
                Y_Pos += Terrain_Tile_Y_Offset;
                Object_Name = $"Terrain_{X}_{Y}";
                break;

            case Board_Modifiers.Hazard:
                Prefab = Hazard_Tile_Prefab;
                Object_Name = $"Hazard_{X}_{Y}";
                break;

            case Board_Modifiers.None:
            default:
                return null;
        }

        if (Prefab == null)
        {
            Debug.LogWarning($"Prefab for tile type '{Type}' is not assigned!");
            return null;
        }

        Vector3 Position = Get_Tile_Center(X, Y);
        Position.y = Y_Pos;

        GameObject Spawned_Object = Instantiate(Prefab, Position, Quaternion.identity, transform);
        Spawned_Object.name = Object_Name;

        return Spawned_Object;
    }


    // ============================================================
    // TILE RESTORATION (used by Combat_Manager)
    // ============================================================

    /// <summary>
    /// Restores tiles to their appropriate base material, respecting any
    /// active movement, path, or shove highlight. Used by Combat_Manager.
    /// </summary>
    public void Restore_Tiles_To_Default(List<Vector2Int> Tiles_To_Restore)
    {
        foreach (Vector2Int Tile in Tiles_To_Restore)
        {
            if (Tile.x < 0 || Tile.x >= Tile_Count_X || Tile.y < 0 || Tile.y >= Tile_Count_Y)
                continue;

            MeshRenderer Renderer = Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>();
            if (Renderer == null) continue;

            if (Movement_Range_Highlights.Contains(Tile))
                Renderer.material = Movement_Range_Material != null ? Movement_Range_Material : Tile_Material;
            else if (Shove_Target_Highlights.Contains(Tile))
                Renderer.material = Shove_Target_Material != null ? Shove_Target_Material : Tile_Material;
            else if (Shove_Destination_Highlights.Contains(Tile))
                Renderer.material = Shove_Destination_Material != null ? Shove_Destination_Material : Tile_Material;
            else if (Path_Highlight_Tiles.Contains(Tile))
                Renderer.material = Get_Path_Material_For_Tile(Tile);
            else
                Renderer.material = Tile_Material;

            Color C = Renderer.material.color;
            C.a = 1f;
            Renderer.material.color = C;
        }
    }


    // ============================================================
    // UTILITY
    // ============================================================

    private Vector3 Get_Tile_Center(int x, int y)
    {
        float Absolute_Y_Offset = Y_Offset + transform.position.y;
        return new Vector3(x * Tile_Size, Absolute_Y_Offset, y * Tile_Size) - Bounds + new Vector3(Tile_Size / 2, 0, Tile_Size / 2);
    }

    private Vector2Int Convert_World_To_Tile_Position(Vector3 World_Position)
    {
        Vector3 Relative_Pos = World_Position - Board_Origin;
        int X = Mathf.Clamp(Mathf.FloorToInt(Relative_Pos.x / Tile_Size), 0, Tile_Count_X - 1);
        int Y = Mathf.Clamp(Mathf.FloorToInt(Relative_Pos.z / Tile_Size), 0, Tile_Count_Y - 1);
        return new Vector2Int(X, Y);
    }

    private void Assign_Team_Colors()
    {
        if (Player_1_Faction == null || Player_2_Faction == null)
        {
            Debug.LogError("Factions not assigned!");
            return;
        }

        Material[] Player_1_Materials = Player_1_Faction.Team_Materials;
        Material[] Player_2_Materials = Player_2_Faction.Team_Materials;

        if (Player_1_Faction == Player_2_Faction)
            Assign_Different_Colors_Same_Faction(Player_1_Materials);
        else
        {
            Player_1_Mat = Player_1_Materials[0];
            Player_2_Mat = Player_2_Materials[0];
        }
    }

    private void Assign_Different_Colors_Same_Faction(Material[] Materials)
    {
        if (Materials.Length < 2)
        {
            Player_1_Mat = Materials[0];
            Player_2_Mat = Materials[0];
            return;
        }

        List<int> Available_Indices = new List<int>();
        for (int i = 0; i < Materials.Length; i++)
            Available_Indices.Add(i);

        int Player_1_Index = Random.Range(0, Available_Indices.Count);
        Player_1_Mat = Materials[Available_Indices[Player_1_Index]];
        Available_Indices.RemoveAt(Player_1_Index);

        int Player_2_Index = Random.Range(0, Available_Indices.Count);
        Player_2_Mat = Materials[Available_Indices[Player_2_Index]];
    }

    private int Get_Selected_Move_Cost(Vector2Int Start, Vector2Int Target)
    {
        if (All_Paths_To_Target.Count == 0)
            return Mathf.Abs(Target.x - Start.x) + Mathf.Abs(Target.y - Start.y);

        int Index = Mathf.Clamp(Current_Path_Index, 0, All_Paths_To_Target.Count - 1);
        List<Vector2Int> Path = All_Paths_To_Target[Index];
        return Mathf.Max(0, Path.Count - 1);
    }
}