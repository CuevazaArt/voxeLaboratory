// =====================================================================
//  VoxeLabBootstrap.cs
//  VoxelLab :: Scene
//
//  Componente "todo en uno" que crea por código una escena funcional
//  del laboratorio sin necesidad de un .unity prefab serializado:
//      - GameObject "VoxeLab" con el componente principal.
//      - 3 cámaras (orbital / fly / fps) y un CameraSwitcher.
//      - Materiales por defecto cargados desde Shader.Find.
//      - Compute shader cargado por nombre desde Resources si existe.
//
//  Adjúntalo a un GameObject vacío en cualquier escena vacía y al
//  pulsar Play se construirá el laboratorio.
//
//  Dependencias: VoxeLab y submódulos.
// =====================================================================
using UnityEngine;
using VoxelLab.Cameras;
using VoxelLab.Physics;

namespace VoxelLab.Scene
{
    public class VoxeLabBootstrap : MonoBehaviour
    {
        public ComputeShader meshingCompute;     // arrastra Assets/VoxelLab/Shaders/VoxelMeshing.compute
        public Vector3 fpsSpawnOffset = new Vector3(0, 40, 0);

        private void Awake()
        {
            // Buscar shaders por nombre (no requieren Resources)
            var defShader = Shader.Find("VoxelLab/Default");
            var wireShader = Shader.Find("VoxelLab/Wireframe");
            var densShader = Shader.Find("VoxelLab/Density");
            var matShader  = Shader.Find("VoxelLab/Material");

            Material defMat = defShader != null ? new Material(defShader) { name = "VL_Default" } : null;
            Material wireMat = wireShader != null ? new Material(wireShader) { name = "VL_Wire" } : null;
            Material densMat = densShader != null ? new Material(densShader) { name = "VL_Density" } : null;
            Material matMat  = matShader != null ? new Material(matShader)  { name = "VL_Material" } : null;

            // Cámaras
            var camOrbitalGO = new GameObject("Camera_Orbital");
            var camOrbital = camOrbitalGO.AddComponent<Camera>();
            camOrbitalGO.AddComponent<AudioListener>();
            var orbital = camOrbitalGO.AddComponent<OrbitalCamera>();
            camOrbital.farClipPlane = 4000f;

            var camFlyGO = new GameObject("Camera_Fly");
            var camFly = camFlyGO.AddComponent<Camera>();
            var fly = camFlyGO.AddComponent<FlyCamera>();
            camFly.farClipPlane = 4000f;
            camFly.enabled = false;

            var camFpsGO = new GameObject("Camera_FPS");
            camFpsGO.transform.position = fpsSpawnOffset;
            var camFps = camFpsGO.AddComponent<Camera>();
            camFps.farClipPlane = 4000f;
            camFps.enabled = false;
            var rb = camFpsGO.AddComponent<VoxelRigidbody>();
            var fps = camFpsGO.AddComponent<FirstPersonCamera>();

            // Switcher
            var switchGO = new GameObject("CameraSwitcher");
            var switcher = switchGO.AddComponent<CameraSwitcher>();
            switcher.slots = new[]
            {
                new CameraSwitcher.Slot { label = "Orbital",  camera = camOrbital, controller = orbital, hotkey = KeyCode.F1 },
                new CameraSwitcher.Slot { label = "Fly",      camera = camFly,     controller = fly,     hotkey = KeyCode.F2 },
                new CameraSwitcher.Slot { label = "FPS",      camera = camFps,     controller = fps,     hotkey = KeyCode.F3 },
            };

            // Voxel Lab principal
            var labGO = new GameObject("VoxeLab");
            var lab = labGO.AddComponent<VoxelLab.VoxeLab.VoxeLab>();
            lab.defaultMaterial = defMat;
            lab.wireframeMaterial = wireMat;
            lab.densityMaterial = densMat;
            lab.materialOverlayMaterial = matMat;
            lab.meshingCompute = meshingCompute;
            lab.cameraSwitcher = switcher;

            // Centro de planeta para rb (apuntar a VoxeLab transform en 0,0,0).
            rb.planetCenter = labGO.transform;

            // Tool manager: agregar fps body
            // (El lab autocrea ToolManager en Start; lo enlazamos en el siguiente frame.)
            StartCoroutine(LinkBodyNextFrame(rb));
        }

        private System.Collections.IEnumerator LinkBodyNextFrame(VoxelRigidbody rb)
        {
            yield return null;
            var lab = FindObjectOfType<VoxelLab.VoxeLab.VoxeLab>();
            if (lab != null)
            {
                if (rb != null) rb.World = lab.World;
                if (lab.toolManager != null && rb != null) lab.toolManager.bodies.Add(rb);
            }
        }
    }
}
