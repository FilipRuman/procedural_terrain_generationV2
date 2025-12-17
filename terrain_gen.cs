using Godot;

[Tool]
public partial class terrain_gen : Node
{
    [Export] BiomeGenerator biome_generator;
    [Export] MeshInstance3D mesh;

    [Export] bool run;
    public override void _Process(double delta)
    {

        if (run)
        {
            run = false;
            ShaderMaterial material = (ShaderMaterial)mesh.GetSurfaceOverrideMaterial(0);

            ImageTexture map_2 = ImageTexture.CreateFromImage(biome_generator.map_2_image);
            material.SetShaderParameter("map_2", map_2);
            ImageTexture map_1 = ImageTexture.CreateFromImage(biome_generator.map_1_image);
            material.SetShaderParameter("map_1", map_1);
        }


    }
}
