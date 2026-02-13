using System;
using UnityEngine;

namespace ShapeShooter
{
    public class ShapeFaceController : MonoBehaviour, IShapeFace
    {
        [SerializeField] private MeshRenderer meshRenderer;
        
        // 현재 상태 (색상 단계 등)
        private LevelData currentLevelData;
        private int currentHitCount = 0;
        private bool isCompleted = false;

        public event Action OnFaceCompleted;

        public bool IsCompleted => isCompleted;

        private void Awake()
        {
            if (null == meshRenderer)
                meshRenderer = GetComponent<MeshRenderer>();
        }

        public void Initialize(LevelData levelData)
        {
            currentLevelData = levelData;
            isCompleted = false;
            currentHitCount = 0;
            SetColor(Color.white); // 초기 색상
        }

        public void OnHit(Vector3 hitPoint)
        {
            if (isCompleted)
                return;

            currentHitCount++;

            // LevelData가 없거나 색상 설정이 없는 경우 예외 처리 (기본 동작)
            if (null == currentLevelData || null == currentLevelData.stageColors || 0 == currentLevelData.stageColors.Length)
            {
                SetColor(Color.red);
                isCompleted = true;
                OnFaceCompleted?.Invoke();
                return;
            }

            // 색상 변경
            // 히트 횟수에 맞는 색상 인덱스 (배열 범위 내 클램핑)
            int colorIndex = Mathf.Clamp(currentHitCount - 1, 0, currentLevelData.stageColors.Length - 1);
            SetColor(currentLevelData.stageColors[colorIndex]);

            // 완료 체크
            if (currentLevelData.requiredHitsPerFace <= currentHitCount)
            {
                isCompleted = true;
                OnFaceCompleted?.Invoke();
            }
        }

        private void SetColor(Color color)
        {
            if (null != meshRenderer)
                meshRenderer.material.color = color;
        }
    }
}
