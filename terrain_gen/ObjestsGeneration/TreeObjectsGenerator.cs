using Godot;
using System.Collections.Generic;
public static class TreeObjectsGenerator
{

        private class ObjectsSpacingGrid
        {
                // 1'st dimension -> x + y * grid_width 
                // 2'nd dimension -> all objects in this grid cell
                private List<ObjectsSpacingData>[] grid;
                private int grid_width;
#nullable enable
                public List<ObjectsSpacingData>? this[Vector2I pos]
                {
                        get
                        {
                                if (pos.X < -1 || pos.X >= grid_width - 1 || pos.Y < -1 || pos.Y >= grid_width - 1)
                                        return null;

                                int idx = (pos.X + grid_padding / 2) + (pos.Y + grid_padding / 2) * grid_width;
                                return grid[idx];
                        }
                }
                public ObjectsSpacingGrid(int width_height)
                {
                        grid_width = width_height;
                        grid = new List<ObjectsSpacingData>[width_height * width_height];
                        for (int x = 0; x < width_height; x++)
                        {
                                for (int y = 0; y < width_height; y++)
                                {
                                        grid[x + y * width_height] = new();
                                }
                        }
                }

        }
        public struct ObjectsSpacingData
        {
                public Vector2 pos;
                public float min_distance_sqrt;

                public ObjectsSpacingData(Vector2 pos, float min_distance_sqrt)
                {
                        this.pos = pos;
                        this.min_distance_sqrt = min_distance_sqrt;
                }
        }
        private static float GridCellWidth(float minimal_tree_spacing_sqrt)
        {
                var dist_max = minimal_tree_spacing_sqrt;
                return dist_max / Mathf.Sqrt2;
        }
        public static void GenerateTreesForMeshChunk(ulong seed, float base_tree_spawn_chance, float minimal_tree_spacing_sqrt, int chunk_size, ThreadSafeGroundMeshGen ground_mesh_gen,
                BiomeGenerator.OutputData biome_data, Vector2 base_world_position, Biome[] biomes, StructureGen.StructureGrid structure_grid,
                ref Dictionary<TerrainObject, List<ObjectInstantiationData>> instances_data_for_object_type)
        {

                float grid_cell_width = GridCellWidth(minimal_tree_spacing_sqrt);
                var grid_width = Mathf.CeilToInt(chunk_size / grid_cell_width) + grid_padding;
                var grid = new ObjectsSpacingGrid(grid_width);
                for (int x = -1; x < grid_width - 1; x++)
                {
                        for (int y = -1; y < grid_width - 1; y++)
                        {
                                if (RNG.Float(new Vector2(x, y)) > base_tree_spawn_chance)
                                        continue;
                                var base_cell_world_pos = base_world_position + new Vector2(x, y) * grid_cell_width;
                                bool is_margin = x == -1 || y == -1 || x == grid_width - 2 || y == grid_width - 2;

                                GenerateForGridCell(minimal_tree_spacing_sqrt, base_cell_world_pos, new(x, y), grid_cell_width, is_margin,
                                        biomes, ground_mesh_gen, biome_data, structure_grid, ref grid, ref instances_data_for_object_type);
                                GD.Seed(seed);
                        }
                }
        }

        private static void GenerateForGridCell(float minimal_tree_spacing_sqrt, Vector2 base_cell_world_pos, Vector2I grid_pos, float grid_cell_width, bool is_margin,
            Biome[] biomes, ThreadSafeGroundMeshGen ground_mesh_gen, BiomeGenerator.OutputData biome_data, StructureGen.StructureGrid structure_grid,
             ref ObjectsSpacingGrid grid, ref Dictionary<TerrainObject, List<ObjectInstantiationData>> instances_data_for_object_type)
        {

                Vector2 uv = new(GD.Randf(), GD.Randf());
                var world_pos_2d = uv * grid_cell_width + base_cell_world_pos;
                if (!structure_grid.IsObjectValid(world_pos_2d))
                {
                        return;
                }
                if (!IsPosValid(world_pos_2d, minimal_tree_spacing_sqrt, grid_pos, grid))
                {
                        return;
                }

                var height = ground_mesh_gen.CalculateHeight(world_pos_2d, out var terrain_aspects);
                var biomes_influence = biome_data.GetBiomeInfluenceForUV(uv, biomes.Length);
                Vector3 world_pos_3d = new(world_pos_2d.X, height, world_pos_2d.Y);

                foreach (var biome_influence in biomes_influence)
                {
                        var better_influence = biome_influence.influence * biome_influence.influence * biome_influence.influence;
                        if (GD.Randf() > better_influence)
                                continue;
                        var biome = biomes[biome_influence.biome_type_index];
                        var object_inst_data = biome.objects_data.GetObjectOfType(BiomeObjectsGenData.GetterType.tree, world_pos_3d, terrain_aspects);

                        if (object_inst_data == null)
                                continue;

                        if (!is_margin)
                        {
                                if (instances_data_for_object_type.TryGetValue(object_inst_data.Value.obj, out var object_type_array))
                                {
                                        object_type_array.Add(object_inst_data.Value);
                                }
                                else
                                {
                                        instances_data_for_object_type.Add(object_inst_data.Value.obj, [object_inst_data.Value]);
                                }
                        }

                        grid[grid_pos]!.Add(new(world_pos_2d, minimal_tree_spacing_sqrt));
                        break;
                }


        }
        private static bool IsPosValid(Vector2 pos, float main_min_distance_sqrt, Vector2I grid_pos, ObjectsSpacingGrid grid)
        {
                for (int x = -1; x <= 1; x++)
                {
                        for (int y = -1; y <= 1; y++)
                        {
                                var cell = grid[grid_pos + new Vector2I(x, y)];
                                if (cell == null)
                                        continue;
                                if (!IsFarEnoughtFromObjects(pos, main_min_distance_sqrt, cell))
                                        return false;

                        }
                }
                return true;
        }
        private static bool IsFarEnoughtFromObjects(Vector2 pos, float main_min_distance_sqrt, List<ObjectsSpacingData> to_check)
        {
                foreach (var obj in to_check)
                {
                        var spacing = Mathf.Max(main_min_distance_sqrt, obj.min_distance_sqrt);
                        if (pos.DistanceSquaredTo(obj.pos) < spacing * spacing)
                                return false;
                }
                return true;
        }

        const int grid_padding = 2;
}

