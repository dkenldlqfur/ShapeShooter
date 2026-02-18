using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace ShapeShooter
{
    /// <summary>
    /// 도형 관리. 면 초기화, 완료 카운트 추적, 회전 패턴 제어, 스테이지 클리어 판정
    /// </summary>
    public class ShapeManager : MonoBehaviour
    {
        /// <summary>SingleAxis에서 선택 가능한 기본 축 (X, Y, Z)</summary>
        private static readonly Vector3[] CARDINAL_AXES = { Vector3.right, Vector3.up, Vector3.forward };

        private ShapeFace[] faces;
        private LevelData currentLevelData;
        private CancellationTokenSource cts;
        private bool isRotating = false;
        private int totalFaces;
        private int completedFaces;

        /// <summary>ReactiveAxis 패턴 전용: 총알 충돌 시 동적으로 변경되는 회전 축</summary>
        private Vector3 reactiveAxis;

        private void Start()
        {
            Initialize();
        }

        /// <summary>
        /// 하위 면 검색, 이벤트 구독, 링크 서브 면 제외 카운트 계산, 회전 시작
        /// </summary>
        public void Initialize()
        {
            faces = GetComponentsInChildren<ShapeFace>();
            
            if (null == faces || 0 == faces.Length)
                return;

            completedFaces = 0;

            if (null != GameManager.Instance)
                currentLevelData = GameManager.Instance.GetCurrentLevelData();

            // 링크된 서브 면은 완료 카운트에서 제외 (대표 면만 카운트)
            int linkedSubFaceCount = 0;
            foreach (var face in faces)
            {
                face.Initialize(currentLevelData);
                face.OnFaceCompleted += HandleFaceCompleted;
                face.OnFaceRestored += HandleFaceRestored;
                face.OnFaceHit += HandleFaceHit;

                if (IsLinkedSubFace(face))
                    linkedSubFaceCount++;
            }

            totalFaces = faces.Length - linkedSubFaceCount;

            StartRotation();
        }

        /// <summary>
        /// 해당 면이 다른 면의 linkedFaces에 포함된 서브 면인지 확인
        /// </summary>
        private bool IsLinkedSubFace(ShapeFace target)
        {
            foreach (var face in faces)
            {
                if (face == target || !face.HasLinkedFaces)
                    continue;

                foreach (var linked in face.LinkedFaces)
                {
                    if (linked == target)
                        return true;
                }
            }

            return false;
        }

        #region 면 이벤트 핸들러

        private void HandleFaceCompleted()
        {
            completedFaces++;
            CheckCompletion();
        }

        private void HandleFaceRestored()
        {
            if (0 < completedFaces)
                completedFaces--;
        }

        /// <summary>
        /// 면 히트 시 호출. ReactiveAxis 패턴이면 회전 축을 새로 생성
        /// </summary>
        private void HandleFaceHit()
        {
            if (null != currentLevelData && currentLevelData.rotationPattern == RotationPatternType.ReactiveAxis)
                reactiveAxis = new Vector3(Random.value, Random.value, Random.value).normalized;
        }

        #endregion

        /// <summary>
        /// 모든 면이 완료되었으면 스테이지 클리어 처리
        /// </summary>
        private void CheckCompletion()
        {
            if (totalFaces <= completedFaces)
                OnStageClear().Forget();
        }

        private async UniTaskVoid OnStageClear()
        {
            if (null != GameManager.Instance)
                GameManager.Instance.CompleteStage();

            await StopRotationGradually();
        }
        
        private void OnDestroy()
        {
            foreach (var face in faces)
            {
                if (null != face)
                {
                    face.OnFaceCompleted -= HandleFaceCompleted;
                    face.OnFaceRestored -= HandleFaceRestored;
                    face.OnFaceHit -= HandleFaceHit;
                }
            }

            isRotating = false;
            cts?.Cancel();
            cts?.Dispose();
        }

        #region 회전 제어

        /// <summary>
        /// 레벨 데이터의 패턴에 따라 회전 축을 생성하고 회전 루프 시작
        /// </summary>
        private void StartRotation()
        {
            if (null == currentLevelData)
                return;

            var axis = GenerateRotationAxis(currentLevelData.rotationPattern);

            // ReactiveAxis는 히트 시 변경되는 필드 기반이므로 초기값 설정
            if (currentLevelData.rotationPattern == RotationPatternType.ReactiveAxis)
                reactiveAxis = axis;

            isRotating = true;
            
            if (null != cts)
            {
                cts.Cancel();
                cts.Dispose();
            }
            cts = new();

            RotateLoop(axis, cts.Token).Forget();
        }

        /// <summary>
        /// 회전 패턴에 따라 회전 축 벡터를 생성
        /// </summary>
        private Vector3 GenerateRotationAxis(RotationPatternType pattern)
        {
            return pattern switch
            {
                RotationPatternType.SingleAxis => CARDINAL_AXES[Random.Range(0, CARDINAL_AXES.Length)],
                RotationPatternType.MultiAxis or RotationPatternType.ReactiveAxis
                    => new Vector3(Random.value, Random.value, Random.value).normalized,
                _ => Vector3.zero
            };
        }

        /// <summary>
        /// 즉시 회전 정지
        /// </summary>
        public void StopRotation()
        {
            isRotating = false;
            cts?.Cancel();
        }

        /// <summary>
        /// 2초에 걸쳐 점진적으로 감속 정지 (스테이지 클리어 연출)
        /// </summary>
        private async UniTask StopRotationGradually()
        {
            isRotating = false;
            cts?.Cancel();
            cts?.Dispose();
            cts = new();

            float duration = 2.0f;
            float elapsed = 0f;
            float startSpeed = null != currentLevelData ? currentLevelData.rotationSpeed : 10f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float currentSpeed = Mathf.Lerp(startSpeed, 0f, elapsed / duration);
                transform.Rotate(Vector3.up * currentSpeed * Time.deltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        /// <summary>
        /// 패턴별 회전 로직을 매 프레임 실행하는 메인 루프
        /// </summary>
        private async UniTaskVoid RotateLoop(Vector3 axis, CancellationToken token)
        {
            if (null == currentLevelData)
                return;

            while (isRotating && !token.IsCancellationRequested)
            {
                switch (currentLevelData.rotationPattern)
                {
                    case RotationPatternType.Static:
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        break;

                    case RotationPatternType.SingleAxis:
                    case RotationPatternType.MultiAxis:
                        RotateFixedAxis(axis);
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        break;

                    case RotationPatternType.ReactiveAxis:
                        RotateFixedAxis(reactiveAxis);
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        break;

                    case RotationPatternType.Random:
                        await RotateRandom(token);
                        break;
                }
            }
        }

        /// <summary>
        /// 고정 축 기반 회전 (SingleAxis, MultiAxis, ReactiveAxis 공용)
        /// </summary>
        private void RotateFixedAxis(Vector3 axis)
        {
            transform.Rotate(axis * currentLevelData.rotationSpeed * Time.deltaTime);
        }

        /// <summary>
        /// 랜덤 축으로 일정 시간(2~5초) 회전 후 새 축으로 전환
        /// </summary>
        private async UniTask RotateRandom(CancellationToken token)
        {
            var randomAxis = Random.onUnitSphere;
            float duration = Random.Range(2f, 5f);
            float elapsed = 0f;

            while (duration > elapsed && isRotating && !token.IsCancellationRequested)
            {
                transform.Rotate(randomAxis * currentLevelData.rotationSpeed * Time.deltaTime);
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        #endregion
    }
}
