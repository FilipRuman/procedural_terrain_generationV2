using Godot;
[Tool]
public partial class GenerationController : Node
{
        [ExportToolButton("Run")] private Callable RunButton => Callable.From(Run);
        [Export] int terrain_chunk_size;

        [Export] GroundMeshGen ground_mesh_gen;
        [Export] int ground_mesh_resolution;
        private void Run()
        {
                Vector2I base_world_pos = new(0, 0);
                ground_mesh_gen.GenerateChunkData(ground_mesh_resolution, terrain_chunk_size, base_world_pos);
                ground_mesh_gen.ApplyData();
        }

}
