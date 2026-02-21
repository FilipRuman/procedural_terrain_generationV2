using System;
using System.Collections.Generic;
using Godot;
[Tool]
public partial class RiverGen : Node
{
        [Export] Curve river_effect_curve;
        [Export] int max_river_width;
        int Margin => max_river_width + 12;
        [Export] WaterGen water_gen;
        [Export] Material test_material_1;
        [Export] Material test_material_2;
        [Export] Material test_material_3;
        [Export] Material test_material_4;

        [Export] float test_point_size;
        public void InstantiateRiver(MeshChunkRiverData river_data, Node3D parent_node)
        {
                foreach (var point in river_data.test_points_1)
                {
                        var mesh_inst = new MeshInstance3D();
                        mesh_inst.Position = new Vector3I(point.X * 2, point.Y, point.Z * 2) + new Vector3I(river_data.base_world_pos.X, 0, river_data.base_world_pos.Y);
                        mesh_inst.Scale = Vector3.One * test_point_size;
                        var mesh = new SphereMesh()
                        {
                                Rings = 5,
                                Radius = 5,
                                Height = 5
                        };
                        mesh_inst.Mesh = mesh;
                        mesh_inst.MaterialOverride = test_material_1;
                        parent_node.AddChild(mesh_inst);
                }
                foreach (var point in river_data.test_points_2)
                {
                        var mesh_inst = new MeshInstance3D();
                        mesh_inst.Position = new Vector3I(point.X * 2, point.Y, point.Z * 2) + new Vector3I(river_data.base_world_pos.X, 0, river_data.base_world_pos.Y);
                        mesh_inst.Scale = Vector3.One * test_point_size;
                        var mesh = new SphereMesh()
                        {
                                Rings = 8,
                                Radius = 8,
                                Height = 8,
                        };
                        mesh_inst.Mesh = mesh;
                        mesh_inst.MaterialOverride = test_material_2;
                        parent_node.AddChild(mesh_inst);
                }
                foreach (var point in river_data.test_points_3)
                {
                        var mesh_inst = new MeshInstance3D
                        {
                                Position = new Vector3I(point.X * 2, point.Y, point.Z * 2) + new Vector3I(river_data.base_world_pos.X, 0, river_data.base_world_pos.Y),
                                Scale = Vector3.One * test_point_size
                        };
                        var mesh = new SphereMesh()
                        {
                                Rings = 8,
                                Radius = 8,
                                Height = 8,
                        };
                        mesh_inst.Mesh = mesh;
                        mesh_inst.MaterialOverride = test_material_3;
                        parent_node.AddChild(mesh_inst);
                }


                foreach (var point in river_data.test_points_5)
                {
                        var mesh_inst = new MeshInstance3D
                        {
                                Position = new Vector3I(point.X * 2, point.Y, point.Z * 2) + new Vector3I(river_data.base_world_pos.X, 0, river_data.base_world_pos.Y),
                                Scale = Vector3.One * test_point_size
                        };
                        var mesh = new SphereMesh()
                        {
                                Rings = 8,
                                Radius = 8,
                                Height = 8,
                        };
                        mesh_inst.Mesh = mesh;
                        mesh_inst.MaterialOverride = test_material_4;
                        parent_node.AddChild(mesh_inst);
                }
                foreach (var point in river_data.test_points_4)
                {
                        var mesh_inst = new MeshInstance3D
                        {
                                Position = new Vector3I(point.X * 2, point.Y, point.Z * 2) + new Vector3I(river_data.base_world_pos.X, 0, river_data.base_world_pos.Y),
                                Scale = Vector3.One * test_point_size
                        };
                        var mesh = new SphereMesh()
                        {
                                Rings = 8,
                                Radius = 8,
                                Height = 8,
                        };
                        mesh_inst.Mesh = mesh;
                        mesh_inst.MaterialOverride = test_material_1;
                        parent_node.AddChild(mesh_inst);
                }
        }

