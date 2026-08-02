import { readFile, readdir } from "node:fs/promises";
import { dirname, extname, resolve } from "node:path";

const docsRoot = resolve(process.cwd(), "Docs");
const markdownLink = /\[[^\]]+\]\(([^)]+)\)/g;

async function collect(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const nested = await Promise.all(entries.map(async (entry) => {
    const path = resolve(directory, entry.name);
    return entry.isDirectory() ? collect(path) : [path];
  }));
  return nested.flat();
}

const files = (await collect(docsRoot)).filter((file) => extname(file) === ".md");
const broken = [];

for (const file of files) {
  const content = await readFile(file, "utf8");
  for (const match of content.matchAll(markdownLink)) {
    const target = match[1].replace(/^<|>$/g, "").split("#", 1)[0];
    if (!target || /^(https?:|mailto:|#)/.test(target)) continue;
    try {
      await readFile(resolve(dirname(file), target));
    } catch {
      broken.push(`${file} -> ${target}`);
    }
  }
}

if (broken.length) {
  process.stderr.write(`Broken documentation links:\n${broken.join("\n")}\n`);
  process.exitCode = 1;
} else {
  process.stdout.write(`Documentation links verified: ${files.length} Markdown files.\n`);
}
