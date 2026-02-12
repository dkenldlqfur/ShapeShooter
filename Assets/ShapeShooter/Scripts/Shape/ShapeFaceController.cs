using UnityEngine;
using ShapeShooter.Shape;

namespace ShapeShooter.Shape
{
    public class ShapeFaceController : MonoBehaviour, IShapeFace
    {
        [SerializeField] private MeshRenderer meshRenderer;
        
        // 현재 상태 (색상 단계 등)
        private int currentColorIndex = 0;
        private bool isCompleted = false;

        public event System.Action OnFaceCompleted;

        public bool IsCompleted => isCompleted;

        private void Awake()
        {
            if (null == meshRenderer)
                meshRenderer = GetComponent<MeshRenderer>();
        }

        public void Initialize(Color startColor)
        {
            isCompleted = false;
            currentColorIndex = 0;
            SetColor(startColor);
        }

        public void OnHit(Vector3 hitPoint)
        {
            if (isCompleted)
                return;

            // TODO: 색상 변경 로직 고도화 (LevelData 기반)
            // 현재는 임시로 빨간색으로 변경되면 완료 처리
            SetColor(Color.red);
            isCompleted = true;
            OnFaceCompleted?.Invoke();
        }

        private void SetColor(Color color)
        {
            if (null != meshRenderer)
                meshRenderer.material.color = color;
        }
    }
}
