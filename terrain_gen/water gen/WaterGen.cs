using System.Collections.Generic;
using Godot;
using static LakeGen;
[Tool]
public partial class WaterGen : Node3D
{
        [Export] public RiverGen river_gen;
        [Export] LakeGen lake_gen;
        [Export] Material test_material;
        public float ChunkSize(float mesh_chunk_size)
        {
                return mesh_chunk_size * mesh_chunks_per_water_chunk;
        }
        public void HandleSpawningForChunk(Vector2I mesh_chunk_world_pos, LakeGen.LakeSpawningData lake_spawning_data, Node3D parent_node)
        {
                lake_gen.SpawnLake(parent_node, lake_spawning_data);
        }
        public struct ChunkHeightGrid
        {
                public float[] grid;
                public uint grid_width;

                public float? TryGetValue(int x, int y)
                {
                        if (x >= grid_width || y >= grid_width || x < 0 || y < 0)
                        {
                                return null;
                        }
                        return this[x, y];

                }

                public float this[Vector2I pos]
                {
                        get
                        {
                                return grid[pos.X + pos.Y * grid_width];
                        }
                        set
                        {
                                grid[pos.X + pos.Y * grid_width] = value;
                        }
                }
                public float this[int x, int y]
                {
                        get
                        {
                                return grid[x + y * grid_width];
                        }
                        set
                        {
                                grid[x + y * grid_width] = value;
                        }
                }
                public ChunkHeightGrid(uint grid_width)
                {
                        this.grid_width = grid_width;
                        grid = new float[grid_width * grid_width];
                }
        }

        [Export] public uint mesh_chunks_per_water_chunk;

        [Export] uint height_checks_per_chunk_sqrt;
        [Export] uint height_checks_for_lake_system_sqrt;

        public class OutputData(Dictionary<Vector2I, LakeData> world_pos_lakes, RiverGen.RiverDataGrid river_grid, Vector2I test_world_base_pos)
        {
                public Dictionary<Vector2I, LakeData> world_pos_lakes = world_pos_lakes;
                public RiverGen.RiverDataGrid river_grid = river_grid;
                public Vector2I test_world_base_pos = test_world_base_pos;
        }
        public class WaterDataGrid
        {
                const int grid_width = 3;
                private readonly OutputData[] grid;
                private Vector2I current_player_grid_pos;

                private readonly int grid_cell_size;
                private readonly WaterGen water_gen;
                private readonly ThreadSafeGroundMeshGen mesh_gen;
                private readonly int mesh_chunk_size;
                public OutputData this[Vector2I world_pos]
                {
                        get
                        {
                                var global_grid_pos = world_pos / grid_cell_size;
                                var relative_grid_pos = global_grid_pos - current_player_grid_pos;

                                return grid[relative_grid_pos.X + 1 + (relative_grid_pos.Y + 1) * grid_width];
                        }

                }
                public bool IsObjectUnderTheWater(Vector3 world_pos_f)
                {
                        Vector2I world_pos = new((int)world_pos_f.X, (int)world_pos_f.Z);
                        Vector3 mesh_chunk_pos_f = world_pos_f / mesh_chunk_size;
                        Vector2I mesh_chunk_pos = new((int)mesh_chunk_pos_f.X, (int)mesh_chunk_pos_f.Z);
                        Vector2I mesh_chunk_world_pos = mesh_chunk_pos * mesh_chunk_size;
                        if (this[world_pos].world_pos_lakes.TryGetValue(mesh_chunk_world_pos, out var lake))
                        {
                                return lake.water_height > world_pos_f.Y;
                        }
                        return false;

                }

