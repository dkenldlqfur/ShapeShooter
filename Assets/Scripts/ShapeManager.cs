using UnityEngine;

namespace ShapeShooter
{
    public class ShapeManager : MonoBehaviour
    {
        private ShapeFaceController[] faces;
        private ShapeRotationController rotationController;
        private int totalFaces;
        private int completedFaces;

        private void Awake()
        {
            rotationController = GetComponent<ShapeRotationController>();
        }

        private void Start()
        {
            // 초기화 (게임 매니저 이벤트 연결 등은 실제 씬 구성 시 필요)
            // 현재는 Start에서 초기화한다고 가정
            Initialize();
        }

        public void Initialize()
        {
            faces = GetComponentsInChildren<ShapeFaceController>();
            
            if (null == faces || 0 == faces.Length)
            {
                Debug.LogError("면(Face)을 찾을 수 없습니다!");
                return;
            }

            totalFaces = faces.Length;
            completedFaces = 0;

            // 레벨 데이터 로드
            LevelData levelData = null;
            if (null != GameManager.Instance)
                levelData = GameManager.Instance.GetCurrentLevelData();

            foreach (var face in faces)
            {
                face.Initialize(levelData);
                face.OnFaceCompleted += HandleFaceCompleted;
            }

            if (null != levelData && null != rotationController)
                rotationController.Initialize(levelData);
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
            if (null != rotationController)
                rotationController.StopRotation();

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
        }
    }
}
