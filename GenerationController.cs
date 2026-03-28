using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
[Tool]
public partial class GenerationController : Node
{
        [ExportToolButton("Run")] private Callable RunButton => Callable.From(RunClean);
        [Export] int chunk_size;

        [Export] int ground_mesh_resolution;
        [Export] Biome[] biomes;

        [Export] bool Halt;
        [Export] int max_chunks_processed_per_frame;

        [ExportGroup("player")]
        [Export] Vector2 player_pos_offset;
        [Export] Vector2 player_pos;
        [Export] Node3D player;
        [Export] int view_distance;

        [ExportCategory("references")]
        [Export] PackedScene chunk_prefab;
        [Export] GroundMeshGen ground_mesh_gen;
        [Export] BiomeGenerator biome_generator;
        [Export] GroundShaderController ground_shader_controller;


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
                player_pos = new(player.Position.X, player.Position.Z);
                HandleChunkGenerationQue();
                if (clear_all)
                {
                        ClearAll();
                }

        }

        private void RunClean()
        {
                if (max_chunk_data_textures_count != GetAllChunksPositionsInsideACircleRelative(view_distance, chunk_size).Count)
                {
                        GD.PushWarning("The max amount of chunk data textures is not equal to the chunk data textures that are generated.\n" +
                                "This is not optimal and could cause chunks biomes to stop working:\n" +
                                $"current:{max_chunk_data_textures_count} optimal:{GetAllChunksPositionsInsideACircleRelative(view_distance, chunk_size).Count}");

                }
                ClearAll();
                Init();
        }

        private ChunkChange CalculateChunkChangeForPosDelta(Vector2I delta)
        {
                delta *= chunk_size;
                var chunks = GetAllChunksPositionsInsideACircleRelative(view_distance, chunk_size);

                var oldSet = new HashSet<Vector2I>(chunks.Select(c => c));
                var newSet = new HashSet<Vector2I>(oldSet.Select(p => p + delta));

                var to_destroy = oldSet.Except(newSet).ToArray();

                List<Vector2I> to_generate = [];
                foreach (var chunk in chunks)
                {
                        var new_pos = chunk + delta;
                        if (!oldSet.Contains(new_pos))
                        {
                                to_generate.Add(chunk);
                        }
                }

                return new ChunkChange(to_destroy, [.. to_generate]);
        }

        private static List<Vector2I> GetAllChunksPositionsInsideACircleRelative(int radius, int chunk_size)
        {
                List<Vector2I> output = [];

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

        public struct ChunkData(GroundMeshGen.MeshData mesh_data, BiomeGenerator.TextureData biome, Vector2I world_pos)
        {
                public GroundMeshGen.MeshData mesh_data = mesh_data;
                public BiomeGenerator.TextureData biome = biome;
                public Vector2I world_pos = world_pos;
        }




        struct ChunkChange(Vector2I[] chunks_to_destroy_relative_positions, Vector2I[] chunks_to_instantiate)
        {
                public Vector2I[] to_destroy_relative_pos = chunks_to_destroy_relative_positions;
                public Vector2I[] to_generate_relative_pos = chunks_to_instantiate;
        }
        Vector2I WorldToGridPos(Vector2 world_pos)
        {
                return new Vector2I(Mathf.RoundToInt(world_pos.X / chunk_size), Mathf.RoundToInt(world_pos.Y / chunk_size));
        }
        Dictionary<Vector2I, Chunk> chunk_per_world_position;
        Dictionary<Vector2I, ChunkChange> chunk_change_for_position_delta = [];

        bool generated_all_chunks = false;
        bool clear_all;
        bool load_at_once;
        // Run as a task on the main thread
        private void ChunkDataGenerationLoop()
        {
                const int ChunksGenMillisecondsDelay = 2;
                try
                {
                        var last_player_chunk_grid_pos = WorldToGridPos(player_pos);
                        // Initial terrain generation
                        {
                                ReGenerateTheWholeTerrain(last_player_chunk_grid_pos);
                                ReGenerateTheWholeTerrain(last_player_chunk_grid_pos);
                        }

                        while (true)
                        {
                                if (Halt) return;
                                if (!generated_all_chunks)
                                {
                                        continue;
                                }
                                var current_player_chunk_grid_pos = WorldToGridPos(player_pos);
                                if (last_player_chunk_grid_pos == current_player_chunk_grid_pos)
                                {
                                        Task.Delay(ChunksGenMillisecondsDelay);
                                        continue;
                                }
                                var grid_pos_delta = current_player_chunk_grid_pos - last_player_chunk_grid_pos;

                                if (!chunk_change_for_position_delta.TryGetValue(grid_pos_delta, out var chunk_change))
                                {
                                        last_player_chunk_grid_pos = current_player_chunk_grid_pos;
                                        ReGenerateTheWholeTerrain(last_player_chunk_grid_pos);
                                        continue;
                                }


                                foreach (var chunk_relative_pos in chunk_change.to_destroy_relative_pos)
                                {
                                        Vector2I chunk_world_position = chunk_relative_pos + last_player_chunk_grid_pos * chunk_size;

                                        if (!chunk_per_world_position.TryGetValue(chunk_world_position, out var chunk))
                                        {
                                                last_player_chunk_grid_pos = current_player_chunk_grid_pos;
                                                ReGenerateTheWholeTerrain(last_player_chunk_grid_pos);
                                                continue;
                                        }

                                        free_data_maps.Enqueue(chunk.biome_map_index);
                                        chunk.QueueFree();
                                        chunk_per_world_position.Remove(chunk_world_position);

                                }

                                last_player_chunk_grid_pos = current_player_chunk_grid_pos;
                                RunTerrainGeneration(chunk_change.to_generate_relative_pos, current_player_chunk_grid_pos * chunk_size);
                                load_at_once = false;
                                generated_all_chunks = false;
                        }
                }
                catch (Exception e)
                {
                        GD.PrintErr($"ChunkDataGenerationLoop failed: {e}");
                }

                void ReGenerateTheWholeTerrain(Vector2I player_chunk_grid_pos)
                {
                        clear_all = true;
                        load_at_once = true;
                        while (clear_all)
                        {
                                Task.Delay(ChunksGenMillisecondsDelay);
                        }
                        {
                                var chunks_to_generate = GetAllChunksPositionsInsideACircleRelative(view_distance, chunk_size);
                                RunTerrainGeneration(chunks_to_generate.ToArray(), player_chunk_grid_pos * chunk_size);
                        }
                }
        }
        private void RunTerrainGeneration(Vector2I[] chunks_to_generate,
    Vector2I player_pos_snapped_to_chunk)
        {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                Parallel.For(0, chunks_to_generate.Length, i =>
                  {
                          try
                          {

                                  var chunk = chunks_to_generate[i];
                                  Vector2I chunk_world_position = chunk + player_pos_snapped_to_chunk;

                                  var biome_data = biome_generator.GenerateTextureData(new Vector2(chunk_world_position.X, chunk_world_position.Y), chunk_size + 1, biomes, i);
                                  var mesh_data = ground_mesh_gen.GenerateChunkData(chunk_world_position);
                                  completed_chunks.Enqueue(new(mesh_data, biome_data, chunk_world_position));
                          }
                          catch (Exception e)
                          {
                                  GD.PrintErr($"RunTerrainGeneration- parallel loop failed: {e}");
                          }
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
                        ground_shader_controller.UpdateTheBiomeTextures(map_1, map_2);
                }
                else { generated_all_chunks = true; }

        }
        private void HandleGodotSideOfChunk(ChunkData chunk_data)
        {

                var chunk = (Chunk)chunk_prefab.Instantiate();
                chunk_per_world_position.Add(chunk_data.world_pos, chunk);

                AddChild(chunk);
                ground_mesh_gen.ApplyData(chunk_data.mesh_data, chunk.mesh_instance, chunk.collider);

                int map_index = free_data_maps.Dequeue();
                chunk.biome_map_index = map_index;
                map_1[map_index] = chunk_data.biome.GetTexture(0);
                map_2[map_index] = chunk_data.biome.GetTexture(1);
                chunk.mesh_instance.SetInstanceShaderParameter("biome_texture_index", map_index);
        }
        private void ClearAll()
        {
                free_data_maps = new(Enumerable.Range(0, max_chunk_data_textures_count));
                map_1 = new ImageTexture[max_chunk_data_textures_count];
                map_2 = new ImageTexture[max_chunk_data_textures_count];
                chunk_change_for_position_delta = [];
                chunk_per_world_position = [];
                Vector2I delta = new(-1, 0);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosDelta(delta));
                delta = new(-1, 1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosDelta(delta));
                delta = new(0, 1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosDelta(delta));
                delta = new(1, 1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosDelta(delta));
                delta = new(1, 0);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosDelta(delta));
                delta = new(1, -1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosDelta(delta));
                delta = new(0, -1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosDelta(delta));
                delta = new(-1, -1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosDelta(delta));

                foreach (var item in GetChildren())
                {
                        item.QueueFree();
                }
                clear_all = false;
        }

        private void Init()
        {
                ground_mesh_gen.Initialize(ground_mesh_resolution, chunk_size);
                Task.Run(ChunkDataGenerationLoop);
                ground_shader_controller.SetShaderConfiguration(biomes);
        }

}
