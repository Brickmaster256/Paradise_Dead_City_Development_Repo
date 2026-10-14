using UnityEngine;

[CreateAssetMenu(fileName = "Death on Wheels",
                 menuName = "Scriptable Objects/Abilities/Death on Wheels")]
public class Death_On_Wheels_Ability : Ability_Definition_SO
{
    [Header("Death on Wheels")]
    [Tooltip("Movement multiplier while active. 2 doubles the model's " +
             "base movement.")]
    [Range(1f, 4f)]
    public float Movement_Multiplier = 2f;

    [Tooltip("Damage dealt to each enemy shoved during the move.")]
    [Range(1, 3)]
    public int Shove_Damage = 1;

    [Tooltip("How many tiles each shoved enemy is pushed.")]
    [Range(1, 4)]
    public int Shove_Push = 2;

    public override bool Can_Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        if (Model == null || Board == null) return false;
        if (Model.Ability_Recharge_Remaining > 0) return false;
        if (Model.Has_Status(Status_Type.Death_On_Wheels)) return false;
        return true;
    }

    public override void Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        // Set movement directly rather than adding a bonus. This makes
        // "doubled movement" a fixed value regardless of how much the
        // model has already spent.
        int New_Movement = Mathf.RoundToInt(Model.Stats.Movement_Range * Movement_Multiplier);
        Model.Movement_Remaining_This_Turn = New_Movement;

        Model.Add_Status(Status_Type.Death_On_Wheels, 1);
        Model.Ability_Recharge_Remaining = Mathf.Max(Recharge_Turns, 1);

        Board.Refresh_Movement_Range_Highlights(Model);

        Debug.Log($"[Death on Wheels] {Model.Stats.Model_Name} revs: " +
                  $"{New_Movement} movement, chain shove active.");
    }
}