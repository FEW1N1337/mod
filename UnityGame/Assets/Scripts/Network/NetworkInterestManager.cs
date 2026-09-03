using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace DreamCar.Network
{
    // Önceden 500 m uzaktaki araç da her frame sync ediliyordu → gereksiz bant
    // genişliği ve Photon CCU maliyeti. Bu manager mesafeye göre sync sıklığını
    // kademelendirir ve çok uzaktaki araçların görselini kapatır.
    public class NetworkInterestManager : MonoBehaviour
    {
        public static NetworkInterestManager Instance { get; private set; }

        [Header("Mesafe kademeleri (metre)")]
        public float nearDistance = 80f;    // tam hız sync
        public float midDistance = 200f;    // yarı hız
        public float cullDistance = 400f;   // görsel kapalı, minimum sync

        [Header("Send rate (paket/sn)")]
        public int nearSendRate = 20;
        public int midSendRate = 10;
        public int farSendRate = 4;

        public float evaluateIntervalSeconds = 1f;
        public bool cullRenderers = true;

        Transform _localCar;
        float _nextEvaluate;
        readonly Dictionary<PhotonView, Renderer[]> _rendererCache = new();

        void Awake()
        {
            // Yalnızca yinelenen bileşeni yok et: ~Bootstrap üzerinde sahneye özel
            // bileşenler de duruyor (RoomManager, Weather, MapSelector…) ve
            // GameObject yok edilirse onlar da giderdi.
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        public void SetLocalCar(Transform t) => _localCar = t;

        void Update()
        {
            if (!PhotonNetwork.InRoom) return;
            if (Time.time < _nextEvaluate) return;
            _nextEvaluate = Time.time + evaluateIntervalSeconds;

            if (_localCar == null) { FindLocalCar(); if (_localCar == null) return; }

            int closest = int.MaxValue;
            foreach (var view in PhotonNetwork.PhotonViewCollection)
            {
                if (view == null || view.IsMine) continue;
                if (view.GetComponent<Car.CarNetworkSync>() == null) continue;

                float distance = Vector3.Distance(_localCar.position, view.transform.position);
                ApplyVisibility(view, distance);

                int tier = distance <= nearDistance ? 0 : distance <= midDistance ? 1 : 2;
                if (tier < closest) closest = tier;
            }

            // Kendi gönderim hızımızı en yakın komşuya göre ayarla: kimse yakında
            // değilse daha seyrek paket yolla.
            ApplySendRate(closest);
        }

        void ApplyVisibility(PhotonView view, float distance)
        {
            if (!cullRenderers) return;

            if (!_rendererCache.TryGetValue(view, out var renderers) || renderers == null)
            {
                renderers = view.GetComponentsInChildren<Renderer>(true);
                _rendererCache[view] = renderers;
            }

            bool visible = distance <= cullDistance;
            foreach (var r in renderers)
                if (r && r.enabled != visible) r.enabled = visible;
        }

        void ApplySendRate(int closestTier)
        {
            int rate = closestTier switch
            {
                0 => nearSendRate,
                1 => midSendRate,
                _ => farSendRate,
            };
            if (PhotonNetwork.SerializationRate != rate)
            {
                PhotonNetwork.SerializationRate = rate;
                PhotonNetwork.SendRate = Mathf.Max(rate, PhotonNetwork.SerializationRate);
            }
        }

        void FindLocalCar()
        {
            foreach (var view in PhotonNetwork.PhotonViewCollection)
            {
                if (view == null || !view.IsMine) continue;
                if (view.GetComponent<Car.CarNetworkSync>() == null) continue;
                _localCar = view.transform;
                return;
            }
        }

        void OnDisable()
        {
            _rendererCache.Clear();
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.SerializationRate = 10;
                PhotonNetwork.SendRate = 20;
            }
        }
    }
}
