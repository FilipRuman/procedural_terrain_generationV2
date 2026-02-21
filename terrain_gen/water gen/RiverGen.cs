using System.Collections.Generic;
using Godot;
[Tool]
public partial class RiverGen : Node
{
        [Export] Curve river_effect_curve;
        [Export] int max_river_width;
        int Margin => max_river_width + 2;
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
                                Rings = 10,
                                Radius = 15,
                                Height = 15,
                        };
                        mesh_inst.Mesh = mesh;
                        mesh_inst.MaterialOverride = test_material_2;
                        parent_node.AddChild(mesh_inst);
                }
                foreach (var point in river_data.test_points_3)
                {
                        var mesh_inst = new MeshInstance3D();
                        mesh_inst.Position = new Vector3I(point.X * 2, point.Y, point.Z * 2) + new Vector3I(river_data.base_world_pos.X, 0, river_data.base_world_pos.Y);
                        mesh_inst.Scale = Vector3.One * test_point_size;
                        var mesh = new SphereMesh()
                        {
                                Rings = 5,
                                Radius = 10,
                                Height = 10,
                        };
                        mesh_inst.Mesh = mesh;
                        mesh_inst.MaterialOverride = test_material_3;
                        parent_node.AddChild(mesh_inst);
                }


                foreach (var point in river_data.test_points_5)
                {
                        var mesh_inst = new MeshInstance3D();
                        mesh_inst.Position = new Vector3I(point.X * 2, point.Y, point.Z * 2) + new Vector3I(river_data.base_world_pos.X, 0, river_data.base_world_pos.Y);
                        mesh_inst.Scale = Vector3.One * test_point_size;
                        var mesh = new SphereMesh()
                        {
                                Rings = 5,
                                Radius = 20,
                                Height = 20,
                        };
                        mesh_inst.Mesh = mesh;
                        mesh_inst.MaterialOverride = test_material_4;
                        parent_node.AddChild(mesh_inst);
                }
                foreach (var point in river_data.test_points_4)
                {
                        var mesh_inst = new MeshInstance3D();
                        mesh_inst.Position = new Vector3I(point.X * 2, point.Y, point.Z * 2) + new Vector3I(river_data.base_world_pos.X, 0, river_data.base_world_pos.Y);
                        mesh_inst.Scale = Vector3.One * test_point_size;
                        var mesh = new SphereMesh()
                        {
                                Rings = 5,
                                Radius = 20,
                                Height = 20,
                        };
                        mesh_inst.Mesh = mesh;
                        mesh_inst.MaterialOverride = test_material_1;
                        parent_node.AddChild(mesh_inst);
                }
        }

        Vector2I base_chunk_world_pos; //TEST
        public MeshChunkDataGrid GenerateMeshChunkData(Vector2I base_chunk_world_pos, MeshChunkRiverData river_data, int mesh_chunk_size, int mesh_chunk_resolution, float[] base_vertex_height_map, float? lake_height)
        {
                this.base_chunk_world_pos = base_chunk_world_pos;


                var relative_mesh_pos = base_chunk_world_pos / mesh_chunk_size % (int)water_gen.mesh_chunks_per_water_chunk;
                var output = new MeshChunkDataGrid(mesh_chunk_resolution, max_river_width, river_effect_curve);
                GetEndAndStartMeshPossitions(river_data, mesh_chunk_resolution, relative_mesh_pos, base_vertex_height_map, out var relative_start_pos, out var relative_end_pos_option);
                Vector2I relative_end_pos;
                if (relative_end_pos_option == null)
                {
                        if (river_data.test_points_2.Count != 0)
                                GD.Print("END POS  == null");
                        if (lake_height == null)
                        {
                                GD.Print($"lake_height == null but this chunk should contain a lake! base:{base_chunk_world_pos} - relative_mesh_pos{relative_mesh_pos}, river_data.next_mesh_chunk_pos==null:{river_data.next_mesh_chunk_pos == null}, river_data.pos{river_data.pos} river_data.next_mesh_chunk_pos:{river_data.next_mesh_chunk_pos} end:{river_data.is_end}");
                        }
                        relative_end_pos = GetALakePos(mesh_chunk_resolution, base_vertex_height_map, lake_height.Value);
                }
                else
                {
                        relative_end_pos = relative_end_pos_option.Value;
                }



                // if (river_data.test_points_2.Count != 0)
                // {
                //         river_data.test_points_3.Add(new(relative_end_pos.X, 0, relative_end_pos.Y));
                // }
                foreach (var current_start_pos in relative_start_pos)
                {

                        var current_vertex = current_start_pos;
                        output.AddNewRiverVertex(current_vertex, out _);

                        while (true)
                        {

                                if (river_data.test_points_2.Count != 0)
                                {
                                        river_data.test_points_1.Add(new(current_vertex.X, (int)base_vertex_height_map[current_vertex.X + current_vertex.Y * mesh_chunk_resolution], current_vertex.Y));
                                }
                                var next_vertex = NextVertex(current_vertex, relative_end_pos, mesh_chunk_resolution, base_vertex_height_map);


                                output.AddNewRiverVertex(next_vertex, out var already_contains_a_river);

                                if (current_vertex == relative_end_pos || already_contains_a_river)
                                {
                                        if (river_data.test_points_2.Count != 0)
                                        {
                                                GD.Print($"BREAK: {current_vertex == relative_end_pos} {already_contains_a_river}");
                                                if (current_vertex == relative_end_pos)
                                                        river_data.test_points_5.Add(new(current_vertex.X, (int)base_vertex_height_map[current_vertex.X + current_vertex.Y * mesh_chunk_resolution], current_vertex.Y));
                                        }
                                        break;

                                }


                                current_vertex = next_vertex;
                        }

                        //TEST
                        output.AddNewRiverVertex(current_start_pos, out var _);
                }
                if (river_data.test_points_2.Count != 0)
                {

                        river_data.test_points_3.Add(new(relative_end_pos.X, (int)base_vertex_height_map[relative_end_pos.X + relative_end_pos.Y * mesh_chunk_resolution], relative_end_pos.Y));
                        GD.Print($"End point influence: {output.GetRiverEffectOnMeshVertex(relative_end_pos)}");
                        foreach (var current_start_pos in relative_start_pos)
                        {
                                GD.Print($"Start point influence: {output.GetRiverEffectOnMeshVertex(current_start_pos)}");

                                river_data.test_points_3.Add(new(current_start_pos.X, (int)base_vertex_height_map[current_start_pos.X + current_start_pos.Y * mesh_chunk_resolution], current_start_pos.Y));
                        }
                }

                //TEST
                output.AddNewRiverVertex(relative_end_pos, out var _);

                return output;
        }
        // temp
        private Vector2I GetALakePos(int mesh_chunk_resolution, float[] base_vertex_height_map, float lake_height)
        {
                for (int x = Margin; x < mesh_chunk_resolution - Margin; x++)
                {

                        for (int y = Margin; y < mesh_chunk_resolution - Margin; y++)
                        {
                                var i = x + y * mesh_chunk_resolution;
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
        private Vector2I NextVertex(Vector2I base_vertex, Vector2I relative_end_pos, int mesh_chunk_resolution, float[] base_vertex_height_map)
        {
                var vertexes_to_check = GetNeighbourVertexes(base_vertex, mesh_chunk_resolution);
                var best_vertex_points = float.MinValue;
                var best_vertex = new Vector2I();
                foreach (var current_vertex in vertexes_to_check)
                {
                        var vertex_index = current_vertex.X + current_vertex.Y * mesh_chunk_resolution;
                        if (vertex_index < 0 || vertex_index >= base_vertex_height_map.Length)
                                GD.Print($"vertex_index: {vertex_index} base_vertex:{base_vertex} pos: {current_vertex} width: {mesh_chunk_resolution}");
                        var height = base_vertex_height_map[vertex_index];
                        var points = GivePointsToVertex(current_vertex, relative_end_pos, height);
                        if (points > best_vertex_points)
                        {
                                best_vertex = current_vertex;
                                best_vertex_points = points;
                        }
                }
                return best_vertex;
        }
        //TEMP
        private float GivePointsToVertex(Vector2I vertex, Vector2I relative_end_pos, float height) => -vertex.DistanceTo(relative_end_pos) * distance_points_modifier /* - height_points_modifier * height */;
        private List<Vector2I> GetNeighbourVertexes(Vector2I pos, int mesh_chunk_resolution)
        {
                int max = mesh_chunk_resolution - Margin;
                int min = Margin;
                List<Vector2I> output = [];

                bool left = pos.X > min;
                bool right = pos.X < max;
                bool down = pos.Y > min;
                bool up = pos.Y < max;

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

        private void GetEndAndStartMeshPossitions(MeshChunkRiverData river_data, int mesh_chunk_resolution, Vector2I base_chunk_grid_pos, float[] base_vertex_height_map,
                        out Vector2I[] relative_start_pos, out Vector2I? relative_end_pos)
        {

                //TODO: use relative pos 
                relative_start_pos = new Vector2I[river_data.previous_mesh_chunk_pos.Count];

                var test = river_data.test_points_2.Count != 0;

                if (river_data.previous_mesh_chunk_pos.Count == 0)
                {
                        //TODO: Implement getting highest point
                        relative_start_pos = [new(mesh_chunk_resolution / 2, mesh_chunk_resolution / 2)];
                }
                else
                {

                        for (int i = 0; i < river_data.previous_mesh_chunk_pos.Count; i++)
                        {
                                Vector2I previous_chunk_pos = river_data.previous_mesh_chunk_pos[i];
                                // Make this just calculate the lowest point
                                relative_start_pos[i] = CalculateExtremePointAcrossTheChunksBorder(base_vertex_height_map, mesh_chunk_resolution, base_chunk_grid_pos, previous_chunk_pos, LowestOrHighest.lowest, test);
                        }
                }


                if (river_data.next_mesh_chunk_pos != null)
                {
                        relative_end_pos = CalculateExtremePointAcrossTheChunksBorder(base_vertex_height_map, mesh_chunk_resolution, base_chunk_grid_pos, river_data.next_mesh_chunk_pos.Value, LowestOrHighest.lowest, test);
                }
                else
                {
                        // relative_end_pos = null;
                        relative_end_pos = new(0, 0);
                }

        }

        enum LowestOrHighest
        {
                lowest = 1,
                highest = -1,
        }
        private Vector2I CalculateExtremePointAcrossTheChunksBorder(float[] base_vertex_height_map, int mesh_chunk_resolution, Vector2I base_grid_pos, Vector2I next_chunk_grid_pos, LowestOrHighest lowest_or_highest, bool test)
        {
                int check_sign = (int)lowest_or_highest;
                var relative_pos = next_chunk_grid_pos - base_grid_pos;
                var vertex_check_offset = (relative_pos + Vector2I.One) / 2 * mesh_chunk_resolution;
                var vertex_check_axis = relative_pos.X != 0 ? new Vector2I(0, 1) : new Vector2I(1, 0);
                var extreme_height = check_sign * float.MaxValue;
                Vector2I extreme_height_pos = new();

                if (lowest_or_highest == LowestOrHighest.lowest)
                {

                        for (int i = Margin; i < mesh_chunk_resolution - Margin; i++)
                        {

                                var pos = vertex_check_offset + i * vertex_check_axis;
                                int height_idx = pos.X + pos.Y * mesh_chunk_resolution;

                                var height = base_vertex_height_map[height_idx];

                                if (extreme_height > height)
                                {
                                        extreme_height = height;
                                        extreme_height_pos = pos;
                                }

                        }
                }
                else
                {
                        for (int i = max_river_width + 1; i < mesh_chunk_resolution - max_river_width - 1; i++)
                        {

                                var pos = vertex_check_offset + i * vertex_check_axis;
                                int height_idx = pos.X + pos.Y * mesh_chunk_resolution;

                                var height = base_vertex_height_map[height_idx];

                                if (extreme_height < height)
                                {
                                        extreme_height = height;
                                        extreme_height_pos = pos;
                                }

                        }

                }
                if (test)
                        GD.Print($"CalculateExtremePointAcrossTheChunksBorder: world_pos {base_chunk_world_pos}, {base_grid_pos}  {relative_pos} {vertex_check_offset} {vertex_check_axis} {extreme_height_pos} {extreme_height} {lowest_or_highest}");
                return /* vertex_check_offset  */extreme_height_pos;
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
                public MeshChunkDataGrid(int mesh_resolution, int max_river_width, Curve river_effect_curve)
                {
                        this.river_effect_curve = river_effect_curve;

                        river_width = max_river_width;
                        cell_width = max_river_width * 2;
                        grid_width = Mathf.CeilToInt((mesh_resolution + 1) / (float)cell_width);
                        river_vertexes_relative_pos_grid = new HashSet<Vector2I>[grid_width * grid_width];
                        for (int i = 0; i < river_vertexes_relative_pos_grid.Length; i++)
                        {
                                river_vertexes_relative_pos_grid[i] = [];
                        }
                }
                private Vector2I RelativeMeshToGridPos(Vector2I relative_pos) => relative_pos / cell_width;

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
                        {
                                return 0;
                        }
                        return /* river_effect_curve.SampleBaked(dist) */ -1 * river_width;
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
                private MeshChunkRiverData[] grid;
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
                        //?
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
                public List<Vector3I> test_points_1 = new();
                public List<Vector3I> test_points_2 = new();
                public List<Vector3I> test_points_5 = new();
                public List<Vector3I> test_points_3 = new();
                public List<Vector3I> test_points_4 = new();
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
