#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using MushroomSurvival.ClientUI;

public static class TempBuildPageGroups
{
    static readonly string[] GroupNames={"LobbyGroup","CharacterSelectionGroup","ShopGroup","SettingsGroup","LoadingGroup","GameplayGroup","ResultGroup","MessagesGroup"};
    [MenuItem("Tools/로컬 메뉴 UI/빈 페이지 그룹으로 정리")]
    static void Build()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(!scene.IsValid() || !scene.name.Contains("TestScene")){Debug.LogError("TestScene을 연 상태에서 실행하세요.");return;}
        var controller=Object.FindFirstObjectByType<ClientMenuController>(FindObjectsInactive.Include);
        if(!controller){Debug.LogError("ClientMenuController를 찾을 수 없습니다.");return;}
        var so=new SerializedObject(controller);var pages=so.FindProperty("pages");int count=Mathf.Min(pages.arraySize,GroupNames.Length);
        for(int i=0;i<count;i++)
        {
            var group=pages.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;if(!group)continue;
            var root=group.transform;var image=group.GetComponent<Image>();var background=root.Find("Background")??root.Find("CharacterSelectionBackground");
            if(image)
            {
                if(!background){var bg=new GameObject("Background",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));Undo.RegisterCreatedObjectUndo(bg,"Create page background");bg.transform.SetParent(root,false);background=bg.transform;EditorUtility.CopySerialized(image,bg.GetComponent<Image>());}
                background.name="Background";var rect=background as RectTransform;
                if(rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;rect.SetAsFirstSibling();}
                background.GetComponent<Image>().enabled=true;Undo.DestroyObjectImmediate(image);
            }
            var renderer=group.GetComponent<CanvasRenderer>();if(renderer)Undo.DestroyObjectImmediate(renderer);
            group.name=GroupNames[i];pages.GetArrayElementAtIndex(i).objectReferenceValue=group;
        }
        so.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("Page roots are empty activation groups; menu actions activate the selected group and deactivate all other groups.");
    }
}
#endif
