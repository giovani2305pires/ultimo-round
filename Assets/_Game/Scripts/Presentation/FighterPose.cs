using UnityEngine;

namespace UltimoRound.Presentation
{
    /// <summary>
    /// Pose do corpo procedural. Ângulos em graus no plano lateral (rotação em Z):
    /// ombro/quadril positivos = membro para frente; cotovelo positivo = antebraço dobra para frente/cima;
    /// joelho positivo = canela dobra para trás. "Front" é o membro mais próximo da câmera (guarda avançada).
    /// </summary>
    public struct FighterPose
    {
        public float HipY;       // deslocamento vertical do quadril (agachar)
        public float HipX;       // deslocamento horizontal (avançar o peso)
        public float Lean;       // inclinação do tronco para frente
        public float Twist;      // giro do tronco (Y) para dar profundidade aos socos
        public float Head;       // inclinação da cabeça
        public float FShoulder, FElbow, RShoulder, RElbow;
        public float FHip, FKnee, RHip, RKnee;
        public float BodyRoll;   // rotação do corpo inteiro (derrubado/arremessado)
        public float FArmOut, RArmOut; // abertura lateral dos braços (gancho, comemoração)

        public static FighterPose Lerp(in FighterPose a, in FighterPose b, float t)
        {
            t = Mathf.Clamp01(t);
            return new FighterPose
            {
                HipY = Mathf.Lerp(a.HipY, b.HipY, t),
                HipX = Mathf.Lerp(a.HipX, b.HipX, t),
                Lean = Mathf.Lerp(a.Lean, b.Lean, t),
                Twist = Mathf.Lerp(a.Twist, b.Twist, t),
                Head = Mathf.Lerp(a.Head, b.Head, t),
                FShoulder = Mathf.Lerp(a.FShoulder, b.FShoulder, t),
                FElbow = Mathf.Lerp(a.FElbow, b.FElbow, t),
                RShoulder = Mathf.Lerp(a.RShoulder, b.RShoulder, t),
                RElbow = Mathf.Lerp(a.RElbow, b.RElbow, t),
                FHip = Mathf.Lerp(a.FHip, b.FHip, t),
                FKnee = Mathf.Lerp(a.FKnee, b.FKnee, t),
                RHip = Mathf.Lerp(a.RHip, b.RHip, t),
                RKnee = Mathf.Lerp(a.RKnee, b.RKnee, t),
                BodyRoll = Mathf.Lerp(a.BodyRoll, b.BodyRoll, t),
                FArmOut = Mathf.Lerp(a.FArmOut, b.FArmOut, t),
                RArmOut = Mathf.Lerp(a.RArmOut, b.RArmOut, t),
            };
        }

        /// <summary>Suaviza com curva (ease-out) para dar impacto às extensões.</summary>
        public static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
        public static float EaseInOut(float t) => t * t * (3f - 2f * t);
    }
}
