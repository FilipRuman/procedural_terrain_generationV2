using System.Collections.Generic;
using Godot;
using System.Linq;

[Tool]
public partial class TerrainGen : Node3D
{
    [Export] PackedScene chunk_prefab;
    [Export] BiomeGenerator biome_generator;

    [Export] int chunk_size;
    [Export] Biome[] biomes;

    [Export] bool run;

    [Export] Vector2 position;
    [Export] int view_distance;
    [Export] float y_offset;
    const int max_chunk_data_textures_count = 100;

    public override void _Process(double delta)
    {

        if (run)
        {

            run = false;
            free_data_maps = new(Enumerable.Range(0, max_chunk_data_textures_count));
            ClearAllChildren();
            GenerateAll();
        }
    }
    private static List<Vector2> GetAllChunksPositionsInsideACircleRelative(int radius, int chunk_size)
    {
        List<Vector2> output = new();

        // could be pre-calculated once

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y >= radius * radius)
                    continue;

                output.Add(new(x * chunk_size, y * chunk_size));
            }
        }
        // for (int x = -radius; x <= radius; x++)
        // {
        // for (int y = -radius; y <= radius; y++)
        // {
        //     if (x * x + y * y >= radius * radius)
        //         continue;

        // output.Add(new(x * chunk_size, 0));
        // }
        // }

        return output;
    }

    private void ClearAllChildren()
    {
        foreach (var item in GetChildren())
        {
            item.QueueFree();
        }
    }

    Queue<int> free_data_maps = new(Enumerable.Range(0, max_chunk_data_textures_count));
    ImageTexture[] map_1 = new ImageTexture[max_chunk_data_textures_count];
    ImageTexture[] map_2 = new ImageTexture[max_chunk_data_textures_count];
    [Export] ShaderMaterial ground_shader_material;
    private void GenerateAll()
    {
        List<Vector2> chunk_relative_positions = GetAllChunksPositionsInsideACircleRelative(view_distance, chunk_size);
        int i = 0;
        // foreach (Vector2 chunk_relative_pos in chunk_relative_positions)
        // {
        for (int x = 0; x < 2; x++)
        {

            for (int y = 0; y < 2; y++)
            {
                i++;
                Vector2 chunk_absolute_pos = /* chunk_relative_pos + position */ new(x * chunk_size, y * chunk_size);
                var biome_data = biome_generator.GenerateMaps((int)chunk_absolute_pos.X, (int)chunk_absolute_pos.Y, chunk_size, biomes);
                // var biome_data = biome_generator.GenerateMaps((int)i, (int)0, chunk_size, biomes);


                var chunk = (Chunk)chunk_prefab.Instantiate();
                AddChild(chunk);
                chunk.GlobalPosition = new(chunk_absolute_pos.X, y_offset, chunk_absolute_pos.Y);

                var mesh_gen = chunk.mesh_gen;
                mesh_gen.Run(biomes, biome_data, chunk_size);
                int map_index = free_data_maps.Dequeue();

                map_1[map_index] = biome_data.GetTexture(biome_data.map_resolution, 1);
                map_2[map_index] = biome_data.GetTexture(biome_data.map_resolution, 2);
                mesh_gen.SetInstanceShaderParameter("chunk_data_map_index", map_index);
            }
        }
        // }

        ground_shader_material.SetShaderParameter("map_1", map_1);
        ground_shader_material.SetShaderParameter("map_2", map_2);
    }
}
