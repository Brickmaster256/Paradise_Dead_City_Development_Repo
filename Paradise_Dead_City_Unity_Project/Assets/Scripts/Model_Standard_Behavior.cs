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
    public bool Is_Sprinting_This_Turn;

    // ============================================================
    // SMOOTH MOTION
    // ============================================================

    [Tooltip("Higher values snap the model to its target position faster. Multiplied by Time.deltaTime each frame.")]
    [SerializeField] private float Movement_Lerp_Speed = 10f;

    // Distance below which the model snaps to the target and stops lerping.
    private const float Arrival_Epsilon = 0.001f;

    private Vector3 Target_Position;
    private bool Is_Moving = false;

    private void Update()
    {
        if (!Is_Moving)
            return;

        transform.position = Vector3.Lerp(transform.position, Target_Position, Time.deltaTime * Movement_Lerp_Speed);

        if (Vector3.SqrMagnitude(transform.position - Target_Position) <= Arrival_Epsilon * Arrival_Epsilon)
        {
            transform.position = Target_Position;
            Is_Moving = false;
        }
    }

    public void Set_Position(Vector3 Position, bool Force = false)
    {
        Target_Position = Position;

        if (Force)
        {
            transform.position = Target_Position;
            Is_Moving = false;
        }
        else
        {
            Is_Moving = true;
        }
    }

    // ============================================================
    // MOVEMENT QUERIES
    // ============================================================

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
}