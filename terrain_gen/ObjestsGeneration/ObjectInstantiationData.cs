using Godot;

public struct ObjectInstantiationData
{
    public Vector3 pos;
    public Vector3 rotation;
    public Vector3 scale;
    public TerrainObject obj;
    public ObjectInstantiationData(Vector3 pos, Vector3 rotation, Vector3 scale, TerrainObject obj)
    {
        this.pos = pos;
        this.rotation = rotation;
        this.scale = scale;
        this.obj = obj;
    }
}

