using Godot;

[Tool]
public partial class Chunk : Node3D
{
    [Export] public MeshInstance3D mesh_instance;
    [Export] public GroundMeshGen mesh_gen;
}
