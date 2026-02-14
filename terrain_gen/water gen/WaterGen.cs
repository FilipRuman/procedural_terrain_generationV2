using System.Collections.Generic;
using System.Linq;
using Godot;
[Tool]
public partial class WaterGen : Node3D
{
        [Export] PackedScene LakeWater1x1;
        [Export] Material test_material;
        [Export] Material test_material2;
        public float ChunkSize(float mesh_chunk_size)
        {
                return mesh_chunk_size * mesh_chunks_per_water_chunk;
        }
        public void HandleSpawningForChunk(Vector2I mesh_chunk_world_pos, LakeSpawningData lake_spawning_data, Node3D parent_node)
        {

                var water_node = (Node3D)LakeWater1x1.Instantiate();
                parent_node.AddChild(water_node);

                water_node.Scale = lake_spawning_data.scale;
                water_node.Position = lake_spawning_data.pos;
                // {
                //     var meshInstance = new MeshInstance3D();
                //     meshInstance.Position = new(mesh_chunk_world_pos.X, lake_spawning_data.water_height, mesh_chunk_world_pos.Y);
                //     var sphereMesh = new SphereMesh
                //     {
                //         Radius = 6.0f,
                //         Height = 6.0f,  // diameter
                //         RadialSegments = 32,
                //         Rings = 16
                //     };
                //     sphereMesh.Material = test_material;
                //
                //     meshInstance.Mesh = sphereMesh;
                //     AddChild(meshInstance);
                // }

                if (lake_spawning_data.test_points2 != null)
                        foreach (var test_point in lake_spawning_data.test_points2)
                        {
                                var meshInstance = new MeshInstance3D();
                                meshInstance.Position = test_point;
                                var sphereMesh = new SphereMesh
                                {
                                        Radius = 4.0f,
                                        Height = 4.0f,  // diameter
                                        RadialSegments = 32,
                                        Rings = 16
                                };

                                sphereMesh.Material = test_material2;

                                meshInstance.Mesh = sphereMesh;
                                AddChild(meshInstance);
                        }
                // foreach (var test_point in lake_spawning_data.test_points)
                // {
                //     var meshInstance = new MeshInstance3D();
                //     meshInstance.Position = test_point;
                //     var sphereMesh = new SphereMesh
                //     {
                //         Radius = 1.0f,
                //         Height = 2.0f,  // diameter
                //         RadialSegments = 32,
                //         Rings = 16
                //     };
                //
                //     meshInstance.Mesh = sphereMesh;
                //     AddChild(meshInstance);
                // }
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

        [Export] float lake_height;
        [Export] float river_start_height;
        [Export] public uint mesh_chunks_per_water_chunk;
        [Export] uint height_checks_per_chunk_sqrt;
        [Export] uint height_checks_for_lake_system_sqrt;
        [Export] float lake_water_level_offset;
        public class LakeSpawningData(float water_height, float water_mesh_margin)
        {
                float min_x = float.MaxValue;
                float max_x = float.MinValue;
                float min_z = float.MaxValue;
                float max_z = float.MinValue;
                public float water_height = water_height;
                public List<Vector3> test_points = [];
                public List<Vector3> test_points2;
                public Vector3 scale;
                public Vector3 pos;
                private readonly float water_mesh_margin = water_mesh_margin;

                public void HandleNewVertex(Vector3 vertex)
                {

                        if (vertex.Y > water_height)
                                return;
                        // GD.Print("handle_new_vertex- is under the water_height");
                        // there is a faster way
                        min_x = Mathf.Min(vertex.X, min_x);
                        max_x = Mathf.Max(vertex.X, max_x);
                        min_z = Mathf.Min(vertex.Z, min_z);
                        max_z = Mathf.Max(vertex.Z, max_z);
                        // test_points.Add(new(vertex.X, water_height, vertex.Z));
                        test_points.Add(vertex);
                }

                public void FinishCalculation()
                {
                        var length_x = max_x - min_x + water_mesh_margin;
                        var length_z = max_z - min_z + water_mesh_margin;
                        scale = new(length_x, 1, length_z);
                        pos = new Vector3(min_x - water_mesh_margin / 2f + length_x / 2f, water_height, min_z - water_mesh_margin / 2f + length_z / 2f);
                }
        }
        public class OutputData(Dictionary<Vector2I, LakeData> world_pos_lakes)
        {
                public Dictionary<Vector2I, LakeData> world_pos_lakes = world_pos_lakes;
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

                                GD.Print($"world_pos:{world_pos} global_grid_pos:{global_grid_pos} grid_cell_size{grid_cell_size} current_player_grid_pos:{current_player_grid_pos}, min world pos_x: {(current_player_grid_pos.X - 1) * grid_cell_size}");
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
                List<LakeData> lakes = new();
                List<Vector2I> river_start_points = new();

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

                Dictionary<Vector2I, LakeData> world_pos_lakes = new();
                var connected_lakes = ConnectLakeTiles(lakes);
                foreach (var lake_system in connected_lakes)
                {
                        var water_height = GetWatterLevelOfLakeSystem(min_height_grid, lake_system.Value, mesh_gen, world_base_pos, mesh_chunk_size);
                        foreach (var lake in lake_system.Value)
                        {
                                var world_pos = world_base_pos + lake.chunk_grid_pos * mesh_chunk_size;
                                world_pos_lakes.Add(world_pos, lake);
                                lake.water_height = water_height;
                        }
                }

                return new(world_pos_lakes);
        }


        /// Find all border chunks.
        /// Get the lowest point at all of the chunks border -> Set it as the water level, minus some margin like 1.
        private float GetWatterLevelOfLakeSystem(ChunkHeightGrid min_height_grid, List<LakeData> lake_system, ThreadSafeGroundMeshGen mesh_gen, Vector2I world_base_pos, int mesh_chunk_size)
        {

                var border_chunks = GetBorderChunksForLakeSystem(lake_system);

                var min_height_for_border_chunks = float.MaxValue;

                lake_system[0].test_points = new();
                foreach (var chunk in border_chunks)
                {

                        float? chunk_min_height = min_height_grid.TryGetValue(chunk.X, chunk.Y);

                        //calculate chunk height on the fly. this will only be triggered for the chunks at the end of the lake cell
                        if (chunk_min_height == null)
                        {
                                var chunk_world_pos = world_base_pos + chunk * mesh_chunk_size;
                                GetChunkHeightStats(mesh_chunk_size, chunk_world_pos, mesh_gen, out _, out var min_height);
                                // GD.Print($"border chunk- {chunk_world_pos}, min height{min_height} lake_system- {lake_system[0]}, current min height{min_height_for_border_chunks}");
                                chunk_min_height = min_height;

                                // var sum = 0f;
                                // min_height = float.MaxValue;
                                // var distance_per_check = mesh_chunk_size / height_checks_per_chunk_sqrt;
                                // for (int x = 0; x < height_checks_per_chunk_sqrt; x++)
                                // {
                                //         for (int y = 0; y < height_checks_per_chunk_sqrt; y++)
                                //         {
                                //                 var pos = chunk_world_pos + new Vector2(x, y) * distance_per_check;
                                //                 var height = mesh_gen.CalculateHeight(pos);
                                //                 sum += height;
                                //                 min_height = Mathf.Min(min_height, height);
                                //
                                //                 lake_system[0].test_points.Add(new(pos.X, height, pos.Y));
                                //         }
                                // }
                        }

                        min_height_for_border_chunks = Mathf.Min(min_height_for_border_chunks, chunk_min_height.Value);

                        {
                                var chunk_world_pos = world_base_pos + chunk * mesh_chunk_size;
                                // lake_system[0].test_points.Add(new(chunk_world_pos.X, chunk_min_height.Value, chunk_world_pos.Y));
                        }
                }
                return min_height_for_border_chunks - lake_water_level_offset;
        }
        private HashSet<Vector2I> GetBorderChunksForLakeSystem(List<LakeData> lake_system)
        {
                HashSet<Vector2I> border_chunks = new();

                foreach (var lake in lake_system)
                {
                        border_chunks.Add(lake.chunk_grid_pos - new Vector2I(-1, 1));
                        border_chunks.Add(lake.chunk_grid_pos - new Vector2I(0, 1));
                        border_chunks.Add(lake.chunk_grid_pos - new Vector2I(1, 1));
                        border_chunks.Add(lake.chunk_grid_pos - new Vector2I(-1, 0));
                        border_chunks.Add(lake.chunk_grid_pos - new Vector2I(1, 0));
                        border_chunks.Add(lake.chunk_grid_pos - new Vector2I(-1, -1));
                        border_chunks.Add(lake.chunk_grid_pos - new Vector2I(0, -1));
                        border_chunks.Add(lake.chunk_grid_pos - new Vector2I(1, -1));
                }
                foreach (var lake in lake_system)
                {
                        border_chunks.Remove(lake.chunk_grid_pos);
                }
                return border_chunks;
        }



        // TODO: CleanUp
        private Dictionary<int, List<LakeData>> ConnectLakeTiles(List<LakeData> all_lakes)
        {
                // Build spatial lookup for O(1) neighbor checks
                var lake_map = all_lakes.ToDictionary(l => l.chunk_grid_pos);

                // Find neighbors and connect
                foreach (var lake in all_lakes)
                {
                        var neighbors = new[] {
            lake.chunk_grid_pos + new Vector2I(1, 0),
            lake.chunk_grid_pos + new Vector2I(-1, 0),
            lake.chunk_grid_pos + new Vector2I(0, 1),
            lake.chunk_grid_pos + new Vector2I(0, -1)
        };

                        foreach (var neighbor_pos in neighbors)
                        {
                                if (lake_map.TryGetValue(neighbor_pos, out var neighbor))
                                {
                                        if (!lake.connected_lakes.Contains(neighbor))
                                        {
                                                lake.connected_lakes.Add(neighbor);
                                                neighbor.connected_lakes.Add(lake);
                                        }
                                }
                        }
                }

                // Assign system IDs using flood fill
                int current_system_id = 0;
                foreach (var lake in all_lakes)
                {
                        if (lake.lake_system_id == -1)
                        {
                                FloodFillSystemId(lake, current_system_id);
                                current_system_id++;
                        }
                }

                // Group by system ID
                return all_lakes
                    .GroupBy(l => l.lake_system_id)
                    .ToDictionary(g => g.Key, g => g.ToList());
        }

        private void FloodFillSystemId(LakeData start, int system_id)
        {
                var stack = new Stack<LakeData>();
                stack.Push(start);

                while (stack.Count > 0)
                {
                        var current = stack.Pop();

                        if (current.lake_system_id != -1)
                                continue;

                        current.lake_system_id = system_id;

                        foreach (var neighbor in current.connected_lakes)
                        {
                                if (neighbor.lake_system_id == -1)
                                {
                                        stack.Push(neighbor);
                                }
                        }
                }
        }
        private void SetConnectedLakeIdRecursive(LakeData lake, int id, HashSet<LakeData> done)
        {
                lake.lake_system_id = id;
                foreach (var connected in lake.connected_lakes)
                {
                        if (done.Contains(connected))
                                continue;
                        connected.lake_system_id = id;
                        done.Add(connected);
                        SetConnectedLakeIdRecursive(connected, id, done);
                }
        }
        private static int ManhattanDistance(Vector2I a, Vector2I b)
        {
                return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
        }

        public class LakeData
        {
                public int lake_system_id = -1;
                public Vector2I chunk_grid_pos;//?
                public float water_height;
                public List<LakeData> connected_lakes;
                public List<Vector3> test_points;

                public LakeData(Vector2I pos)
                {
                        this.connected_lakes = new();
                        this.chunk_grid_pos = pos;
                }
        }
        private void AddChunkToLakesOrRivers(float average_height, Vector2I chunk, ref List<LakeData> lakes, ref List<Vector2I> river_start_points)
        {
                if (average_height < lake_height)
                {
                        lakes.Add(new(chunk));
                        return;
                }
                if (average_height > river_start_height)
                {
                        river_start_points.Add(chunk);
                }

        }
        private void GetChunkHeightStats(int mesh_chunk_size, Vector2 chunk_base_world_pos, ThreadSafeGroundMeshGen mesh_gen, out float average_height, out float min_height)
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
