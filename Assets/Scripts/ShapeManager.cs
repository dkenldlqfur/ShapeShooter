using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace ShapeShooter
{
    public class ShapeManager : MonoBehaviour
    {
        private ShapeFace[] faces;
        private LevelData currentLevelData;
        private CancellationTokenSource cts;
        private bool isRotating = false;
        private int totalFaces;
        private int completedFaces;

        private void Awake()
        {
        }

        private void Start()
        {
            // 초기화 (게임 매니저 이벤트 연결 등은 실제 씬 구성 시 필요)
            // 현재는 Start에서 초기화한다고 가정
            Initialize();
        }

        public void Initialize()
        {
            faces = GetComponentsInChildren<ShapeFace>();
            
            if (null == faces || 0 == faces.Length)
            {
                Debug.LogError("면(Face)을 찾을 수 없습니다!");
                return;
            }

            completedFaces = 0;

            // 레벨 데이터 로드
            if (null != GameManager.Instance)
                currentLevelData = GameManager.Instance.GetCurrentLevelData();

            // 링크된 서브 면은 카운트에서 제외
            // (링크된 면은 대표 면의 완료 이벤트만 발생시킴)
            int linkedSubFaceCount = 0;
            foreach (var face in faces)
            {
                face.Initialize(currentLevelData);
                face.OnFaceCompleted += HandleFaceCompleted;

                // 다른 면의 linkedFaces에 포함된 면은 서브 면으로 간주
                if (IsLinkedSubFace(face))
                    linkedSubFaceCount++;
            }

            totalFaces = faces.Length - linkedSubFaceCount;

            StartRotation();
        }

        /// <summary>
        /// 해당 면이 다른 면의 linkedFaces 배열에 포함되어 있는지 확인
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

        private void HandleFaceCompleted()
        {
            completedFaces++;
            CheckCompletion();
        }

        private void CheckCompletion()
        {
            // 모든 면이 완료되었는지 확인
            if (totalFaces <= completedFaces)
                OnStageClear();
        }

        private void OnStageClear()
        {
            // 회전 멈춤
            StopRotation();

            // GameManager에 알림
            if (null != GameManager.Instance)
                GameManager.Instance.CompleteStage();
        }
        
        private void OnDestroy()
        {
            foreach (var face in faces)
            {
                if (null != face)
                    face.OnFaceCompleted -= HandleFaceCompleted;
            }

            StopRotation();
            cts?.Dispose();
        }

        private void StartRotation()
        {
            if (null == currentLevelData)
                return;

            isRotating = true;
            
            if (null != cts)
            {
                cts.Cancel();
                cts.Dispose();
            }
            cts = new();

            RotateLoop(cts.Token).Forget();
        }

        public void StopRotation()
        {
            isRotating = false;
            cts?.Cancel();
        }

        private async UniTaskVoid RotateLoop(CancellationToken token)
        {
            if (null == currentLevelData)
                return;

            while (isRotating && !token.IsCancellationRequested)
            {
                switch (currentLevelData.rotationPattern)
                {
                    case RotationPatternType.Static:
                        // 회전 없음
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        break;

                    case RotationPatternType.SingleAxis:
                        transform.Rotate(Vector3.up * currentLevelData.rotationSpeed * Time.deltaTime);
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        break;

                    case RotationPatternType.MultiAxis:
                        transform.Rotate(new Vector3(1, 1, 0) * currentLevelData.rotationSpeed * Time.deltaTime);
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        break;

                    case RotationPatternType.Random:
                        // 랜덤 축으로 일정 시간 회전 후 변경
                        Vector3 randomAxis = Random.onUnitSphere;
                        float duration = Random.Range(2f, 5f);
                        float elapsed = 0f;

                        while (duration > elapsed && isRotating && !token.IsCancellationRequested)
                        {
                            transform.Rotate(randomAxis * currentLevelData.rotationSpeed * Time.deltaTime);
                            elapsed += Time.deltaTime;
                            await UniTask.Yield(PlayerLoopTiming.Update, token);
                        }
                        break;
                }
            }
        }
    }
}
