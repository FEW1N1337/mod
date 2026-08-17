using Photon.Pun;
using UnityEngine;

namespace DreamCar.Effects
{
    // Sol/sağ sinyal + dörtlü flaşör. RPC ile diğer client'lara sync.
    // Light + emissive mesh 0.5 sn interval blink.
    [RequireComponent(typeof(PhotonView))]
    public class TurnSignals : MonoBehaviourPun
    {
        public enum State { Off, Left, Right, Hazard }

        public Light[] leftLights;
        public Light[] rightLights;
        public Renderer[] leftEmissive;
        public Renderer[] rightEmissive;
        public string emissivePropertyName = "_EmissionColor";
        public Color emissiveOn = new Color(3f, 1.6f, 0f);
        public float blinkIntervalSeconds = 0.5f;

        State _state;
        float _timer;
        bool _visible;
        MaterialPropertyBlock _mpb;
        int _emissiveId;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _emissiveId = Shader.PropertyToID(emissivePropertyName);
        }

        public void SetState(State s)
        {
            if (photonView.IsMine) photonView.RPC(nameof(RPC_SetState), RpcTarget.AllBuffered, (int)s);
        }

        [PunRPC]
        void RPC_SetState(int stateInt)
        {
            _state = (State)stateInt;
            _timer = 0f;
            _visible = false;
            ApplyLights(false, false);
        }

        void Update()
        {
            if (_state == State.Off) { if (_visible) { ApplyLights(false, false); _visible = false; } return; }

            _timer += Time.deltaTime;
            if (_timer >= blinkIntervalSeconds)
            {
                _timer = 0f;
                _visible = !_visible;
                bool left = _visible && (_state == State.Left || _state == State.Hazard);
                bool right = _visible && (_state == State.Right || _state == State.Hazard);
                ApplyLights(left, right);
            }
        }

        void ApplyLights(bool left, bool right)
        {
            foreach (var l in leftLights) if (l) l.enabled = left;
            foreach (var l in rightLights) if (l) l.enabled = right;
            SetEmissive(leftEmissive, left);
            SetEmissive(rightEmissive, right);
        }

        void SetEmissive(Renderer[] arr, bool on)
        {
            foreach (var r in arr)
            {
                if (!r) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(_emissiveId, on ? emissiveOn : Color.black);
                r.SetPropertyBlock(_mpb);
            }
        }

        public void Left() => SetState(_state == State.Left ? State.Off : State.Left);
        public void Right() => SetState(_state == State.Right ? State.Off : State.Right);
        public void Hazard() => SetState(_state == State.Hazard ? State.Off : State.Hazard);
    }
}
