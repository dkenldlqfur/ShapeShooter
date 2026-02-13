using UnityEngine;

namespace ShapeShooter
{
    public interface IShapeFace
    {
        void OnHit(Vector3 hitPoint);
    }
}
