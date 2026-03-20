using Godot;
[Tool]
public partial class GroundMeshGen : MeshInstance3D
{
        private int triangles_per_dimension;
        private float triangle_size;
        private Vector2I base_world_pos;
        private int size;

        [Export] private FastNoiseLite noise;
        [Export] private float noise_amplitude;

        [Export] private CollisionShape3D collider;


        private Vector3[] vertices;
        private Vector3[] vertices_padded;
        private int[] indices;
        private Vector3[] normals;
        private Vector2[] uvs;
        private float[] height_map;
        float[] tangents;

        /// Needs to be called after the `GenerateChunkData()`
        public void ApplyData()
        {
                var arrays = new Godot.Collections.Array();
                arrays.Resize((int)Mesh.ArrayType.Max);

                arrays[(int)Mesh.ArrayType.Vertex] = vertices;
                arrays[(int)Mesh.ArrayType.Index] = indices;
                arrays[(int)Mesh.ArrayType.Normal] = normals;
                arrays[(int)Mesh.ArrayType.TexUV] = uvs;
                arrays[(int)Mesh.ArrayType.Tangent] = tangents;

                HeightMapShape3D shape = new()
                {
                        MapWidth = triangles_per_dimension,
                        MapDepth = triangles_per_dimension,
                        MapData = height_map
                };
                collider.Shape = shape;
                collider.Scale = new Vector3(triangle_size, 1, triangle_size);
                // `- size/2f` is needed because otherwise this will be the center point for the collider, and for mesh this will be the bottom left corner. 
                collider.Position = new(base_world_pos.X + size / 2f, 0, base_world_pos.Y + size / 2f);


                var mesh = new ArrayMesh();
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

                Mesh = mesh;
        }
        public void GenerateChunkData(int resolution, int size, Vector2I base_world_pos)
        {
                this.size = size;
                this.base_world_pos = base_world_pos;
                triangles_per_dimension = resolution + 1;
                triangle_size = size / (float)resolution;

                GenerateUVsAndVertexes();
                GenerateIndices();
                GenerateNormals();
                GenerateTangents();
        }
        private void GenerateUVsAndVertexes()
        {
                int paddedWidth = triangles_per_dimension + 2;

                vertices_padded = new Vector3[paddedWidth * paddedWidth];
                vertices = new Vector3[triangles_per_dimension * triangles_per_dimension];
                height_map = new float[triangles_per_dimension * triangles_per_dimension];

                uvs = new Vector2[triangles_per_dimension * triangles_per_dimension];

                for (int x = -1; x < triangles_per_dimension + 1; x++)
                {
                        for (int z = -1; z < triangles_per_dimension + 1; z++)
                        {
                                var relative_pos = new Vector2I(x, z);
                                Vector2 worldPos = (Vector2)relative_pos * triangle_size + base_world_pos;
                                float height = CalculateHeight(worldPos);

                                Vector3 vertex_pos = new(worldPos.X, height, worldPos.Y);

                                vertices_padded[x + 1 + (z + 1) * paddedWidth] = vertex_pos;

                                if (x < 0 || z < 0 || x >= triangles_per_dimension || z >= triangles_per_dimension)
                                        continue;

                                int i = x + z * triangles_per_dimension;

                                height_map[i] = height;
                                vertices[i] = vertex_pos;
                                uvs[i] = new Vector2(
                                    x / (float)triangles_per_dimension,
                                    z / (float)triangles_per_dimension
                                );
                        }
                }

        }

