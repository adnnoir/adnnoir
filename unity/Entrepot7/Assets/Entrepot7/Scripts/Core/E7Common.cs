using System.Collections.Generic;
using UnityEngine;

namespace E7
{
    /// <summary>Couches, constantes et petites fonctions utilisées partout.</summary>
    public static class L
    {
        // Couches (numéros libres dans un projet Unity neuf, pas besoin de les nommer)
        public const int Default = 0;
        public const int Viewmodel = 8;   // arme et mains à la première personne
        public const int Actors = 9;      // corps des personnages (zones de tir)
        public const int Player = 10;     // capsule du joueur
        public const int Props = 11;      // douilles, objets physiques
        public const int Preview = 12;    // modèles de l'arsenal / du personnage

        public static int WorldMask => (1 << Default);
        public static int ShootMask => (1 << Default) | (1 << Actors);
        public static int SightMask => (1 << Default);
    }

    public static class U
    {
        public static float Rand(float a, float b) => Random.Range(a, b);
        public static T Pick<T>(IList<T> list) => list[Random.Range(0, list.Count)];
        public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        public static float Damp(float current, float target, float speed, float dt) => Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));
        public static Vector3 Damp(Vector3 current, Vector3 target, float speed, float dt) => Vector3.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));
        public static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
        public static float FlatDist(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

        public static Color Hex(string hex)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return Color.white;
        }

        public static GameObject Child(Transform parent, string name, Vector3 localPos = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }

        public static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        /// <summary>Temps « de jeu » (s'arrête en pause).</summary>
        public static float Dt => Time.deltaTime;
    }
}
