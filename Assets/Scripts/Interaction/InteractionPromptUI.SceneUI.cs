using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace DetectiveGame.Interaction
{
    public sealed partial class InteractionPromptUI
    {
        [Header("Scene UI — edit Canvas and the three prompt templates")]
        [SerializeField] private bool useSceneUI;
        [SerializeField] private GameObject defaultTemplate;
        [SerializeField] private GameObject recallTemplate;
        [SerializeField] private GameObject interactionTemplate;
        private readonly List<PromptCardKind> sceneCardKinds = new List<PromptCardKind>();

        private void BindSceneUI() { }

        private PromptCard GetSceneCard(int index, PromptCardKind kind)
        {
            while (cards.Count <= index) { cards.Add(null); sceneCardKinds.Add(kind); }
            if (cards[index] != null && sceneCardKinds[index] != kind)
            {
                cards[index].root.SetActive(false);
                Destroy(cards[index].root);
                cards[index] = null;
            }
            if (cards[index] == null)
            {
                GameObject template = kind == PromptCardKind.Recall ? recallTemplate :
                    kind == PromptCardKind.Interaction ? interactionTemplate : defaultTemplate;
                GameObject root = Instantiate(template, promptContainer);
                cards[index] = new PromptCard { root = root, rect = root.GetComponent<RectTransform>(),
                    canvasGroup = root.GetComponent<CanvasGroup>(), background = root.GetComponent<Image>(),
                    layout = root.GetComponent<LayoutElement>(), text = root.GetComponentInChildren<Text>(true) };
                sceneCardKinds[index] = kind;
            }
            PromptCard card = cards[index];
            card.root.SetActive(true);
            card.rect.SetSiblingIndex(index);
            return card;
        }
    }
}
