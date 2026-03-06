using System.Collections.Generic;
using Godot;
using static WaterGridRiverGen;
[Tool]
public partial class MeshChunksRiverGen : Node
{
        [Export] Curve river_effect_curve;
        [Export] int max_river_width;
        [Export] float river_water_margin;
        [Export] int additional_chunk_border_margin_for_rivers;
        [Export] int river_water_height;
        public int Margin => max_river_width + 1 + additional_chunk_border_margin_for_rivers;
        [Export] WaterGen water_gen;

        [Export] float test_point_size;
        [Export] Material test_material;
        [Export] Material test_material2;

        [Export] float height_points_modifier = 0;
        [Export] float distance_points_modifier = 1;

        [Export] PackedScene RiverPath;
        public void InstantiateRiver(MeshChunkRiverData river_data, Node3D parent_node)
        {
                if (river_data.river_water_mesh_data != null)
                {
                        foreach (var curve in river_data.river_water_mesh_data.river_curves)
                        {
                                var path = (Path3D)RiverPath.Instantiate();
                                parent_node.AddChild(path);
                                path.Curve = curve;
                        }
                }

        }

        public class RiverWaterMeshData(bool river_beginning, int river_width, float river_water_margin, float river_water_height_offset, Vector2I base_chunk_position, float space_between_vertexes,
                                        int mesh_triangles_count, float[] base_vertex_height_map, float? lake_height)
        {
                readonly float river_water_margin = river_water_margin;
                readonly float river_water_height_offset = river_water_height_offset;
                readonly Vector2I base_chunk_position = base_chunk_position;
                readonly float space_between_vertexes = space_between_vertexes;
                readonly int mesh_triangles_count = mesh_triangles_count;

                readonly float[] base_vertex_height_map = base_vertex_height_map;
                readonly float? lake_height = lake_height;

                public List<Curve3D> river_curves = [];
                Curve3D current_curve = new()
                {
                        BakeInterval = .2f,
                        UpVectorEnabled = false
                };


                private float CalculateVertexHeight(Vector2 vertex_local)
                {
                        vertex_local = vertex_local.Clamp(0, mesh_triangles_count - 1);
                        // Maybe remove later
                        var safe_pos = new Vector2I(Mathf.FloorToInt(vertex_local.X), Mathf.FloorToInt(vertex_local.Y));
                        var base_height = base_vertex_height_map[safe_pos.X + safe_pos.Y * mesh_triangles_count];

                        // Avoid seams between the water when river goes thru a lake
                        if (lake_height != null && Mathf.Abs(lake_height.Value - base_height) < river_water_margin)
                        {
                                // To avoid Z fighting
                                return lake_height.Value - 0.003f;
                        }
                        return base_height - river_water_height_offset;
                }
                private Vector3 LocalToGlobalVertex(Vector2I vertex)
                {
                        var global_2d = (Vector2)vertex * 2f * space_between_vertexes + base_chunk_position;
                        return new(global_2d.X, CalculateVertexHeight(vertex), global_2d.Y);
                }
                public void EndRiverBranch()
                {
                        river_curves.Add(current_curve);
                        current_curve = new()
                        {
                                BakeInterval = .2f,
                                UpVectorEnabled = false
                        };
                }
                public void HandleNormalCenterVertex(Vector2I relative_center_pos)
                {
                        var global_pos = LocalToGlobalVertex(relative_center_pos);
                        current_curve.AddPoint(global_pos);
                }
        }



