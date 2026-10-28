// J0 design evidence only: no product calls, writes, network, or production parser claim.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';

const read = name => readFileSync(new URL(name, import.meta.url));
const load = name => JSON.parse(read(name).toString('utf8'));
const catalog = load('capabilities.design.json');
const capabilities = new Map(catalog.capabilities.map(c => [c.id, c]));
const fail = code => { throw new Error(code); };
const ensure = (condition, code) => { if (!condition) fail(code); };
const object = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const id = value => typeof value === 'string' && /^[a-z][a-zA-Z0-9-]{0,79}$/.test(value);
const keys = (value, allowed) => {
  ensure(object(value), 'object-required');
  ensure(Object.keys(value).every(k => allowed.includes(k)), 'unknown-field');
};
const unique = (items, key) => {
  ensure(Array.isArray(items), 'array-required');
  const result = new Map();
  for (const item of items) {
    ensure(id(item[key]) && !result.has(item[key]), 'duplicate-or-invalid-id');
    result.set(item[key], item);
  }
  return result;
};
function valueMatches(value, definition) {
  switch (definition.type) {
    case 'number': return typeof value === 'number' && Number.isFinite(value) && value >= definition.min && value <= definition.max;
    case 'boolean': return typeof value === 'boolean';
    case 'color': return typeof value === 'string' && /^#[0-9a-f]{6}$/i.test(value);
    case 'string': return typeof value === 'string' && value.length <= definition.maxLength;
    case 'enum': return definition.values.includes(value);
    case 'rect': return Array.isArray(value) && value.length === 4 && value.every(n => typeof n === 'number' && Number.isFinite(n) && n >= 0) && value[2] > 0 && value[3] > 0 && value[0] + value[2] <= 1.000001 && value[1] + value[3] <= 1.000001;
    default: return false;
  }
}
function property(value, definition, parameters) {
  if (object(value)) {
    keys(value, ['param']);
    const parameter = parameters.get(value.param);
    ensure(parameter && parameter.type === definition.type, 'parameter-binding');
    ensure(valueMatches(parameter.default, definition), 'binding-value');
    if (definition.type === 'number') ensure(parameter.min >= definition.min && parameter.max <= definition.max, 'binding-range');
    if (definition.type === 'string') ensure(parameter.maxLength <= definition.maxLength, 'binding-range');
  } else ensure(valueMatches(value, definition), 'property-value');
}
function recipe(value) {
  keys(value, ['format','protocolVersion','id','version','name','requires','regions','nodes','parameters','resources','contentSlots','operationAnchors','editor']);
  ensure(value.format === 'desktopup.canvas-recipe.design' && value.protocolVersion === '0.1-draft', 'recipe-version');
  ensure(id(value.id) && Number.isInteger(value.version) && value.version > 0 && typeof value.name === 'string', 'recipe-identity');
  ensure(Buffer.byteLength(JSON.stringify(value)) <= catalog.budgets.recipeBytes, 'recipe-budget');
  ensure(Array.isArray(value.requires) && new Set(value.requires).size === value.requires.length && value.requires.every(c => capabilities.has(c)), 'unsupported-capability');
  ensure(value.requires.includes('surface.regions@1'), 'surface-capability');
  for (const key of ['nodes','regions','parameters','resources']) ensure(Array.isArray(value[key]) && value[key].length <= catalog.budgets[key], 'count-budget');
  const regions = unique(value.regions, 'id');
  ensure(regions.size > 0, 'region-required');
  for (const region of regions.values()) {
    keys(region, ['id','shape','bounds','radiusDip']);
    ensure(catalog.surfaceShapes.includes(region.shape), 'unsupported-shape');
    ensure(valueMatches(region.bounds, {type:'rect'}), 'region-bounds');
    if (region.radiusDip !== undefined) ensure(region.shape === 'roundedRect' && valueMatches(region.radiusDip, {type:'number',min:0,max:100}), 'region-radius');
  }
  keys(value.editor, ['groups']);
  const groups = unique(value.editor.groups, 'id');
  const parameters = unique(value.parameters, 'key');
  for (const p of parameters.values()) {
    keys(p, ['key','label','type','default','group','min','max','maxLength','values']);
    ensure(groups.has(p.group) && typeof p.label === 'string', 'parameter-group');
    ensure(valueMatches(p.default, p), 'parameter-default');
  }
  // No resources are exercised in J0. Resource decoding and handle authority belong to J1.
  ensure(value.resources.length === 0, 'resource-design-not-covered');
  const nodes = unique(value.nodes, 'id');
  const roots = value.nodes.filter(n => n.parent === null);
  ensure(roots.length === 1 && roots[0].type === 'layout.canvas@1', 'single-root');
  for (const n of nodes.values()) {
    keys(n, ['id','parent','type','region','props']);
    const capability = capabilities.get(n.type);
    ensure(capability?.kind === 'node' && value.requires.includes(n.type), 'node-capability');
    ensure(n.parent === null || nodes.has(n.parent), 'parent-missing');
    if (n.parent !== null) ensure(capabilities.get(nodes.get(n.parent).type)?.children, 'children-forbidden');
    ensure(n.region === undefined || regions.has(n.region), 'region-reference');
    // An explicit region starts directly below the root; children inherit that local space.
    ensure(n.region === undefined || n.parent === roots[0].id, 'region-scope');
    if (n.parent === roots[0].id) ensure(n.region !== undefined, 'region-required');
    const chain = new Set();
    for (let cursor = n; cursor; cursor = nodes.get(cursor.parent)) {
      ensure(!chain.has(cursor.id), 'tree-cycle');
      chain.add(cursor.id);
      ensure(chain.size <= catalog.budgets.depth, 'tree-depth');
    }
    const definitions = {...catalog.commonProperties, ...capability.properties};
    keys(n.props, Object.keys(definitions));
    for (const [key, val] of Object.entries(n.props)) property(val, definitions[key], parameters);
    for (const key of Object.keys(capability.properties)) ensure(Object.hasOwn(n.props, key), 'required-property');
    if (n.parent !== null) ensure(Object.hasOwn(n.props, 'box'), 'node-box');
    if (n.type === 'content.slot@1') ensure(!Object.hasOwn(n.props, 'visible'), 'content-visibility');
  }
  ensure(value.contentSlots.length === 1 && value.contentSlots[0].id === 'members', 'single-members-slot');
  for (const slot of value.contentSlots) {
    keys(slot, ['id','node']);
    ensure(nodes.get(slot.node)?.type === 'content.slot@1', 'slot-node');
  }
  ensure(value.nodes.filter(n => n.type === 'content.slot@1').length === 1, 'duplicate-content');
  for (const anchor of value.operationAnchors) {
    keys(anchor, ['action','node']);
    ensure(['move','host-menu'].includes(anchor.action) && nodes.has(anchor.node), 'anchor-reference');
    if (anchor.action === 'host-menu') ensure(nodes.get(anchor.node).type === 'host.action@1', 'menu-anchor');
  }
  for (const group of groups.values()) {
    keys(group, ['id','label','collapsed','whenNode']);
    ensure(typeof group.label === 'string' && typeof group.collapsed === 'boolean' && nodes.has(group.whenNode), 'editor-group');
  }
  return parameters;
}
const rainBytes = read('rainy-desk.recipe.json');
const rain = JSON.parse(rainBytes.toString('utf8'));
const rainDigest = createHash('sha256').update(rainBytes).digest('hex');
const fixtureState = {containerId:'fixture-container-1',designRevision:7,latestDraftRevision:1};
function draft(value, state = fixtureState) {
  keys(value, ['format','protocolVersion','requestId','draftId','draftRevision','target','capabilityRevision','candidate','requestedBy']);
  ensure(value.format === 'desktopup.canvas-draft.design' && value.protocolVersion === '0.1-draft', 'draft-version');
  ensure(id(value.requestId) && id(value.draftId) && ['ai','human'].includes(value.requestedBy), 'draft-identity');
  ensure(Number.isInteger(value.draftRevision) && value.draftRevision >= state.latestDraftRevision, 'stale-draft');
  keys(value.target, ['containerId','expectedDesignRevision']);
  ensure(value.target.containerId === state.containerId, 'target-scope');
  ensure(value.target.expectedDesignRevision === state.designRevision, 'stale-design');
  ensure(value.capabilityRevision === catalog.revision, 'stale-capabilities');
  keys(value.candidate, ['recipeId','recipeVersion','sha256','overrides']);
  ensure(value.candidate.recipeId === rain.id && value.candidate.recipeVersion === rain.version && value.candidate.sha256 === rainDigest, 'candidate-identity');
  const parameters = recipe(rain);
  keys(value.candidate.overrides, [...parameters.keys()]);
  for (const [key, val] of Object.entries(value.candidate.overrides)) ensure(valueMatches(val, parameters.get(key)), 'override-value');
}

