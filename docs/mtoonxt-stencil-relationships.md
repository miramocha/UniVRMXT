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

The Built-in pipeline executes those auxiliary draws with cached camera command
buffers. The renderer does not allocate materials, discover renderers, or rebuild draw
lists per frame. Materials and command buffers rebuild only when a relationship graph
is applied or cleared.

The packaged URP shader supports the primary stencil/depth properties. Compound modes
that require an extra render phase need an equivalent URP renderer feature; the
Built-in command-buffer helper deliberately stays inactive under SRP instead of
injecting at an unsafe phase.

## Legacy compatibility

The root graph is additive. Existing per-material MToonXT stencil operations remain
supported:

- `write`
- `inside`
- `insideOverlay`
- `outside`
- outline `same`

Files without `stencilRelationships` continue through the legacy compiler unchanged.
