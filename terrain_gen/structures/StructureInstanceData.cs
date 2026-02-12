using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Godot;
public class StructureInstanceData
{
        public Vector2 base_world_pos;
        public float base_height;
        public float scale;
        public float rotation_y;
        public StructureType structure_type;

        public void Instantiate(Node3D parent)
        {
                GD.Print("Instantiate Structure");
                var node = (Node3D)structure_type.model.Instantiate();
                parent.AddChild(node);
                node.Scale = Vector3.One * scale;
                node.RotationDegrees = new Vector3(0, rotation_y, 0);
                node.Position = new(base_world_pos.X, base_height, base_world_pos.Y);
        }

        public StructureInstanceData(Vector2 base_world_pos, float scale, float rotation_y, StructureType structure_type)
        {
                this.base_world_pos = base_world_pos;
                this.scale = scale;
                this.rotation_y = rotation_y;
                this.structure_type = structure_type;
        }

        public HashSet<Vector2I> MeshChunksThisStructureSitsOnWorldPos(int mesh_chunk_size)
        {
                HashSet<Vector2I> chunks = new();
                foreach (var shape in structure_type.shapes)
                {
                        foreach (var point in ((IStructureShape)shape).GetSampleWorldPosPointsInsideTheShape(this))
                        {

                                Vector2I chunk = new(Mathf.FloorToInt(point.X / mesh_chunk_size), Mathf.FloorToInt(point.Y / mesh_chunk_size));
                                Vector2I chunk_world_pos = chunk * mesh_chunk_size;
                                chunks.Add(chunk_world_pos);
                        }
                }
                return chunks;
        }
        public bool IsObjectColliding(Vector2I world_pos)
        {
                foreach (var shape in structure_type.shapes)
                {
                        if (((IStructureShape)shape).IsPointWithinTheShape(world_pos, this))
                                return true;
                }
                return false;
        }

        public bool IsValid(ThreadSafeGroundMeshGen mesh_gen)
        {
                var min_height = float.MaxValue;
                var max_height = float.MinValue;
                foreach (var shape in structure_type.shapes)
                {
                        var i_structure_shape = (IStructureShape)shape;
                        var test_points = i_structure_shape.GetSampleWorldPosPointsInsideTheShape(this);
                        foreach (var point in test_points)
                        {
                                var height = mesh_gen.CalculateHeight(point);
                                min_height = Mathf.Min(height, min_height);
                                max_height = Mathf.Max(height, max_height);
                        }
                        if (max_height - min_height > structure_type.maximal_height_delta_inside_the_shapes)
                                return false;
                }
                GD.Print($"Height delta{max_height - min_height}");

                return true;
        }

}
