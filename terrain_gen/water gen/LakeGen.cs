using Godot;
using System.Collections.Generic;
using System.Linq;
using static WaterGen;

[Tool]
public partial class LakeGen : Node
{

        [Export] WaterGen water_gen;
        [Export] PackedScene LakeWater1x1;

        [Export] public float lake_height;
        [Export] public float lake_water_level_offset;

        public class LakeData(Vector2I pos)
        {
                public int lake_system_id = -1;
                public Vector2I chunk_grid_pos = pos;//?
                public float water_height;
                public List<LakeData> connected_lakes = [];
                public List<Vector3> test_points;
        }
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
                        // there is a faster way
                        min_x = Mathf.Min(vertex.X, min_x);
                        max_x = Mathf.Max(vertex.X, max_x);
                        min_z = Mathf.Min(vertex.Z, min_z);
                        max_z = Mathf.Max(vertex.Z, max_z);

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
        public Dictionary<Vector2I, LakeData> GenerateForWaterChunk(List<LakeGen.LakeData> lakes, ChunkHeightGrid min_height_grid, ThreadSafeGroundMeshGen mesh_gen,
                                Vector2I world_base_pos, int mesh_chunk_size)
        {
                Dictionary<Vector2I, LakeData> world_pos_lakes = [];
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

                return world_pos_lakes;
        }

        public void SpawnLake(Node3D parent_node, LakeSpawningData lake_spawning_data)
        {
                var water_node = (Node3D)LakeWater1x1.Instantiate();
                parent_node.AddChild(water_node);

                water_node.Scale = lake_spawning_data.scale;
                water_node.Position = lake_spawning_data.pos;
        }
        private static void SetConnectedLakeIdRecursive(LakeData lake, int id, HashSet<LakeData> done)
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

        public bool IsChunkLake(float average_height)
        {
                return average_height < lake_height;
        }
        // TODO: CleanUp
        private static Dictionary<int, List<LakeData>> ConnectLakeTiles(List<LakeData> all_lakes)
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

        private static void FloodFillSystemId(LakeData start, int system_id)
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

        /// Find all border chunks.
        /// Get the lowest point at all of the chunks border -> Set it as the water level, minus some margin like 1.
        private float GetWatterLevelOfLakeSystem(ChunkHeightGrid min_height_grid, List<LakeData> lake_system, ThreadSafeGroundMeshGen mesh_gen, Vector2I world_base_pos, int mesh_chunk_size)
        {

                var border_chunks = GetBorderChunksForLakeSystem(lake_system);

                var min_height_for_border_chunks = float.MaxValue;

                lake_system[0].test_points = [];
                foreach (var chunk in border_chunks)
                {

                        float? chunk_min_height = min_height_grid.TryGetValue(chunk.X, chunk.Y);

                        //calculate chunk height on the fly. this will only be triggered for the chunks at the end of the lake cell
                        if (chunk_min_height == null)
                        {
                                var chunk_world_pos = world_base_pos + chunk * mesh_chunk_size;
                                // river_margin_size- whatever
                                water_gen.GetChunkHeightStats(river_margin_size: 1, mesh_chunk_size, chunk_world_pos, mesh_gen, out _, out var min_height, out float _);
                                chunk_min_height = min_height;

                        }

                        min_height_for_border_chunks = Mathf.Min(min_height_for_border_chunks, chunk_min_height.Value);
                        {
                                var chunk_world_pos = world_base_pos + chunk * mesh_chunk_size;
                        }
                }
                return min_height_for_border_chunks - lake_water_level_offset;
        }
        private static HashSet<Vector2I> GetBorderChunksForLakeSystem(List<LakeData> lake_system)
        {
                HashSet<Vector2I> border_chunks = [];

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




        private static int ManhattanDistance(Vector2I a, Vector2I b)
        {
                return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
        }

}
