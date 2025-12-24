using Godot;
[Tool, GlobalClass]
public partial class Biome : Resource
{
    [Export] public NoiseComponent terrain_mesh_noise;
    [Export] public byte type_index;
}