        Vector2I base_chunk_world_pos; //TEST
        public MeshChunkDataGrid GenerateMeshChunkData(Vector2I base_chunk_world_pos, MeshChunkRiverData river_data, int mesh_chunk_size, int mesh_triangles_count, float[] base_vertex_height_map, float? lake_height)
        {
                this.base_chunk_world_pos = base_chunk_world_pos;

                var relative_mesh_pos = base_chunk_world_pos / mesh_chunk_size % (int)water_gen.mesh_chunks_per_water_chunk;
                var output = new MeshChunkDataGrid(mesh_triangles_count, max_river_width, river_effect_curve);
                GetEndAndStartMeshPossitions(river_data, mesh_triangles_count, relative_mesh_pos, base_vertex_height_map, out var relative_start_pos, out var relative_end_pos_option, out var margin_override_direction);
                Vector2I relative_end_pos;
                if (relative_end_pos_option == null)
                {
                        if (river_data.test_points_2.Count != 0)
                                GD.Print("END POS  == null");
                        if (lake_height == null)
                        {
                                GD.Print($"lake_height == null but this chunk should contain a lake! base:{base_chunk_world_pos} - relative_mesh_pos{relative_mesh_pos}, river_data.next_mesh_chunk_pos==null:{river_data.next_mesh_chunk_pos == null}, river_data.pos{river_data.pos} river_data.next_mesh_chunk_pos:{river_data.next_mesh_chunk_pos} end:{river_data.is_end}");
                        }
                        relative_end_pos = GetALakePos(mesh_triangles_count, base_vertex_height_map, lake_height.Value);
                }
                else
                {
                        relative_end_pos = relative_end_pos_option.Value;
                }

                foreach (var current_start_pos in relative_start_pos)
                {

                        var current_vertex = current_start_pos.vertex_pos;
                        output.AddNewRiverVertex(current_vertex, out _);

                        while (true)
                        {

                                if (river_data.test_points_2.Count != 0)
                                {
                                        river_data.test_points_1.Add(new(current_vertex.X, (int)base_vertex_height_map[current_vertex.X + current_vertex.Y * mesh_triangles_count], current_vertex.Y));
                                }
                                var next_vertex = NextVertex(current_vertex, relative_end_pos, current_start_pos.vertex_pos, mesh_triangles_count, base_vertex_height_map, margin_override_direction, current_start_pos.margin_override_direction);


                                output.AddNewRiverVertex(next_vertex, out var already_contains_a_river);

                                if (current_vertex == relative_end_pos || already_contains_a_river)
                                {
                                        if (river_data.test_points_2.Count != 0)
                                        {
                                                GD.Print($"BREAK: {current_vertex} {relative_end_pos} {current_vertex == relative_end_pos} {already_contains_a_river}");
                                                if (current_vertex == relative_end_pos)
                                                        river_data.test_points_5.Add(new(current_vertex.X, (int)base_vertex_height_map[current_vertex.X + current_vertex.Y * mesh_triangles_count], current_vertex.Y));
                                        }
                                        break;

                                }


                                current_vertex = next_vertex;
                        }

                        if (current_start_pos.margin_override_direction != null)
                        {
                                var diagonal_dir = current_start_pos.margin_override_direction.Value.X != 0 ? Vector2I.Up : Vector2I.Right;
                                for (int offset = -1; offset < 2; offset++)
                                {
                                        output.AddNewRiverVertex(current_start_pos.vertex_pos + offset * diagonal_dir, out var _);
                                }

                        }
                }

                {
                        var diagonal_dir = margin_override_direction.X != 0 ? Vector2I.Up : Vector2I.Right;
                        for (int offset = -1; offset < 2; offset++)
                        {
                                output.AddNewRiverVertex(relative_end_pos + offset * diagonal_dir, out var _);
                        }

                }


                return output;
        }
        // temp
        private Vector2I GetALakePos(int mesh_triangles_count, float[] base_vertex_height_map, float lake_height)
        {
                for (int x = Margin; x < mesh_triangles_count - Margin; x++)
                {

                        for (int y = Margin; y < mesh_triangles_count - Margin; y++)
                        {
                                var i = x + y * mesh_triangles_count;
                                // TODO: Use Mesh Triangles instead of resolution for this
                                if (base_vertex_height_map[i] <= lake_height)
                                {
                                        return new(x, y);
                                }

                        }
                }
                GD.PushWarning("there was no lake pos inside this chunk!");
                return new(10, 10);
        }
        private Vector2I NextVertex(Vector2I current_vertex, Vector2I end_pos_vertex, Vector2I start_pos, int mesh_triangles_count, float[] base_vertex_height_map, Vector2I end_margin_override_direction, Vector2I? start_override_direction)
        {
                var vertexes_to_check = GetNeighbourVertexes(current_vertex, end_pos_vertex, start_pos, mesh_triangles_count, end_margin_override_direction, start_override_direction);
                var best_vertex_points = float.MinValue;
                var best_vertex = new Vector2I();
                foreach (var neighbour in vertexes_to_check)
                {
                        var vertex_index = neighbour.X + neighbour.Y * mesh_triangles_count;
                        if (vertex_index < 0 || vertex_index >= base_vertex_height_map.Length)
                                GD.Print($"vertex_index: {vertex_index} base_vertex:{neighbour} pos: {neighbour} width: {mesh_triangles_count}");
                        var height = base_vertex_height_map[vertex_index];
                        var points = GivePointsToVertex(neighbour, end_pos_vertex, height);
                        if (points > best_vertex_points)
                        {
                                best_vertex = neighbour;
                                best_vertex_points = points;
                        }
                }
                return best_vertex;
        }
        //TEMP
        private float GivePointsToVertex(Vector2I vertex, Vector2I relative_end_pos, float height) => -vertex.DistanceTo(relative_end_pos) * distance_points_modifier /* - height_points_modifier * height */;


