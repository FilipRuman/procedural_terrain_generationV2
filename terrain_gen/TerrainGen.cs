using System.Collections.Generic;
using Godot;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System;

[Tool]
public partial class TerrainGen : Node3D
{

    [ExportToolButton("Run!")]
    public Callable run => Callable.From(RunClean);
    [Export(hintString: "Runs terrain generation every x seconds. Disables the refreshing if <= 0")]
    float refresh_frequency_sec;

    [Export] public int max_chunks_processed_per_frame;

    [ExportCategory("references")]
    [Export] PackedScene chunk_prefab;
    [Export] BiomeGenerator biome_generator;
    [Export] ThreadSafeGroundMeshGen mesh_gen;
    [Export] TerrainAspectsSolver terrain_aspects_solver;

    [ExportGroup("player")]
    [Export] Vector2 position;
    [Export] int view_distance;

    [ExportCategory("Terrain")]
    [Export] float y_offset;
    [Export] int chunk_size;
    [Export] Biome[] biomes;

    [Export] int ground_mesh_resolution;

    [ExportCategory("ground shader settings")]
    [Export] ShaderMaterial ground_shader_material;

    [ExportGroup("rock")]
    [Export] Texture rock_texture;
    [Export] Texture rock_normal_map;
    [Export] Texture rock_roughness;
    [Export] float rock_scale;
    [Export] float rock_saturation;


    [ExportGroup("additional processing")]
    [Export] float global_saturation;
    [Export] float global_brightness;

    [ExportSubgroup("other noise stats")]
    [Export] Texture other_noise;
    [Export] float metallic;
    [Export] float other_noise_scale;
    [Export] float spectacular;



    const int max_chunk_data_textures_count = 100;

    double refresh_timer;
    public override void _Process(double delta)
    {
        Upd();
        if (refresh_frequency_sec > 0)
        {
            refresh_timer += delta;
            if (refresh_timer >= refresh_frequency_sec)
            {
                refresh_timer = 0;

                RunClean();
            }
        }
    }

    private void RunClean()
    {
        free_data_maps = new(Enumerable.Range(0, max_chunk_data_textures_count));
        ClearAllChildren();
        Init();
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

        return output;
    }
    private void ClearAllChildren()
    {
        foreach (var item in GetChildren())
        {
            item.QueueFree();
        }
    }

    public struct ChunkData
    {
        public ThreadSafeGroundMeshGen.OutputData mesh;
        public BiomeGenerator.OutputData biome;
        public Vector2 world_pos;
        public ChunkData(ThreadSafeGroundMeshGen.OutputData mesh,
                         BiomeGenerator.OutputData biome, Vector2 world_pos)
        {
            this.mesh = mesh;
            this.biome = biome;
            this.world_pos = world_pos;
        }
    }



    private ThreadSafeGroundMeshGen.Config GenerateConfigForGroundMeshGen()
    {
        return new ThreadSafeGroundMeshGen.Config(
                 chunk_size,
                 ground_mesh_resolution
        );
    }


