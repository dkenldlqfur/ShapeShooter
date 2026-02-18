using System;
using UnityEngine;

namespace ShapeShooter
{
    /// <summary>
    /// HP 단계별 색상 정의 (0=White ~ 6=Purple)
    /// </summary>
    public enum FaceColorType
    {
        White = 0,
        Red = 1,
        Orange = 2,
        Yellow = 3,
        Green = 4,
        Blue = 5,
        Purple = 6,
    }

    /// <summary>
    /// 도형의 개별 면. HP 관리, 색상 변경, 링크된 면 동기화, 히트/완료/복원 이벤트 발행
    /// </summary>
    public class ShapeFace : MonoBehaviour
    {
        /// <summary>
        /// HP 값에 해당하는 Color 반환 (범위 초과 시 클램프)
        /// </summary>
        public static Color GetColorByHP(int hp)
        {
            var colorType = (FaceColorType)Mathf.Clamp(hp, 0, (int)FaceColorType.Purple);
            return GetColor(colorType);
        }

        /// <summary>
        /// FaceColorType에 대응하는 Color 반환
        /// </summary>
        public static Color GetColor(FaceColorType type)
        {
            return type switch
            {
                FaceColorType.White => Color.white,
                FaceColorType.Red => Color.red,
                FaceColorType.Orange => new Color(1f, 0.5f, 0f),
                FaceColorType.Yellow => Color.yellow,
                FaceColorType.Green => Color.green,
                FaceColorType.Blue => Color.blue,
                FaceColorType.Purple => new Color(0.6f, 0.2f, 0.8f),
                _ => Color.white
            };
        }

        [SerializeField] protected MeshRenderer meshRenderer;
        [SerializeField] private ShapeFace[] linkedFaces;

        protected int currentHP = 0;
        protected int maxHP = 0;
        protected bool isCompleted = false;

        /// <summary>링크된 면 간 재귀 호출 방지 플래그</summary>
        private bool isProcessingHit = false;

        /// <summary>면이 HP 0에 도달하여 완료될 때 발행</summary>
        public event Action OnFaceCompleted;
        /// <summary>완료된 면이 다시 히트되어 HP가 복원될 때 발행</summary>
        public event Action OnFaceRestored;
        /// <summary>면이 히트될 때마다 발행 (HP 변경 전)</summary>
        public event Action OnFaceHit;

        public bool IsCompleted => isCompleted;
        public bool HasLinkedFaces => null != linkedFaces && 0 < linkedFaces.Length;
        public ShapeFace[] LinkedFaces => linkedFaces;

        /// <summary>
        /// 레벨 데이터를 기반으로 면 초기화 (HP 설정, 색상 적용)
        /// </summary>
        public virtual void Initialize(LevelData levelData)
        {
            isCompleted = false;

            if (null != levelData)
                maxHP = levelData.requiredHitsPerFace;
            else
                maxHP = 1;

            currentHP = maxHP;
            SetColor(GetColorByHP(currentHP));
        }

        /// <summary>
        /// 총알 충돌 시 호출. 이벤트 발행 후 HP 변경 처리
        /// </summary>
        public virtual void OnHit(Vector3 hitPoint)
        {
            if (isProcessingHit)
                return;

            OnFaceHit?.Invoke();
            ApplyHit();
        }

        /// <summary>
        /// HP 변경 → 색상 갱신 → 링크 동기화 → 완료/복원 이벤트 발행
        /// </summary>
        private void ApplyHit()
        {
            isProcessingHit = true;

            bool wasCompleted = isCompleted;

            // HP 0이면 1로 복원, 아니면 감소
            if (0 == currentHP)
            {
                currentHP = 1;
                isCompleted = false;
            }
            else
            {
                currentHP--;
            }

            var color = GetColorByHP(currentHP);
            SetColor(color);
            PropagateToLinkedFaces(currentHP, color);

            // 상태 전이 이벤트
            if (wasCompleted)
                OnFaceRestored?.Invoke();
            else if (0 == currentHP)
                CompleteFace();

            isProcessingHit = false;
        }

        /// <summary>
        /// 링크된 면들에 HP, 색상, 완료 상태를 일괄 동기화
        /// </summary>
        private void PropagateToLinkedFaces(int hp, Color color)
        {
            if (null == linkedFaces)
                return;

            foreach (var face in linkedFaces)
            {
                if (null != face && !face.isProcessingHit)
                {
                    face.isProcessingHit = true;
                    face.currentHP = hp;
                    face.isCompleted = (0 == hp);
                    face.SetColor(color);
                    face.isProcessingHit = false;
                }
            }
        }

        /// <summary>
        /// 면을 완료 상태로 전환하고 이벤트 발행
        /// </summary>
        protected void CompleteFace()
        {
            if (isCompleted)
                return;

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