        private List<Vector2I> GetNeighbourVertexes(Vector2I pos, Vector2I end_pos, Vector2I start_pos, int mesh_triangles_count, Vector2I end_margin_override_direction, Vector2I? start_override_direction)
        {
                // Skip the margin check for the side that contains transition to the next mesh chunk 
                int max = mesh_triangles_count - Margin;
                int min = Margin;
                List<Vector2I> output = [];

                //TODO: Better name
                var axis_diagonal = end_margin_override_direction.X != 0 ? Vector2I.Up : Vector2I.Right;
                bool can_override_the_margin;
                {
                        can_override_the_margin = (pos - end_pos) * axis_diagonal == Vector2I.Zero;
                }

                bool left = pos.X > min || (can_override_the_margin && end_margin_override_direction == Vector2I.Left && pos.X > 0);
                bool right = pos.X < max || (can_override_the_margin && end_margin_override_direction == Vector2I.Right && pos.X < mesh_triangles_count - 1);
                // IF YOU WORK WITH THE CARTESIAN SPACE AND MARK UP WITH NEGATIVE Y, I DONT EVEN WANT TO FUCKING KNOW YOU. I get that this is a screen space pos, but this is unintuitive and annoying anyway.
                bool down = pos.Y > min || (can_override_the_margin && end_margin_override_direction == Vector2I.Up && pos.Y > 0);
                bool up = pos.Y < max || (can_override_the_margin && end_margin_override_direction == Vector2I.Down && pos.Y < mesh_triangles_count - 1);

                if (start_override_direction != null)
                {
                        bool can_move_side_to_side = (pos * start_override_direction.Value.Abs()).DistanceTo(start_pos * start_override_direction.Value.Abs()) >= Margin;
                        if (!can_move_side_to_side)
                        {
                                if (start_override_direction.Value.X != 0)
                                {
                                        up = false;
                                        down = false;
                                }
                                else
                                {
                                        right = false;
                                        left = false;
                                }
                        }

                }

                if (right && up) output.Add(pos + new Vector2I(1, 1));
                if (left && up) output.Add(pos + new Vector2I(-1, 1));
                if (up) output.Add(pos + new Vector2I(0, 1));
                if (right) output.Add(pos + new Vector2I(1, 0));
                if (left) output.Add(pos + new Vector2I(-1, 0));
                if (right && down) output.Add(pos + new Vector2I(1, -1));
                if (down) output.Add(pos + new Vector2I(0, -1));
                if (left && down) output.Add(pos + new Vector2I(-1, -1));



                return output;
        }

