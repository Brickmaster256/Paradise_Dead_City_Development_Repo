using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    // Each player's spawn zone occupies this many rows, anchored to their side.
    // Player 1: rows [0, Spawn_Row_Depth - 1]
    // Player 2: rows [Tile_Count_X - Spawn_Row_Depth, Tile_Count_X - 1]
    private const int Spawn_Row_Depth = 2;

    private const int Player_1 = 1;
    private const int Player_2 = 2;

    private const float Raycast_Max_Distance = 100f;
    private const float Board_Collider_Thickness = 0.01f;
    private const int Default_Movement_Range_If_No_Stats = 3;

    // Number of path priority levels (Primary, Secondary, Tertiary, Quaternary).
    private const int Max_Path_Priority = 3;

    private static readonly Vector2Int No_Tile = new Vector2Int(-1, -1);

    private static readonly Vector2Int[] Cardinal_Directions =
    {
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0)
    };


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
    [SerializeField] private Material Primary_Path_Material;
    [SerializeField] private Material Secondary_Path_Material;
    [SerializeField] private Material Tertiary_Path_Material;
    [SerializeField] private Material Quaternary_Path_Material;
    [SerializeField] private Material Hazard_Path_Material;
    [SerializeField] private float Flash_Speed = 2.0f;
    [SerializeField] private float Flash_Min_Alpha = 0.3f;
    [SerializeField] private float Flash_Max_Alpha = 1.0f;

    [Header("Input System")]
    [SerializeField] private InputActionAsset InputActions;


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

    // Cached layer indices and masks
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

    // Player 1's spawn zone occupies the first Spawn_Row_Depth rows.
    // Player 2's spawn zone occupies the last Spawn_Row_Depth rows.
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
        // Guard: if the Attack button was pressed this frame, skip board click handling.
        if (Combat_Manager_Ref != null && Combat_Manager_Ref.Was_Attack_Button_Just_Pressed())
            return;

        // COMBAT TARGETING MODE - Must be first and block ALL other input
        if (Combat_Manager_Ref != null && Combat_Manager_Ref.Is_Currently_Selecting_Target())
        {
            if (Click_Action != null && Click_Action.WasPressedThisFrame())
                Combat_Manager_Ref.Try_Select_Target(Hit_Position);

            if (RightClick_Action != null && RightClick_Action.WasPressedThisFrame())
                Combat_Manager_Ref.Cancel_Attack();

            return;
        }

        // Normal gameplay input processing
        if (Click_Action != null && Click_Action.WasPressedThisFrame())
            Handle_Model_Selection(Hit_Position);

        if (Click_Action != null && Click_Action.WasReleasedThisFrame())
            Handle_Model_Drop(Hit_Position);

        if (RightClick_Action != null && RightClick_Action.WasPressedThisFrame())
            Deselect_Current_Model();

        if (Space_Bar_Action != null && Space_Bar_Action.WasPressedThisFrame() && Dragged_Model != null)
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

        // Clear combat highlights first, so tile restoration respects movement ranges
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

        Plane Horizontal_Plane = new Plane(Vector3.up, Vector3.up * Y_Offset);
        if (Horizontal_Plane.Raycast(Ray, out float Distance))
        {
            Vector3 Mouse_Position = Ray.GetPoint(Distance);
            float Current_Drag_Offset = Calculate_Drag_Offset(Mouse_Position);
            Dragged_Model.Set_Position(Mouse_Position + Vector3.up * Current_Drag_Offset);
        }

        if (Movement_Range_Highlights.Contains(Hit_Position))
            Show_Paths_To_Target(Hit_Position);
        else
            Clear_Path_Highlights();
    }

    private void Handle_Model_Drop(Vector2Int Hit_Position)
    {
        if (Dragged_Model != null)
        {
            Vector2Int Previous_Position = new Vector2Int(Dragged_Model.Current_X, Dragged_Model.Current_Y);
            bool Valid_Move = Try_Move_Model(Dragged_Model, Hit_Position.x, Hit_Position.y);

            if (!Valid_Move)
            {
                Snap_Model_Back_To_Position(Dragged_Model, Previous_Position);
                Debug.Log("Invalid move - snapping back");
            }
            else
            {
                Debug.Log($"Model moved from {Previous_Position} to {Hit_Position}");

                // Capture the currently selected path BEFORE clearing highlights.
                List<Vector2Int> Chosen_Path = Get_Selected_Path_Copy();

                Clear_Path_Highlights();
                Refresh_Movement_Range_Highlights(Dragged_Model);

                Apply_Hazard_Damage(Dragged_Model, Chosen_Path);
            }
        }

        Reset_Drag_State();
    }

    private List<Vector2Int> Get_Selected_Path_Copy()
    {
        if (All_Paths_To_Target.Count == 0)
            return new List<Vector2Int>();

        int Index = Mathf.Clamp(Current_Path_Index, 0, All_Paths_To_Target.Count - 1);
        return new List<Vector2Int>(All_Paths_To_Target[Index]);
    }

    /// <summary>
    /// Applies 1 damage per hazard tile the model's path crosses, including
    /// the model's starting tile if it was standing on a hazard and moved off.
    /// Models that stay put never reach this method, so a model can safely
    /// remain on a hazard without taking damage.
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

        if (Model.Has_Moved_This_Turn)
        {
            Debug.Log($"{Model.Type} has already moved this turn!");
            return false;
        }

        if (Models[X, Y] != null)
        {
            Debug.Log(Models[X, Y].Team != Model.Team ? "Cannot move onto enemy tile!" : "Tile occupied by friendly model!");
            return false;
        }

        Vector2Int Target_Position = new Vector2Int(X, Y);
        if (!Movement_Range_Highlights.Contains(Target_Position))
        {
            Debug.Log($"Cannot move to ({X}, {Y}) - not in valid movement range!");
            return false;
        }

        Models[X, Y] = Model;
        Models[Previous_Position.x, Previous_Position.y] = null;
        Smooth_Move_To_Position(X, Y);
        return true;
    }


    // ============================================================
    // PATHFINDING & PATH HIGHLIGHTS
    // ============================================================

    private void Show_Paths_To_Target(Vector2Int Target)
    {
        if (Target == Current_Hover_Target || Dragged_Model == null)
            return;

        // Restore movement range on old path tiles
        foreach (Vector2Int Tile in Path_Highlight_Tiles)
        {
            if (Movement_Range_Highlights.Contains(Tile) && Movement_Range_Material != null)
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Movement_Range_Material;
            else
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Tile_Material;
        }

        Path_Highlight_Tiles.Clear();
        All_Paths_To_Target.Clear();
        Current_Path_Index = 0;
        Current_Hover_Target = Target;

        Vector2Int Start = new Vector2Int(Dragged_Model.Current_X, Dragged_Model.Current_Y);
        int Max_Distance = Dragged_Model.Stats != null
            ? Dragged_Model.Stats.Movement_Range
            : Default_Movement_Range_If_No_Stats;

        All_Paths_To_Target = Find_All_Paths(Start, Target, Max_Distance);

        if (All_Paths_To_Target.Count == 0)
        {
            Debug.Log("No valid path found!");
            return;
        }

        Draw_All_Paths_With_Layering();

        Debug.Log($"Path 1/{All_Paths_To_Target.Count} (Primary): {Analyze_Path(All_Paths_To_Target[0])}");
        for (int i = 1; i < All_Paths_To_Target.Count; i++)
        {
            string Priority_Name = i switch
            {
                1 => "Secondary",
                2 => "Tertiary",
                3 => "Quaternary",
                _ => "Alternative"
            };
            Debug.Log($"Path {i + 1}/{All_Paths_To_Target.Count} ({Priority_Name}): {Analyze_Path(All_Paths_To_Target[i])}");
        }

        if (All_Paths_To_Target.Count > 1)
            Debug.Log($"Press SPACE to cycle through {All_Paths_To_Target.Count} available paths");
    }

    private void Switch_To_Next_Path()
    {
        if (All_Paths_To_Target.Count <= 1 || Current_Hover_Target == No_Tile)
            return;

        Current_Path_Index = (Current_Path_Index + 1) % All_Paths_To_Target.Count;

        Path_Highlight_Tiles.Clear();
        Draw_All_Paths_With_Layering();

        string New_Primary_Name = Current_Path_Index switch
        {
            0 => "Primary",
            1 => "Secondary (now Primary)",
            2 => "Tertiary (now Primary)",
            3 => "Quaternary (now Primary)",
            _ => "Alternative (now Primary)"
        };
        Debug.Log($"Switched to path {Current_Path_Index + 1}/{All_Paths_To_Target.Count} ({New_Primary_Name}): {Analyze_Path(All_Paths_To_Target[Current_Path_Index])}");
    }

    private void Draw_All_Paths_With_Layering()
    {
        if (All_Paths_To_Target.Count == 0)
            return;

        Path_Highlight_Tiles.Clear();

        int[] Priority_Order = new int[All_Paths_To_Target.Count];
        for (int i = 0; i < All_Paths_To_Target.Count; i++)
            Priority_Order[i] = (i - Current_Path_Index + All_Paths_To_Target.Count) % All_Paths_To_Target.Count;

        for (int Priority_Level = Max_Path_Priority; Priority_Level >= 0; Priority_Level--)
        {
            for (int i = 0; i < All_Paths_To_Target.Count; i++)
            {
                if (Priority_Order[i] == Priority_Level)
                    Show_Path_Highlights(All_Paths_To_Target[i], Priority_Order[i]);
            }
        }
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

    private Material Get_Path_Material(int Priority)
    {
        return Priority switch
        {
            0 => Primary_Path_Material,
            1 => Secondary_Path_Material,
            2 => Tertiary_Path_Material,
            3 => Quaternary_Path_Material,
            _ => Secondary_Path_Material
        };
    }

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

                if (!Is_Tile_Enterable(Neighbor.x, Neighbor.y, Moving_Team))
                    continue;

                if (Neighbor != Target && Models[Neighbor.x, Neighbor.y] != null && Models[Neighbor.x, Neighbor.y].Team != Moving_Team)
                    continue;

                int New_Dist = Current_Dist + 1;

                if (!Distance.ContainsKey(Neighbor))
                {
                    Distance[Neighbor] = New_Dist;
                    Parents[Neighbor] = new List<Vector2Int> { Current };
                    Queue.Enqueue(Neighbor);

                    if (Neighbor == Target)
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

        string Message = $"Path length: {Path.Count}";
        if (Hazard_Count > 0) Message += $", Hazards: {Hazard_Count}";
        if (Terrain_Count > 0) Message += $", Terrain: {Terrain_Count}";

        return Message;
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
            Tiles[Current_Mouse_Hover.x, Hit_Position.y].layer = Hover_Layer;
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
        else if (Current_Phase == Game_State.Gameplay && Movement_Range_Highlights.Count > 0)
            Apply_Alpha_To_Highlight_Group(Movement_Range_Highlights, Alpha);
    }

    private void Apply_Alpha_To_Highlight_Group(List<Vector2Int> Highlight_Group, float Alpha)
    {
        foreach (Vector2Int Tile in Highlight_Group)
        {
            if (Path_Highlight_Tiles.Contains(Tile))
                continue;

            Renderer Renderer = Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>();
            if (Renderer != null && Renderer.material != null)
            {
                Color Color = Renderer.material.color;
                Color.a = Alpha;
                Renderer.material.color = Color;
            }
        }
    }

    private void Display_Movement_Range(Model_Standard_Behavior Model)
    {
        Clear_Movement_Range_Highlights();

        if (Model.Stats == null)
            return;

        int Movement_Range = Model.Stats.Movement_Range;
        int Start_X = Model.Current_X;
        int Start_Y = Model.Current_Y;

        bool[,] Visited = new bool[Tile_Count_X, Tile_Count_Y];
        Queue<Vector2Int> To_Explore = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> Distance_Map = new Dictionary<Vector2Int, int>();

        Vector2Int Start = new Vector2Int(Start_X, Start_Y);
        To_Explore.Enqueue(Start);
        Visited[Start_X, Start_Y] = true;
        Distance_Map[Start] = 0;

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

                if (!Is_Tile_Enterable(Neighbor.x, Neighbor.y, Model.Team))
                    continue;

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

        if (Movement_Range_Highlights.Count > 0)
            Current_Flash_Material = Movement_Range_Material;
    }

    private void Refresh_Movement_Range_Highlights(Model_Standard_Behavior Model)
    {
        Display_Movement_Range(Model);
    }

    private void Clear_Movement_Range_Highlights()
    {
        foreach (Vector2Int Tile in Movement_Range_Highlights)
            Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Tile_Material;

        Movement_Range_Highlights.Clear();
        Current_Flash_Material = null;
    }

    private void Clear_Path_Highlights()
    {
        foreach (Vector2Int Tile in Path_Highlight_Tiles)
        {
            if (Movement_Range_Highlights.Contains(Tile) && Movement_Range_Material != null)
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Movement_Range_Material;
            else
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Tile_Material;
        }

        Path_Highlight_Tiles.Clear();
        All_Paths_To_Target.Clear();
        Current_Path_Index = 0;
        Current_Hover_Target = No_Tile;
    }

    private void Clear_Spawn_Placement_Highlights()
    {
        foreach (Vector2Int Tile in Spawn_Tile_Highlights)
            Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Tile_Material;

        Spawn_Tile_Highlights.Clear();
    }

    private void Clear_Model_Placement_Highlights()
    {
        foreach (Vector2Int Tile in Model_Placement_Highlights)
            Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Tile_Material;

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
            Debug.Log($"Player 1 spawn tile placed at {Position}. Phase: Player 1 place your models");
        }
        else
        {
            Player_2_Spawn_Tile = Spawn_Tile;
            Player_2_Spawn_Position = Position;
            Clear_Spawn_Placement_Highlights();
            Show_Model_Deployment_Zone(Position, Player);
            Current_Phase = Game_State.Player_2_Place_Models;
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

            Debug.Log($"Player {Player} placed {Random_Type}. Remaining: {Army.Get_Remaining_Count()} models");

            if (!Army.Has_Models_Left())
                Advance_From_Model_Placement(Player);
        }
    }

    private void Advance_From_Model_Placement(int Player)
    {
        Debug.Log($"Player {Player} has no more models to place!");

        if (Player == Player_1)
        {
            Current_Phase = Game_State.Player_2_Place_Spawn;
            Clear_Model_Placement_Highlights();
            Show_Spawn_Zone_Highlights(Player_2);
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
        Debug.Log($"Active player is now Player {Active_Player}");

        if (Card_UI != null)
            Card_UI.Set_Active_Player(Active_Player);

        if (Selected_Model != null && Selected_Model.Team != Active_Player)
            Deselect_Current_Model();

        Reset_Turn_Flags_For_Player(Active_Player);
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
                    Model.Has_Moved_This_Turn = false;
                    Model.Has_Attacked_This_Turn = false;
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
        // Y offset is calculated per-call instead of mutating the serialized field.
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

    private bool Is_Tile_Enterable(int X, int Y, int Team)
    {
        if (X < 0 || X >= Tile_Count_X || Y < 0 || Y >= Tile_Count_Y)
            return false;

        if (!Is_Tile_Passable(X, Y))
            return false;

        if (Models[X, Y] != null && Models[X, Y].Team != Team)
            return false;

        return true;
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
    /// Restores the given tiles to the appropriate base material, respecting
    /// any active movement or path highlights. Used by Combat_Manager when
    /// clearing combat highlights.
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
            else if (Path_Highlight_Tiles.Contains(Tile))
                Renderer.material = Primary_Path_Material != null ? Primary_Path_Material : Tile_Material;
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
}