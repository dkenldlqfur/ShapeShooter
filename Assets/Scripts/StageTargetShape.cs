using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace ShapeShooter
{
    /// <summary>
    /// 타겟 도형 관리자. (단일) 메쉬 하위 PolygonColorManager 자동 부착 및 초기화, 타격 완료 카운트 추적, 회전 패턴 제어, 스테이지 클리어 처리를 담당합니다.
    /// </summary>
    public class StageTargetShape : MonoBehaviour
    {
        /// <summary>SingleAxis에서 선택 가능한 기본 축 (X, Y, Z)</summary>
        private static readonly Vector3[] CARDINAL_AXES = { Vector3.right, Vector3.up, Vector3.forward };

        private PolygonColorManager[] polygonManagers;
        private LevelData currentLevelData;
        private CancellationTokenSource cts;
        private bool isRotating = false;
        private int totalFaces;
        private int completedFaces;

        /// <summary>ReactiveAxis 패턴 전용: 총알 충돌 시 무작위로 변경되는 회전 축</summary>
        private Vector3 reactiveAxis;

        private void Start()
        {
            Initialize();
        }

        /// <summary>
        /// 하위 폴리곤 매니저 검색 및 이벤트 구독, 기하학적 전체 면 갯수 합산, 회전 시작
        /// </summary>
        public void Initialize()
        {
            // 하위에 MeshFilter는 있지만 PolygonColorManager가 없는 경우 자동 부착 (단일 메쉬 분할용)?
            var filters = GetComponentsInChildren<MeshFilter>();
            foreach (var filter in filters)
            {
                if (null != filter.GetComponent<MeshCollider>() && null == filter.GetComponent<PolygonColorManager>())
                {
                    filter.gameObject.AddComponent<PolygonColorManager>();
                }
            }

            polygonManagers = GetComponentsInChildren<PolygonColorManager>();
            
            if (null == polygonManagers || 0 == polygonManagers.Length)
                return;

            completedFaces = 0;
            totalFaces = 0;

            if (null != GameManager.Instance)
                currentLevelData = GameManager.Instance.GetCurrentLevelData();

            foreach (var pm in polygonManagers)
            {
                pm.Initialize(currentLevelData);
                pm.OnFaceCompleted += HandleFaceCompleted;
                pm.OnFaceRestored += HandleFaceRestored;
                pm.OnFaceHit += HandleFaceHit;

                totalFaces += pm.TotalFaces;
            }

            StartRotation();
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
        /// 피격 이벤트 호출 시, ReactiveAxis 패턴이면 회전 축을 무작위로 새로 생성
        /// </summary>
        private void HandleFaceHit()
        {
            if (null != currentLevelData && RotationPatternType.ReactiveAxis == currentLevelData.rotationPattern)
                reactiveAxis = new Vector3(Random.value, Random.value, Random.value).normalized;
        }

        #endregion

        /// <summary>
        /// 모든 면의 색상 변환이 완료되었으면 스테이지 클리어 처리
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
            if (null != polygonManagers)
            {
                foreach (var pm in polygonManagers)
                {
                    if (null != pm)
                    {
                        pm.OnFaceCompleted -= HandleFaceCompleted;
                        pm.OnFaceRestored -= HandleFaceRestored;
                        pm.OnFaceHit -= HandleFaceHit;
                    }
                }
            }

            isRotating = false;
            cts?.Cancel();
            cts?.Dispose();
        }

        #region 회전 제어

        /// <summary>
        /// 레벨 데이터의 패턴에 따라 최초 회전 축을 생성하고 회전 루프 코루틴(UniTask) 시작
        /// </summary>
        private void StartRotation()
        {
            if (null == currentLevelData)
                return;

            var axis = GenerateRotationAxis(currentLevelData.rotationPattern);

            // ReactiveAxis 패턴일 때만 충돌 전까지 사용할 초기 축 설정?정
            if (RotationPatternType.ReactiveAxis == currentLevelData.rotationPattern)
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
        /// 주어진 회전 패턴에 따라 3D 공간 상의 회전 방향 벡터 생성
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
        /// 회전 즉시 멈춤
        /// </summary>
        public void StopRotation()
        {
            isRotating = false;
            cts?.Cancel();
        }

        /// <summary>
        /// 스테이지 클리어 시 2초에 걸쳐 점진적으로 회전 속도를 0으로 감속
        /// </summary>
        private async UniTask StopRotationGradually()
        {
            isRotating = false;
            cts?.Cancel();
            cts?.Dispose();
            cts = new();

            float duration = 2.0f;
            float elapsed = 0f;
            float startSpeed = 10f;
            if (null != currentLevelData)
                startSpeed = currentLevelData.rotationSpeed;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float currentSpeed = Mathf.Lerp(startSpeed, 0f, elapsed / duration);
                transform.Rotate(Vector3.up * currentSpeed * Time.deltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        /// <summary>
        /// 패턴별 회전 로직을 프레임마다 대기하며 비동기로 실행하는 메인 루프
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
        /// 고정 축 기반 회전 (SingleAxis, MultiAxis, ReactiveAxis 공통 사용)
        /// </summary>
        private void RotateFixedAxis(Vector3 axis)
        {
            transform.Rotate(axis * currentLevelData.rotationSpeed * Time.deltaTime);
        }

        /// <summary>
        /// 무작위 축 방향으로 2~5초간 회전 후, 시간 만료 시 새로운 무작위 축으로 전환하는 패턴
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
