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
    // STATS
    // ============================================================

    // Assigned by Battle_Board_Behavior when the model is spawned.
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

    // ============================================================
    // VULNERABILITY
    // ============================================================

    // Number of the owner's turn-starts remaining before vulnerability
    // expires. 0 = not vulnerable. Two simultaneous sources set this to 2
    // so it survives twice as long (per design: duration scales with source
    // count). Decremented by Battle_Board_Behavior at the start of the
    // owner's turn.
    public int Vulnerability_Turns_Remaining;

    public bool Is_Vulnerable => Vulnerability_Turns_Remaining > 0;

    /// <summary>
    /// Adds N turn-units of vulnerability. Sources stack additively on the
    /// duration, not on the modifier magnitude (the modifier is always ±1,
    /// per the non-stacking attack rule).
    /// </summary>
    public void Apply_Vulnerability(int Sources = 1)
    {
        if (Sources <= 0) return;
        Vulnerability_Turns_Remaining += Sources;
    }

    // ============================================================
    // SMOOTH MOTION
    // ============================================================

    [Header("Smooth Motion")]
    [Tooltip("Time in seconds for a discrete board move (Set_Position with Force=false). " +
             "This is the shove/step glide. ~1.0 gives a visible hover-settle.")]
    [SerializeField] private float Move_Duration = 1.0f;

    [Tooltip("Exponential follow speed used during drag (Follow_Position). " +
             "Higher is snappier cursor-tracking.")]
    [SerializeField] private float Follow_Speed = 15f;

    [Tooltip("If true, discrete moves ease in and out (SmoothStep). " +
             "If false, discrete moves are linear.")]
    [SerializeField] private bool Ease_Discrete_Moves = true;

    private Vector3 Target_Position;

    // Discrete-move tween state.
    private Vector3 Tween_Start_Position;
    private float Tween_Elapsed;
    private float Tween_Duration;
    private bool Is_Tweening;

    // Drag-follow state.
    private bool Is_Following;

    private void Update()
    {
        if (Is_Tweening)
        {
            Tween_Elapsed += Time.deltaTime;
            float t = Tween_Duration > 0f ? Mathf.Clamp01(Tween_Elapsed / Tween_Duration) : 1f;

            if (Ease_Discrete_Moves)
                t = t * t * (3f - 2f * t); // smoothstep

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

    /// <summary>
    /// Discrete board move. Force = true snaps instantly. Force = false
    /// runs a fixed-duration eased glide (Move_Duration seconds).
    /// Cancels any in-flight drag-follow.
    /// </summary>
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

    /// <summary>
    /// Continuous cursor-follow used during drag. Fast exponential lerp so
    /// the model tracks the mouse with a slight hover lag. Cancels any
    /// in-flight discrete tween.
    /// </summary>
    public void Follow_Position(Vector3 Position)
    {
        Target_Position = Position;
        Is_Tweening = false;
        Is_Following = true;
    }

    /// <summary>
    /// Applies damage directly to this model. Returns true if the model
    /// survived, false if its health dropped to 0 or below. The caller is
    /// responsible for triggering the kill/cleanup flow when this returns false.
    /// </summary>
    public bool Take_Damage(int Amount)
    {
        if (Amount <= 0)
            return Current_Health > 0;

        Current_Health -= Amount;
        return Current_Health > 0;
    }

    /// <summary>
    /// Overrides the motion tuning normally set per-prefab. Called by
    /// Battle_Board_Behavior at spawn time so the board's inspector values
    /// win. Pass -1 for any argument to leave that value untouched.
    /// </summary>
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