    private void GenerateChunkData()
    {
        while (true)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            List<Vector2> chunk_relative_positions = GetAllChunksPositionsInsideACircleRelative(view_distance, chunk_size);
            ThreadSafeGroundMeshGen.Config config = GenerateConfigForGroundMeshGen();
            GD.Print($"GenerateChunkData: {chunk_relative_positions.Count}");
            try
            {
                Parallel.For(0, chunk_relative_positions.Count, i =>
                  {
                      var chunk_relative_pos = chunk_relative_positions[i];
                      Vector2 chunk_world_position = chunk_relative_pos + position;

                      var biome_data = biome_generator.GenerateMaps(terrain_aspects_solver, new Vector2(chunk_world_position.X, chunk_world_position.Y), chunk_size + 1, biomes);
                      var mesh_data = mesh_gen.GenerateChunk(chunk_world_position, config);
                      completed_chunks.Enqueue(new(mesh_data, biome_data, chunk_world_position));
                  });
            }
            catch (Exception e)
            {
                GD.PrintErr($"Parallel.For failed: {e}");
            }
            stopwatch.Stop();
            GD.Print($"Generate chunk data: {stopwatch.Elapsed.Milliseconds}");
            // TODO: Only Regenerate chunks that need to be regenerated
            break;
        }
    }
    Task chunk_data_gen_task;
    ConcurrentQueue<ChunkData> completed_chunks = new();

    Queue<int> free_data_maps = new(Enumerable.Range(0, max_chunk_data_textures_count));
    ImageTexture[] map_1 = new ImageTexture[max_chunk_data_textures_count];
    ImageTexture[] map_2 = new ImageTexture[max_chunk_data_textures_count];
    private void HandleChunkGenerationQue()
    {

        int processed = 0;
        bool refresh_biome_map_data = false;

        while (processed < max_chunks_processed_per_frame &&
               completed_chunks.TryDequeue(out var chunk_data))
        {
            GD.Print("Dequeue");
            HandleGodotSideOfChunk(chunk_data);
            refresh_biome_map_data = true;
            processed++;
        }
        if (refresh_biome_map_data)
        {
            ground_shader_material.SetShaderParameter("map_1", map_1);
            ground_shader_material.SetShaderParameter("map_2", map_2);
        }

    }
    private void HandleGodotSideOfChunk(ChunkData chunk_data)
    {
        var chunk = (Chunk)chunk_prefab.Instantiate();

        AddChild(chunk);

        var mash_instance = chunk.mesh_instance;
        // chunk.mesh_gen.Run(terrain_aspects_solver, chunk_size, chunk_data.world_pos, ground_mesh_resolution);
        ThreadSafeGroundMeshGen.ApplyData(chunk_data.mesh, mash_instance);

        int map_index = free_data_maps.Dequeue();
        map_1[map_index] = chunk_data.biome.GetTexture(0);

        map_2[map_index] = chunk_data.biome.GetTexture(1);
        mash_instance.SetInstanceShaderParameter("chunk_data_map_index", map_index);

    }
    private void Upd()
    {
        HandleChunkGenerationQue();
    }

    private void Init()
    {
        chunk_data_gen_task = Task.Run(() => GenerateChunkData());

        var biome_albedo_textures = new Texture[biomes.Length];
        var biome_normal_textures = new Texture[biomes.Length];
        var biome_roughness_textures = new Texture[biomes.Length];
        var texture_tint = new Vector3[biomes.Length];
        var texture_saturation = new float[biomes.Length];
        var texture_scale = new float[biomes.Length];
        int i = 0;
        foreach (var biome in biomes)
        {

            biome.index_in_biomes_array = (byte)i;
            biome_albedo_textures[i] = biome.albedo;
            biome_normal_textures[i] = biome.normal;
            biome_roughness_textures[i] = biome.roughness;
            texture_tint[i] = new(biome.tint.R, biome.tint.G, biome.tint.B);
            texture_saturation[i] = biome.saturation;
            texture_scale[i] = biome.scale;
            i++;
        }

        ground_shader_material.SetShaderParameter("rock_saturation", rock_saturation);
        ground_shader_material.SetShaderParameter("rock_scale", rock_scale);
        ground_shader_material.SetShaderParameter("rock_normal_map", rock_normal_map);
        ground_shader_material.SetShaderParameter("rock_roughness", rock_roughness);
        ground_shader_material.SetShaderParameter("rock_texture", rock_texture);

        ground_shader_material.SetShaderParameter("metallic", metallic);
        ground_shader_material.SetShaderParameter("spectacular", spectacular);
        ground_shader_material.SetShaderParameter("other_noise_scale", other_noise_scale);

        ground_shader_material.SetShaderParameter("texture_tint", texture_tint);
        ground_shader_material.SetShaderParameter("texture_saturation", texture_saturation);
        ground_shader_material.SetShaderParameter("texture_scale", texture_scale);

        ground_shader_material.SetShaderParameter("biome_albedo_textures", biome_albedo_textures);
        ground_shader_material.SetShaderParameter("biome_roughness_textures", biome_roughness_textures);
        ground_shader_material.SetShaderParameter("biome_normal_textures", biome_normal_textures);

        ground_shader_material.SetShaderParameter("other_noise", other_noise);

        ground_shader_material.SetShaderParameter("global_brightness", global_brightness);
        ground_shader_material.SetShaderParameter("global_saturation", global_saturation);

        ground_shader_material.SetShaderParameter("map_1", map_1);
        ground_shader_material.SetShaderParameter("map_2", map_2);
    }
}
