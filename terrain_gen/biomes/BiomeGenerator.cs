using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
[Tool]
public partial class BiomeGenerator : Node
{

    [Export] int seed;
    [Export] int grid_size;
    [Export] int biome_map_resolution;
    [Export] int margin_points;
    [Export] float max_overlap_distance;
    [Export] Gradient overlap_gradient;
    [Export] bool run;
    [Export] float biome_spawn_point_exclusion_distance;

    [Export] Biome[] biomes;

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
    private Grid GenerateGrid(int grid_cells_per_axis, Vector2 base_world_position, int grid_stride, int grid_margin)
    {
        var cells = new GridCell[grid_stride * grid_stride];

        // + 2 to generate position outside of this chunk of terrain, on the: left, right, up, down. This is needed to ensure consistency between chunks.
        for (int x = 0; x < grid_stride; x++)
        {
            for (int y = 0; y < grid_stride; y++)
            {
                // - 2 because of the buffer
                float world_x = base_world_position.X + (x - grid_margin) * grid_size;
                float world_y = base_world_position.Y + (y - grid_margin) * grid_size;
                ulong s =
    (ulong)seed ^
    (ulong)Mathf.FloorToInt(world_x) * 73856093UL ^
    (ulong)Mathf.FloorToInt(world_y) * 19349663UL;

                GD.Seed(s);

                float x_offset = GD.Randf() * (grid_size * 0.5f - biome_spawn_point_exclusion_distance);
                float y_offset = GD.Randf() * (grid_size * 0.5f - biome_spawn_point_exclusion_distance);
                int grid_index = x + y * (grid_stride);
                Vector2 final_pos = new(world_x + x_offset, world_y + y_offset);


                Biome biome = biomes[GD.Randi() % (biomes.Length)];
                cells[grid_index] = new(final_pos, biome);
            }
        }

        Grid grid = new(cells, grid_stride, grid_margin);

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



    private void CalculateInfluencesForCeils(
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

        if (neighbor.cell.biome.type_index == main.cell.biome.type_index)
        {
            // if (main.influence != 1)
            // {
            //     // IDK. if this is good or not I:
            //     // float influ = CalculateInfluence(main, bakedGradient, neighbor);
            //     // main.influence = Mathf.Clamp(main.influence + influ, 0, 1);
            // }
            neighbors.RemoveAt(cell_index_in_list);
            return;
        }

        if (neighbor.cell.biome.type_index > main.cell.biome.type_index) { return; }

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

        float overlap_percentage = delta / max_overlap_distance; // 0 at boundary

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

        public ImageTexture GetTexture(int width_height, int map_index)
        {
            var data = biome_maps[map_index];
            var image = Image.CreateFromData(width_height, width_height, false, Image.Format.Rgba8, data);
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
    public float CalculateUvMargin(int width_height)
    {
        int grid_cells_per_axis = Mathf.CeilToInt(width_height / (float)grid_size);
        int points_per_axis = grid_cells_per_axis * biome_map_resolution + margin_points * 2;

        float point_size = width_height / (float)(grid_cells_per_axis * biome_map_resolution);

        // Chosen empirically, this works the best with my use case, but Idk. why I:
        const int filter_padding = -1;
        int effective_margin_points = margin_points + filter_padding;
        return effective_margin_points / (float)points_per_axis;

    }
    public OutputData GenerateMaps(Vector2 base_world_position, int width_height, Biome[] biomes)
    {
        this.biomes = biomes;

        int grid_cells_per_axis = Mathf.CeilToInt(width_height / (float)grid_size);
        // * 2 because the margin is on each side
        int points_per_axis = grid_cells_per_axis * biome_map_resolution + margin_points * 2;

        float point_size = width_height / (float)(grid_cells_per_axis * biome_map_resolution);

        // Chosen empirically, this works the best with my use case, but Idk. why I:
        const int filter_padding = -1;
        int effective_margin_points = margin_points + filter_padding;
        float uv_margin = effective_margin_points / (float)points_per_axis;

        int grid_margin = 1 + Mathf.CeilToInt(margin_points * point_size / grid_size);
        // * 2 because the margin is on each side
        int grid_stride = grid_cells_per_axis + grid_margin * 2;
        Grid grid = GenerateGrid(grid_cells_per_axis, base_world_position, grid_stride, grid_margin);

        byte[][] biome_maps = InitializeBiomeMapArray(points_per_axis);

        float[] backed_gradient = BakeGradient();

        // alloc heap once
        List<CellDataCombo> cells = new(9);
        for (int x = -margin_points; x < points_per_axis - margin_points; x++)
        {
            for (int y = -margin_points; y < points_per_axis - margin_points; y++)
            {
                cells.Clear();

                Vector2 world_pos = new Vector2(x, y) * point_size + base_world_position;
                Vector2I grid_pos = new(x / biome_map_resolution, y / biome_map_resolution);
                GetCellsToCheck(grid_pos, world_pos, grid, cells);
                cells.Sort();

                var main_cell = cells[0]; cells.RemoveAt(0);

                CalculateInfluencesForCeils(cells, main_cell, world_pos, backed_gradient);

                cells.Add(main_cell);

                int base_index = (x + margin_points + (y + margin_points) * points_per_axis) * 4;

                foreach (CellDataCombo cell in cells)
                {
                    var map_index = cell.cell.biome.type_index / 4;
                    int color_channel_index = cell.cell.biome.type_index % 4;
                    // So that the Byte doesn't overflow
                    byte clamped_value = FloatToByte(Math.Clamp(ByteToFloat(biome_maps[map_index][base_index + color_channel_index]) + cell.influence, 0, 1));
                    biome_maps[map_index][base_index + color_channel_index] = clamped_value;
                }
            }
        }

        return new(points_per_axis, biome_maps);
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

    private float[] BakeGradient()
    {
        var backed_gradient = new float[256];
        for (int i = 0; i < 256; i++)
        {
            backed_gradient[i] = overlap_gradient.Sample(i / 255f).R;
        }

        return backed_gradient;
    }
}
