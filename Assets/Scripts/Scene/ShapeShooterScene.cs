using UnityEngine;

namespace ShapeShooter
{
    /// <summary>
    /// 단일 씬 기반 게임 사이클의 엔트리 포인트 제어 및 환경 프로필 확장에 대응하는 최상위 노드입니다.
    /// </summary>
    public class ShapeShooterScene : MonoBehaviour
    {
        [SerializeField] private GameUI cachedGameUI;

        private void Start()
        {
            if (null == cachedGameUI)
                cachedGameUI = FindAnyObjectByType<GameUI>();

            if (null != cachedGameUI)
                cachedGameUI.OnGameStartRequested += HandleGameStartRequested;
        }

        private void OnDestroy()
        {
            if (null != cachedGameUI)
                cachedGameUI.OnGameStartRequested -= HandleGameStartRequested;
        }

        private void HandleGameStartRequested()
        {
            // 이 씬에 진입했을 때의 초기화 로직 순서를 여기서 제어할 수 있습니다.
            if (null != GameManager.Instance)
                GameManager.Instance.StartGame().Forget();
        }
    }
}
