using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Combat_Manager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Battle_Board_Behavior Battle_Board;
    [SerializeField] private Card_UI_Controller Card_UI;

    [Header("Combat Materials")]
    [SerializeField] private Material Attack_Range_Material;
    [SerializeField] private Material Valid_Target_Material;
    [SerializeField] private float Flash_Speed = 2.0f;
    [SerializeField] private float Flash_Min_Alpha = 0.3f;
    [SerializeField] private float Flash_Max_Alpha = 1.0f;

    // Track combat state
    private Model_Standard_Behavior Attacking_Model;
    private bool Is_Selecting_Target = false;
    private List<Vector2Int> Attack_Range_Highlights = new List<Vector2Int>();
    private List<Vector2Int> Valid_Target_Highlights = new List<Vector2Int>();

    private void Update()
    {
        // Flash the attack range tiles when selecting a target
        if (Is_Selecting_Target)
        {
            Update_Attack_Flashing_Effect();
        }
    }

    public void On_Attack_Button_Pressed()
    {
        if (Battle_Board == null)
        {
            Debug.LogError("Combat_Manager: Battle_Board reference is missing!");
            return;
        }

        Model_Standard_Behavior Selected_Model = Battle_Board.Get_Selected_Model();

        if (Selected_Model == null)
        {
            Debug.Log("No model selected to attack with!");
            return;
        }

        if (Selected_Model.Has_Attacked_This_Turn)
        {
            Debug.Log($"{Selected_Model.Stats.Model_Name} has already attacked this turn!");
            return;
        }

        // Close the card UI first
        if (Card_UI != null)
        {
            Card_UI.Hide_Model_Info();
        }

        // Clear movement range highlights from the board
        Battle_Board.Clear_Movement_Highlights_Public();

        // Start target selection mode
        Attacking_Model = Selected_Model;
        Is_Selecting_Target = true;
        Show_Attack_Range(Selected_Model);

        Debug.Log($"Selecting attack target for {Selected_Model.Stats.Model_Name}. Right-click to cancel. Click on an enemy in range.");
    }

    private void Show_Attack_Range(Model_Standard_Behavior Model)
    {
        Clear_Attack_Highlights();

        int Start_X = Model.Current_X;
        int Start_Y = Model.Current_Y;
        int Range = Model.Stats.Attack_Range;

        var Tiles = Battle_Board.Get_Tiles();
        int Tile_Count_X = Battle_Board.Get_Tile_Count_X();
        int Tile_Count_Y = Battle_Board.Get_Tile_Count_Y();

        // Highlight all tiles in attack range
        for (int X = 0; X < Tile_Count_X; X++)
        {
            for (int Y = 0; Y < Tile_Count_Y; Y++)
            {
                int Distance = Mathf.Abs(X - Start_X) + Mathf.Abs(Y - Start_Y);

                if (Distance > 0 && Distance <= Range)
                {
                    // Check if there's an enemy model on this tile
                    Model_Standard_Behavior Target = Battle_Board.Get_Model_At(X, Y);

                    if (Target != null && Target.Team != Model.Team)
                    {
                        // This is a valid target!
                        Valid_Target_Highlights.Add(new Vector2Int(X, Y));
                        if (Valid_Target_Material != null && Tiles[X, Y] != null)
                            Tiles[X, Y].GetComponent<MeshRenderer>().material = Valid_Target_Material;
                    }
                    else if (Target == null)
                    {
                        // In range but no valid target (empty tile)
                        Attack_Range_Highlights.Add(new Vector2Int(X, Y));
                        if (Attack_Range_Material != null && Tiles[X, Y] != null)
                            Tiles[X, Y].GetComponent<MeshRenderer>().material = Attack_Range_Material;
                    }
                    // If friendly model on tile, don't highlight it
                }
            }
        }

        Debug.Log($"Attack range shown. {Valid_Target_Highlights.Count} valid targets found.");
    }

    private void Update_Attack_Flashing_Effect()
    {
        float Alpha = Mathf.Lerp(Flash_Min_Alpha, Flash_Max_Alpha,
            (Mathf.Sin(Time.time * Flash_Speed) + 1.0f) * 0.5f);

        var Tiles = Battle_Board.Get_Tiles();

        // Flash attack range tiles
        foreach (Vector2Int Tile in Attack_Range_Highlights)
        {
            if (Tiles[Tile.x, Tile.y] != null)
            {
                Renderer Renderer = Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>();
                if (Renderer != null && Renderer.material == Attack_Range_Material)
                {
                    Color Color = Renderer.material.color;
                    Color.a = Alpha;
                    Renderer.material.color = Color;
                }
            }
        }

        // Flash valid target tiles with a different feel
        foreach (Vector2Int Tile in Valid_Target_Highlights)
        {
            if (Tiles[Tile.x, Tile.y] != null)
            {
                Renderer Renderer = Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>();
                if (Renderer != null && Renderer.material == Valid_Target_Material)
                {
                    Color Color = Renderer.material.color;
                    Color.a = Alpha;
                    Renderer.material.color = Color;
                }
            }
        }
    }

    public void Try_Select_Target(Vector2Int Hit_Position)
    {
        if (!Is_Selecting_Target || Attacking_Model == null)
            return;

        Model_Standard_Behavior Target = Battle_Board.Get_Model_At(Hit_Position.x, Hit_Position.y);

        if (Target == null)
        {
            Debug.Log("No model at that position!");
            return;
        }

        if (Target.Team == Attacking_Model.Team)
        {
            Debug.Log("Cannot attack friendly models!");
            return;
        }

        if (!Valid_Target_Highlights.Contains(Hit_Position))
        {
            Debug.Log("Target is not in attack range!");
            return;
        }

        // Valid target selected - execute the attack!
        StartCoroutine(Execute_Attack(Attacking_Model, Target));
        Clear_Attack_Highlights();
        Is_Selecting_Target = false;
        Attacking_Model = null;
    }


    public void Cancel_Attack()
    {
        if (Is_Selecting_Target)
        {
            Model_Standard_Behavior Model_To_Reselect = Attacking_Model;

            Clear_Attack_Highlights();
            Is_Selecting_Target = false;
            Attacking_Model = null;

            // Re-select the model and show its card again
            if (Model_To_Reselect != null && Battle_Board != null)
            {
                Battle_Board.Select_Model_Public(Model_To_Reselect);
            }

            Debug.Log("Attack cancelled - model re-selected");
        }
    }


    private IEnumerator Execute_Attack(Model_Standard_Behavior Attacker, Model_Standard_Behavior Defender)
    {
        Debug.Log($"=== COMBAT: {Attacker.Stats.Model_Name} attacks {Defender.Stats.Model_Name} ===");

        // Step 1: Roll to hit
        int Hit_Roll = Random.Range(1, 7); // D6 roll (1-6)
        int Needed_To_Hit = Attacker.Stats.Attack_Skill;

        Debug.Log($"Hit Roll: {Hit_Roll} (needed {Needed_To_Hit}+)");

        if (Hit_Roll < Needed_To_Hit)
        {
            Debug.Log($"ATTACK MISSED! {Attacker.Stats.Model_Name} fails to hit.");
            Attacker.Has_Attacked_This_Turn = true;
            End_Attack(Attacker);
            yield break;
        }

        Debug.Log($"ATTACK HITS! {Attacker.Stats.Model_Name} lands the attack.");

        // Step 2: Defender rolls armor saves
        int Damage_Negated = 0;
        int Armor_Saves = Defender.Stats.Armor_Saves;
        int Armor_Target = Defender.Stats.Armor_Target;

        for (int i = 0; i < Armor_Saves; i++)
        {
            int Armor_Roll = Random.Range(1, 7);
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

            yield return new WaitForSeconds(0.3f); // Small delay for readability
        }

        // Step 3: Calculate final damage
        int Total_Damage = Attacker.Stats.Attack_Damage - Damage_Negated;
        if (Total_Damage < 0) Total_Damage = 0;

        Debug.Log($"Damage: {Attacker.Stats.Attack_Damage} - {Damage_Negated} negated = {Total_Damage} total damage");

        if (Total_Damage <= 0)
        {
            Debug.Log($"{Defender.Stats.Model_Name} fully defended the attack! No damage taken.");
            Attacker.Has_Attacked_This_Turn = true;
            End_Attack(Attacker);
            yield break;
        }

        // Step 4: Apply damage
        Defender.Current_Health -= Total_Damage;
        Debug.Log($"{Defender.Stats.Model_Name} takes {Total_Damage} damage! Health: {Defender.Current_Health}/{Defender.Stats.Health}");

        // Step 5: Check if defender is killed
        if (Defender.Current_Health <= 0)
        {
            Debug.Log($"{Defender.Stats.Model_Name} IS KILLED!");
            Kill_Model(Defender);
        }

        // Mark attacker as having attacked
        Attacker.Has_Attacked_This_Turn = true;

        // End the attack and re-select the attacker
        End_Attack(Attacker);

        Debug.Log("=== COMBAT END ===");
    }

    private void End_Attack(Model_Standard_Behavior Attacker)
    {
        // Re-select the attacking model to show updated card
        if (Attacker != null && Battle_Board != null && Attacker.gameObject.activeSelf)
        {
            Battle_Board.Select_Model_Public(Attacker);
        }
    }

    private void Kill_Model(Model_Standard_Behavior Model)
    {
        Debug.Log($"{Model.Stats.Model_Name} has been slain!");

        // Remove from board grid
        Battle_Board.Remove_Model(Model.Current_X, Model.Current_Y);

        // Clear selection if the killed model was selected
        if (Battle_Board.Get_Selected_Model() == Model)
        {
            Battle_Board.Deselect_Current_Model_Public();
        }

        // Deactivate the GameObject
        Model.gameObject.SetActive(false);
    }

    private void Clear_Attack_Highlights()
    {
        var Tiles = Battle_Board.Get_Tiles();
        Material Default_Material = Battle_Board.Get_Tile_Material();

        foreach (Vector2Int Tile in Attack_Range_Highlights)
        {
            if (Tiles[Tile.x, Tile.y] != null)
            {
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Default_Material;
                // Reset alpha
                Color Color = Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material.color;
                Color.a = 1f;
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material.color = Color;
            }
        }

        foreach (Vector2Int Tile in Valid_Target_Highlights)
        {
            if (Tiles[Tile.x, Tile.y] != null)
            {
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material = Default_Material;
                // Reset alpha
                Color Color = Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material.color;
                Color.a = 1f;
                Tiles[Tile.x, Tile.y].GetComponent<MeshRenderer>().material.color = Color;
            }
        }

        Attack_Range_Highlights.Clear();
        Valid_Target_Highlights.Clear();
    }

    public void Force_Clear_Combat_State()
    {
        if (Is_Selecting_Target)
        {
            Clear_Attack_Highlights();
            Is_Selecting_Target = false;
            Attacking_Model = null;
            Debug.Log("Combat state forcefully cleared");
        }
    }


    public bool Is_Currently_Selecting_Target()
    {
        return Is_Selecting_Target;
    }
}