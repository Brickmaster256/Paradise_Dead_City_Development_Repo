using System;
using System.Collections.Generic;
using UnityEngine;

public class Ability_Targeting_Controller : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Battle_Board_Behavior Board;

    [Header("Materials")]
    [Tooltip("Applied to every tile within range that is NOT a candidate " +
             "(empty ground, allies, walls — anything not selectable).")]
    [SerializeField] private Material Range_Tile_Material;

    [Tooltip("Applied to in-range tiles that contain a selectable target.")]
    [SerializeField] private Material Candidate_Tile_Material;

    [Tooltip("Applied to a candidate tile while the cursor is over it.")]
    [SerializeField] private Material Hover_Tile_Material;

    [Tooltip("Applied to candidates the player has chosen.")]
    [SerializeField] private Material Selected_Tile_Material;

    [Header("Flashing")]
    [SerializeField] private float Flash_Speed = 2.0f;
    [SerializeField] private float Flash_Min_Alpha = 0.4f;
    [SerializeField] private float Flash_Max_Alpha = 1.0f;

    // Runtime
    private bool Is_Active;
    private Model_Standard_Behavior Source;
    private List<Vector2Int> Range_Tiles = new List<Vector2Int>();
    private HashSet<Vector2Int> Candidate_Tiles = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> Selected_Tiles = new HashSet<Vector2Int>();
    private int Max_Selections = int.MaxValue;
    private Action<List<Vector2Int>> On_Confirm;
    private Action On_Cancel;
    private Vector2Int Hover_Tile = new Vector2Int(-1, -1);

    public bool Is_Currently_Targeting() => Is_Active;

    /// <summary>
    /// Begins a targeting session. Range_Tiles is computed from the source
    /// position and the given range; Candidate_Tiles is the subset the
    /// player can actually toggle. On confirm, the callback receives the
    /// chosen tile coordinates.
    /// </summary>
    public bool Begin(
        Model_Standard_Behavior Source_Model,
        int Range,
        List<Vector2Int> Candidate_Tile_List,
        int Max_Selections_Limit,
        Action<List<Vector2Int>> On_Confirm_Callback,
        Action On_Cancel_Callback = null)
    {
        if (Is_Active) return false;
        if (Source_Model == null || Board == null) return false;

        Source = Source_Model;
        Max_Selections = Max_Selections_Limit <= 0 ? int.MaxValue : Max_Selections_Limit;
        On_Confirm = On_Confirm_Callback;
        On_Cancel = On_Cancel_Callback;

        // Suppress any movement / path highlights the board is showing so
        // the two visual systems don't fight.
        Board.Clear_Movement_Highlights_Public();

        Range_Tiles.Clear();
        Candidate_Tiles.Clear();
        Selected_Tiles.Clear();
        Hover_Tile = new Vector2Int(-1, -1);

        int Count_X = Board.Get_Tile_Count_X();
        int Count_Y = Board.Get_Tile_Count_Y();

        for (int X = 0; X < Count_X; X++)
        {
            for (int Y = 0; Y < Count_Y; Y++)
            {
                int Dist = Mathf.Abs(X - Source.Current_X) + Mathf.Abs(Y - Source.Current_Y);
                if (Dist == 0) continue;
                if (Dist > Range) continue;

                Range_Tiles.Add(new Vector2Int(X, Y));
            }
        }

        if (Candidate_Tile_List != null)
        {
            foreach (var Tile in Candidate_Tile_List)
                Candidate_Tiles.Add(Tile);
        }

        Paint_All();
        Is_Active = true;
        return true;
    }

    public void Confirm()
    {
        if (!Is_Active) return;

        var Chosen = new List<Vector2Int>(Selected_Tiles);
        var Callback = On_Confirm;

        End();
        Callback?.Invoke(Chosen);
    }

    public void Cancel()
    {
        if (!Is_Active) return;

        var Callback = On_Cancel;
        End();
        Callback?.Invoke();
    }

    public void Handle_Tile_Click(Vector2Int Tile)
    {
        if (!Is_Active) return;
        if (!Candidate_Tiles.Contains(Tile)) return;

        if (Selected_Tiles.Contains(Tile))
        {
            Selected_Tiles.Remove(Tile);
        }
        else
        {
            if (Selected_Tiles.Count >= Max_Selections) return;
            Selected_Tiles.Add(Tile);
        }

        Paint_Tile(Tile);
    }

    public void Handle_Tile_Hover(Vector2Int Tile)
    {
        if (!Is_Active) return;
        if (Tile == Hover_Tile) return;

        Vector2Int Previous = Hover_Tile;
        Hover_Tile = Tile;

        if (Previous.x >= 0 && Range_Tiles.Contains(Previous))
            Paint_Tile(Previous);

        if (Tile.x >= 0 && Range_Tiles.Contains(Tile))
            Paint_Tile(Tile);
    }

    private void End()
    {
        Is_Active = false;

        foreach (var Tile in Range_Tiles)
            Reset_Tile(Tile);

        Range_Tiles.Clear();
        Candidate_Tiles.Clear();
        Selected_Tiles.Clear();
        Hover_Tile = new Vector2Int(-1, -1);
        Source = null;
        On_Confirm = null;
        On_Cancel = null;
    }

    private void Update()
    {
        if (!Is_Active) return;

        float Alpha = Mathf.Lerp(Flash_Min_Alpha, Flash_Max_Alpha,
            (Mathf.Sin(Time.time * Flash_Speed) + 1.0f) * 0.5f);

        foreach (var Tile in Range_Tiles)
        {
            var Renderer = Get_Renderer(Tile);
            if (Renderer == null || Renderer.material == null) continue;

            Color C = Renderer.material.color;
            C.a = Alpha;
            Renderer.material.color = C;
        }
    }

    private void Paint_All()
    {
        foreach (var Tile in Range_Tiles)
            Paint_Tile(Tile);
    }

    private void Paint_Tile(Vector2Int Tile)
    {
        Material Mat = Get_Material_For_Tile(Tile);
        if (Mat == null) return;

        var Renderer = Get_Renderer(Tile);
        if (Renderer == null) return;

        Renderer.material = Mat;
        Color C = Renderer.material.color;
        C.a = 1f;
        Renderer.material.color = C;
    }

    private Material Get_Material_For_Tile(Vector2Int Tile)
    {
        if (Selected_Tiles.Contains(Tile))
            return Selected_Tile_Material != null ? Selected_Tile_Material : Candidate_Tile_Material;

        if (!Candidate_Tiles.Contains(Tile))
            return Range_Tile_Material;

        if (Tile == Hover_Tile && Hover_Tile_Material != null)
            return Hover_Tile_Material;

        return Candidate_Tile_Material != null ? Candidate_Tile_Material : Range_Tile_Material;
    }

    private void Reset_Tile(Vector2Int Tile)
    {
        var Renderer = Get_Renderer(Tile);
        if (Renderer == null) return;

        Renderer.material = Board.Get_Tile_Material();
        Color C = Renderer.material.color;
        C.a = 1f;
        Renderer.material.color = C;
    }

    private MeshRenderer Get_Renderer(Vector2Int Tile)
    {
        var Tiles = Board.Get_Tiles();
        if (Tiles == null) return null;
        if (Tile.x < 0 || Tile.x >= Tiles.GetLength(0)) return null;
        if (Tile.y < 0 || Tile.y >= Tiles.GetLength(1)) return null;

        var GO = Tiles[Tile.x, Tile.y];
        if (GO == null) return null;
        return GO.GetComponent<MeshRenderer>();
    }
}