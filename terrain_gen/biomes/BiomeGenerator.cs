using System;
using System.Collections.Generic;
using Godot;
[Tool]
public partial class BiomeGenerator : Node
{


    [Export] int seed_base;
    [Export] int grid_cell_size;
    [Export] int biome_map_resolution;
    [Export] float max_overlap_distance;
    [Export] Gradient overlap_gradient;
    [Export] bool run;

    [Export] NoiseComponent x_noise;
    [Export] NoiseComponent y_noise;

    Biome[] biomes;

    private const int data_maps_count = 2;
    private const int color_channels = 4; // rgba

    public class GridCell
    {
        public Vector2 world_pos;
        public Biome biome;

        public GridCell(Vector2 world_pos, Biome biome)
        {
            this.world_pos = world_pos;
            this.biome = biome;
        }
    }
    class Grid
    {
        GridCell[] cells;
        int grid_stride;
        int grid_margin;

        public Grid(GridCell[] cells, int grid_cells_per_axis, int grid_margin)
        {
            this.cells = cells;
            this.grid_stride = grid_cells_per_axis;
            this.grid_margin = grid_margin;
        }

        public GridCell this[int x, int y]
        {
            get
            {
                return cells[x + grid_margin + (y + grid_margin) * grid_stride];
            }
        }
    }
    /// map float (expected 0..1) to byte 0..255.
    static byte FloatToByte(float v)
    {
        return (byte)MathF.Round(v * 255f);
    }

    static float ByteToFloat(byte v)
    {
        return v / 255f;
    }

