using Godot;
[Tool, GlobalClass]
public partial class Biome : Resource
{
    [Export] public NoiseComponent terrain_mesh_noise;
    public byte index_in_biomes_array;
    [Export] public Texture albedo;
    [Export] public Texture normal;
    [Export] public Texture roughness;
    [Export(PropertyHint.ColorNoAlpha)] public Color tint;
    [Export] public float saturation;
    [Export] public float scale;
    [Export] public Resource[] conditions;




    public interface IBiomeGenerationCondition
    {
        public void InitialSetup();
        public bool CheckCondition(BiomeGenerator.GridCell[] neighbors);
    }
}
