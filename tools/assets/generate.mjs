#!/usr/bin/env node
/*
 * 把 gallery/assets-src（160MB 原始资产，不入库）压成 gallery/assets（入库、浏览器友好）。
 *
 * 为什么必须走这一趟：旧工程把 Assets/** 用 AvaloniaResource 编进 Aura3D.Gallery 程序集，
 * 浏览器上就是一个 168MB 的 Aura3D.Gallery.wasm，启动前必须整包下载。新结构里资产完全在程序集之外，
 * 按功能页懒加载，所以这里的产物体积就是用户实际的下载量。
 *
 * 预算（在 checkBudgets 里核对，超了就红着脸改）：
 *   - 全部 WebFriendly 资产合计 ≤ 20 MB
 *   - 单个功能页进页即取的资产合计 ≤ 6 MB
 *   - 清单里不许有没有任何代码取用的 Key
 *
 * 用法：node tools/assets/generate.mjs            重编码全部产物并核对
 *       node tools/assets/generate.mjs --check    不重编码，只按现有产物核对（CI 用这条）
 *       node tools/assets/generate.mjs --only=<Key 前缀>
 */
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { mkdirSync, copyFileSync, existsSync, readdirSync, readFileSync, statSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const run = promisify(execFile);

// gltf-transform 处理大模型时输出会超过 execFile 默认的 1MB 缓冲。
const execOptions = { maxBuffer: 64 * 1024 * 1024 };

const repoRoot = path.resolve(import.meta.dirname, '../..');
const SRC = path.join(repoRoot, 'gallery', 'assets-src');
const OUT = path.join(repoRoot, 'gallery', 'assets');
const CLI = path.join(repoRoot, 'tools', 'assets', 'node_modules', '.bin', 'gltf-transform');

/// 每个产物一行：Key 必须与 C# 侧 AssetManifest.All 一字不差，bytes 由脚本回填进 manifest.json。
const models = [
  { key: 'LionHead', src: 'Models/lion_head_1k.glb', out: 'models/lion_head_1k.glb', largest: 1024 },
  { key: 'CoffeeTable', src: 'Models/coffee_table_round_01_1k.glb', out: 'models/coffee_table_round_01_1k.glb', largest: 1024 },
  { key: 'Stool', src: 'Models/wooden_stool_02_1k.glb', out: 'models/wooden_stool_02_1k.glb', largest: 1024 },
  { key: 'Lightbulb', src: 'Models/lightbulb_01_1k.glb', out: 'models/lightbulb_01_1k.glb', largest: 1024 },
  { key: 'Stones', src: 'Models/stones_01.glb', out: 'models/stones_01.glb', largest: 1024 },
  { key: 'Present', src: 'Models/present_11_BACKED.glb', out: 'models/present_11.glb', largest: 512 },
  { key: 'Soldier', src: 'Models/Soldier.glb', out: 'models/Soldier.glb', largest: 1024 },
  // 原样入库，不走 optimize/jpeg 那两趟：这张图的 baseColor 是「白底 + alpha=0」的 RGBA PNG，
  // 转 JPEG 会把透明区压到黑底（角色整片变黑），而它的卡通材质参数与 ILM/SDF/Ramp 全在
  // AURA3D_TEXTURES_CELSHADING 扩展里，glTF Transform 不认这个扩展。
  { key: 'CelCharacter', src: 'Models/NPC_Avatar_Girl_Sword_Nilou.glb', out: 'models/NPC_Avatar_Girl_Sword_Nilou.glb', largest: 0, raw: true },
];

/// 仅桌面（Assimp 原生库读 FBX）：不减面不转码，原样复制，清单里标 WebFriendly=false。
const fbx = [
  { key: 'FbxMannequin', src: 'Models/SK_Mannequin.FBX', out: 'models/fbx/SK_Mannequin.FBX' },
  { key: 'FbxIdle', src: 'Models/Idle_Rifle_Hip.FBX', out: 'models/fbx/Idle_Rifle_Hip.FBX' },
  { key: 'FbxJogFwd', src: 'Models/Jog_Fwd_Rifle.FBX', out: 'models/fbx/Jog_Fwd_Rifle.FBX' },
];

const textures = [
  { key: 'BackgroundJpg', src: 'Textures/background.jpg', out: 'textures/background-1024.jpg', px: 1024 },
  { key: 'ParticleFirePng', src: 'Textures/fire.png', out: 'textures/fire-512.png', px: 512 },
];

const skyboxFaces = ['px', 'nx', 'py', 'ny', 'pz', 'nz'];

const environments = [
  { key: 'Hdr1k', src: 'Textures/buikslotermeerplein_1k.hdr', out: 'environments/buikslotermeerplein_1k.hdr' },
];

const WEB_BUDGET_BYTES = 20 * 1024 * 1024;

async function main() {
  const only = argValue('--only');

  // --check 不重编码，只按现有产物核对预算与取用覆盖：CI 上没有 160MB 源资产目录，靠这条守住预算。
  if (process.argv.includes('--check')) {
    printAndCheck(JSON.parse(readFileSync(path.join(OUT, 'manifest.json'), 'utf8')));

    return;
  }

  resetOut();

  const produced = [];

  for (const model of models) {
    if (skip(only, model.key)) continue;

    const target = path.join(OUT, model.out);

    if (model.raw) {
      ensureSrc(path.join(SRC, model.src));

      copyFileSync(path.join(SRC, model.src), target);
    } else {
      await resizeGlb(path.join(SRC, model.src), target, model.largest);
    }

    produced.push({ ...model, webFriendly: true });
  }

  for (const item of fbx) {
    if (skip(only, item.key)) continue;

    copyFileSync(path.join(SRC, item.src), path.join(OUT, item.out));

    produced.push({ ...item, largest: 0, webFriendly: false });
  }

  for (const item of textures) {
    if (skip(only, item.key)) continue;

    await resizeRaster(path.join(SRC, item.src), path.join(OUT, item.out), item.px);

    produced.push({ ...item, webFriendly: true });
  }

  for (const face of skyboxFaces) {
    const key = `Skybox${face[0].toUpperCase()}${face[1]}`;

    if (skip(only, key)) continue;

    // 立方图六面降到 512 并转 JPEG：IBL 预滤波本身会把高频抹掉，演示看不出差别，省的是六份下载。
    await resizeRaster(path.join(SRC, 'Textures/skybox', `${face}.png`), path.join(OUT, 'textures/skybox', `${face}.jpg`), 512);

    produced.push({ key, src: `Textures/skybox/${face}.png`, out: `textures/skybox/${face}.jpg`, webFriendly: true });
  }

  for (const item of environments) {
    if (skip(only, item.key)) continue;

    copyFileSync(path.join(SRC, item.src), path.join(OUT, item.out));

    produced.push({ ...item, webFriendly: true });
  }

  const report = produced.map((item) => ({
    key: item.key,
    path: item.out,
    bytes: statSync(path.join(OUT, item.out)).size,
    webFriendly: item.webFriendly,
  }));

  writeFileSync(path.join(OUT, 'manifest.json'), JSON.stringify(report, null, 2));

  // --only 是调试用的局部跑，产物不完整，此时绝不覆盖 C# 侧清单（会把未生成的资产抹掉）。
  if (only === null) {
    writeCsharpManifest(report);
  } else {
    console.log('（--only 局部跑：跳过 AssetManifest.Generated.cs）');
  }

  printAndCheck(report);
}

function printAndCheck(report) {
  print(report);

  checkBudgets(report);
}

const PAGE_BUDGET_BYTES = 6 * 1024 * 1024;
const SHARED_DIR = path.join(repoRoot, 'gallery', 'Aura3D.Gallery');
const REGISTRY = path.join(SHARED_DIR, 'Demos', 'DemoRegistry.cs');

/// 预算的两条核对都在生成环节做死，不靠文档：单页 ≤ 6 MB、清单里不许有没人取的资产。
/// 页与 Key 的关系只存在于 DemoRegistry.cs 的 RequireSet 里，所以这里直接按文本读它。
function checkBudgets(report) {
  const bytesOf = new Map(report.map((row) => [row.key, row.bytes]));
  const problems = [];

  const registry = readFileSync(REGISTRY, 'utf8');

  // 每个 RequireSet 只与它上面最近的 Id 配对：中间不许跨过另一个 Id，否则空资产页会吞掉下一页的集合。
  const pages = [...registry.matchAll(/Id:\s*"([^"]+)"(?:(?!Id:)[\s\S])*?RequireSet\(([^)]*)\)/g)];

  for (const match of pages) {
    const keys = [...match[2].matchAll(/"([^"]+)"/g)].map((m) => m[1]);
    const total = keys.reduce((sum, key) => sum + (bytesOf.get(key) ?? 0), 0);

    console.log(`  ${match[1].padEnd(18)} 进页即取 ${(total / 1024 / 1024).toFixed(2).padStart(5)} MB  ${keys.join(' ')}`);

    if (keys.some((key) => !bytesOf.has(key))) {
      problems.push(`${match[1]}：RequireSet 里有未登记的 Key ${keys.filter((k) => !bytesOf.has(k)).join(', ')}`);
    } else if (total > PAGE_BUDGET_BYTES) {
      problems.push(`${match[1]}：进页即取 ${(total / 1024 / 1024).toFixed(1)} MB > 6 MB`);
    }
  }

  // 除生成清单外，任何 .cs 里以字面量出现过的 Key 都算被取用（含 SkyboxKeys 与页内按需装载的表）。
  const referenced = new Set();

  for (const file of csFiles(SHARED_DIR)) {
    if (file.endsWith('AssetManifest.Generated.cs')) continue;

    for (const key of bytesOf.keys()) {
      if (readFileSync(file, 'utf8').includes(`"${key}"`)) referenced.add(key);
    }
  }

  for (const key of bytesOf.keys()) {
    if (!referenced.has(key)) problems.push(`${key}：登记在清单里但没有任何代码取用，删资产或补功能页`);
  }

  if (problems.length > 0) {
    console.error(`\n资产预算核对不通过：\n  ${problems.join('\n  ')}`);

    process.exitCode = 1;
  } else {
    console.log(`单页预算（≤ 6 MB）与取用覆盖（${bytesOf.size} 个 Key 全部有页取用）均通过。`);
  }
}

function csFiles(dir) {
  const out = [];

  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (entry.name !== 'obj' && entry.name !== 'bin') out.push(...csFiles(full));
    } else if (entry.name.endsWith('.cs')) {
      out.push(full);
    }
  }

  return out;
}