    class CellDataCombo
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

    }
    private Grid GenerateGrid(Vector2 base_world_position, int cells_per_axis_with_margin, int grid_margin)
    {
        var cells = new GridCell[cells_per_axis_with_margin * cells_per_axis_with_margin];

        for (int x = 0; x < cells_per_axis_with_margin; x++)
        {
            for (int y = 0; y < cells_per_axis_with_margin; y++)
            {
                float world_x = base_world_position.X + (x - grid_margin) * grid_cell_size;
                float world_y = base_world_position.Y + (y - grid_margin) * grid_cell_size;
                ulong seed =
    (ulong)seed_base ^
    (ulong)Mathf.FloorToInt(world_x) * 73856093UL ^
    (ulong)Mathf.FloorToInt(world_y) * 19349663UL;

                GD.Seed(seed);

                float x_offset = GD.Randf() * (grid_cell_size * 0.5f);
                float y_offset = GD.Randf() * (grid_cell_size * 0.5f);
                int grid_index = x + y * cells_per_axis_with_margin;
                Vector2 final_pos = new(world_x + x_offset, world_y + y_offset);


                Biome biome = biomes[GD.Randi() % (biomes.Length)];
                cells[grid_index] = new(final_pos, biome);
            }
        }

        Grid grid = new(cells, cells_per_axis_with_margin, grid_margin);

        return grid;
    }


    private CellDataCombo HandleCell(Vector2 world_pos, GridCell cell)
    {
        float distance = cell.world_pos.DistanceTo(world_pos);
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



    private void CalculateInfluencesForCells(
        List<CellDataCombo> neighbors,
        CellDataCombo main, Vector2 world_position,
        float[] bakedGradient)
    {
        main.influence = 1.0f;


        // In a reverse order so that we can remove neighbors that doesn't mach our requirements
        for (int i = neighbors.Count - 1; i >= 0; i--)
        {
            set_neighbor_cell_influence(neighbors, main, world_position, bakedGradient, i);
        }

        // normalize
        float sum = main.influence;
        foreach (var n in neighbors)
            sum += n.influence;

        main.influence /= sum;
        foreach (var n in neighbors)
            n.influence /= sum;

    }

    private void set_neighbor_cell_influence(List<CellDataCombo> neighbors, CellDataCombo main, Vector2 world_pos, float[] bakedGradient, int cell_index_in_list)
    {
        var neighbor = neighbors[cell_index_in_list];

        if (neighbor.cell.biome.index_in_biomes_array == main.cell.biome.index_in_biomes_array)
        {
            neighbors.RemoveAt(cell_index_in_list);
            return;
        }

        if (neighbor.cell.biome.index_in_biomes_array > main.cell.biome.index_in_biomes_array) { return; }

        float influence = CalculateInfluence(main, bakedGradient, neighbor);

        float influence_change = main.influence - Mathf.Clamp(main.influence - influence, 0, 1);

        neighbor.influence = influence_change;
        main.influence -= influence_change;
        return;
    }

    private float CalculateInfluence(CellDataCombo main, float[] bakedGradient, CellDataCombo neighbor)
    {

        float delta =
            neighbor.distance - main.distance;
        delta = MathF.Max(delta, 0);


        if (delta >= max_overlap_distance)
            return 0;

        float overlap_percentage = delta / max_overlap_distance;

        return bakedGradient[FloatToByte(overlap_percentage)];

    }


    public class OutputData
    {
        public readonly int map_resolution;
        readonly byte[][] biome_maps;


        public OutputData(int map_resolution, byte[][] biome_maps_array)
        {
            this.map_resolution = map_resolution;
            this.biome_maps = biome_maps_array;
        }

        public ImageTexture GetTexture(int map_index)
        {
            var data = biome_maps[map_index];
            var image = Image.CreateFromData(map_resolution, map_resolution, false, Image.Format.Rgba8, data);
            return ImageTexture.CreateFromImage(image);
        }
        public List<BiomeInfluenceOutput> SampleBiomeDataForMesh(Vector2 UV)
        {
            int pixel_x = Mathf.Clamp((int)(UV.X * map_resolution), 0, map_resolution - 1);
            int pixel_y = Mathf.Clamp((int)(UV.Y * map_resolution), 0, map_resolution - 1);
            int pixel_index = pixel_x + pixel_y * map_resolution;
            int base_color_channel_index = pixel_index * color_channels;

            var output = new List<BiomeInfluenceOutput>(1);
            for (int biome_map_index = 0; biome_map_index < data_maps_count; biome_map_index++)
            {
                for (int color_channel = 0; color_channel < color_channels; color_channel++)
                {
                    int index_inside_biome_map = color_channel + base_color_channel_index;
                    int biome_index = biome_map_index * color_channel;
                    ReadFromMapAndAddToOutput(output, index_inside_biome_map, biome_index, biome_maps[biome_map_index]);
                }
            }

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


        private static void ReadFromMapAndAddToOutput(List<BiomeInfluenceOutput> output, int index_inside_biome_map, int biome_index, byte[] map)
        {
            var influence = map[index_inside_biome_map];
            if (influence == 0) return;
            output.Add(new(biome_index, ByteToFloat(influence)));
        }
    }
    public OutputData GenerateMaps(Vector2 base_world_position, int width_height, Biome[] biomes)
    {
        this.biomes = biomes;

        int grid_cells_per_axis = Mathf.CeilToInt(width_height / (float)grid_cell_size);
        int points_per_axis = grid_cells_per_axis * biome_map_resolution;

        float point_size = width_height / (float)(grid_cells_per_axis * biome_map_resolution);


        // 1 on each side + noise displacement(grid cells) so that the margin points good looking values 
        int grid_margin = 1 + Mathf.CeilToInt(Mathf.Max(x_noise.Amplitude, y_noise.Amplitude) / (float)grid_cell_size);
        int grid_cells_with_margin = grid_cells_per_axis + grid_margin * 2;
        Grid grid = GenerateGrid(base_world_position, grid_cells_with_margin, grid_margin: grid_margin);

        byte[][] biome_maps = InitializeBiomeMapArray(points_per_axis);

        float[] baked_gradient = BakeGradient();

        // alloc heap once
        List<CellDataCombo> cells = new(9);

        for (int x = 0; x < points_per_axis; x++)
        {
            for (int y = 0; y < points_per_axis; y++)
            {
                cells.Clear();

                Vector2 world_pos = new Vector2(x, y) * point_size + base_world_position;
                Vector2 noisy_world_pos = world_pos + GetNoise(world_pos);
                Vector2I grid_pos = new(x / biome_map_resolution, y / biome_map_resolution);
                GetCellsToCheck(grid_pos, noisy_world_pos, grid, cells);

                var main_cell = GetClosestCell(cells);
                cells.Remove(main_cell);

                CalculateInfluencesForCells(cells, main_cell, noisy_world_pos, baked_gradient);

                cells.Add(main_cell);

                int base_index = (x + y * points_per_axis) * 4;

                foreach (CellDataCombo cell in cells)
                {
                    var map_index = cell.cell.biome.index_in_biomes_array / 4;
                    int color_channel_index = cell.cell.biome.index_in_biomes_array % 4;
                    // So that the Byte doesn't overflow
                    byte clamped_value = FloatToByte(Math.Clamp(ByteToFloat(biome_maps[map_index][base_index + color_channel_index]) + cell.influence, 0, 1));
                    biome_maps[map_index][base_index + color_channel_index] = clamped_value;
                }
            }
        }

        return new(points_per_axis, biome_maps);
    }
    private Vector2 GetNoise(Vector2 pos)
    {
        return new Vector2(x_noise.Sample(pos), y_noise.Sample(pos));
    }
    private static byte[][] InitializeBiomeMapArray(int points_per_axis)
    {
        var biome_maps = new byte[data_maps_count][];

        int mapSize = points_per_axis * points_per_axis * color_channels;

        for (int i = 0; i < data_maps_count; i++)
        {
            biome_maps[i] = new byte[mapSize];
        }

        return biome_maps;
    }


    CellDataCombo GetClosestCell(IEnumerable<CellDataCombo> cells)
    {
        CellDataCombo closest = null;
        float minDistance = float.MaxValue;

        foreach (var cell in cells)
        {
            if (cell == null)
                continue;

            if (cell.distance < minDistance)
            {
                minDistance = cell.distance;
                closest = cell;
            }
        }

        return closest;
    }
    private float[] BakeGradient()
    {
        var baked_gradient = new float[256];
        for (int i = 0; i < 256; i++)
        {
            baked_gradient[i] = overlap_gradient.Sample(i / 255f).R;
        }

        return baked_gradient;
    }
}
