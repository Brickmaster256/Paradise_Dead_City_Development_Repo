using System.Collections.Generic;
using UnityEngine;

public enum Model_Type
{
    Chaff = 0,
    Specialist_A = 1,
    Specialist_B = 2,
    Axillary = 3,
    DeathHead = 4
}

public class Model_Standard_Behavior : MonoBehaviour
{
    // ============================================================
    // IDENTITY
    // ============================================================

    public int Team;
    public int Current_X;
    public int Current_Y;
    public Model_Type Type;

    // ============================================================
    // ABILITY
    // ============================================================

    // Assigned at spawn from Stats.Ability_Data. May be null.
    public Ability_Definition_SO Ability;

    // Turns remaining before the ability can be used again. 0 means
    // ready. Ticked at the start of the owner's turn.
    public int Ability_Recharge_Remaining;

    public bool Ability_Is_Ready => Ability_Recharge_Remaining <= 0;

    /// <summary>
    /// Ticks the recharge counter down by one, floored at zero. Called
    /// from Battle_Board_Behavior at the start of the owner's turn.
    /// </summary>
    public void Tick_Ability_Recharge()
    {
        if (Ability_Recharge_Remaining > 0)
            Ability_Recharge_Remaining--;
    }

    // ============================================================
    // STATS
    // ============================================================

    public Model_Stats_SO Stats;

    // ============================================================
    // CURRENT STATE
    // ============================================================

    public int Current_Health;
    public int Movement_Remaining_This_Turn;
    public bool Has_Attacked_This_Turn;
    public bool Has_Ended_Turn;
    public int Movement_Before_Sprint;
    public bool Is_Sprinting_This_Turn;
    public bool Has_Moved_Since_Sprint;
    public bool Has_Used_Sprint_Bonus;

    // ============================================================
    // STATUSES
    // ============================================================

    private Status_Registry_SO Status_Registry;
    private readonly List<Status_Instance> Active_Statuses = new List<Status_Instance>();

    private readonly List<Status_Instance> Display_Statuses = new List<Status_Instance>();

    public IReadOnlyList<Status_Instance> Get_Active_Statuses()
    {
        Display_Statuses.Clear();

        // Passive abilities are always-on. Synthesize a permanent entry
        // here rather than storing one in Active_Statuses, so it never
        // ticks down or gets removed by Tick_Statuses.
        if (Stats != null
            && Stats.Ability_Data != null
            && Stats.Ability_Data.Kind == Ability_Kind.Passive
            && Stats.Ability_Data.Status_Display != null)
        {
            Display_Statuses.Add(new Status_Instance(
                Status_Type.Passive_Ability,
                Stats.Ability_Data.Status_Display,
                int.MaxValue));
        }

        Display_Statuses.AddRange(Active_Statuses);
        return Display_Statuses;
    }

    /// <summary>
    /// Called by Battle_Board_Behavior at spawn so the model can look up
    /// definitions when a status is added. If this is never set, statuses
    /// still track turns but render with the gray fallback icon.
    /// </summary>
    public void Set_Status_Registry(Status_Registry_SO Registry)
    {
        Status_Registry = Registry;
    }

    public void Add_Status(Status_Type Type, int Turns = 1)
    {
        if (Turns <= 0) return;

        for (int i = 0; i < Active_Statuses.Count; i++)
        {
            if (Active_Statuses[i].Type == Type)
            {
                Status_Instance Existing = Active_Statuses[i];
                Existing.Turns_Remaining += Turns;
                Active_Statuses[i] = Existing;
                return;
            }
        }

        Status_Definition_SO Def = Status_Registry != null ? Status_Registry.Get(Type) : null;
        Active_Statuses.Add(new Status_Instance(Type, Def, Turns));
    }

    public void Remove_Status(Status_Type Type)
    {
        for (int i = Active_Statuses.Count - 1; i >= 0; i--)
            if (Active_Statuses[i].Type == Type)
                Active_Statuses.RemoveAt(i);
    }