        public void GenerateTangents()
        {
                int vertexCount = vertices.Length;

                var raw_tangents = new Vector3[vertexCount];
                var raw_bitangents = new Vector3[vertexCount];

                // Accumulate tangents per triangle
                for (int i = 0; i < indices.Length; i += 3)
                {
                        int idx0 = indices[i];
                        int idx1 = indices[i + 1];
                        int idx2 = indices[i + 2];

                        Vector3 v0 = vertices[idx0];
                        Vector3 v1 = vertices[idx1];
                        Vector3 v2 = vertices[idx2];

                        Vector2 uv0 = uvs[idx0];
                        Vector2 uv1 = uvs[idx1];
                        Vector2 uv2 = uvs[idx2];

                        Vector3 edge_1 = v1 - v0;
                        Vector3 edge_2 = v2 - v0;

                        float uv_delta_x1 = uv1.X - uv0.X;
                        float uv_delta_x2 = uv2.X - uv0.X;
                        float uv_delta_y1 = uv1.Y - uv0.Y;
                        float uv_delta_y2 = uv2.Y - uv0.Y;

                        float signed_area_of_triangle = uv_delta_x1 * uv_delta_y2 - uv_delta_x2 * uv_delta_y1;
                        // we check if the triangle is valid
                        if (Mathf.Abs(signed_area_of_triangle) < 1e-8f)
                                continue;

                        float inv_area_of_triangle = 1.0f / signed_area_of_triangle;

                        Vector3 tangent_dir = (edge_1 * uv_delta_y2 - edge_2 * uv_delta_y1) * inv_area_of_triangle;
                        Vector3 bitangent_dir = (edge_2 * uv_delta_x1 - edge_1 * uv_delta_x2) * inv_area_of_triangle;

                        // sum up the tangents for tech vertex in the triangle. 
                        // This will be later normalized and will result in smoother output.
                        raw_tangents[idx0] += tangent_dir;
                        raw_tangents[idx1] += tangent_dir;
                        raw_tangents[idx2] += tangent_dir;


                        raw_bitangents[idx0] += bitangent_dir;
                        raw_bitangents[idx1] += bitangent_dir;
                        raw_bitangents[idx2] += bitangent_dir;
                }


                tangents = new float[vertexCount * 4];
                for (int i = 0; i < vertexCount; i++)
                {
                        Vector3 normal = normals[i];
                        Vector3 raw_tangent = raw_tangents[i];

                        // Gram-Schmidt orthogonalization -> https://en.wikipedia.org/wiki/Gram%E2%80%93Schmidt_process
                        Vector3 normalized_tangent = (raw_tangent - normal * normal.Dot(raw_tangent)).Normalized();

                        float handedness = (normal.Cross(raw_tangent).Dot(raw_bitangents[i]) < 0.0f) ? -1.0f : 1.0f;

                        int baseIndex = i * 4;
                        tangents[baseIndex + 0] = normalized_tangent.X;
                        tangents[baseIndex + 1] = normalized_tangent.Y;
                        tangents[baseIndex + 2] = normalized_tangent.Z;
                        tangents[baseIndex + 3] = handedness;
                }

        }

        private Vector2 CalculateVertexWorldPosition(int relative_x, int relative_z) => new Vector2(relative_x, relative_z) * triangle_size + base_world_pos;


        private float CalculateHeight(Vector2 world_pos)
        {
                return noise.GetNoise2D(world_pos.X, world_pos.Y) * noise_amplitude;
        }


        //  padding is needed for generating normals to avoid any seems between chunks.
        private void GenerateNormals()
        {
                int paddedWidth = triangles_per_dimension + 2;
                normals = new Vector3[triangles_per_dimension * triangles_per_dimension];

                for (int x = 0; x < triangles_per_dimension; x++)
                {
                        for (int z = 0; z < triangles_per_dimension; z++)
                        {
                                // padded indices
                                int left = x + (z + 1) * paddedWidth;
                                int right = x + 2 + (z + 1) * paddedWidth;
                                int down = x + 1 + (z + 0) * paddedWidth;
                                int up = x + 1 + (z + 2) * paddedWidth;

                                // central difference for normal
                                Vector3 normal = new Vector3(
                                    vertices_padded[left].Y - vertices_padded[right].Y,
                                    2.0f,
                                    vertices_padded[down].Y - vertices_padded[up].Y
                                ).Normalized();

                                normals[x + z * triangles_per_dimension] = normal;
                        }
                }

        }

        private void GenerateIndices()
        {

                int vertex_count = triangles_per_dimension - 1;
                indices = new int[vertex_count * vertex_count * 6];
                int array_index = 0;

                for (int z = 0; z < vertex_count; z++)
                {
                        for (int x = 0; x < vertex_count; x++)
                        {
                                int vertex_idx = x + z * triangles_per_dimension;
                                // counter-clockwise order. 
                                indices[array_index++] = vertex_idx;
                                indices[array_index++] = vertex_idx + 1;
                                indices[array_index++] = vertex_idx + triangles_per_dimension;

                                indices[array_index++] = vertex_idx + triangles_per_dimension + 1;
                                indices[array_index++] = vertex_idx + triangles_per_dimension;
                                indices[array_index++] = vertex_idx + 1;
                        }
                }

        }


}
