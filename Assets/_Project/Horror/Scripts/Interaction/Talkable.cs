using System;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Dialogue;
using FantaLandia.Horror.Player;
using UnityEngine;

namespace FantaLandia.Horror.Interaction
{
    /// <summary>Un personaggio con cui parlare: una piccola conversazione con una scelta.</summary>
    public class Talkable : Interactable
    {
        [Serializable]
        public class Line
        {
            public string speaker = "tom";
            [TextArea] public string text;
        }

        public string displayName = "Tom";
        public Line[] lines;
        [Tooltip("Risposte possibili del giocatore (vuoto = nessuna scelta)")]
        public string[] choices;
        [Tooltip("Una risposta del personaggio per ogni scelta")]
        public Line[] replies;

        public override string Prompt(Interactor who) => $"Parla con {displayName}";

        public override void Interact(Interactor who)
        {
            var conv = DialogueSystem.Conversation();
            foreach (var l in lines) conv.Say(Speakers.Find(l.speaker), l.text);
            if (choices != null && choices.Length > 0)
            {
                conv.Choice(choices, i =>
                {
                    if (replies != null && i < replies.Length)
                        DialogueSystem.Insert((Speakers.Find(replies[i].speaker), replies[i].text));
                });
            }
            conv.Start();
        }
    }
}
