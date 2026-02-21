using System.Collections.Generic;
using Godot;
[Tool]
public partial class StructureGen : Node3D
{
        [Export] StructureType[] structure_pool;

        [Export] public int mesh_chunks_per_structure_grid_cell;

        public class StructureChunk
        {
                public Dictionary<Vector2I, StructureInstanceData> structures_for_mesh_chunk_world_pos;
                public Dictionary<Vector2I, StructureInstanceData> structure_gen_for_mesh_chunk_world_pos;

                public StructureChunk(Dictionary<Vector2I, StructureInstanceData> structures_for_mesh_chunk_world_pos, Dictionary<Vector2I, StructureInstanceData> structure_gen_for_mesh_chunk_world_pos)
                {
                        this.structures_for_mesh_chunk_world_pos = structures_for_mesh_chunk_world_pos;
                        this.structure_gen_for_mesh_chunk_world_pos = structure_gen_for_mesh_chunk_world_pos;
                }
        }
        public class StructureGrid
        {
                const int grid_width = 3;
                readonly StructureChunk[] grid;
                private Vector2I current_player_grid_pos;

                private readonly int grid_cell_size;
                private readonly StructureGen structure_gen;
                private readonly ThreadSafeGroundMeshGen mesh_gen;
                private readonly int mesh_chunk_size;
                private readonly WaterGen.WaterDataGrid water_grid;
                public bool IsObjectValid(Vector2 world_pos_f)
                {
                        Vector2I world_pos = (Vector2I)world_pos_f;
                        var chunk_pos = world_pos / mesh_chunk_size;
                        var chunk_world_pos = chunk_pos * mesh_chunk_size;



                        if (!this[world_pos].structures_for_mesh_chunk_world_pos.TryGetValue(chunk_world_pos, out var structure))
                                return true;

                        return !structure.IsObjectColliding(world_pos);
                }
                public StructureChunk this[Vector2I world_pos]
                {
                        get
                        {
                                var global_grid_pos = world_pos / grid_cell_size;
                                var relative_grid_pos = global_grid_pos - current_player_grid_pos;
                                return grid[relative_grid_pos.X + 1 + (relative_grid_pos.Y + 1) * grid_width];
                        }

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
                        var grid_copy = (StructureChunk[])grid.Clone();
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
                                                grid[x + y * grid_width] = GenerateChunk(new(world_x, world_y));
                                        }

                                        grid[new_x + new_y * grid_width] = grid_copy[x + y * grid_width];
                                }
                        }

                }
                public StructureGrid(StructureGen structure_gen, ThreadSafeGroundMeshGen mesh_gen, int mesh_chunk_size, Vector2 player_world_pos, WaterGen.WaterDataGrid water_grid)
                {
                        this.water_grid = water_grid;
                        grid_cell_size = structure_gen.mesh_chunks_per_structure_grid_cell * mesh_chunk_size;
                        this.structure_gen = structure_gen;
                        this.mesh_gen = mesh_gen;
                        this.mesh_chunk_size = mesh_chunk_size;

                        current_player_grid_pos = new Vector2I((int)player_world_pos.X / grid_cell_size, (int)player_world_pos.Y / grid_cell_size);
                        grid = new StructureChunk[grid_width * grid_width];
                        for (int x = 0; x < grid_width; x++)
                        {
                                for (int y = 0; y < grid_width; y++)
                                {
                                        //Generate new cell data
                                        var world_x = (x - 1 + current_player_grid_pos.X) * grid_cell_size;
                                        var world_y = (y - 1 + current_player_grid_pos.Y) * grid_cell_size;
                                        grid[x + y * grid_width] = GenerateChunk(new(world_x, world_y));
                                }
                        }
                }

                public StructureChunk GenerateChunk(Vector2I base_world_pos)
                {
                        Dictionary<Vector2I, StructureInstanceData> structure_gen_for_mesh_chunk_world_pos = [];
                        Dictionary<Vector2I, StructureInstanceData> structures_for_mesh_chunk_world_pos = [];
                        GD.Seed(RNG.GenerateSeed(base_world_pos));
                        foreach (var structure_type in structure_gen.structure_pool)
                        {

                                for (int i = 0; i < structure_type.generation_attempts_per_structure_chunk; i++)
                                {

                                        if (structure_type.spawn_chance < GD.Randf())
                                                continue;
                                        if (structure_gen.mesh_chunks_per_structure_grid_cell < 2 * structure_type.min_distance_from_grid_border_in_mesh_chunks)
                                                GD.PrintErr("structure_gen.mesh_chunks_per_structure_grid_cell has to ge at least 2x the structure_type.min_distance_from_grid_border_in_mesh_chunks.");


                                        var mesh_chunk_x = RNG.Range(structure_type.min_distance_from_grid_border_in_mesh_chunks, structure_gen.mesh_chunks_per_structure_grid_cell - structure_type.min_distance_from_grid_border_in_mesh_chunks);
                                        var mesh_chunk_y = RNG.Range(structure_type.min_distance_from_grid_border_in_mesh_chunks, structure_gen.mesh_chunks_per_structure_grid_cell - structure_type.min_distance_from_grid_border_in_mesh_chunks);
                                        var base_chunk_world_pos = new Vector2I(mesh_chunk_x, mesh_chunk_y) * mesh_chunk_size + base_world_pos;

                                        if (structures_for_mesh_chunk_world_pos.ContainsKey(base_chunk_world_pos))
                                                continue;

                                        var structure_world_pos = new Vector2(GD.Randf(), GD.Randf()) * mesh_chunk_size + base_chunk_world_pos;
                                        var structure_rotation = GD.Randf() * 360f;
                                        var structure_scale = structure_type.base_sale + (GD.Randf() * 2 - 1) * structure_type.scale_change_amplitude;
                                        var structure_instance = new StructureInstanceData(structure_world_pos, structure_scale, structure_rotation, structure_type);
                                        if (!structure_instance.IsValid(mesh_gen))
                                                continue;
                                        structure_instance.base_height = mesh_gen.CalculateHeight(structure_world_pos);

                                        if (water_grid.IsObjectUnderTheWater(new(structure_world_pos.X, structure_instance.base_height, structure_world_pos.Y)))
                                        {
                                                continue;
                                        }

                                        bool there_already_was_struct_on_one_of_the_chunks = false;

                                        var all_chunks = structure_instance.MeshChunksThisStructureSitsOnWorldPos(mesh_chunk_size);

                                        foreach (var chunk_world_pos in all_chunks)
                                        {
                                                if (structures_for_mesh_chunk_world_pos.ContainsKey(chunk_world_pos))
                                                {
                                                        there_already_was_struct_on_one_of_the_chunks = true;
                                                        break;
                                                }
                                        }
                                        if (there_already_was_struct_on_one_of_the_chunks)
                                                continue;

                                        foreach (var chunk_world_pos in all_chunks)
                                        {
                                                structures_for_mesh_chunk_world_pos.Add(chunk_world_pos, structure_instance);
                                        }

                                        structure_gen_for_mesh_chunk_world_pos.Add(base_chunk_world_pos, structure_instance);

                                }
                        }
                        return new(structures_for_mesh_chunk_world_pos, structure_gen_for_mesh_chunk_world_pos);

                }
        }
}
