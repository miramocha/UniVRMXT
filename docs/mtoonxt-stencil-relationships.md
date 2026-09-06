# MToonXT stencil relationships

UniVRMXT owns the portable root-level relationship graph used to reproduce the
confirmed Blender and Unity stencil modes without depending on BVT at import or export
time.

## JSON shape

Relationships live at:

```text
extensions.VRMXT_materials_mtoonxt.stencilRelationships[]
```

Each entry contains non-empty, disjoint glTF material-index arrays named `writers` and
`readers`. Optional fields and defaults are:

| Field | Default |
|---|---|
| `comparison` | `outside` |
| `showWritersThroughOccluders` | `false` |
| `writersOnlyInsideReaders` | `false` |
| `writersOnlyOutsideReaders` | `false` |
| `writersSelfOcclude` | `true` |
| `ignoreOccludedReaderAreas` | `true` |
| `writersWriteColor` | `true` |
| `writersWriteDepth` | `true` |
| `readersWriteDepth` | `true` |
| `writerDepthTest` | `lessEqual` |
| `readerDepthTest` | `lessEqual` |

`writersOnlyInsideReaders` and `writersOnlyOutsideReaders` are mutually exclusive.
`writersWriteColor` independently controls writer body and outline color. Set it to
`false` for invisible stencil controls such as a moving avatar reveal plane; stencil
writes and `writersWriteDepth` remain independently active.
Depth-test values use the existing MToonXT comparison vocabulary: `never`, `less`,
`equal`, `lessEqual`, `greater`, `notEqual`, `greaterEqual`, and `always`.

Invalid entries are skipped individually. Valid sibling relationships continue to
import.

## Unity authoring and export

`VrmxtMaterialsMtoonxtInstance.StencilRelationships` stores Unity `Material` references
and presentation controls. On export, `VrmxtMaterialsMtoonxtAuthoring` resolves those
materials through Extended-UniVRM's final export material-index map and
`VrmxtMaterialsMtoonxtExportHookBootstrap` writes the root extension using
`Vrm10ExportExtensionContext.AddRootExtension`.

This path is self-contained in UniVRMXT. BVT may synchronize its own UI model into the
public authoring component, but UniVRMXT does not import or reference BVT code.

## Runtime compilation

`VrmxtMaterialsMtoonxtRelationshipCompiler` turns each portable relationship into a
small pass plan. Primary writer and reader passes reuse imported renderer/material
slots. Compound modes add retained auxiliary draws:

- a second writer subject pass for through-occluder presentation;
- reader-silhouette coverage when occluded reader areas must remain eligible;
- full-scene-without-writers coverage for background-only outside presentation.

The Built-in pipeline submits colored secondary writer passes through extra material
slots on the **original renderer**, so Unity supplies its normal lights, probes, and
received shadows. `CommandBuffer.DrawRenderer` does not initialize this lighting state
and must not be used for these lit overlays. Only colorless coverage (including the
reader coverage in M08) uses cached `BeforeForwardOpaque` command buffers.

For a show-through relationship with the default self-occlusion/depth flags (M02),
the imported configuration is:

| Surface | Queue | Stencil comparison / operation | Depth test / write | Surface culling |
|---|---|---|---|---|
| Reader | 2451 | Always / Replace | LEqual / On | Source material |
| Writer base | 2452 | NotEqual / Keep | LEqual / On | Source material |
| Writer overlay | 2453 | Equal / Keep | Always / On | Back |

Queues above are the first relationship's queues; later relationships receive their
compiler-assigned offsets. Each pass shares its relationship's allocated stencil ref.
Source double-sidedness remains unchanged. The overlay clones the source appearance
but disables its `ShadowCaster` pass; the base surface remains the caster. The original
renderer still controls shadow casting/receiving, skinning, bounds, and transforms.
No helper renderer, proxy mesh object, or bounds override is created.

With `writersSelfOcclude=false`, the overlay instead keeps Cull Off. M07 also sets
`writersWriteDepth=false`, giving ZWrite Off; these controls remain independent.
Other stencil/depth/color settings still follow the compiled relationship plan.
Back-face culling is a closed-surface approximation, **not** a
general nearest-writer-depth solution for concave/open double-sided geometry. Other supported lit rows use the same native-lighting path without inheriting this culling override.

Appending a material alone repeats only the last submesh. For a writer on an earlier
submesh, the helper clones the mesh and appends the targeted index ranges, preserving
vertices, bone weights, bindposes, blend shapes, and the renderer's local bounds. It
does not change the source mesh. Imported meshes must remain CPU-readable for this
multi-submesh path. Reapply and disable restore source slots/meshes before rebuilding;
ownership checks protect live resources when an avatar/export copy is instantiated.
There is no per-frame mesh, material, or renderer discovery/rebuilding.

Asset imports persist the original slots plus the portable graph, not transient
generated layers. `VrmxtMaterialsMtoonxtInstance.OnEnable` reconstructs native layers
when the avatar is instantiated or reloaded. The export hook strips derived layers
from UniVRM's temporary export copy before mesh/material collection, preventing
duplicate overlay geometry/materials in the exported VRM.

The packaged URP shader supports the primary stencil/depth properties. Compound modes
that require an extra render phase need an equivalent URP renderer feature; the
Built-in command-buffer helper deliberately stays inactive under SRP instead of
injecting at an unsafe phase.

## Scenario matrix

[Compare the 13 Blender / Unity examples](https://tdw46.github.io/BVT-Stencil-Matrix/).
The [scenario specification](https://github.com/tdw46/Extended-VRM-Specs/blob/codex/mtoonxt-stencil-parity/examples/stencil-parity-matrix.md)
provides each row's exact flags, control variant and Unity pass mapping.

- M01: colorless mask; M02: reader-qualified show-through; M03/M04: inside-only clipping.
- M05/M06: reader exclusion versus all-scene background-only coverage.
- M07: translucent show-through with self-occlusion and writer depth disabled.
- M08: complete hidden-reader coverage, without revealing reader color.
- M09/M10: independent writer/reader depth writes, demonstrated with late HUD surfaces.
- A01: Inside/Outside comparison; A02/A03: explicit writer/reader Always depth tests.

Preserve source alpha and reader color for M07; use complementary writer coverage.
M09/M10 disable depth writes, not depth testing. A02/A03 use camera-locked sorted-face
projections: their exported double-sided setting and active Cull Off stencil variants
differ, so these examples do not establish arbitrary-camera culling parity.
M05/M06 use one textured writer; general multi-writer interaction is a separate constraint.
Compound URP modes require equivalent coverage phases.

## Legacy compatibility

The root graph is additive. Existing per-material MToonXT stencil operations remain
supported:

- `write`
- `inside`
- `insideOverlay`
- `outside`
- outline `same`

Files without `stencilRelationships` continue through the legacy compiler unchanged.
