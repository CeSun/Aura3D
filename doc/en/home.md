# Aura3D Documentation

Welcome to Aura3D. This documentation covers everything from installation to custom rendering pipelines.

## Documents

| Document | Contents |
|---|---|
| **[Get Started](./get-started.md)** | Install NuGet packages → XAML control declaration → Initialize scene → Camera setup → Load models → Configure lights → Scene background → Render loop |
| **[Rendering Pipelines](./pipelines.md)** | Built-in BlinnPhong / NoLight / PBR / CelShading → Custom RenderPipeline + RenderPass → Shader macro system → Lifecycle hooks → Multi-camera |
| **[Animation System](./animation.md)** | Skeletal animation / Loop modes / External animation import → 2D blend space → Animation graph → Bone manipulation |
| **[Instanced Rendering](./instanced-rendering.md)** | GPU instancing (InstancedMesh) → Per-instance attributes / Transform updates → Hierarchical instancing (HISM) → Incremental updates |
| **[Rendering Topics](./rendering.md)** | Shadows / Point clouds / Primitive rendering → Advanced materials → Node operations (bounding box / clone / batch transform / find children) |
| **[GPU Resource Lifecycle](./gpu-resource-lifecycle.md)** | GPU-state ownership → Idempotent destruction → Context loss and recovery → Custom-state contract |
| **[Platforms and Render Backends](./platform-render-backends.md)** | Which path each platform takes → iOS ANGLE(Metal) backend and framework setup → GLES 3.0 subset constraints → Frame scheduling and thread semantics |

## Example Project

Clone the repository and run `example/Example.Desktop` to see interactive demos of all features:

```shell
git clone https://github.com/CeSun/Aura3d.git
cd Aura3d
dotnet restore
dotnet run --project example/Example.Desktop
```

Controls: WASD to move, right-click drag to rotate, scroll to zoom, middle-click to pan. The left sidebar switches between the 21 feature pages.

Four structural rules the example project follows:

- **The page table is the only registry**: one entry per page in `example/Aura3D.Example.Shared/Demos/DemoRegistry.cs`. Navigation, deep links and size budgets all read it, so adding a page never touches the host.
- **Assets live outside the assembly**: `example/assets` (committed, recompressed) is lazy-loaded per page and never goes through `<AvaloniaResource>` — that would bake every byte into the wasm bundle. The 160 MB originals sit in the gitignored `example/assets-src`; `node tools/assets/generate.mjs` re-encodes them, and `--check` only verifies the budget (≤ 20 MB downloadable from the browser, ≤ 6 MB per page on load, no manifest key left unused). CI runs that check on every push.
- **Deep links**: `--page=<Id>` on desktop, `?page=<Id>` in the browser, with the ids from that same table.
- **Every string lives in resx, in two languages**: `Localization/Strings.resx` (English) plus `Strings.zh-Hans.resx`, read through [Irihi.Lingua](https://github.com/irihitech/Irihi.Lingua) — `{Translate {x:Static loc:Strings+Keys.X}}` in AXAML, `Strings.Keys.X.T()` / `.Format(...)` in C#. The header button switches language live and rebuilds the current page; `--lang=` / `?lang=` (or `AURA3D_LANG`) picks it at startup, defaulting to the UI culture. `python3 tools/i18n/check.py` enforces this in CI: identical key sets, no hardcoded CJK in attributes or string literals, `Format` arguments matching the placeholders, and option lists whose entries stay distinguishable once translated.
