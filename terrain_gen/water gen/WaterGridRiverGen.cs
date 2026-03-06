using System.Collections.Generic;
using Godot;

[Tool]
public partial class WaterGridRiverGen : Node3D
{
        [Export] float river_start_height;


        public class MeshChunkDataGrid
        {
                readonly int mesh_triangles_count;
                readonly int river_width;
                readonly int cell_width;
                HashSet<Vector3I>[] river_vertexes_relative_pos_grid_with_height;
                readonly Curve river_effect_curve;
                readonly int grid_width;
                readonly int river_height;
                readonly float[] base_mesh_height_map;
                public HashSet<Vector3I> this[Vector2I grid_pos]
                {
                        get
                        {
                                return river_vertexes_relative_pos_grid_with_height[grid_pos.X + grid_pos.Y * grid_width];
                        }
                }
                public MeshChunkDataGrid(int mesh_triangles_count, int max_river_width, Curve river_effect_curve, int river_height, float[] base_mesh_height_map)
                {
                        this.mesh_triangles_count = mesh_triangles_count;
                        this.river_effect_curve = river_effect_curve;
                        this.river_height = river_height;
                        this.base_mesh_height_map = base_mesh_height_map;

                        river_width = max_river_width;
                        cell_width = max_river_width * 2;
                        grid_width = Mathf.CeilToInt(mesh_triangles_count / (float)cell_width);
                        river_vertexes_relative_pos_grid_with_height = new HashSet<Vector3I>[grid_width * grid_width];
                        for (int i = 0; i < river_vertexes_relative_pos_grid_with_height.Length; i++)
                        {
                                river_vertexes_relative_pos_grid_with_height[i] = [];
                        }
                }
                private Vector2I RelativeMeshToGridPos(Vector2I relative_mesh_pos) => relative_mesh_pos / cell_width;

                public void AddNewRiverVertex(Vector2I relative_mesh_pos, out bool already_contains_a_river)
                {
                        var base_height = Mathf.FloorToInt(base_mesh_height_map[relative_mesh_pos.X + relative_mesh_pos.Y * mesh_triangles_count]);
                        already_contains_a_river = !this[RelativeMeshToGridPos(relative_mesh_pos)].Add(new(relative_mesh_pos.X, base_height - river_height, relative_mesh_pos.Y));
                }
                public float GetMeshHeightAfterRiver(Vector2I relative_mesh_pos)
                {
                        GetClosestRiverVertexDistance(relative_mesh_pos, out float distance, out Vector3I vertex);

                        var base_height = base_mesh_height_map[relative_mesh_pos.X + relative_mesh_pos.Y * mesh_triangles_count];
                        if (distance > river_width)
                                return base_height;

                        var height_delta = vertex.Y - base_height;
                        return base_height + height_delta * river_effect_curve.SampleBaked(distance);
                }
                public bool IsVertexInsideARiver(Vector2I relative_mesh_pos)
                {
                        var grid_cells = GetAllRelevantGridCells(relative_mesh_pos);
                        foreach (var cell in grid_cells)
                        {
                                foreach (var river_vertex in cell)
                                {
                                        if (relative_mesh_pos.DistanceTo(new(river_vertex.X, river_vertex.Z)) < river_width)
                                        {
                                                return true;
                                        }
                                }
                        }
                        return false;
                }
                private HashSet<Vector3I>[] GetAllRelevantGridCells(Vector2I relative_mesh_pos)
                {
                        var base_grid_pos = RelativeMeshToGridPos(relative_mesh_pos);
                        int max = grid_width - 1;

                        bool left = base_grid_pos.X > 0;
                        bool right = base_grid_pos.X < max;
                        bool down = base_grid_pos.Y > 0;
                        bool up = base_grid_pos.Y < max;

                        List<HashSet<Vector3I>> output = [];

                        if (right && up) output.Add(this[base_grid_pos + new Vector2I(1, 1)]);
                        if (up) output.Add(this[base_grid_pos + new Vector2I(0, 1)]);
                        if (left && up) output.Add(this[base_grid_pos + new Vector2I(-1, 1)]);
                        if (right) output.Add(this[base_grid_pos + new Vector2I(1, 0)]);
                        if (left) output.Add(this[base_grid_pos + new Vector2I(-1, 0)]);
                        if (right && down) output.Add(this[base_grid_pos + new Vector2I(1, -1)]);
                        if (down) output.Add(this[base_grid_pos + new Vector2I(0, -1)]);
                        if (left && down) output.Add(this[base_grid_pos + new Vector2I(-1, -1)]);

                        output.Add(this[base_grid_pos + new Vector2I(0, 0)]);

                        return [.. output];
                }


