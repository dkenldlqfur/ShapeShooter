using UnityEngine;

namespace ShapeShooter.Shape
{
    public interface IShapeFace
    {
        void OnHit(Vector3 hitPoint);
    }
}
