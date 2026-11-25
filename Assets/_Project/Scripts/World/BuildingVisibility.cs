using System;
using System.Collections.Generic;
using FantaLandia.Player;
using UnityEngine;

namespace FantaLandia.World
{
    /// <summary>
    /// Quando il personaggio seguito è dentro l'edificio, nasconde il tetto e i piani sopra di lui.
    /// Sul piano corrente i muri possono essere pieni oppure tagliati sul lato verso la camera.
    /// I pezzi nascosti passano sul layer "Ignore Raycast", così i clic li attraversano.
    /// </summary>
    public class BuildingVisibility : MonoBehaviour
    {
        public enum WallMode { Full, FrontCutaway }

        [Serializable]
        public class Floor
        {
            public string displayName = "Piano";
            [Tooltip("Altezza locale del pavimento calpestabile")]
            public float baseHeight;
            [Tooltip("Solaio, scale, arredi: nascosti quando il personaggio è a un piano inferiore")]
            public Transform structure;
            [Tooltip("Muri: sul piano corrente seguono la modalità muri")]
            public Transform walls;
        }

        /// <summary>Modalità muri condivisa da tutti gli edifici (tasto V).</summary>
        public static WallMode Mode = WallMode.FrontCutaway;

        /// <summary>L'edificio in cui si trova il personaggio seguito, o null se è all'aperto.</summary>
        public static BuildingVisibility Current { get; private set; }

        public Vector3 interiorCenter = new Vector3(0f, 5f, 0f);
        public Vector3 interiorSize = new Vector3(12f, 11f, 10f);
        public float floorTolerance = 0.6f;
        [Tooltip("Quanto un muro deve essere rivolto verso la camera per essere tagliato")]
        public float frontWallThreshold = 0.2f;
        public List<Floor> floors = new List<Floor>();

        public int CurrentFloor { get; private set; } = -1;
        public string CurrentFloorName =>
            CurrentFloor >= 0 && CurrentFloor < floors.Count ? floors[CurrentFloor].displayName : "";

        class Piece
        {
            public Renderer renderer;
            public int floor;
            public bool isWall;
            public Vector3 localNormal;
            public int originalLayer;
            public bool hidden;
        }

        const int IgnoreRaycastLayer = 2;
        readonly List<Piece> pieces = new List<Piece>();

        void Awake()
        {
            for (int i = 0; i < floors.Count; i++)
            {
                Collect(floors[i].structure, i, false);
                Collect(floors[i].walls, i, true);
            }
        }

        void Collect(Transform root, int floor, bool isWall)
        {
            if (root == null) return;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                var p = new Piece { renderer = r, floor = floor, isWall = isWall, originalLayer = r.gameObject.layer };
                if (isWall) p.localNormal = ComputeLocalNormal(r.bounds.center);
                pieces.Add(p);
            }
        }

        /// <summary>Normale verso l'esterno di un muro, dedotta dalla sua posizione rispetto al centro dell'edificio.</summary>
        Vector3 ComputeLocalNormal(Vector3 worldCenter)
        {
            Vector3 p = transform.InverseTransformPoint(worldCenter) - interiorCenter;
            float nx = Mathf.Abs(p.x) / Mathf.Max(0.01f, interiorSize.x * 0.5f);
            float nz = Mathf.Abs(p.z) / Mathf.Max(0.01f, interiorSize.z * 0.5f);
            return nx >= nz ? new Vector3(Mathf.Sign(p.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(p.z));
        }

        void LateUpdate()
        {
            var sel = SelectionController.Instance;
            Transform focus = sel != null && sel.Focus != null ? sel.Focus.transform : null;

            int floor = -1;
            if (focus != null && Contains(focus.position))
                floor = FloorAt(transform.InverseTransformPoint(focus.position).y);
            CurrentFloor = floor;
            if (floor >= 0) Current = this;
            else if (Current == this) Current = null;

            Vector3 toCamera = Vector3.zero;
            Camera cam = Camera.main;
            if (cam != null)
            {
                toCamera = -cam.transform.forward;
                toCamera.y = 0f;
                toCamera.Normalize();
            }

            foreach (var p in pieces)
            {
                bool hide = false;
                if (floor >= 0)
                {
                    if (p.floor > floor) hide = true;
                    else if (p.isWall && p.floor == floor)
                    {
                        if (Mode == WallMode.FrontCutaway)
                            hide = Vector3.Dot(transform.TransformDirection(p.localNormal), toCamera) > frontWallThreshold;
                    }
                }
                SetHidden(p, hide);
            }
        }

        void OnDisable()
        {
            foreach (var p in pieces) SetHidden(p, false);
            if (Current == this) Current = null;
        }

        void SetHidden(Piece p, bool hide)
        {
            if (p.hidden == hide || p.renderer == null) return;
            p.hidden = hide;
            p.renderer.enabled = !hide;
            p.renderer.gameObject.layer = hide ? IgnoreRaycastLayer : p.originalLayer;
        }

        bool Contains(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world) - interiorCenter;
            Vector3 h = interiorSize * 0.5f;
            return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
        }

        int FloorAt(float localY)
        {
            int idx = 0;
            for (int i = 0; i < floors.Count; i++)
                if (localY >= floors[i].baseHeight - floorTolerance) idx = i;
            return idx;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(interiorCenter, interiorSize);
        }
    }
}
