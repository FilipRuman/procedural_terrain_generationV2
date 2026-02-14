using System.Collections.Generic;
using Godot;
[Tool]
public partial class ObjectsGenerator : Node3D
{


        [Export] float minimal_tree_spacing_sqrt;
        [Export(PropertyHint.Range, "0,1,0.001")] float base_tree_spawn_chance;
        [Export] int rock_spawn_attempts_per_mesh_chunk;
        [Export] int grass_spawn_attempts_per_mesh_chunk;

        public class ObjectTypeSpawnData
        {
                public int instance_count;
                public Mesh mesh;
                public float[] mesh_buffer;
                public PackedScene collision_shape;
                public Vector4[] colliders_pos_scale;
                public ObjectTypeSpawnData(Mesh mesh, PackedScene collision_shape, List<ObjectInstantiationData> object_instances)
                {
                        this.mesh = mesh;
                        this.collision_shape = collision_shape;

                        instance_count = object_instances.Count;
                        mesh_buffer = new float[object_instances.Count * 12];
                        colliders_pos_scale = new Vector4[object_instances.Count];
                        for (int i = 0; i < object_instances.Count; i++)
                        {
                                var object_data = object_instances[i];
                                colliders_pos_scale[i] = new Vector4(object_data.pos.X, object_data.pos.Y, object_data.pos.Z, object_data.scale.X);
                                Basis basis = new Basis(Godot.Quaternion.FromEuler(object_data.rotation * Mathf.DegToRad(1.0f)));
                                basis = basis.Scaled(object_data.scale);

                                var base_idx = i * 12;

                                mesh_buffer[base_idx] = basis.X.X;
                                mesh_buffer[base_idx + 1] = basis.Y.X;
                                mesh_buffer[base_idx + 2] = basis.Z.X;
                                mesh_buffer[base_idx + 3] = object_data.pos.X;

                                mesh_buffer[base_idx + 4] = basis.X.Y;
                                mesh_buffer[base_idx + 5] = basis.Y.Y;
                                mesh_buffer[base_idx + 6] = basis.Z.Y;
                                mesh_buffer[base_idx + 7] = object_data.pos.Y;

                                mesh_buffer[base_idx + 8] = basis.X.Z;
                                mesh_buffer[base_idx + 9] = basis.Y.Z;
                                mesh_buffer[base_idx + 10] = basis.Z.Z;
                                mesh_buffer[base_idx + 11] = object_data.pos.Z;
                        }
                }

        }

        public ObjectTypeSpawnData[] GenerateObjectsData(int chunk_size, ThreadSafeGroundMeshGen ground_mesh_gen,
                BiomeGenerator.OutputData biome_data, Vector2 base_world_position, Biome[] biomes, StructureGen.StructureGrid structure_grid, WaterGen.WaterDataGrid water_grid)
        {
                chunk_size -= 2;

                var seed = RNG.GenerateSeed(base_world_position);
                Dictionary<TerrainObject, List<ObjectInstantiationData>> instances_data_for_object_type = [];

                TreeObjectsGenerator.GenerateTreesForMeshChunk(seed, base_tree_spawn_chance, minimal_tree_spacing_sqrt, chunk_size, ground_mesh_gen,
                                 biome_data, base_world_position, biomes, structure_grid, water_grid, ref instances_data_for_object_type);

                GenerateNotSpacedObjectsOfType(BiomeObjectsGenData.GetterType.grass, grass_spawn_attempts_per_mesh_chunk, seed, chunk_size, ground_mesh_gen,
                                        biome_data, base_world_position, biomes, structure_grid, water_grid, ref instances_data_for_object_type);

                GenerateNotSpacedObjectsOfType(BiomeObjectsGenData.GetterType.rock, rock_spawn_attempts_per_mesh_chunk, seed, chunk_size, ground_mesh_gen,
                                        biome_data, base_world_position, biomes, structure_grid, water_grid, ref instances_data_for_object_type);


                {
                        var output = new ObjectTypeSpawnData[instances_data_for_object_type.Count];
                        int i = 0;
                        foreach (var instances_for_type in instances_data_for_object_type)
                        {
                                output[i] = new(instances_for_type.Key.mesh, instances_for_type.Key.collision_shape, instances_for_type.Value);
                                i++;
                        }

                        return output;
                }
        }
        public void GenerateNotSpacedObjectsOfType(BiomeObjectsGenData.GetterType object_type, int spawn_attempts_per_mesh_chunk, ulong seed, int chunk_size, ThreadSafeGroundMeshGen ground_mesh_gen,
                BiomeGenerator.OutputData biome_data, Vector2 base_world_position, Biome[] biomes, StructureGen.StructureGrid structure_grid, WaterGen.WaterDataGrid water_grid,
                ref Dictionary<TerrainObject, List<ObjectInstantiationData>> instances_data_for_object_type)
        {

                GD.Seed(seed);
                for (int i = 0; i < spawn_attempts_per_mesh_chunk; i++)
                {
                        Vector2 uv = new(GD.Randf(), GD.Randf());
                        var world_pos_2d = uv * chunk_size + base_world_position;
                        if (!structure_grid.IsObjectValid(world_pos_2d))
                        {
                                return;
                        }
                        var height = ground_mesh_gen.CalculateHeight(world_pos_2d, out var terrain_aspects);
                        if (water_grid.IsObjectUnderTheWater(new(world_pos_2d.X, height, world_pos_2d.Y)))
                        {
                                return;
                        }
                        var biomes_influence = biome_data.GetBiomeInfluenceForUV(uv, biomes.Length);
                        Vector3 world_pos_3d = new(world_pos_2d.X, height, world_pos_2d.Y);

                        foreach (var biome_influence in biomes_influence)
                        {
                                var better_influence = biome_influence.influence * biome_influence.influence * biome_influence.influence;
                                if (GD.Randf() > better_influence)
                                        continue;
                                var biome = biomes[biome_influence.biome_type_index];
                                var object_inst_data = biome.objects_data.GetObjectOfType(object_type, world_pos_3d, terrain_aspects);

                                if (object_inst_data == null)
                                        continue;
                                if (instances_data_for_object_type.TryGetValue(object_inst_data.Value.obj, out var list_of_instances))
                                {
                                        list_of_instances.Add(object_inst_data.Value);
                                }
                                else
                                {
                                        instances_data_for_object_type.Add(object_inst_data.Value.obj, [object_inst_data.Value]);
                                }
                                break;
                        }



                }

        }
        private void SpawnObjectType(ObjectTypeSpawnData spawn_data, Node3D parent_for_objects)
        {
                var mesh_instance = new MultiMeshInstance3D();
                parent_for_objects.AddChild(mesh_instance);

                mesh_instance.Multimesh = new()
                {
                        Mesh = spawn_data.mesh,
                        TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                        InstanceCount = spawn_data.instance_count,
                        Buffer = spawn_data.mesh_buffer
                };

                if (spawn_data.collision_shape == null)
                        return;
                foreach (var collider_data in spawn_data.colliders_pos_scale)
                {
                        var pos = new Vector3(collider_data.X, collider_data.Y, collider_data.Z);
                        var scale = collider_data.W;

                        var collision_node = (Node3D)spawn_data.collision_shape.Instantiate();
                        collision_node.Scale = Vector3.One * scale;
                        collision_node.Position = pos;
                        parent_for_objects.AddChild(collision_node);
                }

        }

        public void SpawnObjects(ObjectTypeSpawnData[] input, Node3D parent_for_objects)
        {
                foreach (var spawn_data in input)
                {
                        SpawnObjectType(spawn_data, parent_for_objects);
                }
        }
}
