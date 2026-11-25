using System.Collections.Generic;
using UnityEngine;

namespace FantaLandia.Characters
{
    /// <summary>Un membro del party che il giocatore può selezionare e comandare.</summary>
    [RequireComponent(typeof(CharacterMotor))]
    public class SelectableCharacter : MonoBehaviour
    {
        public static readonly List<SelectableCharacter> All = new List<SelectableCharacter>();

        public string displayName = "Viandante";
        public GameObject selectionIndicator;

        public CharacterMotor Motor { get; private set; }
        public bool IsSelected { get; private set; }

        void Awake()
        {
            Motor = GetComponent<CharacterMotor>();
            SetSelected(false);
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (selectionIndicator != null) selectionIndicator.SetActive(selected);
        }
    }
}
