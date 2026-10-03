using System;
using System.Collections;
using System.IO;
using System.Reflection;
using DetectiveGame.Dialogue;
using DetectiveGame.Evidence;
using DetectiveGame.Interaction;
using DetectiveGame.Recall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DetectiveGame.EditorTools
{
    // One-time migration. Never regenerates an already authored scene or overwrites edited Prefabs.
    public static class SceneUIAuthoringBuilder
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static string folder;
        private static Font uiFont;
        private static Transform uiRoot;
        private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, Flags).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Flags).SetValue(owner, value);
        private static object Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Flags).Invoke(owner, args);

        [MenuItem("Tools/Detective Game/UI/Convert Test and Tutorial to Scene UI")]
        public static void ConvertBoth()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before converting UI.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene edits before converting UI.");
            string original = SceneManager.GetActiveScene().path;
            EnsureFolder("Assets/UI");
            const string fontPath = "Assets/UI/Fonts/NotoSansCJKsc-Regular.otf";
            uiFont = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (uiFont == null)
            {
                throw new InvalidOperationException("Import the bundled Noto Sans CJK font before converting UI.");
            }
            ConvertScene("Assets/Scenes/Test.unity");
            ConvertScene("Assets/Scenes/Tutorial.unity");
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
            Debug.Log("Scene UI conversion complete: Test and Tutorial. Expand UI in Hierarchy; edit templates under Assets/UI.");
        }

        private static void ConvertScene(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path);
            if (GameObject.Find("UI") != null) throw new InvalidOperationException(path + " already has a UI root; refusing to overwrite it.");
            folder = "Assets/UI/" + scene.name;
            EnsureFolder(folder);
            EnsureFolder(folder + "/Templates");
            uiRoot = new GameObject("UI").transform;
            foreach (DialogueRuntimeUI view in Object.FindObjectsByType<DialogueRuntimeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) BakeDialogue(view);
            foreach (EvidenceBoardRuntimeUI view in Object.FindObjectsByType<EvidenceBoardRuntimeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) BakeEvidence(view);
            foreach (RecallRuntimeUI view in Object.FindObjectsByType<RecallRuntimeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) BakeRecall(view);
            foreach (PlayerInteractor player in Object.FindObjectsByType<PlayerInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var view = player.GetComponent<InteractionPromptUI>();
                if (view == null) view = player.gameObject.AddComponent<InteractionPromptUI>();
            }
            foreach (InteractionPromptUI view in Object.FindObjectsByType<InteractionPromptUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) BakePrompt(view);
            if (Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).transform.SetParent(uiRoot, false);
            // Existing Tutorial canvases (telephone, subtitles, fades) are already editable.
            // Keep persistent fade at scene root because DontDestroyOnLoad requires it.
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.transform.parent != null || canvas.GetComponent<DetectiveGame.Player.ScreenFade>() != null) continue;
                canvas.transform.SetParent(uiRoot, false);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BakeDialogue(DialogueRuntimeUI view)
        {
            Invoke(view, "BuildUI");
            bool notebook = view.GetComponent<DialogueController>().UseCaseNotebook;
            view.ConfigureCaseNotebook(notebook);
            GameObject canvas = Get<GameObject>(view, "canvasObject");
            var hud = Get<GameObject>(view, "persistentHud");
            Set(view, "notebookButton", hud.transform.Find("Investigation Menu Button").GetComponent<Button>());
            Set(view, "advanceButton", Get<GameObject>(view, "dialogueRoot").transform.Find("Dialogue Box").GetComponent<Button>());
            Set(view, "boardCloseButton", Get<GameObject>(view, "boardRoot").transform.Find("Close").GetComponent<Button>());
            Set(view, "logCloseButton", Get<GameObject>(view, "logRoot").transform.Find("Close").GetComponent<Button>());

            GameObject choice = ButtonObject("DialogueChoice", "[1] 对话选项", 30);
            choice.GetComponent<RectTransform>().sizeDelta = new Vector2(900, 82);
            choice.GetComponent<LayoutElement>().preferredHeight = 82;
            choice.GetComponent<Image>().color = Color.white;
            var colors = choice.GetComponent<Button>().colors;
            colors.normalColor = new Color(.93f, .91f, .84f, 1f);
            colors.highlightedColor = Color.white;
            choice.GetComponent<Button>().colors = colors;
            choice.GetComponentInChildren<Text>().color = Color.black;
            Set(view, "choiceTemplate", SaveTemplate(choice).GetComponent<Button>());
            RectTransform choices = Get<RectTransform>(view, "choiceContainer");
            choices.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 900f);
            var fitter = choices.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // Generated wheel defaults to alpha zero. Keep it visible when designers enable its GameObject.
            Get<CanvasGroup>(view, "wheelCanvasGroup").alpha = 1f;
            Material blur = Get<Material>(view, "blurMaterial");
            if (blur != null)
            {
                AssetDatabase.CreateAsset(blur, folder + "/PauseBackground.mat");
                Get<GameObject>(view, "wheelRoot").GetComponent<Image>().material = blur;
            }
            Set(view, "font", uiFont);
            Set(view, "ownsFont", false);
            Set(view, "useSceneUI", true);
            FinishCanvas(canvas, "Dialogue and Case Notebook");
            EditorUtility.SetDirty(view);
        }

        private static void BakeEvidence(EvidenceBoardRuntimeUI view)
        {
            Invoke(view, "BuildUI");
            var board = Get<GameObject>(view, "boardRoot");
            bool notebook = view.GetComponent<DialogueController>() != null && view.GetComponent<DialogueController>().UseCaseNotebook;
            view.SetNotebookLayout(notebook);
            Set(view, "closeButton", board.transform.Find("Close").GetComponent<Button>());
            Set(view, "playButton", Get<Text>(view, "playButtonText").GetComponentInParent<Button>(true));

            GameObject card = ButtonObject("EvidenceCard", "物证名称", 23);
            card.GetComponent<RectTransform>().sizeDelta = new Vector2(175, 132);
            var cardView = card.AddComponent<EvidenceCardView>();
            cardView.button = card.GetComponent<Button>(); cardView.label = card.GetComponentInChildren<Text>();
            var icon = Panel("Icon", card.transform, Color.white);
            Stretch(icon.GetComponent<RectTransform>(), new Vector2(.1f, .3f), new Vector2(.9f, .92f));
            cardView.icon = icon.GetComponent<Image>(); cardView.icon.preserveAspect = true; cardView.icon.raycastTarget = false;
            cardView.icon.gameObject.SetActive(false);
            Set(view, "cardTemplate", SaveTemplate(card).GetComponent<EvidenceCardView>());

            var row = Panel("EvidenceMoment", null, new Color(.055f, .06f, .075f, .98f));
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(960, 215);
            row.AddComponent<LayoutElement>().preferredHeight = 215;
            var moment = row.AddComponent<EvidenceMomentView>();
            moment.fact = Label("Observed Fact", row.transform, "[19:00] 客观记录", 25);
            Position(moment.fact.rectTransform, new Vector2(0, 1), new Vector2(.58f, 1), new Vector2(18,-16), new Vector2(-26,64), new Vector2(0,1));
            var drop = Panel("Inference Drop Zone", row.transform, new Color(.08f,.085f,.1f,1));
            Position(drop.GetComponent<RectTransform>(), new Vector2(.6f,1), Vector2.one, new Vector2(-18,-14), new Vector2(-12,68), Vector2.one);
            var outline = drop.AddComponent<Outline>(); outline.effectColor = new Color(.34f,.62f,.88f,1); outline.effectDistance = new Vector2(2,-2);
            moment.selection = Label("Selection", drop.transform, "拖拽选项到这里", 22);
            Stretch(moment.selection.rectTransform, Vector2.zero, Vector2.one, 8);
            moment.dropZone = drop.AddComponent<EvidenceChoiceDropZone>();
            moment.options = Rect("Options", row.transform);
            Position(moment.options, Vector2.zero, Vector2.right, new Vector2(0,18), new Vector2(0,66), new Vector2(.5f,0));
            var layout = moment.options.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20; layout.padding = new RectOffset(10,10,0,0);
            layout.childControlWidth = true; layout.childForceExpandWidth = true; layout.childControlHeight = true;
            Set(view, "momentTemplate", SaveTemplate(row).GetComponent<EvidenceMomentView>());
            var option = ButtonObject("EvidenceInferenceOption", "A. 玩家推测", 21);
            option.GetComponent<LayoutElement>().flexibleWidth = 1;
            option.AddComponent<CanvasGroup>(); option.AddComponent<EvidenceChoiceDragHandler>();
            Set(view, "optionTemplate", SaveTemplate(option).GetComponent<Button>());
            var marker = Panel("EvidenceTimeMarker", null, new Color(.34f,.62f,.88f,1));
            marker.GetComponent<RectTransform>().sizeDelta = new Vector2(8,30);
            marker.GetComponent<Image>().raycastTarget = false;
            Set(view, "markerTemplate", SaveTemplate(marker));
            Set(view, "font", uiFont); Set(view, "ownsFont", false); Set(view, "useSceneUI", true);
            FinishCanvas(Get<GameObject>(view, "canvasObject"), "Evidence Board");
            EditorUtility.SetDirty(view);
        }

        private static void BakeRecall(RecallRuntimeUI view)
        {
            Invoke(view, "BuildUI");
            var selection = Get<GameObject>(view, "selectionPanel");
            RectTransform content = Rect("Period Entries", selection.transform);
            Position(content, Vector2.up, Vector2.one, new Vector2(0,-48), new Vector2(-20,124), new Vector2(.5f,1));
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8; layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.childControlWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Set(view, "periodContent", content);
            var entry = Panel("RecallPeriod", null, new Color(.1f,.08f,.065f,.9f));
            entry.GetComponent<RectTransform>().sizeDelta = new Vector2(480,54);
            entry.AddComponent<LayoutElement>().preferredHeight = 54;
            var text = Label("Period Label", entry.transform, "[1] 19:00–19:15 回溯时间", 21);
            text.alignment = TextAnchor.MiddleLeft;
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, 6);
            text.rectTransform.offsetMin = new Vector2(18,6); text.rectTransform.offsetMax = new Vector2(-18,-6);
            Set(view, "periodTemplate", SaveTemplate(entry).GetComponent<Image>());
            var hint = Get<Text>(view, "selectionHint").rectTransform;
            Position(hint, Vector2.zero, Vector2.right, new Vector2(0,6), new Vector2(-20,30), new Vector2(.5f,0));
            Get<Text>(view, "selectionTitle").text = "选择回溯时间";
            Get<Text>(view, "promptText").text = "[R] 回溯";
            Get<Text>(view, "titleText").text = "物体名称 · 回溯时间";
            Get<Text>(view, "statusText").text = "已暂停";
            Get<Text>(view, "startTimeText").text = "19:00";
            Get<Text>(view, "endTimeText").text = "19:15";
            Get<Text>(view, "currentTimeText").text = "19:00";
            Get<Text>(view, "instructionText").text = "空格：播放 / 暂停    拖动时间轴：查看状态    Esc：退出回溯";
            view.HideRecall(); view.SetWorldPrompt(null);
            Set(view, "font", uiFont); Set(view, "useSceneUI", true);
            FinishCanvas(Get<GameObject>(view, "canvasObject"), "Recall");
            EditorUtility.SetDirty(view);
        }

        private static void BakePrompt(InteractionPromptUI view)
        {
            Invoke(view, "BuildUI");
            foreach (string kind in new[] { "default", "recall", "interaction" })
            {
                object card = Invoke(view, "CreateCard", 0);
                var root = Get<GameObject>(card, "root");
                root.name = kind + "Prompt";
                object style = Get<object>(view, kind + "CardStyle");
                root.GetComponent<Image>().color = Get<Color>(style, "background");
                Vector2 size = Get<Vector2>(style, "size");
                root.GetComponent<RectTransform>().sizeDelta = size;
                var element = root.GetComponent<LayoutElement>(); element.preferredWidth = size.x; element.preferredHeight = size.y;
                Text label = root.GetComponentInChildren<Text>(); label.font = uiFont;
                label.fontSize = Get<int>(style, "fontSize"); label.color = Get<Color>(style, "textColor");
                label.text = kind == "recall" ? "【R 回溯】" : "[E] 交互";
                label.rectTransform.offsetMin = new Vector2(Get<float>(style, "horizontalPadding"),Get<float>(style, "verticalPadding"));
                label.rectTransform.offsetMax = -label.rectTransform.offsetMin;
                Set(view, kind + "Template", SaveTemplate(root));
            }
            Get<GameObject>(view, "promptContainerObject").SetActive(false);
            Set(view, "runtimeFont", uiFont); Set(view, "useSceneUI", true);
            FinishCanvas(Get<GameObject>(view, "canvasObject"), "Interaction Prompts");
            EditorUtility.SetDirty(view);
        }

        private static void FinishCanvas(GameObject canvas, string name)
        {
            canvas.name = name;
            canvas.transform.SetParent(uiRoot, false);
            foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.font = uiFont;
            PrefabUtility.SaveAsPrefabAssetAndConnect(canvas, folder + "/" + name + ".prefab", InteractionMode.AutomatedAction);
        }
        private static GameObject SaveTemplate(GameObject instance)
        {
            string path = folder + "/Templates/" + instance.name + ".prefab";
            foreach (Text text in instance.GetComponentsInChildren<Text>(true)) text.font = uiFont;
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return asset;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\','/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go.GetComponent<RectTransform>();
        }
        private static GameObject Panel(string name, Transform parent, Color color)
        {
            var r = Rect(name,parent); r.gameObject.AddComponent<Image>().color = color; return r.gameObject;
        }
        private static Text Label(string name, Transform parent, string value, int size)
        {
            var text = Rect(name,parent).gameObject.AddComponent<Text>(); text.font = uiFont; text.fontSize = size;
            text.text = value; text.color = new Color(.92f,.9f,.82f,1); text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
        }
        private static GameObject ButtonObject(string name, string value, int size)
        {
            var go = Panel(name,null,Color.white); var button = go.AddComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
            var colors = button.colors; colors.normalColor = new Color(.105f,.115f,.14f,1); colors.highlightedColor = new Color(.2f,.25f,.32f,1);
            colors.selectedColor = colors.highlightedColor; button.colors = colors;
            go.AddComponent<LayoutElement>(); var text = Label("Text",go.transform,value,size); Stretch(text.rectTransform,Vector2.zero,Vector2.one,8); return go;
        }
        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, float inset=0)
        { rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=Vector2.one*inset;rect.offsetMax=-Vector2.one*inset; }
        private static void Position(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size, Vector2 pivot)
        {rect.anchorMin=min;rect.anchorMax=max;rect.pivot=pivot;rect.sizeDelta=size;rect.anchoredPosition=position;}
    }
}