    public bool Has_Status(Status_Type Type)
    {
        for (int i = 0; i < Active_Statuses.Count; i++)
            if (Active_Statuses[i].Type == Type)
                return true;
        return false;
    }

    /// <summary>
    /// Decrements every active status by one turn and removes any that
    /// reach zero. Called by Battle_Board_Behavior at the start of this
    /// model's owner's turn.
    /// </summary>
    public void Tick_Statuses()
    {
        for (int i = Active_Statuses.Count - 1; i >= 0; i--)
        {
            Status_Instance S = Active_Statuses[i];
            S.Turns_Remaining--;
            if (S.Turns_Remaining <= 0)
                Active_Statuses.RemoveAt(i);
            else
                Active_Statuses[i] = S;
        }
    }

    // ---- Vulnerability is a status under the hood -------------------

    public bool Is_Vulnerable => Has_Status(Status_Type.Vulnerable);

    /// <summary>
    /// Adds N turn-units of vulnerability. Sources stack additively on
    /// duration, not on modifier magnitude (the modifier is always ±1
    /// per the non-stacking attack rule).
    /// </summary>
    public void Apply_Vulnerability(int Sources = 1)
    {
        Add_Status(Status_Type.Vulnerable, Sources);
    }

    // ============================================================
    // SMOOTH MOTION
    // ============================================================

    [Header("Smooth Motion")]
    [SerializeField] private float Move_Duration = 1.0f;
    [SerializeField] private float Follow_Speed = 15f;
    [SerializeField] private bool Ease_Discrete_Moves = true;

    private Vector3 Target_Position;
    private Vector3 Tween_Start_Position;
    private float Tween_Elapsed;
    private float Tween_Duration;
    private bool Is_Tweening;
    private bool Is_Following;

    private void Update()
    {
        if (Is_Tweening)
        {
            Tween_Elapsed += Time.deltaTime;
            float t = Tween_Duration > 0f ? Mathf.Clamp01(Tween_Elapsed / Tween_Duration) : 1f;

            if (Ease_Discrete_Moves)
                t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(Tween_Start_Position, Target_Position, t);

            if (Tween_Elapsed >= Tween_Duration)
            {
                transform.position = Target_Position;
                Is_Tweening = false;
            }
            return;
        }

        if (Is_Following)
        {
            transform.position = Vector3.Lerp(transform.position, Target_Position, Time.deltaTime * Follow_Speed);

            if (Vector3.SqrMagnitude(transform.position - Target_Position) <= 0.000001f)
            {
                transform.position = Target_Position;
                Is_Following = false;
            }
        }
    }

    public void Set_Position(Vector3 Position, bool Force = false)
    {
        Target_Position = Position;
        Is_Following = false;

        if (Force)
        {
            transform.position = Target_Position;
            Is_Tweening = false;
            return;
        }

        Tween_Start_Position = transform.position;
        Tween_Elapsed = 0f;
        Tween_Duration = Move_Duration;
        Is_Tweening = true;
    }

    public void Follow_Position(Vector3 Position)
    {
        Target_Position = Position;
        Is_Tweening = false;
        Is_Following = true;
    }

    public bool Take_Damage(int Amount)
    {
        if (Amount <= 0)
            return Current_Health > 0;

        Current_Health -= Amount;
        return Current_Health > 0;
    }

    public void Configure_Motion(float Move_Duration_Override, float Follow_Speed_Override, bool? Ease_Discrete_Moves_Override)
    {
        if (Move_Duration_Override >= 0f)
            Move_Duration = Move_Duration_Override;

        if (Follow_Speed_Override > 0f)
            Follow_Speed = Follow_Speed_Override;

        if (Ease_Discrete_Moves_Override.HasValue)
            Ease_Discrete_Moves = Ease_Discrete_Moves_Override.Value;
    }
}