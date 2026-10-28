// Design artifact assembly only. No product builds, data access or installation.
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const destination = process.argv[2];
if (!destination || !destination.endsWith('.html')) throw new Error('Pass an explicit output .html path.');
const source = readFileSync(resolve(here, 'creation-workbench.template.html'), 'utf8');
const jpg = readFileSync(resolve(here, 'rain-window.jpg'));
const result = source.replaceAll('__RAIN_DATA_URL__', 'data:image/jpeg;base64,' + jpg.toString('base64'));
if (Buffer.byteLength(result) > 1_000_000) throw new Error('Inline design exceeds 1 MB budget.');
if (/__RAIN_DATA_URL__|<!doctype|<html|<body/i.test(result)) throw new Error('Expected a complete fragment.');
mkdirSync(dirname(resolve(destination)), { recursive: true });
writeFileSync(destination, result, 'utf8');
console.log(JSON.stringify({ output: resolve(destination), bytes: Buffer.byteLength(result), productChanged: false }));
