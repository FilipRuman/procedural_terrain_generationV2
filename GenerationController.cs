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
        [Export] int terrain_chunk_size;
        [Export] Biome[] biomes;
        [Export] bool stop_terrain_generation_task;
        [Export] int max_main_thread_chunk_instantiation_per_frame;

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
                MainThreadGenerationQue();
                if (clear_all)
                {
                        ClearAll();
                }
        }

        private void RunClean()
        {
                if (max_chunk_data_textures_count != GetAllChunksPositionsInsideACircleRelative(view_distance, terrain_chunk_size).Count)
                {
                        GD.PushWarning("The max amount of chunk data textures is not equal to the chunk data textures that are generated.\n" +
                                "This is not optimal and could cause chunks biomes to stop working:\n" +
                                $"current:{max_chunk_data_textures_count} optimal:{GetAllChunksPositionsInsideACircleRelative(view_distance, terrain_chunk_size).Count}");
                }
                ClearAll();

                ground_mesh_gen.Initialize(terrain_chunk_size);

                Task.Run(ChunkDataGenerationLoop);
                ground_shader_controller.SetShaderConfiguration(biomes);
        }

        private ChunkChange CalculateChunkChangeForPosChange(Vector2I delta)
        {
                delta *= terrain_chunk_size;

                HashSet<Vector2I> old_chunk_pos = [.. GetAllChunksPositionsInsideACircleRelative(view_distance, terrain_chunk_size)];
                HashSet<Vector2I> new_chunk_pos = [.. old_chunk_pos.Select(pos => pos + delta)];

                var to_destroy = old_chunk_pos.Except(new_chunk_pos).ToArray();

                List<Vector2I> to_generate = [];
                foreach (var chunk in old_chunk_pos)
                {
                        var new_pos = chunk + delta;
                        if (!old_chunk_pos.Contains(new_pos))
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
                return new Vector2I(Mathf.RoundToInt(world_pos.X / terrain_chunk_size), Mathf.RoundToInt(world_pos.Y / terrain_chunk_size));
        }
        Dictionary<Vector2I, Chunk> chunk_per_world_position;
        Dictionary<Vector2I, ChunkChange> chunk_change_for_position_delta = [];

        bool generated_all_chunks = false;
        bool clear_all;
        bool load_at_once;
        // Run as a task on the main thread
        private void ChunkDataGenerationLoop()
        {
                const int ChunksGenMSDelay = 2;
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
                                if (stop_terrain_generation_task) return;
                                if (!generated_all_chunks)
                                {
                                        continue;
                                }
                                var current_player_chunk_grid_pos = WorldToGridPos(player_pos);
                                if (last_player_chunk_grid_pos == current_player_chunk_grid_pos)
                                {
                                        Task.Delay(ChunksGenMSDelay);
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
                                        Vector2I chunk_world_position = chunk_relative_pos + last_player_chunk_grid_pos * terrain_chunk_size;

                                        if (!chunk_per_world_position.TryGetValue(chunk_world_position, out var chunk))
                                        {
                                                last_player_chunk_grid_pos = current_player_chunk_grid_pos;
                                                ReGenerateTheWholeTerrain(last_player_chunk_grid_pos);
                                                continue;
                                        }

                                        free_biome_texture_slots.Enqueue(chunk.biome_map_index);
                                        chunk.QueueFree();
                                        chunk_per_world_position.Remove(chunk_world_position);

                                }

                                last_player_chunk_grid_pos = current_player_chunk_grid_pos;
                                RunTerrainGeneration(chunk_change.to_generate_relative_pos, current_player_chunk_grid_pos * terrain_chunk_size);
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
                                Task.Delay(ChunksGenMSDelay);
                        }
                        {
                                var chunks_to_generate = GetAllChunksPositionsInsideACircleRelative(view_distance, terrain_chunk_size);
                                RunTerrainGeneration(chunks_to_generate.ToArray(), player_chunk_grid_pos * terrain_chunk_size);
                        }
                }
        }
        private void RunTerrainGeneration(Vector2I[] chunks_to_generate, Vector2I player_pos_snapped_to_chunk)
        {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                Parallel.For(0, chunks_to_generate.Length, i =>
                  {
                          try
                          {

                                  var chunk = chunks_to_generate[i];
                                  Vector2I chunk_world_position = chunk + player_pos_snapped_to_chunk;

                                  var biome_data = biome_generator.GenerateTextureData(new Vector2(chunk_world_position.X, chunk_world_position.Y), terrain_chunk_size + 1, biomes);
                                  var mesh_data = ground_mesh_gen.GenerateChunkData(chunk_world_position);
                                  chunks_to_instantiate_on_main_thread.Enqueue(new(mesh_data, biome_data, chunk_world_position));
                          }
                          catch (Exception e)
                          {
                                  GD.PrintErr($"RunTerrainGeneration- parallel loop failed: {e}");
                          }
                  });
                stopwatch.Stop();
        }
        ConcurrentQueue<ChunkData> chunks_to_instantiate_on_main_thread = new();
        private void MainThreadGenerationQue()
        {
                int processed = 0;
                while ((processed < max_main_thread_chunk_instantiation_per_frame || load_at_once) &&
                       chunks_to_instantiate_on_main_thread.TryDequeue(out var chunk_data))
                {
                        MainThreadChunkInstantiation(chunk_data);
                        processed++;
                }
                // send updated biome textures only once processed all textures in a burst
                if (processed != 0)
                {
                        ground_shader_controller.UpdateTheBiomeTextures(biome_textures_channel_1, biome_textures_channel_2);
                }
                else
                {
                        generated_all_chunks = true;
                }

        }

        Queue<int> free_biome_texture_slots;
        ImageTexture[] biome_textures_channel_1;
        ImageTexture[] biome_textures_channel_2;
        private void MainThreadChunkInstantiation(ChunkData chunk_data)
        {

                var chunk = (Chunk)chunk_prefab.Instantiate();
                chunk_per_world_position.Add(chunk_data.world_pos, chunk);

                AddChild(chunk);
                ground_mesh_gen.ApplyData(chunk_data.mesh_data, chunk.mesh_instance, chunk.collider);

                int map_index = free_biome_texture_slots.Dequeue();
                chunk.biome_map_index = map_index;
                biome_textures_channel_1[map_index] = chunk_data.biome.GetTexture(0);
                biome_textures_channel_2[map_index] = chunk_data.biome.GetTexture(1);
                chunk.mesh_instance.SetInstanceShaderParameter("biome_texture_index", map_index);
        }
        private void ClearAll()
        {
                free_biome_texture_slots = new(Enumerable.Range(0, max_chunk_data_textures_count));
                biome_textures_channel_1 = new ImageTexture[max_chunk_data_textures_count];
                biome_textures_channel_2 = new ImageTexture[max_chunk_data_textures_count];
                chunk_change_for_position_delta = [];
                chunk_per_world_position = [];
                Vector2I delta = new(-1, 0);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosChange(delta));
                delta = new(-1, 1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosChange(delta));
                delta = new(0, 1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosChange(delta));
                delta = new(1, 1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosChange(delta));
                delta = new(1, 0);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosChange(delta));
                delta = new(1, -1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosChange(delta));
                delta = new(0, -1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosChange(delta));
                delta = new(-1, -1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosChange(delta));

                foreach (var child in GetChildren())
                {
                        child.QueueFree();
                }
                clear_all = false;
        }
}
