using System;
using System.Collections.Generic;
using Godot;
[Tool]
public partial class BiomeGenerator : Node
{

    [Export] int seed;
    [Export] int grid_size;
    [Export] int biome_map_resolution;

    [Export] float max_overlap_distance;
    [Export] Gradient overlap_gradient;
    [Export] bool run;

    [Export] Biome[] biomes;

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
        int grid_stride;

        public Grid(GridCell[] cells, int grid_cells_per_axis)
        {
            this.cells = cells;
            this.grid_stride = grid_cells_per_axis;
        }

        public GridCell this[int x, int y]
        {
            get
            {
                return cells[x + 1 + (y + 1) * grid_stride];
            }
        }
    }
    /// map float (expected 0..1) to byte 0..255.
    static byte FloatToByte(float v)
    {
        return (byte)MathF.Round(v * 255f);
    }

    /// map float (expected 0..1) to byte 0..255.
    static float ByteToFloat(byte v)
    {
        return v / 255f;
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
    private Grid GenerateGrid(int grid_cells_per_axis, int x_base, int y_base, int grid_stride)
    {
        var cells = new GridCell[(grid_stride) * (grid_stride)];

        // + 2 to generate position outside of this chunk of terrain, on the: left, right, up, down. This is needed to ensure consistency between chunks.
        for (int x = 0; x < grid_stride; x++)
        {
            for (int y = 0; y < grid_stride; y++)
            {
                int world_x = x_base + (x - 1) * grid_size;
                int world_y = y_base + (y - 1) * grid_size;
                // -1 for the border 
                ulong s =
    (ulong)seed ^
    (ulong)world_x * 73856093UL ^
    (ulong)world_y * 19349663UL;

                GD.Seed(s);

                float x_offset = GD.Randf() * grid_size * 0.5f;
                float y_offset = GD.Randf() * grid_size * 0.5f;
                int grid_index = x + y * (grid_stride);
                // Vector2 final_pos = new(world_x + x_offset, world_y + y_offset);

                Vector2 final_pos = new(world_x, world_y);

                Biome biome = biomes[GD.Randi() % (biomes.Length)];
                cells[grid_index] = new(final_pos, biome);
            }
        }

        Grid grid = new(cells, grid_stride);

        return grid;
    }

    // private CellDataCombo HandleCell(int x, int y, GridCell cell)
    // {
    //
    //     float distance = cell.pos.DistanceTo(new(x, y));
    //     return new(cell, distance, influence: 0/*will be calculated later*/);
    // }
    // private void GetCellsToCheck(int x, int y, Grid grid, List<CellDataCombo> output)
    // {
    //     int x_grid = x / grid_size;
    //     int y_grid = y / grid_size;
    //
    //     // really fast because the output list has already allocated the memory
    //     output.Add(HandleCell(x, y, grid[x_grid - 1, y_grid + 1]));
    //     output.Add(HandleCell(x, y, grid[x_grid, y_grid + 1]));
    //     output.Add(HandleCell(x, y, grid[x_grid + 1, y_grid + 1]));
    //     output.Add(HandleCell(x, y, grid[x_grid - 1, y_grid]));
    //     output.Add(HandleCell(x, y, grid[x_grid, y_grid]));
    //     output.Add(HandleCell(x, y, grid[x_grid + 1, y_grid]));
    //     output.Add(HandleCell(x, y, grid[x_grid - 1, y_grid - 1]));
    //     output.Add(HandleCell(x, y, grid[x_grid, y_grid - 1]));
    //     output.Add(HandleCell(x, y, grid[x_grid + 1, y_grid - 1]));
    // }

    private CellDataCombo HandleCell(Vector2 world_pos, GridCell cell)
    {
        float distance = cell.pos.DistanceTo(world_pos);
        return new(cell, distance, influence: 0/*will be calculated later*/);
    }
    private void GetCellsToCheck(Vector2I grid_pos, Vector2 world_pos, Grid grid, List<CellDataCombo> output)
    {


        // really fast because the output list has already allocated the memory
        output.Add(HandleCell(world_pos, grid[grid_pos.X - 1, grid_pos.Y + 1]));
        output.Add(HandleCell(world_pos, grid[grid_pos.X, grid_pos.Y + 1]));
        output.Add(HandleCell(world_pos, grid[grid_pos.X + 1, grid_pos.Y + 1]));
        output.Add(HandleCell(world_pos, grid[grid_pos.X - 1, grid_pos.Y]));
        output.Add(HandleCell(world_pos, grid[grid_pos.X, grid_pos.Y]));
        output.Add(HandleCell(world_pos, grid[grid_pos.X + 1, grid_pos.Y]));
        output.Add(HandleCell(world_pos, grid[grid_pos.X - 1, grid_pos.Y - 1]));
        output.Add(HandleCell(world_pos, grid[grid_pos.X, grid_pos.Y - 1]));
        output.Add(HandleCell(world_pos, grid[grid_pos.X + 1, grid_pos.Y - 1]));

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

    public class OutputData
    {
        public readonly int map_resolution;
        readonly byte[] map_1_data;
        readonly byte[] map_2_data;

        public OutputData(int map_resolution, byte[] map_1_data, byte[] map_2_data)
        {
            this.map_resolution = map_resolution;
            this.map_1_data = map_1_data;
            this.map_2_data = map_2_data;
        }

        /// map_index: 1- map_1_data; other- map_2_data
        public ImageTexture GetTexture(int width_height, int map_index)
        {
            var data = map_index == 1 ? map_1_data : map_2_data;
            var image = Image.CreateFromData(width_height, width_height, false, Image.Format.Rgba8, data);
            return ImageTexture.CreateFromImage(image);
        }
        public List<BiomeInfluenceOutput> SampleBiomeDataForMesh(Vector2 UV)
        {
            int i = Mathf.FloorToInt(map_resolution * UV.X + map_resolution * map_resolution * UV.Y);

            var output = new List<BiomeInfluenceOutput>(3);
            ReadFromMapAndAddToOutput(output, i, biome_index: 0, map_1_data);
            ReadFromMapAndAddToOutput(output, i + 1, biome_index: 1, map_1_data);
            ReadFromMapAndAddToOutput(output, i + 2, biome_index: 2, map_1_data);
            ReadFromMapAndAddToOutput(output, i + 3, biome_index: 3, map_1_data);
            ReadFromMapAndAddToOutput(output, i, biome_index: 4, map_2_data);
            ReadFromMapAndAddToOutput(output, i + 1, biome_index: 5, map_2_data);
            ReadFromMapAndAddToOutput(output, i + 2, biome_index: 6, map_2_data);
            ReadFromMapAndAddToOutput(output, i + 3, biome_index: 7, map_2_data);

            return output;

        }
        public struct BiomeInfluenceOutput
        {
            public int biome_type_index;
            public float influence;

            public BiomeInfluenceOutput(int biome_index, float influence)
            {
                this.biome_type_index = biome_index;
                this.influence = influence;
            }
        }


        private static void ReadFromMapAndAddToOutput(List<BiomeInfluenceOutput> output, int i, int biome_index, byte[] map)
        {
            var _byte = map[i];
            if (biome_index == 0 || _byte == 0) return;
            output.Add(new(biome_index, ByteToFloat(_byte)));
        }
    }

    public OutputData GenerateMaps(int x_base, int y_base, int width_height, Biome[] biomes)
    {

        this.biomes = biomes;

        int grid_cells_per_axis = Mathf.CeilToInt(width_height / (float)grid_size);

        int grid_stride = grid_cells_per_axis + 2;
        Grid grid = GenerateGrid(grid_cells_per_axis, x_base, y_base, grid_stride);

        int points_per_axis = grid_cells_per_axis * biome_map_resolution;

        float point_size = width_height / (float)points_per_axis;
        var map_1_data = new byte[points_per_axis * points_per_axis * 4];
        var map_2_data = new byte[points_per_axis * points_per_axis * 4];

        float[] backed_gradient = bake_gradient();

        // alloc heap once
        List<CellDataCombo> cells = new(9);
        GD.Print($"poits_per_axis: {points_per_axis}");
        for (int x = 0; x < points_per_axis; x++)
        {
            for (int y = 0; y < points_per_axis; y++)
            {

                cells.Clear();

                Vector2 world_pos = new Vector2(x + x_base, y + y_base) * point_size;
                Vector2I grid_pos = new(x / biome_map_resolution, y / biome_map_resolution);
                GetCellsToCheck(grid_pos, world_pos, grid, cells);
                cells.Sort();

                var main_cell = cells[0]; cells.RemoveAt(0);

                int base_index = (x + y * points_per_axis) * 4;
                if (main_cell.distance < 5)
                {
                    map_1_data[base_index + 1] = FloatToByte(1);

                }
                //TEST END

                // GetCellsToCheck(x, y, grid, cells);
                // cells.Sort();

                // var main_cell = cells[0]; cells.RemoveAt(0);

                // CalculateInfluencesForCeils(cells, main_cell, new(x, y), backed_gradient);
                //
                // cells.Add(main_cell);
                // int base_index = (x + y * points_per_axis) * 4;
                //
                // foreach (CellDataCombo cell in cells)
                // {
                //     // using big ass switch statement would be faster, especially when using more than 2 textures but this is cleaner, so choose your poison.
                //     var map = cell.cell.biome.type_index / 4 == 0 ? map_1_data : map_2_data;
                //     int index = cell.cell.biome.type_index % 4;
                //     map[base_index + index] = FloatToByte(cell.influence);
                // }
            }
        }

        return new(points_per_axis, map_1_data, map_2_data);
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
