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
        public Callable Run => Callable.From(RunClean);
        [Export(hintString: "Runs terrain generation every x seconds. Disables the refreshing if <= 0")]
        float refresh_frequency_sec;
        [Export] bool Halt;
        [Export] int test_lod;

        [Export] public int max_chunks_processed_per_frame;

        [ExportCategory("references")]
        [Export] Node3D player;
        [Export] Vector2 player_pos_offset;
        [Export] PackedScene chunk_prefab;
        [Export] BiomeGenerator biome_generator;
        [Export] ThreadSafeGroundMeshGen ground_mesh_gen;
        [Export] TerrainAspectsSolver terrain_aspects_solver;
        [Export] ObjectsGenerator objects_generator;
        [Export] WaterGen water_gen;
        [Export] StructureGen structure_gen;

        [ExportGroup("player")]
        [Export] Vector2 player_pos;
        [Export] int view_distance;

        [ExportCategory("Terrain")]
        [Export] float y_offset;
        [Export] int chunk_size;
        [Export] Biome[] biomes;

        [Export] int ground_mesh_resolution;

        [ExportCategory("ground shader settings")]
        [Export] ShaderMaterial ground_shader_material;
        [Export] Curve LOD_curve;

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


        public override void _Ready()
        {
                if (!Engine.IsEditorHint())
                        RunClean();
        }

        /// When you want to change you need to also change the value in the ground shader 
        const int max_chunk_data_textures_count = 517;
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
                if (max_chunk_data_textures_count != GetAllChunksPositionsInsideACircleRelative(view_distance, chunk_size).Count)
                {
                        GD.Print($"The max amount of chunk data textures is not equal to the chunk data textures that are generated, this is not optimal and could cause chunks biomes to not work- current:{max_chunk_data_textures_count} optimal:{GetAllChunksPositionsInsideACircleRelative(view_distance, chunk_size).Count}");

                }
                free_data_maps = new(Enumerable.Range(0, max_chunk_data_textures_count));
                map_1 = new ImageTexture[max_chunk_data_textures_count];
                map_2 = new ImageTexture[max_chunk_data_textures_count];
                chunk_change_for_position_delta = new();
                chunk_per_world_position = new();
                Vector2I delta = new(-1, 0);
                chunk_change_for_position_delta.Add(delta, CaluclateChunkChangeForPosDelta(delta));
                delta = new(-1, 1);
                chunk_change_for_position_delta.Add(delta, CaluclateChunkChangeForPosDelta(delta));
                delta = new(0, 1);
                chunk_change_for_position_delta.Add(delta, CaluclateChunkChangeForPosDelta(delta));
                delta = new(1, 1);
                chunk_change_for_position_delta.Add(delta, CaluclateChunkChangeForPosDelta(delta));
                delta = new(1, 0);
                chunk_change_for_position_delta.Add(delta, CaluclateChunkChangeForPosDelta(delta));
                delta = new(1, -1);
                chunk_change_for_position_delta.Add(delta, CaluclateChunkChangeForPosDelta(delta));
                delta = new(0, -1);
                chunk_change_for_position_delta.Add(delta, CaluclateChunkChangeForPosDelta(delta));
                delta = new(-1, -1);
                chunk_change_for_position_delta.Add(delta, CaluclateChunkChangeForPosDelta(delta));

                ClearAllChildren();
                Init();
        }
        private ChunkChange CaluclateChunkChangeForPosDelta(Vector2I delta)
        {
                delta *= chunk_size;
                var chunks = GetAllChunksPositionsInsideACircleRelative(view_distance, chunk_size);

                var oldSet = new HashSet<Vector2I>(chunks.Select(c => c));
                var newSet = new HashSet<Vector2I>(oldSet.Select(p => p + delta));

                var to_destroy = oldSet.Except(newSet).ToArray();

                // var to_generate = chunks
                //     .Where(c => !oldSet.Contains(c.pos + delta))
                //     .Select(c => new ChunkSettings(c.pos, c.lod))
                //     .ToArray();
                List<Vector2I> to_generate = new();
                foreach (var chunk in chunks)
                {
                        var new_pos = chunk + delta;
                        if (!oldSet.Contains(new_pos))
                        {
                                to_generate.Add(chunk);
                        }
                }

                return new ChunkChange(to_destroy, to_generate.ToArray());
        }

        private int GetLod(float distance)
        {
                return test_lod;
        }
        private List<Vector2I> GetAllChunksPositionsInsideACircleRelative(int radius, int chunk_size)
        {
                List<Vector2I> output = new();

                // could be pre-calculated once
                for (int x = -radius; x <= radius; x++)
                {
                        for (int y = -radius; y <= radius; y++)
                        {
                                if (x * x + y * y >= radius * radius)
                                        continue;

                                output.Add(new Vector2I(x * chunk_size, y * chunk_size));
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

        public struct ChunkData(ThreadSafeGroundMeshGen.OutputData mesh,
                         BiomeGenerator.OutputData biome, Vector2I world_pos, ObjectsGenerator.ObjectTypeSpawnData[] objects_data, StructureInstanceData structure)
        {
                public ThreadSafeGroundMeshGen.OutputData mesh = mesh;
                public BiomeGenerator.OutputData biome = biome;
                public Vector2I world_pos = world_pos;
                public ObjectsGenerator.ObjectTypeSpawnData[] objects_data = objects_data;
                public StructureInstanceData? structure = structure;
        }




        struct ChunkChange
        {
                public Vector2I[] to_destroy_relative_pos;
                public Vector2I[] to_generate_relative_pos;
                public ChunkChange(Vector2I[] chunks_to_destroy_relative_positions, Vector2I[] chunks_to_instantiate)
                {
                        this.to_destroy_relative_pos = chunks_to_destroy_relative_positions;
                        this.to_generate_relative_pos = chunks_to_instantiate;
                }
        }
        Vector2I world_to_grid_pos(Vector2 world_pos)
        {
                return new Vector2I(Mathf.RoundToInt(world_pos.X / chunk_size), Mathf.RoundToInt(world_pos.Y / chunk_size));
        }
        Dictionary<Vector2I, Chunk> chunk_per_world_position;
        Dictionary<Vector2I, ChunkChange> chunk_change_for_position_delta = new();

        bool generated_all_chunks;
        bool clear_all;
        bool load_at_once;
        private void ChunkDataGenerationLoop()
        {

                const int ChunksGenMillisecondsDelay = 2;
                try
                {

                        if (water_gen.mesh_chunks_per_water_chunk < structure_gen.mesh_chunks_per_structure_grid_cell)
                        {
                                /// This is required because otherwise the  structure gen will try to access water chunks that are outside the view distance.
                                /// This happens because the structure gen needs to check every structure whether it is under the water or not, when generating it's position.
                                GD.Print("'WaterGen.mesh_chunks_per_water_chunk' has to be greater than the 'StructureGen.mesh_chunks_per_structure_grid_cell'");
                        }

                        var water_data = new WaterGen.WaterDataGrid(water_gen, ground_mesh_gen, chunk_size, player_pos);
                        var structure_grid = new StructureGen.StructureGrid(structure_gen, ground_mesh_gen, chunk_size, player_pos, water_data);
                        var last_player_chunk_grid_pos = world_to_grid_pos(player_pos);
                        // Initial terrain generation
                        {
                                var chunks_to_generate = GetAllChunksPositionsInsideACircleRelative(view_distance, chunk_size);
                                RunTerrainGeneration(chunks_to_generate.ToArray(), last_player_chunk_grid_pos * chunk_size, water_data, structure_grid);
                                load_at_once = true;
                        }

                        while (true)
                        {
                                water_data.UpdatePlayerPos(player_pos);
                                structure_grid.UpdatePlayerPos(player_pos);
                                if (Halt) return;
                                if (!generated_all_chunks)
                                {
                                        continue;
                                }
                                var current_player_chunk_grid_pos = world_to_grid_pos(player_pos);
                                if (last_player_chunk_grid_pos == current_player_chunk_grid_pos)
                                {
                                        Task.Delay(ChunksGenMillisecondsDelay);
                                        continue;
                                }
                                //TODO: if the offset is to big clear everything and run fresh

                                var grid_pos_delta = current_player_chunk_grid_pos - last_player_chunk_grid_pos;

                                // TODO: Wrap this in a nice funciton
                                if (!chunk_change_for_position_delta.TryGetValue(grid_pos_delta, out var chunk_change))
                                {
                                        last_player_chunk_grid_pos = current_player_chunk_grid_pos;
                                        clear_all = true;
                                        load_at_once = true;
                                        while (clear_all)
                                        {
                                                Task.Delay(ChunksGenMillisecondsDelay);
                                        }
                                        {
                                                var chunks_to_generate = GetAllChunksPositionsInsideACircleRelative(view_distance, chunk_size);
                                                RunTerrainGeneration(chunks_to_generate.ToArray(), last_player_chunk_grid_pos * chunk_size, water_data, structure_grid);
                                        }
                                        continue;
                                }


                                foreach (var chunk_relative_pos in chunk_change.to_destroy_relative_pos)
                                {
                                        Vector2I chunk_world_position = chunk_relative_pos + last_player_chunk_grid_pos * chunk_size;
                                        // TODO: Wrap this in a nice funciton
                                        if (!chunk_per_world_position.TryGetValue(chunk_world_position, out var chunk))
                                        {
                                                last_player_chunk_grid_pos = current_player_chunk_grid_pos;
                                                clear_all = true;
                                                load_at_once = true;
                                                while (clear_all)
                                                {
                                                        Task.Delay(ChunksGenMillisecondsDelay);
                                                }
                                                {
                                                        var chunks_to_generate = GetAllChunksPositionsInsideACircleRelative(view_distance, chunk_size);
                                                        RunTerrainGeneration(chunks_to_generate.ToArray(), last_player_chunk_grid_pos * chunk_size, water_data, structure_grid);
                                                }
                                                continue;
                                        }

                                        free_data_maps.Enqueue(chunk.biome_map_index);
                                        chunk.QueueFree();
                                        chunk_per_world_position.Remove(chunk_world_position);

                                }

                                last_player_chunk_grid_pos = current_player_chunk_grid_pos;
                                RunTerrainGeneration(chunk_change.to_generate_relative_pos, current_player_chunk_grid_pos * chunk_size, water_data, structure_grid);
                                load_at_once = false;
                                generated_all_chunks = false;
                        }
                }
                catch (Exception e)
                {
                        GD.PrintErr($"ChunkDataGenerationLoop failed: {e}");
                }
        }
        private void RunTerrainGeneration(Vector2I[] chunks_to_generate,
    Vector2I player_pos_snapped_to_chunk, WaterGen.WaterDataGrid water_grid, StructureGen.StructureGrid structure_grid)
        {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                Parallel.For(0, chunks_to_generate.Length, i =>
                  {
                          var chunk = chunks_to_generate[i];
                          Vector2I chunk_world_position = chunk + player_pos_snapped_to_chunk;

                          var chunk_water_data = water_grid[chunk_world_position];

                          structure_grid[chunk_world_position].structure_gen_for_mesh_chunk_world_pos.TryGetValue(chunk_world_position, out var chunk_structure_data);
                          // if (chunk_water_data.world_pos_lakes.ContainsKey(chunk_world_position))
                          //         GD.Print("Lake chunk!");

                          var biome_data = biome_generator.GenerateMaps(terrain_aspects_solver, new Vector2(chunk_world_position.X, chunk_world_position.Y), chunk_size + 1, biomes);
                          var mesh_data = ground_mesh_gen.GenerateChunk(chunk_world_position, ground_mesh_resolution, chunk_size, chunk_water_data);

                          var objects_data = objects_generator.GenerateObjectsData(chunk_size, ground_mesh_gen, biome_data, chunk_world_position, biomes, structure_grid, water_grid);

                          completed_chunks.Enqueue(new(mesh_data, biome_data, chunk_world_position, objects_data, chunk_structure_data));
                  });
                stopwatch.Stop();
        }
        Task chunk_data_gen_task;
        ConcurrentQueue<ChunkData> completed_chunks = new();

        Queue<int> free_data_maps;
        ImageTexture[] map_1;
        ImageTexture[] map_2;
        private void HandleChunkGenerationQue()
        {

                int processed = 0;
                bool refresh_biome_map_data = false;

                while ((processed < max_chunks_processed_per_frame || load_at_once) &&
                       completed_chunks.TryDequeue(out var chunk_data))
                {
                        HandleGodotSideOfChunk(chunk_data);
                        refresh_biome_map_data = true;
                        processed++;
                }
                if (refresh_biome_map_data)
                {
                        ground_shader_material.SetShaderParameter("map_1", map_1);
                        ground_shader_material.SetShaderParameter("map_2", map_2);
                }
                else { generated_all_chunks = true; }

        }
        private void HandleGodotSideOfChunk(ChunkData chunk_data)
        {

                var chunk = (Chunk)chunk_prefab.Instantiate();
                chunk_per_world_position.Add(chunk_data.world_pos, chunk);

                AddChild(chunk);


                ground_mesh_gen.ApplyData(chunk_data.mesh, chunk.mesh_instance, chunk.collider);

                int map_index = free_data_maps.Dequeue();
                chunk.biome_map_index = map_index;
                map_1[map_index] = chunk_data.biome.GetTexture(0);
                map_2[map_index] = chunk_data.biome.GetTexture(1);
                chunk.mesh_instance.MaterialOverride = ground_shader_material;
                chunk.mesh_instance.SetInstanceShaderParameter("chunk_data_map_index", map_index);

                objects_generator.SpawnObjects(chunk_data.objects_data, chunk);
                if (chunk_data.mesh.lake_spawning_data != null)
                        water_gen.HandleSpawningForChunk(chunk_data.world_pos, chunk_data.mesh.lake_spawning_data, chunk);
                chunk_data.structure?.Instantiate(chunk);

        }
        private void Upd()
        {
                player_pos = new Vector2(player.Position.X, player.Position.Z) + player_pos_offset;
                HandleChunkGenerationQue();
                if (clear_all)
                {
                        ClearAll();
                }
        }
        private void ClearAll()
        {
                free_data_maps = new(Enumerable.Range(0, max_chunk_data_textures_count));
                map_1 = new ImageTexture[max_chunk_data_textures_count];
                map_2 = new ImageTexture[max_chunk_data_textures_count];
                chunk_change_for_position_delta = new();
                chunk_per_world_position = new();
                Vector2I delta = new(-1, 0);
                chunk_change_for_position_delta.Add(delta, CaluclateChunkChangeForPosDelta(delta));
                delta = new(-1, 1);
                chunk_change_for_position_delta.Add(delta, CaluclateChunkChangeForPosDelta(delta));
                delta = new(0, 1);
                chunk_change_for_position_delta.Add(delta, CaluclateChunkChangeForPosDelta(delta));
                delta = new(1, 1);
                chunk_change_for_position_delta.Add(delta, CaluclateChunkChangeForPosDelta(delta));
                delta = new(1, 0);
                chunk_change_for_position_delta.Add(delta, CaluclateChunkChangeForPosDelta(delta));
                delta = new(1, -1);
                chunk_change_for_position_delta.Add(delta, CaluclateChunkChangeForPosDelta(delta));
                delta = new(0, -1);
                chunk_change_for_position_delta.Add(delta, CaluclateChunkChangeForPosDelta(delta));
                delta = new(-1, -1);
                chunk_change_for_position_delta.Add(delta, CaluclateChunkChangeForPosDelta(delta));

                ClearAllChildren();
                clear_all = false;
        }

        private void Init()
        {
                if (chunk_data_gen_task == null)
                {
                        chunk_data_gen_task = Task.Run(() => ChunkDataGenerationLoop());
                }

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