                public void GetClosestRiverVertexDistance(Vector2I relative_mesh_pos, out float distance, out Vector3I vertex)
                {
                        var grid_cells = GetAllRelevantGridCells(relative_mesh_pos);
                        distance = float.MaxValue;
                        vertex = Vector3I.Zero;

                        foreach (var cell in grid_cells)
                        {
                                foreach (var river_vertex in cell)
                                {
                                        var dist = relative_mesh_pos.DistanceTo(new(river_vertex.X, river_vertex.Z));
                                        if (distance > dist)
                                        {
                                                distance = dist;
                                                vertex = river_vertex;
                                        }
                                }
                        }
                }

        }
        public class RiverDataGrid
        {
                private readonly MeshChunkRiverData[] grid;
                private readonly int mesh_chunks_per_water_chunk;
                public RiverDataGrid(int mesh_chunks_per_water_chunk, Vector2I base_world_pos, int mesh_chunk_size)
                {

                        grid = new MeshChunkRiverData[mesh_chunks_per_water_chunk * mesh_chunks_per_water_chunk];
                        this.mesh_chunks_per_water_chunk = mesh_chunks_per_water_chunk;

                        for (int i = 0; i < mesh_chunks_per_water_chunk * mesh_chunks_per_water_chunk; i++)
                        {
                                Vector2I pos = new(i % mesh_chunks_per_water_chunk, i / mesh_chunks_per_water_chunk);

                                grid[i] = new(pos, base_world_pos + mesh_chunk_size * pos);
                        }
                }
                public MeshChunkRiverData AccessDataWithWorldPos(Vector2I world_pos, int mesh_chunk_size)
                {
                        var grid_pos = world_pos / mesh_chunk_size;
                        return this[grid_pos % mesh_chunks_per_water_chunk];
                }
                public MeshChunkRiverData this[Vector2I chunk_grid_pos]
                {
                        get
                        {
                                return grid[chunk_grid_pos.X + chunk_grid_pos.Y * mesh_chunks_per_water_chunk];
                        }
                        set
                        {
                                grid[chunk_grid_pos.X + chunk_grid_pos.Y * mesh_chunks_per_water_chunk] = value;
                        }
                }
        }
        public class MeshChunkRiverData(Vector2I pos, Vector2I base_world_pos)
        {
                public bool contains_river = false;
                public Vector2I pos = pos;
                public bool is_end;
                public List<Vector2I> previous_mesh_chunk_pos = [];
                public Vector2I? next_mesh_chunk_pos = null;
                public Vector2I base_world_pos = base_world_pos;
                public MeshChunksRiverGen.RiverWaterMeshData river_water_mesh_data;
                public MeshChunkDataGrid mesh_chunk_data_grid;
        }
        public bool DoesChunkContainRiverStart(float average_height)
        {
                return average_height > river_start_height;
        }
        public struct RiverGoalsData(Vector2I start_mesh_chunk_grid_pos, Vector2I end_lake_mesh_chunk_grid_pos)
        {
                //TODO: Change to relative pos 
                public Vector2I start_mesh_chunk_grid_pos = start_mesh_chunk_grid_pos;
                public Vector2I end_lake_mesh_chunk_grid_pos = end_lake_mesh_chunk_grid_pos;
        }

