using System.Collections.Generic;
using Godot;
[Tool]
public partial class ObjectsGenerator : Node3D
{
    [Export]
    float minimal_tree_spacing_sqrt;
    [Export] uint tree_count;
    [Export] uint grass_count;

    public void GenerateObjects(BiomeGenerator.OutputData biome_data, Biome[] biomes, int chunk_size,
GroundMeshGen.VertexDataStorage vertex_data, float vertex_size, Vector2 base_world_position)
    {
        // chunk_size -= 2;
        // var seed = RNG.GenerateSeed(base_world_position);
        // GD.Seed(seed);
        //
        //
        // List<Vector2> trees = new();
        // for (int i = 0; i < tree_count; i++)
        // {
        //     var random_uv = new Vector2(GD.Randf(), GD.Randf());
        //     var random_pos_on_chunk = random_uv * chunk_size + Vector2.One * vertex_size;
        //     if (!IsPosValid(random_pos_on_chunk, trees))
        //     {
        //         continue;
        //     }
        //     var closest_vertex_pos = new Vector2I(Mathf.FloorToInt(random_pos_on_chunk.X / vertex_size), Mathf.FloorToInt(random_pos_on_chunk.Y / vertex_size));
        //     var closest_vertex_data = vertex_data[closest_vertex_pos.X, closest_vertex_pos.Y];
        //     var biome_influences = biome_data.SampleBiomeDataForMesh(random_uv);
        //
        //     //TODO: make this sample all trees from all the biomes with correct chances to spawn
        //     var biome = biomes[biome_influences[0].biome_type_index];
        //     SpawnObject(biome.trees[0], new(closest_vertex_data.world_pos.X, closest_vertex_data.height, closest_vertex_data.world_pos.Y));
        //     trees.Add(random_pos_on_chunk);
        // }
    }
    public bool IsPosValid(Vector2 pos, List<Vector2> trees)
    {
        foreach (Vector2 to_check in trees)
        {
            if (pos.DistanceSquaredTo(to_check) < minimal_tree_spacing_sqrt)
            {
                return false;
            }
        }
        return true;

    }
    public void SpawnObject(ObjectData objects_data, Vector3 pos)
    {
        var node = (Node3D)objects_data.model.Instantiate();
        AddChild(node);
        node.Position = pos;
        node.Scale = Vector3.One * (objects_data.scale_range.X + RNG.Float(pos) * (objects_data.scale_range.Y - objects_data.scale_range.X));
        node.RotationDegrees = RNG.Vector3(pos) * objects_data.rotation_amplitude;
    }

}

