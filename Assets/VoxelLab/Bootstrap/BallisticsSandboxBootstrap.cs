// =====================================================================
//  BallisticsSandboxBootstrap.cs
//  VoxelLab :: Scene
//
//  Bootstrap dedicado para el laboratorio balistico:
//      - Mundo cubico de voxels (sin planeta).
//      - Camaras Orbital/Fly para apuntado y disparo.
//      - ProjectileLauncher con gravedad configurable (0 por defecto).
//
//  Uso:
//      1) Crear escena vacia.
//      2) Agregar este componente a un GameObject vacio.
//      3) Asignar VoxelMeshing.compute.
//      4) Play.
// =====================================================================
using UnityEngine;
using VoxelLab.Cameras;
using VoxelLab.Core;
using VoxelLab.LabViews;
using VoxelLab.Planet;
using VoxelLab.Physics;

namespace VoxelLab.Scene
{
    public class BallisticsSandboxBootstrap : MonoBehaviour
    {
        public ComputeShader meshingCompute;

        [Header("Cubo voxel")]
        public int cubeSize = 32;
        public Vector3Int cubeCenter = new Vector3Int(0, 0, 48);
        public MaterialId cubeMaterial = MaterialId.Rock;
        public float cubeDensity = 1f;
        public float cubeHardness = 0.7f;

        [Header("Spawn camara")]
        public Vector3 orbitalPosition = new Vector3(0f, 12f, -18f);
        public Vector3 flyPosition = new Vector3(0f, 8f, -20f);

        [Header("Sandbox configurable (opcional)")]
        public VoxelMaterialRegistry materialRegistry;
        public ProjectileTypeDef[] projectileTypes;
        public DebrisProfileDef[] debrisProfiles;
        public VoxelLab.Planet.TargetDef[] targets;
        [Min(64)] public int debrisCapacity = 4096;

        private DebrisSimulator _debris;