const CSHARP_MANIFEST = path.join(repoRoot, 'gallery', 'Aura3D.Gallery', 'Assets', 'AssetManifest.Generated.cs');

/// 分组顺序决定 C# 清单里的归类注释；匹配不到任何分组的路径要当场报错，不能静默漏登记。
const groups = [
  { title: '仅桌面资产：Assimp 原生库读的 FBX', match: (p) => p.startsWith('models/fbx/') },
  { title: '模型', match: (p) => p.startsWith('models/') },
  { title: '贴图', match: (p) => p.startsWith('textures/') && !p.startsWith('textures/skybox/') },
  { title: '环境：HDRI 与立方图六面', match: (p) => p.startsWith('environments/') || p.startsWith('textures/skybox/') },
];

function writeCsharpManifest(report) {
  const rows = report.map((row) => {
    const index = groups.findIndex((group) => group.match(row.path));

    if (index < 0) {
      throw new Error(`资产 ${row.path} 不属于任何已知分组，请在 groups 里补一条。`);
    }

    return { ...row, group: groups[index].title, groupIndex: index };
  });

  rows.sort((a, b) => a.groupIndex - b.groupIndex);

  const body = [];
  let currentGroup = null;

  for (const row of rows) {
    if (row.group !== currentGroup) {
      currentGroup = row.group;

      body.push('', `        // —— ${currentGroup} ——`);
    }

    const web = row.webFriendly ? '' : ', WebFriendly: false';

    body.push(`        new("${row.key}", "${row.path}", ${grouped(row.bytes)}${web}),`);
  }

  writeFileSync(CSHARP_MANIFEST, `// <auto-generated>
// 由 tools/assets/generate.mjs 依据 gallery/assets 磁盘实测字节生成，不要手改。
// 重新生成：node tools/assets/generate.mjs
// </auto-generated>

using System.Collections.Generic;

namespace Aura3D.Gallery.Assets;

public static partial class AssetManifest
{
    /// <summary>
    /// 全部已登记资产。顺序即功能页资产列表的展示顺序。
    /// </summary>
    public static IReadOnlyList<AssetRef> All { get; } =
    [${body.join('\n')}
    ];
}
`);
}

