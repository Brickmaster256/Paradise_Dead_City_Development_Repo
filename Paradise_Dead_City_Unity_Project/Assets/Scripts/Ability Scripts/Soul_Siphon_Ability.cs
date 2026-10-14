using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Soul Siphon",
                 menuName = "Scriptable Objects/Abilities/Soul Siphon")]
public class Soul_Siphon_Ability : Ability_Definition_SO
{
    [Header("Soul Siphon")]
    [Tooltip("Maximum distance from the Psychopomp to a HUSK or to an " +
             "eligible ally, in tiles.")]
    [Range(1, 8)]
    public int Siphon_Range = 3;

    public override bool Can_Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        if (Model == null || Board == null) return false;
        if (Model.Ability_Recharge_Remaining > 0) return false;

        // Both a HUSK and an eligible recipient must be in range, or the
        // ability would open a targeting phase with no valid play.
        if (Find_Husk_Tiles_In_Range(Model, Board).Count == 0) return false;
        if (Find_Eligible_Ally_Tiles_In_Range(Model, Board).Count == 0) return false;

        return true;
    }

    public override void Activate(Model_Standard_Behavior Model, Battle_Board_Behavior Board)
    {
        var Targeting = Board.Get_Ability_Targeting_Controller();
        if (Targeting == null)
        {
            Debug.LogWarning("Soul Siphon: no Ability_Targeting_Controller on the board.");
            return;
        }

        var Husk_Tiles = Find_Husk_Tiles_In_Range(Model, Board);
        var Ally_Tiles = Find_Eligible_Ally_Tiles_In_Range(Model, Board);

        int Max = Mathf.Min(Husk_Tiles.Count, Ally_Tiles.Count);
        if (Max <= 0) return;

        // ---- Phase 1: choose HUSKs to siphon. ----
        // On confirm, chain into Phase 2 with the HUSK choice captured in
        // the closure. Nothing is applied to the world until Phase 2
        // confirms, so cancelling at any point is a true no-op.
        Targeting.Begin(
            Model,
            Siphon_Range,
            Husk_Tiles,
            Max,
            Husk_Chosen => Begin_Phase_2(Model, Board, Targeting, Husk_Chosen));
    }

    private void Begin_Phase_2(
        Model_Standard_Behavior Model,
        Battle_Board_Behavior Board,
        Ability_Targeting_Controller Targeting,
        List<Vector2Int> Husk_Chosen)
    {
        if (Husk_Chosen == null || Husk_Chosen.Count == 0) return;

        var Ally_Tiles = Find_Eligible_Ally_Tiles_In_Range(Model, Board);
        if (Ally_Tiles.Count == 0) return;

        // ---- Phase 2: choose allies to receive the buffs. ----
        // Cap equals the HUSK count from Phase 1, so the pairing is 1:1.
        Targeting.Begin(
            Model,
            Siphon_Range,
            Ally_Tiles,
            Husk_Chosen.Count,
            Ally_Chosen => Resolve(Model, Board, Husk_Chosen, Ally_Chosen));
    }

    private void Resolve(
        Model_Standard_Behavior Model,
        Battle_Board_Behavior Board,
        List<Vector2Int> Husk_Tiles,
        List<Vector2Int> Ally_Tiles)
    {
        // Pair index i: HUSK i takes the damage, ally i gets the buff.
        // Both lists come out of HashSet iteration in the targeting
        // controller, so their order isn't guaranteed stable across runs
        // — but the *pairing* is well-defined as "same index."
        int Pairs = Mathf.Min(Husk_Tiles.Count, Ally_Tiles.Count);
        if (Pairs == 0) return;

        var Combat = Board.Get_Combat_Manager();
        int Damaged = 0;
        int Granted = 0;

        for (int i = 0; i < Pairs; i++)
        {
            var Husk = Board.Get_Model_At(Husk_Tiles[i].x, Husk_Tiles[i].y);
            var Ally = Board.Get_Model_At(Ally_Tiles[i].x, Ally_Tiles[i].y);

            if (Husk != null)
            {
                bool Survived = Husk.Take_Damage(1);
                Damaged++;

                if (!Survived && Combat != null)
                    Combat.Kill_Model(Husk);
            }

            if (Ally != null)
            {
                Ally.Add_Status(Status_Type.Advantage, 1);
                Granted++;
            }
        }

        Model.Ability_Recharge_Remaining = Mathf.Max(Recharge_Turns, 1);

        Debug.Log($"[Soul Siphon] Damaged {Damaged} HUSK(s), granted {Granted} Advantage(s).");
    }

    // ============================================================
    // QUERIES
    // ============================================================

    private List<Vector2Int> Find_Husk_Tiles_In_Range(
        Model_Standard_Behavior Source,
        Battle_Board_Behavior Board)
    {
        return Find_Tiles_In_Range(Source, Board, M => M.Type == Model_Type.Chaff);
    }

    private List<Vector2Int> Find_Eligible_Ally_Tiles_In_Range(
        Model_Standard_Behavior Source,
        Battle_Board_Behavior Board)
    {
        // Allied non-HUSKs other than the Psychopomp itself.
        return Find_Tiles_In_Range(
            Source,
            Board,
            M => M.Team == Source.Team && M != Source && M.Type != Model_Type.Chaff);
    }

    private List<Vector2Int> Find_Tiles_In_Range(
        Model_Standard_Behavior Source,
        Battle_Board_Behavior Board,
        System.Func<Model_Standard_Behavior, bool> Filter)
    {
        var Result = new List<Vector2Int>();
        if (Source == null || Board == null) return Result;

        int Count_X = Board.Get_Tile_Count_X();
        int Count_Y = Board.Get_Tile_Count_Y();

        for (int X = 0; X < Count_X; X++)
        {
            for (int Y = 0; Y < Count_Y; Y++)
            {
                var M = Board.Get_Model_At(X, Y);
                if (M == null || !M.gameObject.activeSelf) continue;
                if (M == Source) continue;

                int Dist = Mathf.Abs(X - Source.Current_X) + Mathf.Abs(Y - Source.Current_Y);
                if (Dist > Siphon_Range) continue;

                if (!Filter(M)) continue;
                Result.Add(new Vector2Int(X, Y));
            }
        }

        return Result;
    }
}