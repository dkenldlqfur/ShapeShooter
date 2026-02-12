using UnityEngine;
using Cysharp.Threading.Tasks;
using ShapeShooter.Core;
using System.Threading;

namespace ShapeShooter.Shape
{
    public class ShapeRotationController : MonoBehaviour
    {
        private LevelData currentLevelData;
        private CancellationTokenSource cts;
        private bool isRotating = false;

        public void Initialize(LevelData data)
        {
            currentLevelData = data;
            isRotating = true;
            
            if (null != cts)
            {
                cts.Cancel();
                cts.Dispose();
            }
            cts = new CancellationTokenSource();

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

                    case RotationPatternType.Chaos:
                         // 매 프레임 랜덤 회전 (어지러움 주의)
                        transform.Rotate(Random.onUnitSphere * currentLevelData.rotationSpeed * Time.deltaTime);
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        break;
                }
            }
        }

        private void OnDestroy()
        {
            cts?.Cancel();
            cts?.Dispose();
        }
    }
}