function grouped(bytes) {
  return String(bytes).replace(/\B(?=(\d{3})+(?!\d))/g, '_');
}

function resetOut() {
  rmSync(OUT, { recursive: true, force: true });

  for (const dir of ['models/fbx', 'textures/skybox', 'environments']) {
    mkdirSync(path.join(OUT, dir), { recursive: true });
  }
}

function argValue(name) {
  const hit = process.argv.find((a) => a.startsWith(`${name}=`));

  return hit ? hit.split('=')[1] : null;
}

function skip(only, key) {
  return only !== null && !key.startsWith(only);
}

async function resizeGlb(src, dst, largest) {
  ensureSrc(src);

  // 只走 optimize 一遍：它内部的 dedup/prune/resample 都是普通顶点缓冲层面的无损整理，
  // 产物仍是纯 glTF 核心属性——SharpGLTF 读不了 meshopt/Draco/KTX2 这类压缩扩展，
  // 所以 --compress/--simplify/--flatten/--join/--instance 全部关掉，场景图与蒙皮保持原样。
  await run(CLI, [
    'optimize',
    src,
    dst,
    '--compress', 'false',
    '--simplify', 'false',
    '--flatten', 'false',
    '--join', 'false',
    '--instance', 'false',
    '--palette', 'false',
    '--sparse', 'false',
    '--texture-compress', 'auto',
    '--texture-size', String(largest),
  ], execOptions);

  // 内嵌贴图的字节几乎全在 normal / metallicRoughness 两张 PNG 上（lion head：2.4MB + 0.83MB → 整包 4.26MB）。
  // 传 PNG 给 jpeg 命令即可：glTF Transform 会按通道掩码跳过真正需要 alpha 的贴图（只留警告），
  // 所以 baseColor 有 alpha 的模型不会被压坏。
  // --slots/--formats 都是**正则**（对 edge name 与 MIME 测试），不是逗号列表，写错会静默不匹配。
  // 不能转 webp/avif——引擎的贴图解码是 StbImageSharp，只吃 PNG/JPEG/BMP。
  await run(CLI, ['jpeg', dst, `${dst}.tmp.glb`, '--formats', 'png', '--quality', '80'], execOptions);

  rmSync(dst);

  copyFileSync(`${dst}.tmp.glb`, dst);

  rmSync(`${dst}.tmp.glb`);
}

