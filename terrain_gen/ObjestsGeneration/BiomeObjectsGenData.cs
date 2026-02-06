using Godot;
[Tool, GlobalClass]
public partial class BiomeObjectsGenData : Resource
{
    [Export] TerrainObject[] trees;
    [Export] TerrainObject[] grass;
    [Export] FastNoiseLite rotation_noise;
    [Export] FastNoiseLite scale_noise;
    [Export] FastNoiseLite spawn_chance_noise;
    public ObjectInstantiationData? GetTree(Vector3 world_pos, TerrainAspectsSolver.TerrainAspects terrain_aspects)
    {
        return GetObjectOfTypeForPos(world_pos, trees, terrain_aspects);
    }
    private const int offset_per_biome_index = 21507;
    public Vector2 GetModifiedPosForNoiseModifingObjectsTransform(Vector3 original_world_pos, int biomeOffset)
    {
        Vector2 original_2d = new(original_world_pos.X, original_world_pos.Y);
        return original_2d + Vector2.One * offset_per_biome_index * biomeOffset;
    }

    private ObjectInstantiationData? GetObjectOfTypeForPos(Vector3 world_pos, TerrainObject[] obj_array, TerrainAspectsSolver.TerrainAspects terrain_aspects)
    {
        for (int i = 0; i < obj_array.Length; i++)
        {
            var obj = obj_array[i];
            var pos_for_noise_generation = GetModifiedPosForNoiseModifingObjectsTransform(world_pos, i);

            if (terrain_aspects.ruggedness > obj.max_ruggedness)
            {
                continue;
            }
            float chance_to_spawn = obj.base_chance_to_spawn + spawn_chance_noise.GetNoise2D(pos_for_noise_generation.X, pos_for_noise_generation.Y);
            if (RNG.Float(pos_for_noise_generation) > chance_to_spawn)
            {
                continue;
            }
            return GetInstantiationData(world_pos, pos_for_noise_generation, obj);
        }

        return null;
    }
    private ObjectInstantiationData GetInstantiationData(Vector3 world_pos, Vector2 pos_for_noise_generation, TerrainObject obj)
    {
        world_pos -= Vector3.Up * obj.mesh_y_offset;
        var scale = obj.base_sale + scale_noise.GetNoise2D(pos_for_noise_generation.X, pos_for_noise_generation.Y) * obj.scale_change_amplitude;
        var rotation = GetRotation(pos_for_noise_generation, obj.rotation_amplitude);
        return new(world_pos, rotation, Vector3.One * scale, obj);
    }

    public Vector3 GetRotation(Vector2 pos, float rotation_amplitude)
    {
        var x_pos = pos;
        var y_pos = pos + Vector2.One * 09172970;
        var z_pos = pos + Vector2.One * 1520209;

        // side to side rotation- trees always grow at some angle
        var x = rotation_amplitude * rotation_noise.GetNoise2D(x_pos.X, x_pos.Y);
        var z = rotation_amplitude * rotation_noise.GetNoise2D(z_pos.X, z_pos.Y);
        // direction that the objects is facing
        var y = 180 * rotation_noise.GetNoise2D(y_pos.X, y_pos.Y);

        return new(x, y, z);
    }

}
