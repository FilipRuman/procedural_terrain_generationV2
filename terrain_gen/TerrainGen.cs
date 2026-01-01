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
    [Export(hintString: "Runs terrain generation every x seconds -Disables the refreshing if <= 0")]
    float refresh_frequency_sec;

    [Export] Vector2 position;
    [Export] int view_distance;
    [Export] float y_offset;
    [ExportGroup("ground shader settings")]
    [Export] Texture other_noise;
    [Export] Texture texture_repetition_noise;
    [Export] Texture roughness_gradient;
    [Export] Vector2 texture_uv_noise_strength;
    [Export] float uv_noise_frequency;

    [Export] Vector2 uv_noise_strength;
    [Export] Texture uv_noise_texture;
    [Export] Texture rock_texture;
    [Export] float rock_scale;
    [Export] float rock_saturation;
    [Export] float global_saturation;
    [Export] float global_brightness;
    const int max_chunk_data_textures_count = 100;

    double refresh_timer;
    public override void _Process(double delta)
    {
        if (run)
        {
            run = false;
            Run();
        }

        if (refresh_frequency_sec > 0)
        {
            refresh_timer += delta;
            if (refresh_timer >= refresh_frequency_sec)
            {
                refresh_timer = 0;
                Run();
            }
        }
    }

    private void Run()
    {
        free_data_maps = new(Enumerable.Range(0, max_chunk_data_textures_count));
        ClearAllChildren();
        GenerateAll();
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
        // foreach (Vector2 chunk_relative_pos in chunk_relative_positions)
        // {
        for (int x = 0; x < 2; x++)
        {

            for (int y = 0; y < 2; y++)
            {
                Vector2 chunk_world_position = /* chunk_relative_pos + position */ new(x * chunk_size, y * chunk_size);
                var biome_data = biome_generator.GenerateMaps(new(chunk_world_position.X, chunk_world_position.Y), chunk_size, biomes);
                // var biome_data = biome_generator.GenerateMaps((int)i, (int)0, chunk_size, biomes);


                var chunk = (Chunk)chunk_prefab.Instantiate();
                AddChild(chunk);
                chunk.GlobalPosition = new(chunk_world_position.X, y_offset, chunk_world_position.Y);

                var mesh_gen = chunk.mesh_gen;
                mesh_gen.Run(biomes, biome_data, chunk_size);
                int map_index = free_data_maps.Dequeue();

                map_1[map_index] = biome_data.GetTexture(biome_data.map_resolution, 1);
                map_2[map_index] = biome_data.GetTexture(biome_data.map_resolution, 2);
                mesh_gen.SetInstanceShaderParameter("chunk_data_map_index", map_index);
            }
        }
        // }


        var biome_textures = new Texture[biomes.Length];
        var texture_tint = new Vector3[biomes.Length];
        var texture_saturation = new float[biomes.Length];
        var texture_scale = new float[biomes.Length];
        int i = 0;
        foreach (var biome in biomes)
        {
            biome_textures[i] = biome.texture;
            texture_tint[i] = new(biome.tint.R, biome.tint.G, biome.tint.B);
            texture_saturation[i] = biome.saturation;
            texture_scale[i] = biome.scale;
            i++;
        }

        ground_shader_material.SetShaderParameter("texture_tint", texture_tint);
        ground_shader_material.SetShaderParameter("texture_saturation", texture_saturation);
        ground_shader_material.SetShaderParameter("texture_scale", texture_scale);
        ground_shader_material.SetShaderParameter("biome_textures", biome_textures);
        ground_shader_material.SetShaderParameter("uv_noise_frequency", uv_noise_frequency);
        ground_shader_material.SetShaderParameter("other_noise", other_noise);
        ground_shader_material.SetShaderParameter("texture_uv_noise", texture_repetition_noise);
        ground_shader_material.SetShaderParameter("texture_uv_noise_strength", texture_uv_noise_strength);

        ground_shader_material.SetShaderParameter("uv_noise_strength", uv_noise_strength);
        ground_shader_material.SetShaderParameter("uv_noise_texture", uv_noise_texture);
        ground_shader_material.SetShaderParameter("global_brightness", global_brightness);
        ground_shader_material.SetShaderParameter("global_saturation", global_saturation);
        ground_shader_material.SetShaderParameter("rock_texture", rock_texture);
        ground_shader_material.SetShaderParameter("uv_margin", biome_generator.CalculateUvMargin(chunk_size));
        ground_shader_material.SetShaderParameter("map_1", map_1);
        ground_shader_material.SetShaderParameter("map_2", map_2);
    }
}