        private void Awake()
        {
            // 1) Aplicar registry de materiales antes de crear el mundo.
            if (materialRegistry != null) materialRegistry.Apply();

            var defShader = Shader.Find("VoxelLab/Default");
            var wireShader = Shader.Find("VoxelLab/Wireframe");
            var densShader = Shader.Find("VoxelLab/Density");
            var matShader = Shader.Find("VoxelLab/Material");

            Material defMat = defShader != null ? new Material(defShader) { name = "VL_Default" } : null;
            Material wireMat = wireShader != null ? new Material(wireShader) { name = "VL_Wire" } : null;
            Material densMat = densShader != null ? new Material(densShader) { name = "VL_Density" } : null;
            Material matMat = matShader != null ? new Material(matShader) { name = "VL_Material" } : null;

            // Camara orbital
            var camOrbitalGO = new GameObject("Camera_Orbital");
            camOrbitalGO.tag = "MainCamera";
            camOrbitalGO.transform.position = orbitalPosition;
            var camOrbital = camOrbitalGO.AddComponent<Camera>();
            camOrbitalGO.AddComponent<AudioListener>();
            var orbital = camOrbitalGO.AddComponent<OrbitalCamera>();
            var pivotGO = new GameObject("OrbitalPivot");
            pivotGO.transform.position = new Vector3(cubeCenter.x, cubeCenter.y, cubeCenter.z);
            orbital.target = pivotGO.transform;
            camOrbital.farClipPlane = 4000f;

            // Camara fly
            var camFlyGO = new GameObject("Camera_Fly");
            camFlyGO.transform.position = flyPosition;
            var camFly = camFlyGO.AddComponent<Camera>();
            var fly = camFlyGO.AddComponent<FlyCamera>();
            camFly.farClipPlane = 4000f;
            camFly.enabled = false;

            var switchGO = new GameObject("CameraSwitcher");
            var switcher = switchGO.AddComponent<CameraSwitcher>();
            switcher.slots = new[]
            {
                new CameraSwitcher.Slot { label = "Orbital", camera = camOrbital, controller = orbital, hotkey = UnityEngine.InputSystem.Key.F1 },
                new CameraSwitcher.Slot { label = "Fly", camera = camFly, controller = fly, hotkey = UnityEngine.InputSystem.Key.F2 },
            };

            var labGO = new GameObject("VoxeLab");
            var lab = labGO.AddComponent<VoxelLab.Boot.VoxeLab>();
            lab.defaultMaterial = defMat;
            lab.wireframeMaterial = wireMat;
            lab.densityMaterial = densMat;
            lab.materialOverlayMaterial = matMat;
            lab.meshingCompute = meshingCompute;
            lab.cameraSwitcher = switcher;
            lab.worldMode = VoxelLab.Boot.WorldMode.CubeSandbox;
            lab.cube = new CubeSettings
            {
                center = cubeCenter,
                size = Mathf.Max(1, cubeSize),
                material = (byte)cubeMaterial,
                densidad = Mathf.Clamp01(cubeDensity),
                dureza = Mathf.Clamp01(cubeHardness),
            };

            var launcherGO = new GameObject("ProjectileLauncher");
            launcherGO.transform.SetParent(labGO.transform, false);
            var launcher = launcherGO.AddComponent<ProjectileLauncher>();
            launcher.gravity = Vector3.zero;
            launcher.drag = 0f;
            launcher.initialSpeed = 80f;
            launcher.mass = 1f;
            launcher.radius = 0.25f;
            if (projectileTypes != null && projectileTypes.Length > 0)
                launcher.availableTypes = projectileTypes;

            // Debris simulator + renderer.
            _debris = new DebrisSimulator(Mathf.Max(64, debrisCapacity));
            if (debrisProfiles != null)
            {
                for (int i = 0; i < debrisProfiles.Length; i++)
                {
                    int idx = _debris.RegisterProfile(debrisProfiles[i]);
                    // Mapeo material->perfil si la lista de materiales lo declara.
                    if (idx >= 0 && materialRegistry != null && materialRegistry.materials != null)
                    {
                        for (int m = 0; m < materialRegistry.materials.Length; m++)
                        {
                            var md = materialRegistry.materials[m];
                            if (md != null && md.debrisProfileKey == debrisProfiles[i].profileKey)
                                _debris.MapMaterialToProfile(md.id, debrisProfiles[i].profileKey);
                        }
                    }
                }
            }
            var rendererGO = new GameObject("DebrisRenderer");
            rendererGO.transform.SetParent(labGO.transform, false);
            var debrisRenderer = rendererGO.AddComponent<DebrisRenderer>();
            debrisRenderer.Simulator = _debris;

            launcher.OnDestructionSamples += (result, normal) =>
            {
                _debris.SpawnFromExplosion(result, normal, profileKeyFallback: "Default");
            };

            StartCoroutine(LinkAfterWorldReady(lab, launcher));
        }

        private void FixedUpdate()
        {
            if (_debris == null) return;
            // Acceso al mundo a través del lab.
            var lab = GetComponentInChildren<VoxelLab.Boot.VoxeLab>(true);
            if (lab != null && lab.World != null)
                _debris.Step(lab.World, UnityEngine.Physics.gravity, Time.fixedDeltaTime);
        }

        private void OnDestroy()
        {
            if (_debris != null) { _debris.Dispose(); _debris = null; }
        }

        private System.Collections.IEnumerator LinkAfterWorldReady(VoxelLab.Boot.VoxeLab lab, ProjectileLauncher launcher)
        {
            yield return null;
            if (lab == null || launcher == null) yield break;

            launcher.World = lab.World;
            if (lab.ui != null)
            {
                lab.ui.launcher = launcher;
                lab.ui.availableTargets = targets;
                lab.ui.targetSpawnOrigin = new Vector3(cubeCenter.x, cubeCenter.y, cubeCenter.z);
                lab.ui.debrisSimulator = _debris;
            }
        }
    }
}
