BIRP and URP MToon10 forks with pass stencil for VRMC_materials_mtoonxt.

ShaderLab:
  Builtin/vrmc_materials_mtoonxt.shader → VRMXT/MToonXT10
  Urp/vrmc_materials_mtoonxt_urp.shader → VRMXT/Universal Render Pipeline/MToonXT10

Pin: UniVRM 0.131.2 Packages/VRM10/MToon10/Shaders (MIT). See LICENSE.txt.

URP passes use PackageRequirements (com.unity.render-pipelines.universal 12.0.0)
so Built-in hosts skip compiling URP includes. XRMotionVectors pass omitted.

Warudo UMod Shader.Find is null. Warudo ships the same forks as shader UMods
(`mira.shaders.mtoonxt.birp` / `.urp`) and warms via ModHost.Assets.Load. Those UMods
do not include `MtoonxtInspector`.

Material inspector: CustomEditor UniVRMXT.Editor.Mtoonxt.MtoonxtInspector (wraps UniVRM
MToonInspector, then stencil op / writer dropdowns into avatar pair JSON).