        public RiverDataGrid GenerateDataForWaterChunk(WaterGen.ChunkHeightGrid average_height_grid, RiverGoalsData[] rivers, int mesh_chunks_per_water_chunk, Vector2I base_world_pos, int mesh_chunk_size)
        {
                RiverDataGrid grid = new(mesh_chunks_per_water_chunk, base_world_pos, mesh_chunk_size);
                foreach (var current_river_data in rivers)
                {
                        var current_chunk_grid_pos = current_river_data.start_mesh_chunk_grid_pos;

                        while (true)
                        {
                                grid[current_chunk_grid_pos].contains_river = true;
                                var neighbours = GetNeighbourChunks(current_chunk_grid_pos, mesh_chunks_per_water_chunk);

                                if (IsConnectedWithTheEndPoint(current_chunk_grid_pos, current_river_data.end_lake_mesh_chunk_grid_pos))
                                {
                                        grid[current_chunk_grid_pos].next_mesh_chunk_pos = current_river_data.end_lake_mesh_chunk_grid_pos;

                                        var end_cell = grid[current_river_data.end_lake_mesh_chunk_grid_pos];
                                        end_cell.contains_river = true;
                                        end_cell.previous_mesh_chunk_pos.Add(current_chunk_grid_pos);
                                        end_cell.is_end = true;
                                        break;
                                }

                                var next_chunk_world_pos = GetChunkWithTheMostPoints(average_height_grid, neighbours, current_river_data.end_lake_mesh_chunk_grid_pos);
                                grid[current_chunk_grid_pos].next_mesh_chunk_pos = next_chunk_world_pos;
                                grid[next_chunk_world_pos].previous_mesh_chunk_pos.Add(current_chunk_grid_pos);
                                if (grid[next_chunk_world_pos].next_mesh_chunk_pos != null)
                                {
                                        break;
                                }

                                current_chunk_grid_pos = next_chunk_world_pos;
                        }
                }
                return grid;
        }

        [Export] float height_points_modifier;
        [Export] float distance_points_modifier;
        public static bool IsConnectedWithTheEndPoint(Vector2I current_chunk_world_pos, Vector2I end_chunk_world_pos)
        {
                return Mathf.Abs(end_chunk_world_pos.X - current_chunk_world_pos.X) + Mathf.Abs(end_chunk_world_pos.Y - current_chunk_world_pos.Y) <= 1;
        }
        public Vector2I GetChunkWithTheMostPoints(WaterGen.ChunkHeightGrid average_height_grid, List<Vector2I> chunks, Vector2I end_goal)
        {
                var max_points = float.MinValue;
                var max_points_chunk = Vector2I.Zero;
                foreach (var chunk in chunks)
                {
                        var points = -chunk.DistanceTo(end_goal) * distance_points_modifier - height_points_modifier * average_height_grid[chunk];
                        if (max_points < points)
                        {
                                max_points = points;
                                max_points_chunk = chunk;
                        }
                }
                return max_points_chunk;

        }
        public static List<Vector2I> GetNeighbourChunks(Vector2I base_chunk_grid_pos, int mesh_chunks_per_water_chunk)
        {
                List<Vector2I> output = [];

                if (base_chunk_grid_pos.X < mesh_chunks_per_water_chunk - 1)
                        output.Add(base_chunk_grid_pos + new Vector2I(1, 0));
                if (base_chunk_grid_pos.Y < mesh_chunks_per_water_chunk - 1)
                        output.Add(base_chunk_grid_pos + new Vector2I(0, 1));

                if (base_chunk_grid_pos.X > 0)
                        output.Add(base_chunk_grid_pos + new Vector2I(-1, 0));
                if (base_chunk_grid_pos.Y > 0)
                        output.Add(base_chunk_grid_pos + new Vector2I(0, -1));

                return output;

        }


}
