using Godot;
[Tool]
public partial class GenerationController : Node
{
        [ExportToolButton("Run")] private Callable RunButton => Callable.From(Run);
        [Export] int terrain_chunk_size;

        [Export] GroundMeshGen ground_mesh_gen;
        [Export] BiomeGenerator biome_generator;
        [Export] MeshInstance3D mesh_instance;
        [Export] CollisionShape3D collider;
        [Export] Biome[] biomes;
        [Export] GroundShaderController ground_shader_controller;
        private void Run()
        {

                Vector2I base_world_pos = new(0, 0);
                ground_mesh_gen.Initialize(terrain_chunk_size);
                var mesh_data = ground_mesh_gen.GenerateChunkData(base_world_pos);
                ground_mesh_gen.ApplyData(mesh_data, mesh_instance, collider);
                var biome_data = biome_generator.GenerateTextureData(base_world_pos, terrain_chunk_size, biomes);
                ground_shader_controller.SetShaderConfiguration(biomes,
                [biome_data.GetTexture(0)],
                [biome_data.GetTexture(1)]
);

        }

}


