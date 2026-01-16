using Godot;
[Tool]
public partial class GroundMeshGen : MeshInstance3D
{

    private float triangle_size;
    private int triangle_count_per_dimension;
    private Vector2 base_world_position;

    [Export] NoiseComponent noise_component;

    // Just 1 function
    public void Run(int size, Vector2 world_position, int resolution)
    {

        // needed because otherwise there will be a gap of 1 triangle size
        size += 1;

        // assign all of the needed variables
        this.triangle_size = 1f / resolution;
        this.triangle_count_per_dimension = size * resolution;

        GD.Print($"mesh- chunk_size {size}, ground_mesh_resolution{resolution} {triangle_count_per_dimension}");
        this.base_world_position = world_position;

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);


        GenerateVertexes(st);
        GenerateIndexes(st);

        st.GenerateNormals();
        st.GenerateTangents();

        Mesh = st.Commit();
    }

    private void GenerateIndexes(SurfaceTool st)
    {
        for (int x = 0; x < triangle_count_per_dimension - 1; x++)
        {
            for (int z = 0; z < triangle_count_per_dimension - 1; z++)
            {
                int i = x + z * triangle_count_per_dimension;
                st.AddIndex(i);
                st.AddIndex(i + triangle_count_per_dimension);
                st.AddIndex(i + 1);

                st.AddIndex(i + 1);
                st.AddIndex(i + triangle_count_per_dimension);
                st.AddIndex(i + 1 + triangle_count_per_dimension);
            }
        }
    }

    private Vector2 VertexWorldPosition(uint x, uint z)
    {
        return new Vector2(x * triangle_size, z * triangle_size) + base_world_position;
    }

    private void GenerateVertexes(SurfaceTool st)
    {
        GD.Print($"triangle_count_per_dimension -{triangle_count_per_dimension}");
        for (uint x = 0; x < triangle_count_per_dimension; x++)
        {
            for (uint z = 0; z < triangle_count_per_dimension; z++)
            {
                var uv = new Vector2(x / (float)triangle_count_per_dimension, z / (float)triangle_count_per_dimension);
                st.SetUV(uv);

                Vector2 world_pos = VertexWorldPosition(x, z);
                float height = noise_component.GetHeight(world_pos);
                st.AddVertex(new(world_pos.X, height, world_pos.Y));

            }
        }

    }
}