async function resizeRaster(src, dst, px) {
  ensureSrc(src);

  mkdirSync(path.dirname(dst), { recursive: true });

  const format = path.extname(dst) === '.jpg' ? 'jpeg' : 'png';

  await run('/usr/bin/sips', ['-Z', String(px), '-s', 'format', format, src, '--out', dst]);
}

function ensureSrc(src) {
  if (!existsSync(src)) {
    throw new Error(`缺少源资产：${src}\ngallery/assets-src 是 160MB 原始资产目录（已 gitignore），需要先从备份或原仓库分支取回。`);
  }
}

function print(report) {
  const total = report.reduce((sum, r) => sum + r.bytes, 0);
  const webTotal = report.filter((r) => r.webFriendly).reduce((sum, r) => sum + r.bytes, 0);

  for (const item of [...report].sort((a, b) => b.bytes - a.bytes)) {
    const tag = item.webFriendly ? 'web  ' : '桌面 ';

    console.log(`${(item.bytes / 1024 / 1024).toFixed(2).padStart(7)} MB  [${tag}]  ${item.key.padEnd(16)} ${item.path}`);
  }

  console.log(`\n产物合计 ${(total / 1024 / 1024).toFixed(1)} MB；其中浏览器可下载 ${(webTotal / 1024 / 1024).toFixed(1)} MB（源目录 160 MB）`);

  if (webTotal > WEB_BUDGET_BYTES) {
    console.error(`超出预算：浏览器侧 ${(webTotal / 1024 / 1024).toFixed(1)} MB > ${(WEB_BUDGET_BYTES / 1024 / 1024).toFixed(0)} MB`);

    process.exitCode = 1;
  } else {
    console.log(`预算内（≤ ${(WEB_BUDGET_BYTES / 1024 / 1024).toFixed(0)} MB）。`);
  }
}

await main();
