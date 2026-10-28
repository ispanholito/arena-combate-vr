// Converte um .glb gerado por IA (TRELLIS/Hunyuan) para o Arena VR:
//   - reduz a malha para N triângulos (meshoptimizer, preservando UV/normal);
//   - textura base (webp/png/jpg) → PNG no tamanho pedido;
//   - malha em JSON (coordenadas do glTF; a conversão para a Unity é feita no editor).
// Uso: node glb2arena.js entrada.glb saida_sem_extensao [triangulos=6000] [textura=1024]
const fs = require('fs');
const sharp = require('sharp');
const { MeshoptSimplifier } = require('meshoptimizer');

async function main() {
    const [inp, out, trisArg, texArg] = process.argv.slice(2);
    const targetTris = parseInt(trisArg || '6000', 10), texSize = parseInt(texArg || '1024', 10);
    const b = fs.readFileSync(inp);
    const jsonLen = b.readUInt32LE(12);
    const gltf = JSON.parse(b.slice(20, 20 + jsonLen).toString());
    const binStart = 20 + jsonLen + 8;
    const bin = b.slice(binStart, binStart + b.readUInt32LE(20 + jsonLen));
    const view = i => { const v = gltf.bufferViews[i]; return bin.slice(v.byteOffset || 0, (v.byteOffset || 0) + v.byteLength); };
    const read = i => {
        const a = gltf.accessors[i], v = gltf.bufferViews[a.bufferView];
        const n = { SCALAR: 1, VEC2: 2, VEC3: 3, VEC4: 4 }[a.type];
        const buf = view(a.bufferView), off = a.byteOffset || 0;
        const stride = v.byteStride || n * (a.componentType === 5126 || a.componentType === 5125 ? 4 : a.componentType === 5123 ? 2 : 1);
        const out = a.componentType === 5126 ? new Float32Array(a.count * n) : new Uint32Array(a.count * n);
        for (let k = 0; k < a.count; k++)
            for (let c = 0; c < n; c++) {
                const p = off + k * stride + c * (a.componentType === 5123 ? 2 : a.componentType === 5121 ? 1 : 4);
                out[k * n + c] = a.componentType === 5126 ? buf.readFloatLE(p) : a.componentType === 5125 ? buf.readUInt32LE(p) : a.componentType === 5123 ? buf.readUInt16LE(p) : buf.readUInt8(p);
            }
        return out;
    };
    const prim = gltf.meshes[0].primitives[0];
    const pos = read(prim.attributes.POSITION), nor = read(prim.attributes.NORMAL), uv = read(prim.attributes.TEXCOORD_0);
    const idx = new Uint32Array(read(prim.indices));
    const vcount = pos.length / 3;
    console.log(`entrada: ${vcount} vértices, ${idx.length / 3} triângulos`);

    // Atributos para o simplificador: normal (peso 0,5) e UV (peso 1).
    const attr = new Float32Array(vcount * 5);
    for (let v = 0; v < vcount; v++) {
        attr[v * 5] = nor[v * 3]; attr[v * 5 + 1] = nor[v * 3 + 1]; attr[v * 5 + 2] = nor[v * 3 + 2];
        attr[v * 5 + 3] = uv[v * 2]; attr[v * 5 + 4] = uv[v * 2 + 1];
    }
    await MeshoptSimplifier.ready;
    const [simp, err] = MeshoptSimplifier.simplifyWithAttributes(idx, pos, 3, attr, 5, [0.5, 0.5, 0.5, 1, 1], null, targetTris * 3, parseFloat(process.env.ERR || '0.2'), (process.env.FLAGS || 'Permissive').split(',').filter(Boolean));
    console.log(`reduzida: ${simp.length / 3} triângulos (erro ${err.toFixed(4)})`);

    // Só os vértices usados.
    const remap = new Map(); const P = [], N = [], U = [], I = [];
    for (const v of simp) {
        if (!remap.has(v)) {
            remap.set(v, P.length / 3);
            P.push(pos[v * 3], pos[v * 3 + 1], pos[v * 3 + 2]);
            N.push(nor[v * 3], nor[v * 3 + 1], nor[v * 3 + 2]);
            U.push(uv[v * 2], uv[v * 2 + 1]);
        }
        I.push(remap.get(v));
    }
    const r4 = x => Math.round(x * 1e5) / 1e5;
    fs.writeFileSync(out + '.json', JSON.stringify({ positions: P.map(r4), normals: N.map(r4), uvs: U.map(r4), indices: I }));
    console.log(`malha: ${P.length / 3} vértices → ${out}.json`);

    // Textura base.
    const mat = gltf.materials[prim.material];
    const tex = gltf.textures[mat.pbrMetallicRoughness.baseColorTexture.index];
    const src = tex.source ?? tex.extensions?.EXT_texture_webp?.source;
    const img = view(gltf.images[src].bufferView);
    await sharp(img).resize(texSize, texSize).png().toFile(out + '_cor.png');
    console.log(`textura ${texSize}px → ${out}_cor.png`);
}
main().catch(e => { console.error(e); process.exit(1); });
