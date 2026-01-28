using Godot;
[Tool]
public partial class GroundMeshGen : MeshInstance3D
{

    [Export] ObjectsGenerator objects_generator;

    [Export] NoiseComponent high_frequency_noise;
    [Export] NoiseComponent medium_frequency_noise;
    [Export] NoiseComponent low_frequency_noise;

    [Export] TerrainAspectEffectOnMesh mousture_effect;
    [Export] TerrainAspectEffectOnMesh elevation_effect;
    [Export] TerrainAspectEffectOnMesh temperature_effect;
    [Export] TerrainAspectEffectOnMesh roughness_effect;

    private int triangle_count_per_dimension;
    private float triangle_size;
    public void Run(TerrainAspectsSolver terrain_aspects_solver, int size, Vector2 base_world_position, int resolution)
    {
        // needed because otherwise there will be a gap of 1 triangle size
        size += 1;
        triangle_count_per_dimension = size * resolution;
        triangle_size = 1f / resolution;

        var arrayMesh = GenerateTerrainMesh(terrain_aspects_solver, base_world_position, resolution, size);
        Mesh = arrayMesh;

    }


    private ArrayMesh GenerateTerrainMesh(TerrainAspectsSolver terrain_aspects_solver, Vector2 base_world_position, int resolution, int size)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        var vertex_data = GenerateVertexData(terrain_aspects_solver, base_world_position, resolution);
        // objects_generator.GenerateObjects(size, vertex_data, triangle_size, base_world_position);
        SetupMeshVertexAndUV(st, base_world_position, resolution, vertex_data);
        GenerateIndexes(st);

        st.GenerateTangents();

        return st.Commit();
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


    private Vector2 CalculateVertexWorldPosition(int relative_x, int relative_z, Vector2 base_world_position)
    {
        return new Vector2(relative_x * triangle_size, relative_z * triangle_size) + base_world_position;
    }

    private float CalculateHeight(TerrainAspectsSolver terrain_aspects_solver, Vector2 uv, Vector2 world_position)
    {
        var terain_aspects = terrain_aspects_solver.SolveForPos(world_position);

        TerrainAspectEffectOnMesh.OutputData noise_amplitude_data = new();
        mousture_effect.AddEffectToOutput(terain_aspects.moisture, ref noise_amplitude_data);
        elevation_effect.AddEffectToOutput(terain_aspects.elevation, ref noise_amplitude_data);
        temperature_effect.AddEffectToOutput(terain_aspects.temperature, ref noise_amplitude_data);
        roughness_effect.AddEffectToOutput(terain_aspects.roughness, ref noise_amplitude_data);

        float output_height
             = high_frequency_noise.Sample(world_position) * noise_amplitude_data.high_freq_noise_amplitude
             + medium_frequency_noise.Sample(world_position) * noise_amplitude_data.medium_freq_noise_amplitude
             + low_frequency_noise.Sample(world_position) * noise_amplitude_data.low_freq_noise_amplitude
             + noise_amplitude_data.base_height;


        return output_height;
    }

    private Vector3 CalculateNormal(int x, int y, VertexDataStorage vertex_data)
    {
        float l = vertex_data[x - 1, y].height;
        float r = vertex_data[x + 1, y].height;
        float d = vertex_data[x, y - 1].height;
        float u = vertex_data[x, y + 1].height;

        Vector3 normal = new Vector3(
               l - r,
               2.0f,
               d - u
           );

        return normal.Normalized();
    }

    private void SetupMeshVertexAndUV(SurfaceTool st, Vector2 base_world_position, int resolution, VertexDataStorage vertex_data_storage)
    {
        var output = new float[triangle_count_per_dimension * triangle_count_per_dimension];
        for (int relative_x = 1; relative_x < triangle_count_per_dimension + 1; relative_x++)
        {
            for (int relative_z = 1; relative_z < triangle_count_per_dimension + 1; relative_z++)
            {

                var vertex_data = vertex_data_storage[relative_x, relative_z];
                st.SetUV(vertex_data.uv);
                st.SetNormal(CalculateNormal(relative_x, relative_z, vertex_data_storage));


                st.AddVertex(new(vertex_data.world_pos.X, vertex_data.height, vertex_data.world_pos.Y));
            }
        }
    }
    private VertexDataStorage GenerateVertexData(TerrainAspectsSolver terrain_aspects_solver, Vector2 base_world_position, int resolution)
    {
        // + 2 for padding
        var data_array = new VertexData[(triangle_count_per_dimension + 2) * (triangle_count_per_dimension + 2)];
        int padded_with = triangle_count_per_dimension + 2;
        for (int relative_x = -1; relative_x < triangle_count_per_dimension + 1; relative_x++)
        {
            for (int relative_z = -1; relative_z < triangle_count_per_dimension + 1; relative_z++)
            {

                var uv = new Vector2(relative_x / (float)triangle_count_per_dimension, relative_z / (float)triangle_count_per_dimension);
                Vector2 world_pos = CalculateVertexWorldPosition(relative_x, relative_z, base_world_position);
                float height = CalculateHeight(terrain_aspects_solver, uv, world_pos);

                data_array[relative_x + 1 + (relative_z + 1) * padded_with] = new(height, uv, world_pos);
            }
        }

        return new(data_array, padded_with);
    }

    public class VertexDataStorage
    {
        VertexData[] data;
        int width;
        public VertexDataStorage(VertexData[] data,
                int width)
        {
            this.data = data;
            this.width = width;
        }
        public VertexData this[int x, int z]
        {
            get
            {
                return data[x + z * width];
            }
        }
    }
    public struct VertexData
    {
        public float height;
        public Vector2 uv;
        public Vector2 world_pos;

        public VertexData(float height, Vector2 uv, Vector2 world_pos)
        {
            this.height = height;
            this.uv = uv;
            this.world_pos = world_pos;
        }
    }
}
