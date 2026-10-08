using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Combat_Manager : MonoBehaviour
{
    // ============================================================
    // CONSTANTS
    // ============================================================

    private const int D6_Min = 1;
    private const int D6_Max_Exclusive = 7; // Random.Range upper bound is exclusive

    // Delay between armor-save rolls so the player can read the log.
    private const float Armor_Save_Delay = 0.3f;


    // ============================================================
    // SERIALIZED FIELDS
    // ============================================================

    [Header("References")]
    [SerializeField] private Battle_Board_Behavior Battle_Board;
    [SerializeField] private Card_UI_Controller Card_UI;

    [Header("Combat Materials")]
    [SerializeField] private Material Attack_Range_Material;
    [SerializeField] private Material Valid_Target_Material;

    [Header("Flashing")]
    [SerializeField] private float Flash_Speed = 2.0f;
    [SerializeField] private float Flash_Min_Alpha = 0.3f;
    [SerializeField] private float Flash_Max_Alpha = 1.0f;


    // ============================================================
    // COMBAT STATE
    // ============================================================

    private Model_Standard_Behavior Attacking_Model;
    private bool Is_Selecting_Target = false;

    private List<Vector2Int> Attack_Range_Highlights = new List<Vector2Int>();
    private List<Vector2Int> Valid_Target_Highlights = new List<Vector2Int>();



    // ============================================================
    // UNITY LIFECYCLE
    // ============================================================

    private void Update()
    {
        if (Is_Selecting_Target)
            Update_Attack_Flashing_Effect();
    }


    // ============================================================
    // PUBLIC STATE QUERIES
    // ============================================================

    public bool Is_Currently_Selecting_Target() => Is_Selecting_Target;

    // ============================================================
    // ATTACK INITIATION
    // ============================================================

    public void On_Attack_Button_Pressed()
    {
        if (Battle_Board == null)
        {
            Debug.LogError("Combat_Manager: Battle_Board reference is missing!");
            return;
        }

        Model_Standard_Behavior Selected_Model = Battle_Board.Get_Selected_Model();

        if (Selected_Model == null) return;
        if (Selected_Model.Is_Sprinting_This_Turn) return;
        if (Selected_Model.Team != Battle_Board.Get_Active_Player()) return;
        if (Selected_Model.Has_Ended_Turn) return;

        Battle_Board.Cancel_Active_Drag();

        if (Card_UI != null)
            Card_UI.Hide_Model_Info();

        Battle_Board.Clear_Movement_Highlights_Public();

        Attacking_Model = Selected_Model;
        Is_Selecting_Target = true;
        Show_Attack_Range(Selected_Model);
    }

    private void Show_Attack_Range(Model_Standard_Behavior Model)
    {
        Clear_Attack_Highlights();

        int Start_X = Model.Current_X;
        int Start_Y = Model.Current_Y;
        int Range = Model.Stats.Attack_Range;

        bool Requires_LOS = Model.Stats.Is_Ranged;
        bool Is_Melee = !Model.Stats.Is_Ranged;

        Vector2Int Attacker_Pos = new Vector2Int(Start_X, Start_Y);
        bool Attacker_High = Battle_Board.Is_Tile_Elevated(Start_X, Start_Y);

        var Tiles = Battle_Board.Get_Tiles();
        int Tile_Count_X = Battle_Board.Get_Tile_Count_X();
        int Tile_Count_Y = Battle_Board.Get_Tile_Count_Y();

        for (int X = 0; X < Tile_Count_X; X++)
        {
            for (int Y = 0; Y < Tile_Count_Y; Y++)
            {
                int Distance = Mathf.Abs(X - Start_X) + Mathf.Abs(Y - Start_Y);
                if (Distance <= 0 || Distance > Range) continue;

                Vector2Int Tile_Pos = new Vector2Int(X, Y);

                if (Requires_LOS && !Battle_Board.Has_Line_Of_Sight(Attacker_Pos, Tile_Pos))
                    continue;

                Model_Standard_Behavior Target = Battle_Board.Get_Model_At(X, Y);
                bool Tile_Occupied_By_Enemy = Target != null
                                              && Target.gameObject.activeSelf
                                              && Target.Team != Model.Team;

                if (Is_Melee && Tile_Occupied_By_Enemy)
                {
                    bool Target_High = Battle_Board.Is_Tile_Elevated(X, Y);
                    if (Target_High && !Attacker_High)
                        continue;
                }

                if (Tile_Occupied_By_Enemy)
                {
                    Valid_Target_Highlights.Add(Tile_Pos);
                    if (Valid_Target_Material != null && Tiles[X, Y] != null)
                    {
                        var Renderer = Tiles[X, Y].GetComponent<MeshRenderer>();
                        if (Renderer != null)
                        {
                            Renderer.material = Valid_Target_Material;
                            Color C = Renderer.material.color;
                            C.a = 1f;
                            Renderer.material.color = C;
                        }
                    }
                }
                else if (Target == null || !Target.gameObject.activeSelf)
                {
                    Attack_Range_Highlights.Add(Tile_Pos);
                    if (Attack_Range_Material != null && Tiles[X, Y] != null)
                    {
                        var Renderer = Tiles[X, Y].GetComponent<MeshRenderer>();
                        if (Renderer != null)
                        {
                            Renderer.material = Attack_Range_Material;
                            Color C = Renderer.material.color;
                            C.a = 1f;
                            Renderer.material.color = C;
                        }
                    }
                }
            }
        }
    }


    // ============================================================
    // TARGET SELECTION
    // ============================================================

    public void Try_Select_Target(Vector2Int Hit_Position)
    {
        if (!Is_Selecting_Target || Attacking_Model == null)
            return;

        Model_Standard_Behavior Target = Battle_Board.Get_Model_At(Hit_Position.x, Hit_Position.y);


        if (Target == null || Target.Team == Attacking_Model.Team || !Valid_Target_Highlights.Contains(Hit_Position))
        {
            Clear_Attack_Highlights();
            Is_Selecting_Target = false;
            Attacking_Model = null;

            // Fully deselect rather than reselect, since the player clicked away.
            if (Battle_Board != null)
                Battle_Board.Deselect_Current_Model_Public();

            return;
        }

        if (Battle_Board != null)
            Battle_Board.Cancel_Active_Drag();

        StartCoroutine(Execute_Attack(Attacking_Model, Target));

        Clear_Attack_Highlights();
        Is_Selecting_Target = false;
        Attacking_Model = null;
    }

    public void Cancel_Attack()
    {
        if (!Is_Selecting_Target)
            return;

        Model_Standard_Behavior Model_To_Reselect = Attacking_Model;

        Clear_Attack_Highlights();
        Is_Selecting_Target = false;
        Attacking_Model = null;

        if (Model_To_Reselect != null && Battle_Board != null)
            Battle_Board.Select_Model_Public(Model_To_Reselect);
    }


    // ============================================================
    // ATTACK EXECUTION
    // ============================================================

    private IEnumerator Execute_Attack(Model_Standard_Behavior Attacker, Model_Standard_Behavior Defender)
    {
        Debug.Log($"=== COMBAT: {Attacker.Stats.Model_Name} attacks {Defender.Stats.Model_Name} ===");

        int Hit_Roll = Roll_D6();
        int Base_Target = Attacker.Stats.Attack_Skill;
        int Modifier = Get_Attack_Modifier(Attacker, Defender);
        int Needed_To_Hit = Mathf.Clamp(Base_Target - Modifier, 2, 6);

        string Mod_Label = Modifier > 0 ? "advantage" : Modifier < 0 ? "disadvantage" : "neutral";
        Debug.Log($"Hit Roll: {Hit_Roll} (needed {Needed_To_Hit}+, base {Base_Target}, {Mod_Label})");

        if (Hit_Roll < Needed_To_Hit)
        {
            Debug.Log($"ATTACK MISSED! {Attacker.Stats.Model_Name} fails to hit.");
            Attacker.Has_Attacked_This_Turn = true;
            Attacker.Has_Ended_Turn = true;
            End_Attack(Attacker);
            yield break;
        }

        Debug.Log($"ATTACK HITS! {Attacker.Stats.Model_Name} lands the attack.");

        int Damage_Negated = 0;
        yield return Roll_Armor_Saves(Defender, result => Damage_Negated = result);

        int Total_Damage = Attacker.Stats.Attack_Damage - Damage_Negated;
        if (Total_Damage < 0) Total_Damage = 0;

        Debug.Log($"Damage: {Attacker.Stats.Attack_Damage} - {Damage_Negated} negated = {Total_Damage} total damage");

        if (Total_Damage <= 0)
        {
            Debug.Log($"{Defender.Stats.Model_Name} fully defended the attack! No damage taken.");
            Attacker.Has_Attacked_This_Turn = true;
            Attacker.Has_Ended_Turn = true;
            End_Attack(Attacker);
            yield break;
        }

        Defender.Current_Health -= Total_Damage;
        Debug.Log($"{Defender.Stats.Model_Name} takes {Total_Damage} damage! Health: {Defender.Current_Health}/{Defender.Stats.Health}");

        if (Defender.Current_Health <= 0)
        {
            Debug.Log($"{Defender.Stats.Model_Name} IS KILLED!");
            Kill_Model(Defender);
        }

        Attacker.Has_Attacked_This_Turn = true;
        Attacker.Has_Ended_Turn = true;
        End_Attack(Attacker);

        Debug.Log("=== COMBAT END ===");
    }

    private void End_Attack(Model_Standard_Behavior Attacker)
    {
        if (Attacker != null && Battle_Board != null && Attacker.gameObject.activeSelf)
            Battle_Board.Select_Model_Public(Attacker);
    }

    public void Kill_Model(Model_Standard_Behavior Model)
    {
        if (Model == null) return;

        Debug.Log($"{Model.Stats.Model_Name} has been slain!");

        // Clear from every tile the model might be registered on, not just
        // Current_X/Y. This prevents a stale Current_X/Y from leaving a ghost
        // entry in the Models array that blocks selection.
        int Tile_Count_X = Battle_Board.Get_Tile_Count_X();
        int Tile_Count_Y = Battle_Board.Get_Tile_Count_Y();
        for (int X = 0; X < Tile_Count_X; X++)
        {
            for (int Y = 0; Y < Tile_Count_Y; Y++)
            {
                if (Battle_Board.Get_Model_At(X, Y) == Model)
                    Battle_Board.Remove_Model(X, Y);
            }
        }

        if (Battle_Board.Get_Selected_Model() == Model)
            Battle_Board.Deselect_Current_Model_Public();

        Model.gameObject.SetActive(false);
    }

    private IEnumerator Roll_Armor_Saves(Model_Standard_Behavior Defender, System.Action<int> On_Complete)
    {
        int Damage_Negated = 0;
        int Armor_Saves = Defender.Stats.Armor_Saves;
        int Armor_Target = Defender.Stats.Armor_Target;

        for (int i = 0; i < Armor_Saves; i++)
        {
            int Armor_Roll = Roll_D6();
            Debug.Log($"{Defender.Stats.Model_Name} Armor Save Roll {i + 1}/{Armor_Saves}: {Armor_Roll} (needed {Armor_Target}+)");

            if (Armor_Roll >= Armor_Target)
            {
                Damage_Negated++;
                Debug.Log($"Armor save SUCCESS! Damage negated ({Damage_Negated}/{Armor_Saves})");
            }
            else
            {
                Debug.Log($"Armor save FAILED!");
            }

            yield return new WaitForSeconds(Armor_Save_Delay);
        }

        On_Complete?.Invoke(Damage_Negated);
    }

    private static int Roll_D6()
    {
        return Random.Range(D6_Min, D6_Max_Exclusive);
    }

    // ============================================================
    // ATTACK MODIFIERS
    // ============================================================

    /// <summary>
    /// Returns +1 if the attacker has net advantage, -1 if net
    /// disadvantage, 0 if neutral. Sources do not stack — one or more
    /// advantages with zero disadvantages is still +1, and any mix of
    /// both cancels to 0.
    ///
    /// Instantaneous sources (elevation, cover) apply only to this attack.
    /// Persistent sources (defender vulnerable, attacker vulnerable) are
    /// state flags on the model and were set by Battle_Board_Behavior.
    ///
    /// Elevation advantage is ranged-only, per design — a melee attacker
    /// on elevation gains nothing. Melee elevation is instead handled as
    /// a targeting restriction in Show_Attack_Range.
    /// </summary>
    private int Get_Attack_Modifier(Model_Standard_Behavior Attacker, Model_Standard_Behavior Defender)
    {
        int Advantage = 0;
        int Disadvantage = 0;

        bool Attacker_High = Battle_Board.Is_Tile_Elevated(Attacker.Current_X, Attacker.Current_Y);
        bool Defender_High = Battle_Board.Is_Tile_Elevated(Defender.Current_X, Defender.Current_Y);

        // Elevation — ranged only.
        if (Attacker.Stats.Is_Ranged)
        {
            if (Attacker_High && !Defender_High) Advantage++;
            if (Defender_High && !Attacker_High) Disadvantage++;
        }

        // Cover between attacker and defender.
        if (Cover_Between(Attacker, Defender))
            Disadvantage++;

        // Persistent state.
        if (Defender.Is_Vulnerable) Advantage++;
        if (Attacker.Is_Vulnerable) Disadvantage++;

        if (Advantage > 0 && Disadvantage == 0) return +1;
        if (Disadvantage > 0 && Advantage == 0) return -1;
        return 0;
    }

    /// <summary>
    /// True if any strictly-intermediate tile on the straight cardinal
    /// line between attacker and defender is Cover. Only meaningful for
    /// ranged attacks — melee has no intervening tiles — but harmless
    /// to call for melee too.
    /// </summary>
    private bool Cover_Between(Model_Standard_Behavior Attacker, Model_Standard_Behavior Defender)
    {
        Vector2Int From = new Vector2Int(Attacker.Current_X, Attacker.Current_Y);
        Vector2Int To = new Vector2Int(Defender.Current_X, Defender.Current_Y);

        if (From == To) return false;
        if (From.x != To.x && From.y != To.y) return false;   // diagonal — shouldn't happen for ranged

        if (From.x == To.x)
        {
            int Step = To.y > From.y ? 1 : -1;
            for (int y = From.y + Step; y != To.y; y += Step)
                if (Battle_Board.Get_Tile_Type_At(From.x, y) == Board_Modifiers.Cover)
                    return true;
        }
        else
        {
            int Step = To.x > From.x ? 1 : -1;
            for (int x = From.x + Step; x != To.x; x += Step)
                if (Battle_Board.Get_Tile_Type_At(x, From.y) == Board_Modifiers.Cover)
                    return true;
        }

        return false;
    }

    // ============================================================
    // HIGHLIGHT FLASHING
    // ============================================================

    private void Update_Attack_Flashing_Effect()
    {
        float Alpha = Mathf.Lerp(Flash_Min_Alpha, Flash_Max_Alpha,
            (Mathf.Sin(Time.time * Flash_Speed) + 1.0f) * 0.5f);

        var Tiles = Battle_Board.Get_Tiles();

        Apply_Alpha_To_Tiles(Tiles, Attack_Range_Highlights, Alpha);
        Apply_Alpha_To_Tiles(Tiles, Valid_Target_Highlights, Alpha);
    }

    private static void Apply_Alpha_To_Tiles(GameObject[,] Tiles, List<Vector2Int> Tile_List, float Alpha)
    {
        foreach (Vector2Int Tile in Tile_List)
        {
            GameObject Tile_Object = Tiles[Tile.x, Tile.y];
            if (Tile_Object == null) continue;

            MeshRenderer Renderer = Tile_Object.GetComponent<MeshRenderer>();
            if (Renderer == null) continue;

            Color C = Renderer.material.color;
            C.a = Alpha;
            Renderer.material.color = C;
        }
    }


    // ============================================================
    // HIGHLIGHT CLEANUP
    // ============================================================

    private void Clear_Attack_Highlights()
    {
        if (Battle_Board == null)
            return;

        List<Vector2Int> All_Highlights = new List<Vector2Int>(
            Attack_Range_Highlights.Count + Valid_Target_Highlights.Count);

        All_Highlights.AddRange(Attack_Range_Highlights);
        All_Highlights.AddRange(Valid_Target_Highlights);

        Battle_Board.Restore_Tiles_To_Default(All_Highlights);

        Attack_Range_Highlights.Clear();
        Valid_Target_Highlights.Clear();
    }

    public void Force_Clear_Combat_State()
    {
        if (Battle_Board == null)
        {
            Is_Selecting_Target = false;
            Attacking_Model = null;
            return;
        }

        Clear_Attack_Highlights();

        Is_Selecting_Target = false;
        Attacking_Model = null;
    }
}