ensure(catalog.status === 'proposed' && capabilities.size === catalog.capabilities.length, 'catalog-design-status');
const basic = load('basic-storage.recipe.json');
const initial = load('rainy-desk.draft.json');
const refined = load('rainy-desk-refined.draft.json');
recipe(basic); recipe(rain); draft(initial); draft(refined, {...fixtureState,latestDraftRevision:2});
assert.equal(basic.nodes.some(n => n.type === 'draw.text@1'), false);
assert.equal(rain.regions.length, 2);
assert.deepEqual(basic.contentSlots.map(s => s.id), rain.contentSlots.map(s => s.id));
assert.equal(initial.draftId, refined.draftId);
assert.equal(refined.draftRevision, initial.draftRevision + 1);

const rejected = [];
function negative(name, original, change, verify, code) {
  const copy = structuredClone(original);
  change(copy);
  assert.throws(() => verify(copy), error => error.message === code, name);
  rejected.push(name);
}
negative('unknown capability', basic, x => x.requires.push('execute.script@1'), recipe, 'unsupported-capability');
negative('untrusted property', basic, x => x.nodes[1].props.onClick = 'shell', recipe, 'unknown-field');
negative('wrong value type', basic, x => x.parameters[2].default = '12', recipe, 'parameter-default');
negative('out of bounds', basic, x => x.nodes[1].props.box = [0,0,2,1], recipe, 'property-value');
negative('missing region', basic, x => x.nodes[1].region = 'missing', recipe, 'region-reference');
negative('duplicate node', basic, x => x.nodes.push(x.nodes[1]), recipe, 'duplicate-or-invalid-id');
negative('cyclic parent', basic, x => x.nodes[0].parent = 'body', recipe, 'single-root');
negative('duplicate membership', basic, x => x.contentSlots.push({id:'again',node:'items'}), recipe, 'single-members-slot');
negative('missing operation anchor', basic, x => x.operationAnchors[0].node = 'missing', recipe, 'anchor-reference');
negative('executable resource not covered', basic, x => x.resources.push({path:'C:/run.exe'}), recipe, 'resource-design-not-covered');
negative('stale target design', initial, x => x.target.expectedDesignRevision = 6, draft, 'stale-design');
negative('wrong target', initial, x => x.target.containerId = 'other-container', draft, 'target-scope');
negative('stale catalog', initial, x => x.capabilityRevision = 'old', draft, 'stale-capabilities');
negative('changed candidate bytes', initial, x => x.candidate.sha256 = '0'.repeat(64), draft, 'candidate-identity');
negative('model self approval', initial, x => x.approved = true, draft, 'unknown-field');
negative('invalid override', refined, x => x.candidate.overrides.lampIntensity = 2, draft, 'override-value');
negative('late draft', initial, () => {}, x => draft(x,{...fixtureState,latestDraftRevision:2}), 'stale-draft');

console.log(JSON.stringify({scope:'J0 static design only',passed:true,recipes:2,drafts:2,negativeCases:rejected.length,rejected,filesWritten:0,productExecuted:false},null,2));
