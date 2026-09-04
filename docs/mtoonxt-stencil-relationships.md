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
Back-face culling is the user-confirmed closed-box self-occlusion setup, **not** a
general nearest-writer-depth solution for concave/open double-sided geometry. M04,
M08, and other compound rows inherit native lighting, but still require their own
visual confirmation; this change does not mark the entire parity matrix validated.

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

## Validation of the native-layer importer change

**Checkpoint, not full-matrix sign-off.** Full Blender-to-Unity visual validation is
underway. Compiler coverage is not a substitute for row-by-row visual acceptance.

| Matrix rows | Progress at this checkpoint |
|---|---|
| M01 | Blender reveal approved; Unity capture exists, user review pending. |
| M02 | Supplemental Mira Bunny closed-box live Unity result approved; automated fresh import and reload checked. Original facial-feature comparison videos still need review. |
| M03 | Blender face-only shadow approved; Unity capture exists, user review pending. |
| M04 | Blender visor/shadow case under active investigation; presentation being redesigned as a stationary-visor stencil OFF/ON comparison. No Unity visual approval. |
| M05-M10, A01-A03 | Visual acceptance pending. Related native-lighting behavior is documented, not claimed fully validated. |

Next gate: user approval of the revised Blender M04 example, then matched Unity
capture and side-by-side/overlay review. Do not label older videos as validating this
new importer checkpoint, or infer general double-sided self-depth support from M02.

Validated in Unity 2022.3.22f1 / Built-in on 2026-09-04:

- 58 MToonXT NUnit test cases passed through direct in-editor invocation with
  setup/teardown. The standard runner was canceled at its unsaved-scene prompt;
  no scene was saved or discarded for testing.
- A fresh Mira Bunny VRM asset import persisted the original writer slot and graph.
  Instantiation rebuilt the reader/base/overlay configuration above, with 16 original
  renderers still totaling 16 (no proxy renderer) and the overlay caster disabled.
- Script reload retained exactly two writer slots. Bounds and placement in the
  approved scene were unchanged. Visual acceptance remains the user's earlier
  confirmation of this setup, not an automated visual comparison of other rows.
- Existing UniVRM spring-bone subasset identifier-uniqueness errors were also emitted
  on import; they are separate from these stencil/material changes.

## Legacy compatibility

The root graph is additive. Existing per-material MToonXT stencil operations remain
supported:

- `write`
- `inside`
- `insideOverlay`
- `outside`
- outline `same`

Files without `stencilRelationships` continue through the legacy compiler unchanged.
