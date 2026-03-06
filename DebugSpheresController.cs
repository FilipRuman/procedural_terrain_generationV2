using Godot;
[Tool]
public partial class DebugSpheresController : Node3D
{
        public override void _Ready()
        {
                GD.Print("_Ready");
                DebugSpheresStatic.parent = this;
                base._Ready();
        }


}
public static class DebugSpheresStatic
{
        public static StandardMaterial3D material;
        public static float size = 1;
        public static Node3D parent;

        public static void Spawn(Vector2I world_pos)
        {
                Spawn(new(world_pos.X, 0, world_pos.X), Colors.Magenta);
        }
        public static void Spawn(Vector3 world_pos, Color color, float size)
        {

                var mesh_inst = new MeshInstance3D
                {
                        Position = world_pos,
                        Scale = Vector3.One * size
                };
                var mesh = new SphereMesh()
                {
                        Rings = 5,
                        Radius = 5,
                        Height = 5
                };
                mesh_inst.Mesh = mesh;
                material = new StandardMaterial3D();
                material.AlbedoColor = color;
                mesh_inst.MaterialOverride = material;
                parent.CallDeferred("add_child", mesh_inst);
        }
        public static void Spawn(Vector3 world_pos, Color color)
        {

                var mesh_inst = new MeshInstance3D
                {
                        Position = world_pos,
                        Scale = Vector3.One * size
                };
                var mesh = new SphereMesh()
                {
                        Rings = 5,
                        Radius = 5,
                        Height = 5
                };
                mesh_inst.Mesh = mesh;
                material = new StandardMaterial3D();
                material.AlbedoColor = color;
                mesh_inst.MaterialOverride = material;
                parent.CallDeferred("add_child", mesh_inst);
        }
}