        Vector2I base_chunk_world_pos; //TEST
        public MeshChunkDataGrid GenerateMeshChunkData(Vector2I base_chunk_world_pos, MeshChunkRiverData river_data, int mesh_chunk_size, int mesh_triangles_count, float[] base_vertex_height_map, float? lake_height)
        {
                this.base_chunk_world_pos = base_chunk_world_pos;

                var relative_mesh_pos = base_chunk_world_pos / mesh_chunk_size % (int)water_gen.mesh_chunks_per_water_chunk;
                var output = new MeshChunkDataGrid(mesh_triangles_count, max_river_width, river_effect_curve, river_water_height, base_vertex_height_map);
                GetEndAndStartMeshPossitions(river_data, mesh_triangles_count, relative_mesh_pos, base_vertex_height_map, out var relative_start_pos, out var relative_end_pos_option, out var margin_override_direction, out bool river_beginning);
                RiverWaterMeshData water_mesh_data = new(river_beginning, max_river_width, river_water_margin, river_water_height, base_chunk_world_pos,
                                                        space_between_vertexes: mesh_chunk_size / mesh_triangles_count, mesh_triangles_count, base_vertex_height_map, lake_height);

                Vector2I relative_end_pos;
                if (relative_end_pos_option == null)
                {
                        if (lake_height == null)
                        {
                                GD.Print($"lake_height == null but this chunk should contain a lake! base:{base_chunk_world_pos} - relative_mesh_pos{relative_mesh_pos}, river_data.next_mesh_chunk_pos==null:{river_data.next_mesh_chunk_pos == null}, river_data.pos{river_data.pos} river_data.next_mesh_chunk_pos:{river_data.next_mesh_chunk_pos} end:{river_data.is_end}");
                        }
                        relative_end_pos = GetALakePos(mesh_triangles_count, base_chunk_world_pos, base_vertex_height_map, lake_height.Value);
                }
                else
                {
                        relative_end_pos = relative_end_pos_option.Value;
                }
                // WARN: IF there are multiple start points this won't work
                foreach (var current_start_pos in relative_start_pos)
                {

                        // First river vertex

                        if (current_start_pos.margin_override_direction != null)
                        {
                                var diagonal_dir = current_start_pos.margin_override_direction.Value.X != 0 ? Vector2I.Up : Vector2I.Right;
                                for (int offset = -1; offset < 2; offset++)
                                {
                                        output.AddNewRiverVertex(current_start_pos.vertex_pos + offset * diagonal_dir, out var _);
                                }

                        }
                        var current_vertex = current_start_pos.vertex_pos;
                        output.AddNewRiverVertex(current_vertex, out _);
                        water_mesh_data.HandleNormalCenterVertex(current_vertex);
                        while (true)
                        {
                                var next_vertex = NextVertex(current_vertex, relative_end_pos, current_start_pos.vertex_pos, mesh_triangles_count, base_vertex_height_map, margin_override_direction,
                                                                 current_start_pos.margin_override_direction);
                                // DebugSpheresStatic.Spawn(next_vertex);
                                output.AddNewRiverVertex(next_vertex, out var already_contains_a_river);
                                water_mesh_data.HandleNormalCenterVertex(next_vertex);

                                if (next_vertex == relative_end_pos || already_contains_a_river)
                                {
                                        water_mesh_data.EndRiverBranch();
                                        break;
                                }

                                current_vertex = next_vertex;
                        }
                }

                {
                        var diagonal_dir = margin_override_direction.X != 0 ? Vector2I.Up : Vector2I.Right;
                        for (int offset = -1; offset < 2; offset++)
                        {
                                output.AddNewRiverVertex(relative_end_pos + offset * diagonal_dir, out var _);
                        }
                }
                river_data.river_water_mesh_data = water_mesh_data;

                return output;
        }
        // temp
        private Vector2I GetALakePos(int mesh_triangles_count, Vector2I base_chunk_world_pos, float[] base_vertex_height_map, float lake_height)
        {
                for (int x = Margin; x < mesh_triangles_count - Margin; x++)
                {

                        for (int y = Margin; y < mesh_triangles_count - Margin; y++)
                        {
                                var i = x + y * mesh_triangles_count;
                                if (base_vertex_height_map[i] <= lake_height)
                                {
                                        return new(x, y);
                                }

                        }
                }
                DebugSpheresStatic.Spawn(base_chunk_world_pos);
                GD.PushWarning("there was no lake pos inside this chunk!");
                return new(mesh_triangles_count / 2, mesh_triangles_count / 2);
        }
        private Vector2I NextVertex(Vector2I current_vertex, Vector2I end_pos_vertex, Vector2I start_pos, int mesh_triangles_count, float[] base_vertex_height_map,
                                        Vector2I end_margin_override_direction, Vector2I? start_override_direction)
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
        private float GivePointsToVertex(Vector2I vertex, Vector2I relative_end_pos, float height) => -vertex.DistanceTo(relative_end_pos) * distance_points_modifier - height_points_modifier * height;


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
                        out StartPoint[] start_points, out Vector2I? relative_end_pos, out Vector2I end_margin_override_direction, out bool river_beginning)
        {

                //TODO: use relative pos 
                start_points = new StartPoint[river_data.previous_mesh_chunk_pos.Count];

                if (river_data.previous_mesh_chunk_pos.Count == 0)
                {
                        var start_vertex = GetBestRiverStartPointVertex(mesh_triangles_count, base_vertex_height_map);
                        start_points = [new StartPoint(start_vertex, null)];
                        river_beginning = true;
                }
                else
                {

                        river_beginning = false;
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
        public Vector2I GetBestRiverStartPointVertex(int mesh_triangles_count, float[] base_vertex_height_map)
        {
                var max_height = float.MinValue;
                var max_height_pos = Vector2I.Zero;
                for (int x = Margin; x < mesh_triangles_count - Margin; x++)
                {
                        for (int y = Margin; y < mesh_triangles_count - Margin; y++)
                        {
                                var height = base_vertex_height_map[x + y * mesh_triangles_count];
                                if (height > max_height)
                                {
                                        max_height = height;
                                        max_height_pos = new(x, y);
                                }
                        }
                }
                return max_height_pos;
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


}
