using System;
using System.Collections.Generic;
using Godot;
[Tool]
public partial class BiomeGenerator : Node
{

    [Export] int seed;
    [Export] int grid_size;
    [Export] int biome_map_resolution;

    [Export] public Image map_1_image;

    [Export] public Image map_2_image;

    [Export] float max_overlap_distance;
    [Export] Gradient overlap_gradient;
    [Export] bool run;

    [Export] int width_height;
    [Export] Biome[] biomes;
    public override void _Process(double delta)
    {

        if (run)
        {
            run = false;
            GenerateMaps(0, 0);
        }

        base._Process(delta);
    }

    class GridCell
    {
        public Vector2 pos;
        public Biome biome;

        public GridCell(Vector2 pos, Biome biome)
        {
            this.pos = pos;
            this.biome = biome;
        }
    }
    class Grid
    {
        GridCell[] cells;
        int grid_cells_per_axis;

        public Grid(GridCell[] cells, int grid_cells_per_axis)
        {
            this.cells = cells;
            this.grid_cells_per_axis = grid_cells_per_axis;
        }

        public GridCell this[int x, int y]
        {
            get
            {
                return cells[x + 1 + (y + 1) * grid_cells_per_axis];
            }
        }
    }
    /// map float (expected 0..1) to byte 0..255.
    static byte FloatToByte(float v)
    {
        // TODO: try removing the round operation
        return (byte)MathF.Round(v * 255f);
    }
    class CellDataCombo : IComparable<CellDataCombo>
    {
        public GridCell cell;
        public float distance;
        public float influence;

        public CellDataCombo(GridCell cell, float distance, float influence)
        {
            this.cell = cell;
            this.distance = distance;
            this.influence = influence;
        }

        public int CompareTo(CellDataCombo other)
        {
            return distance.CompareTo(other.distance);
        }
    }
    private Grid GenerateGrid(int grid_cells_per_axis, int x_base, int y_base)
    {
        var cells = new GridCell[(grid_cells_per_axis + 2) * (grid_cells_per_axis + 2)];

        // + 2 to generate position outside of this chunk of terrain, on the: left, right, up, down. This is needed to ensure consistency between chunks.
        for (int x = 0; x < grid_cells_per_axis + 2; x++)
        {
            for (int y = 0; y < grid_cells_per_axis + 2; y++)
            {
                // -1 for the border 
                GD.Seed((ulong)((x + x_base - 1) * (y + y_base - 1) * seed));

                float x_offset = GD.Randf() * grid_size - grid_size / 2f;
                float y_offset = GD.Randf() * grid_size - grid_size / 2f;
                int grid_index = x + y * grid_cells_per_axis;
                Vector2 final_pos = new((x - 1) * grid_size + x_offset, (y - 1) * grid_size + y_offset);

                Biome biome = biomes[GD.Randi() % (biomes.Length)];
                cells[grid_index] = new(final_pos, biome);
            }
        }

        Grid grid = new(cells, grid_cells_per_axis);

        return grid;
    }

    private CellDataCombo HandleCell(int x, int y, GridCell cell)
    {
        float distance = cell.pos.DistanceTo(new(x, y));
        return new(cell, distance, influence: 0/*will be calculated later*/);
    }
    private void GetCellsToCheck(int x, int y, Grid grid, List<CellDataCombo> output)
    {
        int x_grid = x / biome_map_resolution;
        int y_grid = y / biome_map_resolution;

        // really fast because the output list has already allocated the memory
        output.Add(HandleCell(x, y, grid[x_grid - 1, y_grid + 1]));
        output.Add(HandleCell(x, y, grid[x_grid, y_grid + 1]));
        output.Add(HandleCell(x, y, grid[x_grid + 1, y_grid + 1]));
        output.Add(HandleCell(x, y, grid[x_grid - 1, y_grid]));
        output.Add(HandleCell(x, y, grid[x_grid, y_grid]));
        output.Add(HandleCell(x, y, grid[x_grid + 1, y_grid]));
        output.Add(HandleCell(x, y, grid[x_grid - 1, y_grid - 1]));
        output.Add(HandleCell(x, y, grid[x_grid, y_grid - 1]));
        output.Add(HandleCell(x, y, grid[x_grid + 1, y_grid - 1]));
    }



