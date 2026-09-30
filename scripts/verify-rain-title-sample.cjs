// Local HTML contract test only; no connection to user browser or native DesktopUp.
const { createRequire } = require('node:module');
const { pathToFileURL } = require('node:url');
const path = require('node:path');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const [packageRoot, output] = process.argv.slice(2);
if (!packageRoot || !output) throw new Error('Supply bundled node_modules and a new output directory.');
const { chromium } = createRequire(path.join(packageRoot, 'package.json'))('playwright');
(async()=>{
 if(fs.existsSync(output)) throw new Error('Preserve previous results.');
 fs.mkdirSync(output,{recursive:true});
 const browser=await chromium.launch({headless:true,channel:'msedge'});
 try {
  const page=await browser.newPage({viewport:{width:1280,height:800}}), errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(path.resolve('Docs/A_Project/20_Architecture/Designs/rain-title-rr01.html')).href);
  assert.equal(await page.locator('.poster').count(),3);
  const heights=await page.locator('.cloud').evaluateAll(es=>es.map(e=>e.getBoundingClientRect().height));
  assert.deepEqual(heights,[84,60,84]);
  const p=page.locator('.poster').first(), button=p.locator('.collapse');
  const before=await button.boundingBox(); await button.click(); const after=await button.boundingBox();
  assert.equal(after.x,before.x);assert.equal(after.y,before.y);
  assert.equal(await p.locator('.cloud').evaluate(e=>e.getBoundingClientRect().height),60);
  await button.click(); await page.locator('#regions').check();
  assert.equal(await page.locator('.show-grips').count(),3);
  await page.locator('#regions').uncheck();
  const box=await p.boundingBox();await page.mouse.move(box.x+30,box.y+40);await page.mouse.down();
  await page.mouse.move(box.x+42,box.y+48);await page.mouse.up();
  const moved=await p.boundingBox();assert.equal(Math.round(moved.x-box.x),12);assert.equal(Math.round(moved.y-box.y),8);
  await page.mouse.move(moved.x+1,moved.y+1);await page.mouse.down();await page.mouse.move(moved.x+15,moved.y+10);await page.mouse.up();
  assert.equal((await p.boundingBox()).x,moved.x);
  await page.screenshot({path:path.join(output,'wide.png'),fullPage:true});
  await page.setViewportSize({width:375,height:900});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
  await page.screenshot({path:path.join(output,'narrow.png'),fullPage:true});
  assert.deepEqual(errors,[]);
  fs.writeFileSync(path.join(output,'result.json'),JSON.stringify({passed:true,heights,pageErrors:errors,nativeInput:false},null,2));
  console.log('Title sample: geometry, fixed actions, grip/blank, narrow layout PASS; native input NOT tested.');
 } finally { await browser.close(); }
})().catch(e=>{console.error(e);process.exitCode=1;});