                public void UpdatePlayerPos(Vector2 player_world_pos)
                {
                        var new_player_grid_pos = new Vector2I((int)player_world_pos.X / grid_cell_size, (int)player_world_pos.Y / grid_cell_size);
                        var delta = current_player_grid_pos - new_player_grid_pos;
                        if (delta == Vector2I.Zero)
                        {
                                return;
                        }
                        current_player_grid_pos = new_player_grid_pos;

                        // this is expensive, but allows for really fast access of the data 
                        var grid_copy = (OutputData[])grid.Clone();
                        for (int x = 0; x < grid_width; x++)
                        {
                                for (int y = 0; y < grid_width; y++)
                                {
                                        var new_x = x - delta.X;
                                        var new_y = y - delta.Y;
                                        if (new_x < 0 || new_y < 0 || new_x >= grid_width || new_y >= grid_width)
                                        {
                                                return;
                                        }
                                        if (new_x == 1 || new_y == 1)
                                        {
                                                //Generate new cell data
                                                var world_x = (x - 1 + new_player_grid_pos.X) * grid_cell_size;
                                                var world_y = (y - 1 + new_player_grid_pos.Y) * grid_cell_size;
                                                grid[x + y * grid_width] = water_gen.GenerateCell(new(world_x, world_y), mesh_gen, mesh_chunk_size);
                                        }

                                        grid[new_x + new_y * grid_width] = grid_copy[x + y * grid_width];
                                }
                        }

                }
                public WaterDataGrid(WaterGen water_gen, ThreadSafeGroundMeshGen mesh_gen, int mesh_chunk_size, Vector2 player_world_pos)
                {
                        grid_cell_size = (int)water_gen.mesh_chunks_per_water_chunk * mesh_chunk_size;
                        this.water_gen = water_gen;
                        this.mesh_gen = mesh_gen;
                        this.mesh_chunk_size = mesh_chunk_size;

                        current_player_grid_pos = new Vector2I((int)player_world_pos.X / grid_cell_size, (int)player_world_pos.Y / grid_cell_size);
                        grid = new OutputData[grid_width * grid_width];
                        for (int x = 0; x < grid_width; x++)
                        {
                                for (int y = 0; y < grid_width; y++)
                                {
                                        //Generate new cell data
                                        var world_x = (x - 1 + current_player_grid_pos.X) * grid_cell_size;
                                        var world_y = (y - 1 + current_player_grid_pos.Y) * grid_cell_size;
                                        grid[x + y * grid_width] = water_gen.GenerateCell(new(world_x, world_y), mesh_gen, mesh_chunk_size);
                                }
                        }
                }
        }
        public OutputData GenerateCell(Vector2I world_base_pos, ThreadSafeGroundMeshGen mesh_gen, int mesh_chunk_size)
        {
                List<LakeGen.LakeData> lakes = [];
                List<Vector2I> river_start_points = [];

                ChunkHeightGrid average_height_grid = new(mesh_chunks_per_water_chunk);
                ChunkHeightGrid min_height_grid = new(mesh_chunks_per_water_chunk);
                for (int x = 0; x < mesh_chunks_per_water_chunk; x++)
                {
                        for (int y = 0; y < mesh_chunks_per_water_chunk; y++)
                        {
                                var chunk_base_pos = world_base_pos + new Vector2(x, y) * mesh_chunk_size;
                                GetChunkHeightStats(mesh_chunk_size, chunk_base_pos, mesh_gen, out float average_height, out float min_height);
                                average_height_grid[x, y] = average_height;
                                min_height_grid[x, y] = min_height;

                                AddChunkToLakesOrRivers(average_height, new(x, y), ref lakes, ref river_start_points);
                        }
                }
                var rivers = new RiverGen.RiverGoalsData[river_start_points.Count];
                for (int i = 0; i < river_start_points.Count; i++)
                {
                        Vector2I river_start_point = river_start_points[i];
                        var min_distance = float.MaxValue;
                        LakeData min_distance_lake = null;
                        foreach (var lake in lakes)
                        {
                                var distance = river_start_point.DistanceSquaredTo(lake.chunk_grid_pos);
                                if (distance < min_distance)
                                {
                                        min_distance = distance;
                                        min_distance_lake = lake;
                                }
                        }
                        rivers[i] = new(river_start_point, min_distance_lake.chunk_grid_pos);
                }
                var world_pos_lakes = lake_gen.GenerateForWaterChunk(lakes, min_height_grid, mesh_gen, world_base_pos, mesh_chunk_size);
                var river_data = river_gen.GenerateDataForWaterChunk(average_height_grid, rivers, (int)mesh_chunks_per_water_chunk, world_base_pos, mesh_chunk_size);

                return new(world_pos_lakes, river_data, world_base_pos);
        }




        private void AddChunkToLakesOrRivers(float average_height, Vector2I chunk, ref List<LakeData> lakes, ref List<Vector2I> river_start_points)
        {
                if (lake_gen.IsChunkLake(average_height))
                        lakes.Add(new(chunk));
                if (river_gen.DoesChunkContainRiverStart(average_height))
                        river_start_points.Add(chunk);
        }

        public void GetChunkHeightStats(int mesh_chunk_size, Vector2 chunk_base_world_pos, ThreadSafeGroundMeshGen mesh_gen, out float average_height, out float min_height)
        {
                var sum = 0f;
                min_height = float.MaxValue;
                var distance_per_check = mesh_chunk_size / (float)height_checks_per_chunk_sqrt;
                for (int x = -1; x < height_checks_per_chunk_sqrt + 1; x++)
                {
                        for (int y = -1; y < height_checks_per_chunk_sqrt + 1; y++)
                        {
                                var pos = chunk_base_world_pos + new Vector2(x, y) * distance_per_check;
                                var height = mesh_gen.CalculateHeight(pos);
                                sum += height;
                                min_height = Mathf.Min(min_height, height);
                        }
                }

                average_height = sum / ((height_checks_per_chunk_sqrt + 2) * (height_checks_per_chunk_sqrt + 2));
        }


}
