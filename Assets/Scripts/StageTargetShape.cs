using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace ShapeShooter
{
    /// <summary>
    /// 개별 스테이지의 표적 도형 인스턴스를 관리하는 컴포넌트입니다. 모델 데이터의 초기화, 폴리곤 분리 매니저의 할당,
    /// 피격 누적 횟수 추적 및 회전 애니메이션의 생명주기를 전담합니다.
    /// </summary>
    public class StageTargetShape : MonoBehaviour
    {
        /// <summary>단일축(SingleAxis) 패턴 적용 시 무작위로 채택되는 3방향의 로컬 직교 기저 벡터입니다.</summary>
        private static readonly Vector3[] CARDINAL_AXES = { Vector3.right, Vector3.up, Vector3.forward };

        private PolygonColorManager[] polygonManagers;
        private LevelData currentLevelData;
        private CancellationTokenSource cts;
        private bool isRotating = false;
        private int totalPolygons;
        private int completedPolygons;

        /// <summary>타격 반응형(ReactiveAxis) 패턴 적용 시, 피격 이벤트 발생마다 새롭게 갱신되는 난수 생성 축입니다.</summary>
        private Vector3 reactiveAxis;

        private void Start()
        {
            Initialize();
        }

        /// <summary>
        /// 하위 폴리곤 노드를 순회하여 종속성을 주입하고 전체 타격 목표량을 합산한 뒤 궤도 회전을 개시합니다.
        /// </summary>
        public void Initialize()
        {
            // 하위 렌더러(MeshFilter) 노드 스캔을 통해 지연 참조된 폴리곤 관리 모듈을 자동 할당합니다.
            var filters = GetComponentsInChildren<MeshFilter>();
            foreach (var filter in filters)
            {
                if (null != filter.GetComponent<MeshCollider>() && null == filter.GetComponent<PolygonColorManager>())
                    filter.gameObject.AddComponent<PolygonColorManager>();
            }

            polygonManagers = GetComponentsInChildren<PolygonColorManager>();
            
            if (null == polygonManagers || 0 == polygonManagers.Length)
                return;

            completedPolygons = 0;
            totalPolygons = 0;

            if (null != GameManager.Instance)
                currentLevelData = GameManager.Instance.GetCurrentLevelData();

            foreach (var pm in polygonManagers)
            {
                pm.Initialize(currentLevelData);
                pm.OnPolygonCompleted += HandlePolygonCompleted;
                pm.OnPolygonRestored += HandlePolygonRestored;
                pm.OnPolygonHit += HandlePolygonHit;

                totalPolygons += pm.TotalPolygons;
            }

            StartRotation();
        }

        #region 면 이벤트 핸들러

        private void HandlePolygonCompleted()
        {
            completedPolygons++;
            CheckCompletion();
        }

        private void HandlePolygonRestored()
        {
            if (0 < completedPolygons)
                completedPolygons--;
        }

        /// <summary>
        /// 피격 콜백 수신 시, 현재 회전 상태가 반응형(Reactive)일 경우 기저 회전축을 무작위로 재할당합니다.
        /// </summary>
        private void HandlePolygonHit()
        {
            if (null != currentLevelData && RotationPatternType.ReactiveAxis == currentLevelData.rotationPattern)
                reactiveAxis = Random.onUnitSphere;
        }

        #endregion

        /// <summary>
        /// 진행률(Progress) 파라미터를 대조하여 목표에 도달 시 스테이지 클리어 이벤트를 전역 브로드캐스팅합니다.
        /// </summary>
        private void CheckCompletion()
        {
            if (totalPolygons <= completedPolygons)
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
                        pm.OnPolygonCompleted -= HandlePolygonCompleted;
                        pm.OnPolygonRestored -= HandlePolygonRestored;
                        pm.OnPolygonHit -= HandlePolygonHit;
                    }
                }
            }

            isRotating = false;
            cts?.Cancel();
            cts?.Dispose();
        }

        #region 회전 제어

        /// <summary>
        /// 할당된 레벨 메타데이터의 행동 강령에 맞추어 주 기저 축을 설정하고 비동기 회전 루틴을 트리거합니다.
        /// </summary>
        private void StartRotation()
        {
            if (null == currentLevelData)
                return;

            var axis = GenerateRotationAxis(currentLevelData.rotationPattern);

            // 타격 반응형 루틴일 경우 최초 1회 충돌 전까지 대기할 상태 축을 캐싱합니다.
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
        /// 열거형(Enum)으로 명시된 회전 성향을 3차원 공간 백터(Vector3) 축으로 컨버팅하여 반환합니다.
        /// </summary>
        private Vector3 GenerateRotationAxis(RotationPatternType pattern)
        {
            return pattern switch
            {
                RotationPatternType.SingleAxis      => CARDINAL_AXES[Random.Range(0, CARDINAL_AXES.Length)],
                RotationPatternType.MultiAxis       => new Vector3(Random.value, Random.value, Random.value).normalized,
                RotationPatternType.ReactiveAxis    => Random.onUnitSphere,
                _ => Vector3.zero
            };
        }

        /// <summary>
        /// 진행중인 트랜스폼 회전(Rotation) 스레드를 즉시 강제 종료합니다.
        /// </summary>
        public void StopRotation()
        {
            isRotating = false;
            cts?.Cancel();
        }

        /// <summary>
        /// 스테이지 클리어 시 2.0s 이징(Easing) 기간에 걸쳐 타겟의 회전 모멘텀을 상쇄시키는 비동기 트위닝 함수입니다.
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
                transform.Rotate(currentSpeed * Time.deltaTime * Vector3.up);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        /// <summary>
        /// 게임 활성 루프 동안 객체의 회전 모션을 프레임 단위 연속 호출하는 코어 백그라운드 태스크입니다.
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
        /// 고정된 축(Axis)을 베이스로 프레임 델타 타임에 비례한 선형 회전값을 트랜스포머에 누적시킵니다.
        /// </summary>
        private void RotateFixedAxis(Vector3 axis)
        {
            transform.Rotate(currentLevelData.rotationSpeed * Time.deltaTime * axis);
        }

        /// <summary>
        /// 일정 주기로 생성되는 구면 무작위 벡터(OnUnitSphere) 기저를 기반으로 회전 궤적을 예측 불능하게 변조합니다.
        /// </summary>
        private async UniTask RotateRandom(CancellationToken token)
        {
            var randomAxis = Random.onUnitSphere;
            float duration = Random.Range(2f, 5f);
            float elapsed = 0f;

            while (duration > elapsed && isRotating && !token.IsCancellationRequested)
            {
                transform.Rotate(currentLevelData.rotationSpeed * Time.deltaTime * randomAxis);
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        #endregion
    }
}
