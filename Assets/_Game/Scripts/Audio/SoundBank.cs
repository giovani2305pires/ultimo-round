using UnityEngine;

namespace UltimoRound.Audio
{
    /// <summary>Sons gravados por categoria. Categoria vazia usa o som sintetizado como reserva.</summary>
    [CreateAssetMenu(menuName = "Último Round/Banco de Sons", fileName = "SoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        public AudioClip[] LightHits = new AudioClip[0];
        public AudioClip[] HeavyHits = new AudioClip[0];
        public AudioClip[] FinisherHits = new AudioClip[0];
        public AudioClip[] LightBlocks = new AudioClip[0];
        public AudioClip[] HeavyBlocks = new AudioClip[0];
        public AudioClip[] ShortWhooshes = new AudioClip[0];
        public AudioClip[] LongWhooshes = new AudioClip[0];
        public AudioClip[] SpecialImpacts = new AudioClip[0];
        public AudioClip[] SignatureImpacts = new AudioClip[0];
        public AudioClip[] ThrowImpacts = new AudioClip[0];
        public AudioClip[] Acrobatics = new AudioClip[0];
    }
}
