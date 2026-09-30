using UnityEngine;

[CreateAssetMenu(fileName = "New Model Stats", menuName = "Scriptable Objects/Model Stats")]
public class Model_Stats_SO : ScriptableObject
{
    // ============================================================
    // IDENTITY
    // ============================================================

    [Header("Identity")]
    public string Model_Name;
    public Model_Type Type;


    // ============================================================
    // BASE STATS
    // ============================================================

    [Header("Base Stats")]
    [Tooltip("Maximum health. Current health is tracked on the model instance.")]
    public int Health = 2;

    [Tooltip("Movement range in tiles (Manhattan distance).")]
    [Range(1, 8)] public int Movement_Range = 2;


    // ============================================================
    // COMBAT STATS (D6 SYSTEM)
    // ============================================================

    [Header("Combat Stats (D6 System)")]
    [Tooltip("Number of armor saves this model gets.")]
    public int Armor_Saves = 1;

    [Tooltip("D6 roll needed for armor save (e.g., 4 means 4+).")]
    [Range(1, 6)] public int Armor_Target = 4;

    [Tooltip("D6 roll needed to hit (e.g., 3 means 3+).")]
    [Range(1, 6)] public int Attack_Skill = 3;

    [Tooltip("Damage dealt on successful attack.")]
    public int Attack_Damage = 1;

    [Tooltip("Range in tiles (1 for melee, 2+ for ranged/large models).")]
    [Range(1, 8)] public int Attack_Range = 1;

    // ============================================================
    // SHOVE RULES
    // ============================================================

    [Header("Shove Rules")]
    [Tooltip("Whether this model can shove enemies when it sprints into them.")]
    public bool Can_Shove = true;

    [Tooltip("How many tiles this model pushes an enemy when it shoves.")]
    [Range(0, 4)] public int Shove_Distance = 1;

    // ============================================================
    // MOVEMENT RULES
    // ============================================================

    [Header("Model Movement")]
    [Tooltip("Extra movement granted when sprinting. Total is capped at Sprint_Range_Cap.")]
    [Range(0, 6)] public int Sprint_Bonus = 2;


    // ============================================================
    // SPECIAL RULES
    // ============================================================

    [Header("Model Special Rules")]
    public bool Is_Ranged = false;
    public bool Has_Splash_Damage = false;


    // ============================================================
    // SPECIAL ABILITIES
    // ============================================================

    [Header("Model Special Abilities")]
    // Placeholder flag for a future ability system. Currently only
    // used by the card UI to decide whether to show the "Special
    // Ability" description line.
    public bool Has_Ability = false;


    // ============================================================
    // RANGE QUERIES
    // ============================================================

    /// <summary>
    /// Maximum tiles a model can cover in a single sprint, regardless of base movement + bonus.
    /// </summary>

    public const int Sprint_Range_Cap = 6;

    public int Get_Sprint_Range()
    {
        return Mathf.Min(Movement_Range + Sprint_Bonus, Sprint_Range_Cap);
    }

    public bool Is_Within_Movement_Range(int Current_X, int Current_Y, int Target_X, int Target_Y)
    {
        return Manhattan_Distance(Current_X, Current_Y, Target_X, Target_Y) <= Movement_Range;
    }

    public bool Is_In_Attack_Range(int Current_X, int Current_Y, int Target_X, int Target_Y)
    {
        return Manhattan_Distance(Current_X, Current_Y, Target_X, Target_Y) <= Attack_Range;
    }

    /// <summary>
    /// Orthogonal distance between two tiles, ignoring terrain and
    /// intermediate tiles. Used for both movement and attack range
    /// checks.
    /// </summary>
    public static int Manhattan_Distance(int From_X, int From_Y, int To_X, int To_Y)
    {
        return Mathf.Abs(To_X - From_X) + Mathf.Abs(To_Y - From_Y);
    }
}