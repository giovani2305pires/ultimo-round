using UnityEngine;

namespace UltimoRound.Presentation
{
    /// <summary>
    /// Câmera lateral em perspectiva com FOV estreito (aparência quase ortográfica, mas com profundidade no cenário).
    /// Acompanha o ponto médio, afasta com a distância entre os lutadores dentro de limites e nunca gira:
    /// a direção dos controles não muda por movimento de câmera. Tremor curto e ajustável.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FightCamera : MonoBehaviour
    {
        public float Fov = 26f;
        public float BaseHeight = 1.45f;
        public float MinDistance = 8.2f;
        public float MaxDistance = 12.5f;
        public float DistancePerMeter = 0.85f;
        public float LateralLimit = 2.4f;
        public float Pitch = 3.5f;
        public float Smooth = 7f;

        Camera _cam;
        Vector3 _pos;
        float _shake;
        float _shakeTime;
        float _zoomPunch;
        Vector3 _focusOverride;
        float _focusWeight;
        public float Intensity = 0.7f;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.fieldOfView = Fov;
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 80f;
        }

        public void Snap(Vector2 a, Vector2 b)
        {
            _pos = Target(a, b);
            transform.position = _pos;
            transform.rotation = Quaternion.Euler(Pitch, 0, 0);
        }

        Vector3 Target(Vector2 a, Vector2 b)
        {
            float mid = (a.x + b.x) * 0.5f;
            float sep = Mathf.Abs(a.x - b.x);
            float maxY = Mathf.Clamp(Mathf.Max(a.y, b.y), 0f, 2.2f);
            // Pulo: sobe e afasta um pouco para manter cabeça e pés no quadro (abaixo do HUD).
            float dist = Mathf.Clamp(MinDistance + Mathf.Max(0, sep - 2f) * DistancePerMeter, MinDistance, MaxDistance) + maxY * 1.1f;
            float y = BaseHeight + maxY * 0.55f;
            float x = Mathf.Clamp(mid, -LateralLimit, LateralLimit);
            return new Vector3(x, y, -dist);
        }

        public void AddShake(float amount)
        {
            _shake = Mathf.Max(_shake, amount * Intensity);
        }

        public void ZoomPunch(float amount) => _zoomPunch = Mathf.Max(_zoomPunch, amount * Mathf.Lerp(0.4f, 1f, Intensity));

        /// <summary>Foco temporário (assinatura/golpe final), suave e curto.</summary>
        public void Focus(Vector3 point, float weight)
        {
            _focusOverride = point;
            _focusWeight = Mathf.Clamp01(weight);
        }

        public void Tick(Vector2 a, Vector2 b, float dt)
        {
            var target = Target(a, b);
            if (_focusWeight > 0f)
            {
                var f = new Vector3(_focusOverride.x, _focusOverride.y + 0.2f, target.z + 2.2f);
                target = Vector3.Lerp(target, f, _focusWeight);
                _focusWeight = Mathf.MoveTowards(_focusWeight, 0f, dt * 1.4f);
            }
            if (_zoomPunch > 0f)
            {
                target.z += _zoomPunch;
                _zoomPunch = Mathf.MoveTowards(_zoomPunch, 0f, dt * 3f);
            }
            _pos = Vector3.Lerp(_pos, target, 1f - Mathf.Exp(-Smooth * dt));

            Vector3 shakeOffset = Vector3.zero;
            if (_shake > 0.0001f)
            {
                _shakeTime += dt * 38f;
                shakeOffset = new Vector3(Mathf.PerlinNoise(_shakeTime, 0.1f) - 0.5f, Mathf.PerlinNoise(0.7f, _shakeTime) - 0.5f, 0f) * _shake;
                _shake = Mathf.MoveTowards(_shake, 0f, dt * 1.6f);
            }
            transform.position = _pos + shakeOffset;
            transform.rotation = Quaternion.Euler(Pitch, 0, 0);
        }
    }
}
