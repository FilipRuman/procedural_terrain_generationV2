using Godot;
[Tool, GlobalClass]
public partial class ObjectData : Resource
{
    [Export] public NoiseComponent generationNoise;
    [Export] public PackedScene model;
    [Export(PropertyHint.Range)] public Vector2 scale_range;
    [Export] public Vector3 rotation_amplitude;
}
