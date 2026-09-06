using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DreamCar.Editor
{
    public static class PlayableMvpMenu
    {
        [MenuItem("DreamCar/Playable MVP/Create Test Scene")]
        public static void CreateTestScene()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("PlayableMVP");
            root.AddComponent<DreamCar.Playable.OfflinePlayableBootstrap>();
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/PlayableMVP.unity");
            Selection.activeGameObject=root;
            Debug.Log("DreamCar Playable MVP scene created. Press Play and drive with WASD/arrows.");
        }
    }
}