        struct StartPoint(Vector2I vertex_pos, Vector2I? margin_override_direction)
        {
                public Vector2I vertex_pos = vertex_pos;
                public Vector2I? margin_override_direction = margin_override_direction;
        }
        private void GetEndAndStartMeshPossitions(MeshChunkRiverData river_data, int mesh_triangles_count, Vector2I base_chunk_grid_pos, float[] base_vertex_height_map,
                        out StartPoint[] start_points, out Vector2I? relative_end_pos, out Vector2I end_margin_override_direction)
        {

                //TODO: use relative pos 
                start_points = new StartPoint[river_data.previous_mesh_chunk_pos.Count];

                if (river_data.previous_mesh_chunk_pos.Count == 0)
                {
                        //TODO: Implement getting highest point
                        start_points = [new(new(mesh_triangles_count / 2, mesh_triangles_count / 2), null)];
                }
                else
                {
                        for (int i = 0; i < river_data.previous_mesh_chunk_pos.Count; i++)
                        {
                                Vector2I previous_chunk_pos = river_data.previous_mesh_chunk_pos[i];
                                // Make this just calculate the lowest point
                                var lowest_point = LowestPointAcrossChunkBorder(base_vertex_height_map, mesh_triangles_count, base_chunk_grid_pos, previous_chunk_pos, out var margin_override_direction);
                                start_points[i] = new(lowest_point, margin_override_direction);
                        }
                }


                if (river_data.next_mesh_chunk_pos != null)
                        relative_end_pos = LowestPointAcrossChunkBorder(base_vertex_height_map, mesh_triangles_count, base_chunk_grid_pos, river_data.next_mesh_chunk_pos.Value, out end_margin_override_direction);
                else
                {
                        end_margin_override_direction = Vector2I.Zero;
                        relative_end_pos = null;
                }

        }

        /// margin_override_direction ->  x or y  equal to: {-1,1} and the other axis to 0, tells at what direction the next chunk will be.
        private Vector2I LowestPointAcrossChunkBorder(float[] base_vertex_height_map, int mesh_triangles_count, Vector2I base_grid_pos, Vector2I next_chunk_grid_pos, out Vector2I margin_override_direction)
        {
                var relative_pos = next_chunk_grid_pos - base_grid_pos;
                margin_override_direction = relative_pos;
                var vertex_check_offset = (relative_pos + Vector2I.One) / 2 * (mesh_triangles_count - 1);
                var vertex_check_axis = relative_pos.X != 0 ? new Vector2I(0, 1) : new Vector2I(1, 0);
                var extreme_height = float.MaxValue;
                Vector2I extreme_height_pos = new();

                for (int i = Margin; i < mesh_triangles_count - Margin; i++)
                {

                        var pos = vertex_check_offset + i * vertex_check_axis;
                        int height_idx = pos.X + pos.Y * mesh_triangles_count;
                        if (height_idx > base_vertex_height_map.Length)
                                GD.Print($"LowestPointAcrossChunkBorder: pos:{pos} height_idx:{height_idx} base_vertex_height_map:{base_vertex_height_map.Length} mesh_triangles_count:{mesh_triangles_count} ");
                        var height = base_vertex_height_map[height_idx];

                        if (extreme_height > height)
                        {
                                extreme_height = height;
                                extreme_height_pos = pos;
                        }

                }
                return extreme_height_pos;
        }

        [Export] float river_start_height;

        public class MeshChunkDataGrid
        {
                readonly int river_width;
                readonly int cell_width;
                HashSet<Vector2I>[] river_vertexes_relative_pos_grid;
                readonly Curve river_effect_curve;
                readonly int grid_width;
                public HashSet<Vector2I> this[Vector2I grid_pos]
                {
                        get
                        {
                                return river_vertexes_relative_pos_grid[grid_pos.X + grid_pos.Y * grid_width];
                        }
                }
                public MeshChunkDataGrid(int mesh_triangles_count, int max_river_width, Curve river_effect_curve)
                {
                        this.river_effect_curve = river_effect_curve;

                        river_width = max_river_width;
                        cell_width = max_river_width * 2;
                        grid_width = Mathf.CeilToInt(mesh_triangles_count / (float)cell_width);
                        river_vertexes_relative_pos_grid = new HashSet<Vector2I>[grid_width * grid_width];
                        for (int i = 0; i < river_vertexes_relative_pos_grid.Length; i++)
                        {
                                river_vertexes_relative_pos_grid[i] = [];
                        }
                }
                private Vector2I RelativeMeshToGridPos(Vector2I relative_pos) => relative_pos / cell_width;


