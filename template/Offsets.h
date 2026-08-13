// FEW1N Mod Menu — Named il2cpp / Unity 6 field offsets
//
// Sadece TIP'e ozel ve okunabilirligi kazanan offset'ler burada. Generic
// il2cpp offset'leri (Array.Length @ 0x18, Array.Data @ 0x20, string
// header @ 0x10) kod icinde 100+ farkli semantic'te tekrar ediyor —
// onlari isimlendirmek yaniltici olur, dokunmadik.
//
// Yeni Unity 6 dump (metadata v39) geldiginde bu dosyayi guncellemek
// yeterli — Tweak.xm'i taramaya gerek yok.

#pragma once

// ===== Unity temel =====
#define OFF_UNITY_CACHED_PTR            0x10   // UnityEngine.Object.m_CachedPtr (unityAlive kontrolu)

// ===== il2cpp System.String =====
#define OFF_IL2CPP_STRING_LEN           0x10   // int32_t length
#define OFF_IL2CPP_STRING_CHARS         0x14   // unichar[] baslangici

// ===== HR_PlayerHandler =====
#define OFF_PLAYERHANDLER_PHOTONVIEW    0xB8   // PhotonView*
#define OFF_PLAYERHANDLER_CANCRASH      0x38   // bool
#define OFF_PLAYERHANDLER_DAMAGE        0x3C   // float

// ===== HR_PhotonLobbyManager =====
#define OFF_LOBBY_PWD_INPUT             0x50   // TMP_InputField* passwordInput
#define OFF_LOBBY_PWD_ON_CONN_INPUT     0x60   // TMP_InputField* passwordOnConnectInput

// ===== HR_UI_RoomListLine (yeni Unity6 offset'ler, il2cpp.h ile dogrulandi) =====
// MonoBehaviour_Fields base = 0x18, sonra:
#define OFF_ROOMLINE_NAME_TEXT          0x18   // TMP_Text* RoomNameText
#define OFF_ROOMLINE_MAP_TEXT           0x20   // TMP_Text* MapNameText
#define OFF_ROOMLINE_PLAYERCOUNT_TEXT   0x28   // TMP_Text* PlayerCountText
#define OFF_ROOMLINE_PASSWORD           0x50   // string* password (Connect'te okunuyor)
