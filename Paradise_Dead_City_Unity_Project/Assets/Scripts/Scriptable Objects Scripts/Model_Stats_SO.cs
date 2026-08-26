using UnityEngine;

[CreateAssetMenu(fileName = "New Model Stats", menuName = "Scriptable Objects/Model Stats")]
public class Model_Stats_SO : ScriptableObject
{
    [Header("Model Name")]
    public string Model_Name;
    public Model_Type Type;

    [Header("Model Stats")]
    public int Health = 2;

    [Header("Combat Stats (D6 System)")]
    [Tooltip("Number of armor saves this model gets")]
    public int Armor_Saves = 1;
    [Tooltip("D6 roll needed for armor save (e.g., 4 means 4+)")]
    [Range(1, 6)] public int Armor_Target = 4;
    [Tooltip("D6 roll needed to hit (e.g., 3 means 3+)")]
    [Range(1, 6)] public int Attack_Skill = 3;
    [Tooltip("Damage dealt on successful attack")]
    public int Attack_Damage = 1;
    [Tooltip("Range in tiles (1 for melee, 2+ for ranged/large models)")]
    public int Attack_Range = 1;

    [Header("Model Movement")]
    public int Movement_Range = 2;

    [Header("Model Special Rules")]
    public bool Is_Ranged = false;
    public bool Has_Splash_Damage = false;

    [Header("Model Special Abilities")]
    public bool Has_Ability = false;

    // Is in movement range?
    public bool Is_Witin_Movement_Range(int Current_X, int Current_Y, int Target_X, int Target_Y)
    {
        int Distance = Mathf.Abs(Target_X - Current_X) + Mathf.Abs(Target_Y - Current_Y);
        return Distance <= Movement_Range;
    }

    // Check if target is in attack range
    public bool Is_In_Attack_Range(int Current_X, int Current_Y, int Target_X, int Target_Y)
    {
        int Distance = Mathf.Abs(Target_X - Current_X) + Mathf.Abs(Target_Y - Current_Y);
        return Distance <= Attack_Range;
    }

}
