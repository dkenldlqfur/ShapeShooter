using System;
using UnityEngine;

namespace ShapeShooter
{
    public abstract class ShapeFace : MonoBehaviour
    {
        [SerializeField] protected MeshRenderer meshRenderer;

        // 인스펙터에서 연결할 링크된 면들
        [SerializeField] private ShapeFace[] linkedFaces;

        protected LevelData currentLevelData;
        protected int currentHitCount = 0;
        protected bool isCompleted = false;

        // 재귀 호출 방지 플래그
        private bool isProcessingHit = false;

        public event Action OnFaceCompleted;

        public bool IsCompleted => isCompleted;

        // 링크된 면이 있는지 여부
        public bool HasLinkedFaces => null != linkedFaces && 0 < linkedFaces.Length;

        // 링크된 면 배열 접근
        public ShapeFace[] LinkedFaces => linkedFaces;

        public virtual void Initialize(LevelData levelData)
        {
            currentLevelData = levelData;
            isCompleted = false;
            currentHitCount = 0;
            SetColor(Color.white); // 초기 색상
        }

        public virtual void OnHit(Vector3 hitPoint)
        {
            if (isCompleted || isProcessingHit)
                return;

            // 히트 처리 (자신 + 링크된 면 모두)
            ApplyHit();
        }

        /// <summary>
        /// 히트 적용 (색상 변경 + 완료 체크)
        /// 링크된 면이 있으면 모든 면에 동시 적용
        /// </summary>
        private void ApplyHit()
        {
            isProcessingHit = true;

            currentHitCount++;

            // LevelData가 없거나 색상 설정이 없는 경우 예외 처리 (기본 동작)
            if (null == currentLevelData || null == currentLevelData.stageColors || 0 == currentLevelData.stageColors.Length)
            {
                SetColor(Color.red);
                PropagateToLinkedFaces(Color.red);
                CompleteFace();
                PropagateCompletionToLinkedFaces();
                isProcessingHit = false;
                return;
            }

            // 색상 변경
            int colorIndex = Mathf.Clamp(currentHitCount - 1, 0, currentLevelData.stageColors.Length - 1);
            Color color = currentLevelData.stageColors[colorIndex];
            SetColor(color);
            PropagateToLinkedFaces(color);

            // 완료 체크
            if (currentLevelData.requiredHitsPerFace <= currentHitCount)
            {
                CompleteFace();
                PropagateCompletionToLinkedFaces();
            }

            isProcessingHit = false;
        }

        /// <summary>
        /// 링크된 면들에 색상 전파
        /// </summary>
        private void PropagateToLinkedFaces(Color color)
        {
            if (null == linkedFaces)
                return;

            foreach (var face in linkedFaces)
            {
                if (null != face && !face.isProcessingHit)
                {
                    face.isProcessingHit = true;
                    face.currentHitCount = currentHitCount;
                    face.SetColor(color);
                    face.isProcessingHit = false;
                }
            }
        }

        /// <summary>
        /// 링크된 면들에 완료 상태 전파
        /// </summary>
        private void PropagateCompletionToLinkedFaces()
        {
            if (null == linkedFaces)
                return;

            foreach (var face in linkedFaces)
            {
                if (null != face && !face.isCompleted)
                {
                    face.isCompleted = true;
                    // 링크된 면의 OnFaceCompleted는 발생시키지 않음
                    // (ShapeManager에서 중복 카운트 방지)
                }
            }
        }

        protected void CompleteFace()
        {
            if (isCompleted) return;

            isCompleted = true;
            OnFaceCompleted?.Invoke();
        }

        public virtual void SetColor(Color color)
        {
            if (null != meshRenderer)
                meshRenderer.material.color = color;
        }
    }
}