    private void CalculateInfluencesForCeils(
        List<CellDataCombo> neighbors,
        CellDataCombo main, Vector2 pos,
        float[] bakedGradient)
    {
        main.influence = 1.0f;

        // In a reverse order so that we can remove neighbors that doesn't mach our requirements
        for (int i = neighbors.Count - 1; i >= 0; i--)
        {
            handle_neighbour_cell(neighbors, main, pos, bakedGradient, i);
        }
        // normalize
        float sum = main.influence;
        foreach (var n in neighbors)
            sum += n.influence;

        if (sum > 0)
        {
            main.influence /= sum;
            foreach (var n in neighbors)
                n.influence /= sum;
        }
    }

    private void handle_neighbour_cell(List<CellDataCombo> neighbors, CellDataCombo main, Vector2 pos, float[] bakedGradient, int cell_index_in_list)
    {
        var neighbor = neighbors[cell_index_in_list];
        if (neighbor.cell.biome.type_index == main.cell.biome.type_index)
        {
            neighbors.RemoveAt(cell_index_in_list);
            return;
        }

        float distance =
            (pos.DistanceSquaredTo(neighbor.cell.pos) - pos.DistanceSquaredTo(main.cell.pos)) /
            (2.0f * main.cell.pos.DistanceTo(neighbor.cell.pos));

        float abs_distance = Mathf.Abs(distance);

        if (abs_distance >= max_overlap_distance)
        {
            neighbors.RemoveAt(cell_index_in_list);
            return;
        }
        float overlap_percentage = abs_distance / max_overlap_distance; // 0 at boundary

        float influence = bakedGradient[FloatToByte(overlap_percentage)];

        // Pairwise split
        if (distance > 0)
        {
            neighbor.influence = influence;
        }
        else
        {
            neighbor.influence = 0;
            main.influence += influence;
        }
        // neighbor.influence = Mathf.Clamp(neighbor.influence, 0.0f, 1.0f);
        return;
    }

    public void GenerateMaps(int x_base, int y_base)
    {

        int grid_cells_per_axis = Mathf.CeilToInt(width_height / (float)grid_size);
        Grid grid = GenerateGrid(grid_cells_per_axis, x_base, y_base);

        int points_per_axis = grid_cells_per_axis * biome_map_resolution;
        var map_1_data = new byte[points_per_axis * points_per_axis * 4];
        var map_2_data = new byte[points_per_axis * points_per_axis * 4];

        float[] backed_gradient = bake_gradient();

        // alloc heap once
        List<CellDataCombo> cells = new(9);
        for (int x = 0; x < points_per_axis; x++)
        {
            for (int y = 0; y < points_per_axis; y++)
            {
                GetCellsToCheck(x, y, grid, cells);
                cells.Sort();

                var main_cell = cells[0]; cells.RemoveAt(0);
                CalculateInfluencesForCeils(cells, main_cell, new(x, y), backed_gradient);

                cells.Add(main_cell);
                int base_index = (x + y * points_per_axis) * 4;

                foreach (CellDataCombo cell in cells)
                {
                    // using big ass switch statement would be faster, especially when using more than 2 textures but this is cleaner, so choose your poison.
                    var map = cell.cell.biome.type_index / 4 == 0 ? map_1_data : map_2_data;
                    int index = cell.cell.biome.type_index % 4;
                    map[base_index + index] = FloatToByte(cell.influence);
                }
                cells.Clear();
            }
        }

        map_1_image = Image.CreateFromData(points_per_axis, points_per_axis, false, Image.Format.Rgba8, map_1_data);
        map_2_image = Image.CreateFromData(points_per_axis, points_per_axis, false, Image.Format.Rgba8, map_2_data);
    }

    private float[] bake_gradient()
    {
        var backed_gradient = new float[256];
        for (int i = 0; i < 256; i++)
        {
            backed_gradient[i] = overlap_gradient.Sample(i / 255f).R;
        }

        return backed_gradient;
    }
}
