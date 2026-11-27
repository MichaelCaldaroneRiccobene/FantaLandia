using UnityEngine;

namespace FantaLandia.Ragdoll
{
    /// <summary>
    /// Scena di prova: imposta la fisica, crea terreno e ostacoli, genera il manichino,
    /// collega camera e strumenti di interazione. Basta un GameObject vuoto con questo componente.
    /// </summary>
    public class RagdollSandbox : MonoBehaviour
    {
        [Header("Fisica globale")]
        [Tooltip("Frequenza della simulazione (Hz). Più alta = ragdoll più stabile.")]
        [Range(50f, 240f)] public float physicsRate = 120f;
        public int defaultSolverIterations = 12;
        public int defaultSolverVelocityIterations = 6;

        [Header("Scena")]
        public bool createGround = true;
        public bool createProps = true;
        public Vector3 spawnPosition = new Vector3(0f, 0.02f, 0f);
        public float spawnYaw = 0f;

        public BalanceController Current { get; private set; }

        FollowCamera followCamera;
        RagdollInteraction interaction;

        void Awake()
        {
            Time.fixedDeltaTime = 1f / physicsRate;
            Physics.defaultSolverIterations = defaultSolverIterations;
            Physics.defaultSolverVelocityIterations = defaultSolverVelocityIterations;

            if (createGround) CreateGround();
            if (createProps) CreateProps();

            Spawn();

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
            }
            followCamera = cam.GetComponent<FollowCamera>();
            if (followCamera == null) followCamera = cam.gameObject.AddComponent<FollowCamera>();
            followCamera.SetTarget(Current.Ragdoll.Pelvis.transform);

            interaction = GetComponent<RagdollInteraction>();
            if (interaction == null) interaction = gameObject.AddComponent<RagdollInteraction>();
            interaction.cam = cam;
            interaction.target = Current;
            interaction.ResetRequested = Respawn;
        }

        void Spawn()
        {
            var go = new GameObject("Manichino");
            go.transform.SetPositionAndRotation(spawnPosition, Quaternion.Euler(0f, spawnYaw, 0f));
            go.AddComponent<ActiveRagdoll>();               // costruisce il corpo nel suo Awake
            Current = go.AddComponent<BalanceController>();
        }

        public void Respawn()
        {
            if (Current != null)
            {
                Current.gameObject.SetActive(false);
                Destroy(Current.gameObject);
            }
            Spawn();
            followCamera.SetTarget(Current.Ragdoll.Pelvis.transform);
            interaction.target = Current;
        }

        static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Terreno";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            ground.GetComponent<Renderer>().material.color = new Color(0.36f, 0.43f, 0.36f);
            ground.GetComponent<Collider>().sharedMaterial = new PhysicsMaterial("Terreno")
            {
                dynamicFriction = 0.8f,
                staticFriction = 0.9f,
                bounciness = 0f
            };
        }

        static void CreateProps()
        {
            var crateColor = new Color(0.55f, 0.38f, 0.2f);
            for (int i = 0; i < 6; i++)
            {
                var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.name = "Cassa";
                crate.transform.position = new Vector3(2.5f + (i % 3) * 0.45f, 0.2f + (i / 3) * 0.41f, 1.5f);
                crate.transform.localScale = Vector3.one * 0.4f;
                crate.GetComponent<Renderer>().material.color = crateColor;
                crate.AddComponent<Rigidbody>().mass = 6f;
            }

            var staticColor = new Color(0.6f, 0.6f, 0.65f);
            CreateStatic("Rampa", new Vector3(-3f, 0.3f, 2f), new Vector3(2f, 0.1f, 3f), Quaternion.Euler(-12f, 0f, 0f), staticColor);
            CreateStatic("Gradino", new Vector3(0f, 0.06f, -2.5f), new Vector3(1.5f, 0.12f, 1.5f), Quaternion.identity, staticColor);
        }

        static void CreateStatic(string name, Vector3 position, Vector3 scale, Quaternion rotation, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().material.color = color;
        }
    }
}