                public void RemoveNewRiverVertex(Vector2I relative_pos)
                {
                        this[RelativeMeshToGridPos(relative_pos)].Remove(relative_pos);
                }
                public void AddNewRiverVertex(Vector2I relative_pos, out bool already_contains_a_river)
                {
                        if (RelativeMeshToGridPos(relative_pos).X + RelativeMeshToGridPos(relative_pos).Y * grid_width > river_vertexes_relative_pos_grid.Length)
                                GD.Print($"AddNewRiverVertex: {relative_pos} {RelativeMeshToGridPos(relative_pos)} {grid_width}");
                        already_contains_a_river = !this[RelativeMeshToGridPos(relative_pos)].Add(relative_pos);
                }
                public float GetRiverEffectOnMeshVertex(Vector2I relative_pos)
                {
                        float dist = GetClosestRiverVertexDistance(relative_pos);
                        if (dist > river_width)
                                return 0;

                        return river_effect_curve.SampleBaked(dist) * river_width;
                }
                private HashSet<Vector2I>[] GetAllRelevantGridCells(Vector2I relative_pos)
                {
                        var base_pos = RelativeMeshToGridPos(relative_pos);
                        int max = grid_width - 1;

                        bool left = base_pos.X > 0;
                        bool right = base_pos.X < max;
                        bool down = base_pos.Y > 0;
                        bool up = base_pos.Y < max;

                        List<HashSet<Vector2I>> output = [];

                        if (right && up) output.Add(this[base_pos + new Vector2I(1, 1)]);
                        if (up) output.Add(this[base_pos + new Vector2I(0, 1)]);
                        if (left && up) output.Add(this[base_pos + new Vector2I(-1, 1)]);
                        if (right) output.Add(this[base_pos + new Vector2I(1, 0)]);
                        if (left) output.Add(this[base_pos + new Vector2I(-1, 0)]);
                        if (right && down) output.Add(this[base_pos + new Vector2I(1, -1)]);
                        if (down) output.Add(this[base_pos + new Vector2I(0, -1)]);
                        if (left && down) output.Add(this[base_pos + new Vector2I(-1, -1)]);

                        output.Add(this[base_pos + new Vector2I(0, 0)]);

                        return [.. output];
                }
                private float GetClosestRiverVertexDistance(Vector2I relative_pos)
                {
                        var grid_cells = GetAllRelevantGridCells(relative_pos);
                        var min_dist = float.MaxValue;
                        foreach (var cell in grid_cells)
                        {
                                foreach (var river_vertex in cell)
                                {
                                        min_dist = Mathf.Min(min_dist, relative_pos.DistanceTo(river_vertex));
                                }
                        }
                        return min_dist;
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
                        //TODO: REVIVE THIS CODE
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
                public Vector2I pos = pos;
                public bool is_end;
                public List<Vector2I> previous_mesh_chunk_pos = [];
                public Vector2I? next_mesh_chunk_pos = null;
                public Vector2I base_world_pos = base_world_pos;
                public List<Vector3I> test_points_1 = [];
                public List<Vector3I> test_points_2 = [];
                public List<Vector3I> test_points_5 = [];
                public List<Vector3I> test_points_3 = [];
                public List<Vector3I> test_points_4 = [];
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
                                var neighbours = GetNeighbourChunks(current_chunk_grid_pos, mesh_chunks_per_water_chunk);

                                if (IsConnectedWithTheEndPoint(current_chunk_grid_pos, current_river_data.end_lake_mesh_chunk_grid_pos))
                                {
                                        grid[current_chunk_grid_pos].next_mesh_chunk_pos = current_river_data.end_lake_mesh_chunk_grid_pos;
                                        var end_cell = grid[current_river_data.end_lake_mesh_chunk_grid_pos];
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
                ;
                grid[rivers[0].start_mesh_chunk_grid_pos].test_points_3.Add(new(0, 0, 0));
                grid[rivers[0].end_lake_mesh_chunk_grid_pos].test_points_3.Add(new(0, 0, 0));
                var current = rivers[0].start_mesh_chunk_grid_pos;
                while (true)
                {
                        var cell = grid[current];
                        cell.test_points_2.Add(new(0, 0, 0));
                        if (cell.next_mesh_chunk_pos == null)
                        {
                                break;
                        }
                        current = cell.next_mesh_chunk_pos.Value;
                }
                // Maybe test all grid cells?
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
